using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Infrastructure.Cobranca;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Boleto/PIX registrado: regras da cobrança e a conversa com o Asaas (HTTP simulado).</summary>
public class CobrancaRegistradaTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    /// <summary>Evento de R$ 10.000 em duas parcelas de R$ 5.000.</summary>
    private static Parcela Parcela()
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), validoAte: data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(Agora);
        var evento = ComId(orcamento.Aprovar("Casamento Ana e Bruno", Agora), 7);
        return ComId(evento.GerarParcelas(2, new DateOnly(2026, 4, 10))[0], 31);
    }

    // ============================== Cobrança ==============================

    [Fact]
    public void Cobranca_nasce_pendente_com_o_valor_e_a_descricao_da_parcela()
    {
        var parcela = Parcela();
        var cobranca = new CobrancaRegistrada(parcela, AmbienteCobranca.Sandbox, TipoCobranca.Boleto, parcela.Vencimento, Hoje, "admin", Agora);

        Assert.Equal((StatusCobranca.Pendente, 5000m, 31, 7), (cobranca.Status, cobranca.Valor, cobranca.ParcelaId, cobranca.EventoId));
        Assert.Equal(parcela.Descricao, cobranca.Descricao);
        Assert.StartsWith("parc31-", cobranca.Referencia);
        Assert.Equal(24, cobranca.Referencia.Length);

        cobranca.RegistrarEmissao("pay_123", "cus_9", "23790.00000 ...", "123", "https://asaas/b.pdf", "https://asaas/i/pay_123", "000201...");
        Assert.Equal(("pay_123", "cus_9"), (cobranca.IdExterno, cobranca.ClienteExterno));
        Assert.Throws<RegraNegocioException>(() => cobranca.RegistrarEmissao("pay_124", null, null, null, null, null, null));
    }

    [Fact]
    public void Cobranca_recusa_vencimento_no_passado_e_parcela_paga()
    {
        var parcela = Parcela();
        var erro = Assert.Throws<RegraNegocioException>(() =>
            new CobrancaRegistrada(parcela, AmbienteCobranca.Simulado, TipoCobranca.Pix, Hoje.AddDays(-1), Hoje, null, Agora));
        Assert.Contains("passado", erro.Message);

        parcela.RegistrarPagamento(Hoje, parcela.Valor, null);
        Assert.Throws<RegraNegocioException>(() =>
            new CobrancaRegistrada(parcela, AmbienteCobranca.Simulado, TipoCobranca.Pix, Hoje, Hoje, null, Agora));
    }

    [Fact]
    public void Cobranca_paga_registra_a_baixa_e_nao_cancela_mais()
    {
        var parcela = Parcela();
        var cobranca = new CobrancaRegistrada(parcela, AmbienteCobranca.Simulado, TipoCobranca.Boleto, Hoje.AddDays(5), Hoje, null, Agora);
        Assert.Throws<RegraNegocioException>(() => cobranca.RegistrarBaixa(true));

        cobranca.RegistrarPagamento(Hoje, 5100m, 5098.01m, TipoFormaPagamento.Pix);
        Assert.Equal((StatusCobranca.Paga, 5100m, 5098.01m, TipoFormaPagamento.Pix), (cobranca.Status, cobranca.ValorPago, cobranca.ValorLiquido, cobranca.PagaCom));
        cobranca.RegistrarBaixa(false, "A parcela já estava recebida");
        Assert.False(cobranca.Baixada);
        Assert.Equal("A parcela já estava recebida", cobranca.Mensagem);

        Assert.Throws<RegraNegocioException>(() => cobranca.Cancelar(Agora));
        Assert.Throws<RegraNegocioException>(() => cobranca.RegistrarPagamento(Hoje, 1, null, null));
    }

    [Fact]
    public void Configuracao_pede_a_conta_e_limita_multa_e_juros()
    {
        Assert.Equal(["a conta bancária que recebe os pagamentos"], new ConfiguracaoCobranca().Pendencias());
        Assert.Empty(new ConfiguracaoCobranca { ContaFinanceiraId = 3 }.Pendencias());
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new ConfiguracaoCobranca { MultaPercentual = 2.5m }));
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new ConfiguracaoCobranca { JurosMensalPercentual = 11 }));
    }

    // ============================== Asaas ==============================

    private sealed class HttpFalso(params (HttpStatusCode Status, string Corpo)[] respostas) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode, string)> _respostas = new(respostas);
        public List<(HttpMethod Metodo, string Endereco, string? Chave, string? Agente, string? Corpo)> Pedidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add((request.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("access_token", out var chave) ? chave.Single() : null, request.Headers.UserAgent.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            var (status, corpo) = _respostas.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly AsaasOptions Chaves = new() { ChaveSandbox = "chave-sandbox", ChaveProducao = "chave-producao" };

    private static PedidoCobranca Pedido(TipoCobranca tipo = TipoCobranca.Boleto, AmbienteCobranca ambiente = AmbienteCobranca.Sandbox) =>
        new(ambiente, new PagadorCobranca("52998224725", "Ana Souza", "ana@exemplo.com", "(11) 98888-7777", "01001-000", "10", "cliente-5"),
            tipo, 5000m, new DateOnly(2026, 4, 10), "Parcela 1/2 - EVT-00007 Casamento", "parc31-abc", 2m, 1m);

    private const string CobrancaCriada =
        """{"id":"pay_080225913252","customer":"cus_000005219613","status":"PENDING","invoiceUrl":"https://sandbox.asaas.com/i/080225913252","bankSlipUrl":"https://sandbox.asaas.com/b/pdf/080225913252","nossoNumero":"6453"}""";

    [Fact]
    public async Task Asaas_usa_o_cliente_existente_e_cria_o_boleto_com_multa_e_juros()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"data":[{"id":"cus_000005219613","cpfCnpj":"52998224725"}],"totalCount":1}"""),
            (HttpStatusCode.OK, CobrancaCriada),
            (HttpStatusCode.OK, """{"identificationField":"00190000090275928800021932978170187890000005000","nossoNumero":"6453","barCode":"0019..."}"""),
            (HttpStatusCode.OK, """{"encodedImage":"iVBORw0KGgo=","payload":"00020101021226820014br.gov.bcb.pix2560qrpix.asaas.com","expirationDate":"2026-05-10 23:59:59"}"""));
        using var asaas = new ProvedorCobrancaAsaas(Chaves, http);

        var emitida = await asaas.EmitirAsync(Pedido(), Ct);

        Assert.Equal(("pay_080225913252", "cus_000005219613"), (emitida.Id, emitida.ClienteId));
        Assert.Equal("00190000090275928800021932978170187890000005000", emitida.LinhaDigitavel);
        Assert.Equal("https://sandbox.asaas.com/b/pdf/080225913252", emitida.UrlBoleto);
        Assert.Equal("https://sandbox.asaas.com/i/080225913252", emitida.UrlFatura);
        Assert.StartsWith("000201", emitida.PixCopiaECola);

        Assert.Equal("https://api-sandbox.asaas.com/v3/customers?cpfCnpj=52998224725", http.Pedidos[0].Endereco);
        Assert.All(http.Pedidos, p => Assert.Equal("chave-sandbox", p.Chave));
        Assert.All(http.Pedidos, p => Assert.StartsWith("BuffetSharp", p.Agente));
        var (metodo, endereco, _, _, corpo) = http.Pedidos[1];
        Assert.Equal((HttpMethod.Post, "https://api-sandbox.asaas.com/v3/payments"), (metodo, endereco));
        var json = JsonNode.Parse(corpo!)!;
        Assert.Equal("cus_000005219613", json["customer"]!.GetValue<string>());
        Assert.Equal("BOLETO", json["billingType"]!.GetValue<string>());
        Assert.Equal(5000m, json["value"]!.GetValue<decimal>());
        Assert.Equal("2026-04-10", json["dueDate"]!.GetValue<string>());
        Assert.Equal("parc31-abc", json["externalReference"]!.GetValue<string>());
        Assert.Equal(2m, json["fine"]!["value"]!.GetValue<decimal>());
        Assert.Equal(1m, json["interest"]!["value"]!.GetValue<decimal>());
        Assert.EndsWith("payments/pay_080225913252/identificationField", http.Pedidos[2].Endereco);
        Assert.EndsWith("payments/pay_080225913252/pixQrCode", http.Pedidos[3].Endereco);
    }

    [Fact]
    public async Task Asaas_cadastra_o_cliente_novo_e_o_pix_sem_linha_digitavel()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"data":[],"totalCount":0}"""),
            (HttpStatusCode.OK, """{"id":"cus_novo"}"""),
            (HttpStatusCode.OK, CobrancaCriada),
            (HttpStatusCode.OK, """{"payload":"000201pix"}"""));
        using var asaas = new ProvedorCobrancaAsaas(Chaves, http);

        var emitida = await asaas.EmitirAsync(Pedido(TipoCobranca.Pix, AmbienteCobranca.Producao), Ct);

        Assert.Equal(("cus_novo", null, "000201pix"), (emitida.ClienteId, emitida.LinhaDigitavel, emitida.PixCopiaECola));
        Assert.Equal(4, http.Pedidos.Count);
        Assert.All(http.Pedidos, p => Assert.StartsWith("https://api.asaas.com/v3/", p.Endereco));
        Assert.All(http.Pedidos, p => Assert.Equal("chave-producao", p.Chave));
        var cliente = JsonNode.Parse(http.Pedidos[1].Corpo!)!;
        Assert.Equal(("Ana Souza", "52998224725", "11988887777", "01001000", "10", "cliente-5"),
            (cliente["name"]!.GetValue<string>(), cliente["cpfCnpj"]!.GetValue<string>(), cliente["mobilePhone"]!.GetValue<string>(),
                cliente["postalCode"]!.GetValue<string>(), cliente["addressNumber"]!.GetValue<string>(), cliente["externalReference"]!.GetValue<string>()));
        Assert.Equal("PIX", JsonNode.Parse(http.Pedidos[2].Corpo!)!["billingType"]!.GetValue<string>());
    }

    [Fact]
    public async Task Asaas_sem_pix_na_conta_mantem_o_boleto()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"data":[{"id":"cus_1"}]}"""),
            (HttpStatusCode.OK, CobrancaCriada),
            (HttpStatusCode.OK, """{"identificationField":"123"}"""),
            (HttpStatusCode.BadRequest, """{"errors":[{"code":"invalid_action","description":"Não há nenhuma chave Pix cadastrada."}]}"""));
        using var asaas = new ProvedorCobrancaAsaas(Chaves, http);

        var emitida = await asaas.EmitirAsync(Pedido(), Ct);
        Assert.Equal(("pay_080225913252", "123", null), (emitida.Id, emitida.LinhaDigitavel, emitida.PixCopiaECola));
    }

    [Fact]
    public async Task Asaas_le_o_pagamento_a_cobranca_em_aberto_e_a_excluida()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"id":"pay_1","status":"RECEIVED","billingType":"PIX","value":5050.00,"netValue":5048.01,"paymentDate":"2026-04-12","clientPaymentDate":"2026-04-12"}"""),
            (HttpStatusCode.OK, """{"id":"pay_1","status":"OVERDUE","billingType":"BOLETO","value":5000.00}"""),
            (HttpStatusCode.OK, """{"id":"pay_1","status":"PENDING","deleted":true}"""),
            (HttpStatusCode.OK, """{"id":"pay_1","status":"CONFIRMED","billingType":"CREDIT_CARD","value":5000,"netValue":4850.5,"confirmedDate":"2026-04-11"}"""));
        using var asaas = new ProvedorCobrancaAsaas(Chaves, http);

        Assert.Equal(new SituacaoCobranca(StatusCobranca.Paga, new DateOnly(2026, 4, 12), 5050m, 5048.01m, TipoFormaPagamento.Pix),
            await asaas.ConsultarAsync(AmbienteCobranca.Sandbox, "pay_1", Ct));
        Assert.Equal(StatusCobranca.Pendente, (await asaas.ConsultarAsync(AmbienteCobranca.Sandbox, "pay_1", Ct)).Status);
        Assert.Equal(StatusCobranca.Cancelada, (await asaas.ConsultarAsync(AmbienteCobranca.Sandbox, "pay_1", Ct)).Status);
        Assert.Equal(new SituacaoCobranca(StatusCobranca.Paga, new DateOnly(2026, 4, 11), 5000m, 4850.5m, TipoFormaPagamento.CartaoCredito),
            await asaas.ConsultarAsync(AmbienteCobranca.Sandbox, "pay_1", Ct));
        Assert.Equal((HttpMethod.Get, "https://api-sandbox.asaas.com/v3/payments/pay_1"), (http.Pedidos[0].Metodo, http.Pedidos[0].Endereco));
    }

    [Fact]
    public async Task Asaas_cancela_e_traduz_os_erros()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"deleted":true,"id":"pay_1"}"""),
            (HttpStatusCode.BadRequest, """{"errors":[{"code":"invalid_customer.cpfCnpj","description":"O CPF/CNPJ informado é inválido."}]}"""),
            (HttpStatusCode.Unauthorized, """{"errors":[{"code":"invalid_access_token","description":"A chave de API informada não pertence a este ambiente"}]}"""));
        using var asaas = new ProvedorCobrancaAsaas(Chaves, http);

        await asaas.CancelarAsync(AmbienteCobranca.Sandbox, "pay_1", Ct);
        Assert.Equal((HttpMethod.Delete, "https://api-sandbox.asaas.com/v3/payments/pay_1"), (http.Pedidos[0].Metodo, http.Pedidos[0].Endereco));

        var recusa = await Assert.ThrowsAsync<RegraNegocioException>(() => asaas.EmitirAsync(Pedido(), Ct));
        Assert.Contains("O CPF/CNPJ informado é inválido.", recusa.Message);
        var chave = await Assert.ThrowsAsync<RegraNegocioException>(() => asaas.ConsultarAsync(AmbienteCobranca.Sandbox, "pay_1", Ct));
        Assert.Contains("chave da API", chave.Message);

        using var semChave = new ProvedorCobrancaAsaas(new AsaasOptions(), http);
        var falta = await Assert.ThrowsAsync<RegraNegocioException>(() => semChave.ConsultarAsync(AmbienteCobranca.Producao, "pay_1", Ct));
        Assert.Contains("ChaveProducao", falta.Message);
        Assert.Equal(3, http.Pedidos.Count);
    }
}
