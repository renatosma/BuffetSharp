using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Fiscal;
using BuffetSharp.Infrastructure.Fiscal;

namespace BuffetSharp.Tests.Dominio;

/// <summary>NFS-e: regras da configuração fiscal e da nota, e a conversa com o Focus NFe (HTTP simulado).</summary>
public class NfseTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private const string CpfValido = "52998224725";
    private const string CnpjValido = "11222333000181";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    /// <summary>100 convidados a R$ 100 = R$ 10.000.</summary>
    private static Evento Evento()
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), validoAte: data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(Agora);
        return ComId(orcamento.Aprovar("Casamento Ana e Bruno", Agora), 7);
    }

    private static NotaFiscalServico Nota(decimal valor = 1000m, string documento = CpfValido) =>
        new(Evento(), AmbienteNfse.Homologacao, valor, 5m, false, "Serviços de buffet", documento, "Ana Souza", "ana@exemplo.com", "admin", Agora);

    // ============================== Configuração fiscal ==============================

    [Fact]
    public void Configuracao_nova_aponta_o_que_falta_para_emitir()
    {
        var configuracao = new ConfiguracaoFiscal();
        Assert.Equal(AmbienteNfse.Simulado, configuracao.Ambiente);
        Assert.Equal("17.11", configuracao.ItemListaServico);
        Assert.Equal(["CNPJ do buffet", "inscrição municipal", "código IBGE do município"], configuracao.Pendencias());

        configuracao.Cnpj = CnpjValido;
        configuracao.InscricaoMunicipal = "1234567";
        configuracao.CodigoMunicipio = "3550308";
        Assert.Empty(configuracao.Pendencias());
        Validador.Validar(configuracao);
    }

    [Fact]
    public void Configuracao_recusa_cnpj_e_municipio_invalidos()
    {
        var erro = Assert.Throws<ValidacaoException>(() =>
            Validador.Validar(new ConfiguracaoFiscal { Cnpj = "11222333000100", CodigoMunicipio = "35503" }));
        Assert.Contains(erro.Erros, e => e.MemberNames.Contains(nameof(ConfiguracaoFiscal.Cnpj)));
        Assert.Contains(erro.Erros, e => e.MemberNames.Contains(nameof(ConfiguracaoFiscal.CodigoMunicipio)));
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new ConfiguracaoFiscal { AliquotaIss = 6 }));
    }

    // ============================== Nota ==============================

    [Fact]
    public void Nota_nasce_em_processamento_com_os_dados_do_tomador_e_o_iss()
    {
        var nota = Nota(1234.50m, "529.982.247-25");
        Assert.Equal(StatusNfse.Processando, nota.Status);
        Assert.True(nota.Ativa);
        Assert.Equal(CpfValido, nota.TomadorDocumento);
        Assert.Equal(61.73m, nota.ValorIss);
        Assert.StartsWith("evt7-", nota.Referencia);
        Assert.Equal(24, nota.Referencia.Length);
        Assert.NotEqual(Nota().Referencia, nota.Referencia);
    }

    [Theory]
    [InlineData(0, CpfValido, "valor")]
    [InlineData(10_000.01, CpfValido, "não pode passar")]
    [InlineData(1000, "12345678900", "CPF ou CNPJ")]
    [InlineData(1000, "", "CPF ou CNPJ")]
    public void Nota_recusa_valor_e_documento_invalidos(decimal valor, string documento, string trecho)
    {
        var erro = Assert.Throws<RegraNegocioException>(() => Nota(valor, documento));
        Assert.Contains(trecho, erro.Message);
    }

    [Fact]
    public void Nota_autorizada_so_cancela_com_motivo_de_15_caracteres()
    {
        var nota = Nota();
        Assert.Throws<RegraNegocioException>(() => nota.ConferirCancelamento("Erro de digitação no valor"));
        nota.RegistrarAutorizacao("123", "ABC-99", Agora, "https://prefeitura/nota/123", "/arquivos/123.xml");
        Assert.Equal((StatusNfse.Autorizada, "123", "ABC-99"), (nota.Status, nota.Numero, nota.CodigoVerificacao));

        Assert.Throws<RegraNegocioException>(() => nota.RegistrarCancelamento("Erro", Agora));
        Assert.Throws<RegraNegocioException>(() => nota.RegistrarErro("recusada"));
        nota.RegistrarCancelamento("Valor emitido errado, nova nota em seguida", Agora);
        Assert.Equal(StatusNfse.Cancelada, nota.Status);
        Assert.False(nota.Ativa);
        Assert.Throws<RegraNegocioException>(() => nota.RegistrarAutorizacao("124", null, null, null, null));
    }

    [Fact]
    public void Nota_recusada_guarda_o_motivo_e_libera_o_evento()
    {
        var nota = Nota();
        nota.RegistrarProcessamento("Aguardando a prefeitura.");
        nota.RegistrarErro("Inscrição municipal não encontrada");
        Assert.Equal((StatusNfse.Erro, "Inscrição municipal não encontrada"), (nota.Status, nota.Mensagem));
        Assert.False(nota.Ativa);
    }

    [Fact]
    public void Evento_cancelado_nao_recebe_nota()
    {
        var evento = Evento();
        evento.Cancelar("Cliente desistiu do evento");
        Assert.Throws<RegraNegocioException>(() =>
            new NotaFiscalServico(evento, AmbienteNfse.Simulado, 100, 5, false, "Buffet", CpfValido, "Ana", null, null, Agora));
    }

    // ============================== Focus NFe ==============================

    private sealed class HttpFalso(params (HttpStatusCode Status, string Corpo)[] respostas) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode, string)> _respostas = new(respostas);
        public List<(HttpMethod Metodo, Uri Endereco, string? Autorizacao, string? Corpo)> Pedidos { get; } = [];
        public Exception? Falha { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            if (Falha is not null)
                throw Falha;
            var (status, corpo) = _respostas.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly NfseOptions Tokens = new() { TokenHomologacao = "tok-homologacao", TokenProducao = "tok-producao" };

    private static PedidoNfse Pedido(AmbienteNfse ambiente = AmbienteNfse.Homologacao, string tomador = CpfValido,
        RegimeTributario regime = RegimeTributario.SimplesNacional) =>
        new("evt7-abc", ambiente, new DateTime(2026, 6, 21, 10, 0, 0), regime,
            new PrestadorNfse(CnpjValido, "1234567", "3550308"),
            new TomadorNfse(tomador, "Ana Souza", "ana@exemplo.com", "(11) 98888-7777",
                new Endereco { Logradouro = "Rua A", Numero = "10", Bairro = "Centro", Uf = "SP", Cep = "01001-000" }),
            new ServicoNfse(1500.50m, 5m, false, "17.11", null, "5620-1/02", "Serviços de buffet", "3550308"));

    [Fact]
    public async Task Focus_envia_a_nota_no_leiaute_da_api_com_o_token_do_ambiente()
    {
        var http = new HttpFalso((HttpStatusCode.Accepted, """{"cnpj_prestador":"11222333000181","ref":"evt7-abc","status":"processando_autorizacao"}"""));
        using var focus = new ProvedorNfseFocus(Tokens, http);

        var resposta = await focus.EmitirAsync(Pedido(), Ct);

        Assert.Equal(StatusNfse.Processando, resposta.Status);
        var (metodo, endereco, autorizacao, corpo) = Assert.Single(http.Pedidos);
        Assert.Equal(HttpMethod.Post, metodo);
        Assert.Equal("https://homologacao.focusnfe.com.br/v2/nfse?ref=evt7-abc", endereco.ToString());
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("tok-homologacao:")), autorizacao);

        var json = JsonNode.Parse(corpo!)!;
        Assert.Equal("2026-06-21T10:00:00", json["data_emissao"]!.GetValue<string>()[..19]);
        Assert.True(json["optante_simples_nacional"]!.GetValue<bool>());
        Assert.Equal(6, json["regime_especial_tributacao"]!.GetValue<int>());
        Assert.Equal(CnpjValido, json["prestador"]!["cnpj"]!.GetValue<string>());
        Assert.Equal("3550308", json["prestador"]!["codigo_municipio"]!.GetValue<string>());
        Assert.Equal(CpfValido, json["tomador"]!["cpf"]!.GetValue<string>());
        Assert.Null(json["tomador"]!["cnpj"]);
        Assert.Equal("11988887777", json["tomador"]!["telefone"]!.GetValue<string>());
        Assert.Equal("01001000", json["tomador"]!["endereco"]!["cep"]!.GetValue<string>());
        Assert.Equal(1500.50m, json["servico"]!["valor_servicos"]!.GetValue<decimal>());
        Assert.Equal("17.11", json["servico"]!["item_lista_servico"]!.GetValue<string>());
        Assert.Equal("5620102", json["servico"]!["codigo_cnae"]!.GetValue<string>());
        Assert.False(json["servico"]!["iss_retido"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Focus_usa_o_endereco_de_producao_e_cnpj_do_tomador_pessoa_juridica()
    {
        var http = new HttpFalso((HttpStatusCode.Accepted, """{"status":"processando_autorizacao"}"""));
        using var focus = new ProvedorNfseFocus(Tokens, http);

        await focus.EmitirAsync(Pedido(AmbienteNfse.Producao, CnpjValido, RegimeTributario.LucroPresumido), Ct);

        var pedido = Assert.Single(http.Pedidos);
        Assert.StartsWith("https://api.focusnfe.com.br/v2/nfse", pedido.Endereco.ToString());
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("tok-producao:")), pedido.Autorizacao);
        var json = JsonNode.Parse(pedido.Corpo!)!;
        Assert.Equal(CnpjValido, json["tomador"]!["cnpj"]!.GetValue<string>());
        Assert.False(json["optante_simples_nacional"]!.GetValue<bool>());
        Assert.Null(json["regime_especial_tributacao"]);
    }

    [Fact]
    public async Task Focus_le_a_nota_autorizada_e_os_erros_da_prefeitura()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """
                {"status":"autorizado","numero":"4521","codigo_verificacao":"X9K2-PL","data_emissao":"2026-06-21T10:02:03-03:00",
                 "url":"https://nfse.prefeitura.sp.gov.br/nota?n=4521","caminho_xml_nota_fiscal":"/arquivos/4521.xml"}
                """),
            (HttpStatusCode.OK, """
                {"status":"erro_autorizacao","erros":[{"codigo":"E145","mensagem":"Inscrição municipal inválida","correcao":"Confira o cadastro"},
                 {"codigo":"E10","mensagem":"Alíquota incompatível"}]}
                """),
            (HttpStatusCode.NotFound, """{"codigo":"nao_encontrado","mensagem":"Nota fiscal não encontrada"}"""));
        using var focus = new ProvedorNfseFocus(Tokens, http);

        var autorizada = await focus.ConsultarAsync(AmbienteNfse.Homologacao, "evt7-abc", Ct);
        Assert.Equal((StatusNfse.Autorizada, "4521", "X9K2-PL", "/arquivos/4521.xml"),
            (autorizada.Status, autorizada.Numero, autorizada.CodigoVerificacao, autorizada.CaminhoXml));
        Assert.Equal(new DateTimeOffset(2026, 6, 21, 10, 2, 3, TimeSpan.FromHours(-3)).LocalDateTime, autorizada.DataEmissao);
        Assert.Equal(HttpMethod.Get, http.Pedidos[0].Metodo);
        Assert.Equal("https://homologacao.focusnfe.com.br/v2/nfse/evt7-abc", http.Pedidos[0].Endereco.ToString());

        var recusada = await focus.ConsultarAsync(AmbienteNfse.Homologacao, "evt7-abc", Ct);
        Assert.Equal(StatusNfse.Erro, recusada.Status);
        Assert.Equal("Inscrição municipal inválida (Confira o cadastro) | Alíquota incompatível", recusada.Mensagem);

        var inexistente = await focus.ConsultarAsync(AmbienteNfse.Homologacao, "evt7-abc", Ct);
        Assert.Equal(StatusNfse.Erro, inexistente.Status);
    }

    [Fact]
    public async Task Focus_recusa_o_envio_e_o_token()
    {
        var http = new HttpFalso(
            (HttpStatusCode.UnprocessableEntity, """{"codigo":"requisicao_invalida","mensagem":"CNPJ do emitente não autorizado"}"""),
            (HttpStatusCode.Unauthorized, """{"codigo":"permissao_negada","mensagem":"Token inválido"}"""));
        using var focus = new ProvedorNfseFocus(Tokens, http);

        var recusada = await focus.EmitirAsync(Pedido(), Ct);
        Assert.Equal((StatusNfse.Erro, "CNPJ do emitente não autorizado"), (recusada.Status, recusada.Mensagem));

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => focus.EmitirAsync(Pedido(), Ct));
        Assert.Contains("token", erro.Message);
    }

    [Fact]
    public async Task Focus_sem_token_nao_envia_e_sem_resposta_deixa_a_nota_aguardando()
    {
        var http = new HttpFalso { Falha = new HttpRequestException("Tempo esgotado") };
        using (var semToken = new ProvedorNfseFocus(new NfseOptions(), http))
        {
            var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => semToken.EmitirAsync(Pedido(), Ct));
            Assert.Contains("appsettings.Local.json", erro.Message);
            Assert.Empty(http.Pedidos);
        }

        using var focus = new ProvedorNfseFocus(Tokens, http);
        var resposta = await focus.EmitirAsync(Pedido(), Ct);
        Assert.Equal(StatusNfse.Processando, resposta.Status);
        Assert.Contains("Tempo esgotado", resposta.Mensagem);
        await Assert.ThrowsAsync<RegraNegocioException>(() => focus.ConsultarAsync(AmbienteNfse.Homologacao, "evt7-abc", Ct));
    }

    [Fact]
    public async Task Focus_cancela_com_a_justificativa()
    {
        var http = new HttpFalso((HttpStatusCode.OK, """{"status":"cancelado"}"""), (HttpStatusCode.OK, "<xml>nota</xml>"));
        using var focus = new ProvedorNfseFocus(Tokens, http);

        var resposta = await focus.CancelarAsync(AmbienteNfse.Producao, "evt7-abc", "Valor emitido errado no evento", Ct);
        Assert.Equal(StatusNfse.Cancelada, resposta.Status);
        Assert.Equal(HttpMethod.Delete, http.Pedidos[0].Metodo);
        Assert.Equal("https://api.focusnfe.com.br/v2/nfse/evt7-abc", http.Pedidos[0].Endereco.ToString());
        Assert.Equal("Valor emitido errado no evento", JsonNode.Parse(http.Pedidos[0].Corpo!)!["justificativa"]!.GetValue<string>());

        var xml = await focus.BaixarXmlAsync(AmbienteNfse.Producao, "/arquivos/4521.xml", Ct);
        Assert.Equal("<xml>nota</xml>", Encoding.UTF8.GetString(xml));
        Assert.Equal("https://api.focusnfe.com.br/arquivos/4521.xml", http.Pedidos[1].Endereco.ToString());
    }
}
