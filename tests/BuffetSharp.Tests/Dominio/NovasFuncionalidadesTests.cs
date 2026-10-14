using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Contratos, degustações, materiais, comissões, envio de proposta e auditoria.</summary>
public class NovasFuncionalidadesTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly DataEvento = new(2026, 6, 20);
    private const string CpfValido = "52998224725";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static Evento NovoEvento(int convidados = 100) =>
        new("Casamento", 1, 1, 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), convidados, 30_000m);

    private static Material Material(int patrimonio = 100, decimal locacao = 2m, decimal reposicao = 30m) =>
        ComId(new Material { Nome = "Prato raso", QuantidadePropria = patrimonio, CustoLocacao = locacao, ValorReposicao = reposicao }, 7);

    private static string TextoContrato => new string('x', 120) + "\nCLÁUSULA 1ª - DO OBJETO\nTexto do contrato.";

    // ------------------------- Contrato -------------------------

    [Fact]
    public void Contrato_emitido_guarda_hash_e_exige_assinatura_e_documento_validos()
    {
        var contrato = new Contrato(1, TextoContrato, Agora);
        Assert.Equal(StatusContrato.AguardandoAssinatura, contrato.Status);
        Assert.Equal(64, contrato.HashConteudo.Length);
        Assert.True(contrato.ConteudoIntegro);

        Assert.Throws<RegraNegocioException>(() => contrato.Assinar("Ana", "111.111.111-11", Png, null, null, Agora));
        Assert.Throws<RegraNegocioException>(() => contrato.Assinar("Ana", CpfValido, [], null, null, Agora));
        Assert.Throws<RegraNegocioException>(() => contrato.Assinar("Ana", CpfValido, Png, null, Png, Agora)); // falta o representante

        contrato.Assinar("Ana Souza", "529.982.247-25", Png, "Carla", Png, Agora);
        Assert.Equal(StatusContrato.Assinado, contrato.Status);
        Assert.Equal(CpfValido, contrato.DocumentoSignatario);
        Assert.Throws<RegraNegocioException>(() => contrato.Assinar("Ana", CpfValido, Png, null, null, Agora));
    }

    [Fact]
    public void Contrato_com_texto_alterado_apos_emissao_perde_a_integridade_e_nao_pode_ser_assinado()
    {
        var contrato = new Contrato(1, TextoContrato, Agora);
        typeof(Contrato).GetProperty(nameof(Contrato.Conteudo))!.SetValue(contrato, TextoContrato + " (adulterado)");

        Assert.False(contrato.ConteudoIntegro);
        Assert.Throws<RegraNegocioException>(() => contrato.Assinar("Ana", CpfValido, Png, null, null, Agora));
    }

    [Fact]
    public void Hash_do_contrato_ignora_diferenca_de_quebra_de_linha()
    {
        Assert.Equal(Contrato.CalcularHash("a\nb"), Contrato.CalcularHash("a\r\nb"));
        Assert.NotEqual(Contrato.CalcularHash("a\nb"), Contrato.CalcularHash("a\nc"));
    }

    [Fact]
    public void Contrato_cancelado_exige_motivo_e_nao_cancela_duas_vezes()
    {
        var contrato = new Contrato(1, TextoContrato, Agora);
        Assert.Throws<RegraNegocioException>(() => contrato.Cancelar(" ", Agora));
        contrato.Cancelar("Cliente pediu alteração", Agora);
        Assert.Equal(StatusContrato.Cancelado, contrato.Status);
        Assert.Throws<RegraNegocioException>(() => contrato.Cancelar("De novo", Agora));
    }

    // ------------------------- Degustação -------------------------

    [Fact]
    public void Degustacao_respeita_data_futura_horario_e_limite_de_pessoas()
    {
        var amanha15h = Agora.Date.AddDays(1).AddHours(15);
        Assert.Throws<RegraNegocioException>(() => new Degustacao(1, Agora.AddHours(-1), 2, null, null, null, Agora));
        Assert.Throws<RegraNegocioException>(() => new Degustacao(1, Agora.Date.AddDays(1).AddHours(22), 2, null, null, null, Agora));
        Assert.Throws<RegraNegocioException>(() => new Degustacao(1, amanha15h, Degustacao.MaximoPessoas + 1, null, null, null, Agora));

        var degustacao = new Degustacao(1, amanha15h, 4, 2, null, "Sem glúten", Agora);
        Assert.Equal(StatusDegustacao.Agendada, degustacao.Status);
    }

    [Fact]
    public void Degustacao_realizada_somente_apos_o_horario_e_com_nota_de_1_a_5()
    {
        var quando = Agora.Date.AddDays(1).AddHours(15);
        var degustacao = new Degustacao(1, quando, 2, null, null, null, Agora);

        Assert.Throws<RegraNegocioException>(() => degustacao.Realizar(5, null, quando.AddMinutes(-10)));
        Assert.Throws<RegraNegocioException>(() => degustacao.Realizar(6, null, quando.AddHours(1)));

        degustacao.Realizar(4, "Adoraram", quando.AddHours(1));
        Assert.Equal(StatusDegustacao.Realizada, degustacao.Status);
        Assert.Equal(4, degustacao.Avaliacao);
        Assert.Throws<RegraNegocioException>(() => degustacao.Cancelar("Tarde demais"));
    }

    // ------------------------- Materiais -------------------------

    [Fact]
    public void Reserva_de_material_valida_quantidades_e_atualiza_reserva_existente()
    {
        var evento = NovoEvento();
        var material = Material();

        Assert.Throws<RegraNegocioException>(() => evento.ReservarMaterial(material, 0));
        Assert.Throws<RegraNegocioException>(() => evento.ReservarMaterial(material, 10, alugada: 11));

        evento.ReservarMaterial(material, 120, alugada: 20);
        evento.ReservarMaterial(material, 130, alugada: 30);

        var reserva = Assert.Single(evento.Materiais);
        Assert.Equal(100, reserva.QuantidadePropria);
        Assert.Equal(60m, evento.CustoMateriais); // 30 alugadas x R$ 2
        Assert.Equal(1, evento.MateriaisPendentesDeDevolucao);
    }

    [Fact]
    public void Devolucao_registra_perdas_no_custo_e_informa_perdas_do_patrimonio_proprio()
    {
        var evento = NovoEvento();
        var material = Material(patrimonio: 100, locacao: 2m, reposicao: 30m);
        evento.ReservarMaterial(material, 105, alugada: 5);

        Assert.Throws<RegraNegocioException>(() => evento.RegistrarDevolucao(material.Id, 100, 4, evento.Fim)); // não fecha
        Assert.Throws<RegraNegocioException>(() => evento.RegistrarDevolucao(material.Id, 102, 3, evento.Inicio.AddHours(-1)));

        var perdasProprias = evento.RegistrarDevolucao(material.Id, 102, 3, evento.Fim.AddHours(10));

        Assert.Equal(3, perdasProprias);
        Assert.Equal(10m + 90m, evento.CustoMateriais); // locação 5 x 2 + perdas 3 x 30
        Assert.Equal(0, evento.MateriaisPendentesDeDevolucao);
        Assert.Throws<RegraNegocioException>(() => evento.RemoverMaterial(material.Id));
        Assert.Throws<RegraNegocioException>(() => evento.RegistrarDevolucao(material.Id, 105, 0, evento.Fim));

        material.AjustarPatrimonio(-perdasProprias);
        Assert.Equal(97, material.QuantidadePropria);
        Assert.Throws<RegraNegocioException>(() => material.AjustarPatrimonio(-98));
    }

    [Fact]
    public void Perdas_alem_das_unidades_proprias_sao_de_itens_alugados()
    {
        var evento = NovoEvento();
        var material = Material(patrimonio: 10);
        evento.ReservarMaterial(material, 30, alugada: 28); // 2 próprias

        Assert.Equal(2, evento.RegistrarDevolucao(material.Id, 25, 5, evento.Fim));
    }

    // ------------------------- Comissão -------------------------

    [Fact]
    public void Evento_aprovado_herda_o_vendedor_e_congela_a_comissao()
    {
        var orcamento = new Orcamento(1, 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), 100, DataEvento.AddDays(-30));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 5000 }, 1));
        orcamento.DefinirVendedor(9);

        var evento = orcamento.Aprovar("Festa", Agora, percentualComissao: 3m);
        Assert.Equal(9, evento.VendedorId);
        Assert.Equal(3m, evento.PercentualComissao);

        evento.DefinirVendedor(null, 5m);
        Assert.Equal(0m, evento.PercentualComissao);
        Assert.Throws<RegraNegocioException>(() => evento.DefinirVendedor(9, Usuario.ComissaoMaximaPercentual + 1));
    }

    [Fact]
    public void Fechamento_de_comissao_gera_conta_a_pagar_e_detecta_sobreposicao()
    {
        var vendedor = ComId(new Usuario("Carla", "carla", PerfilUsuario.Comercial, "hash"), 3);
        var fechamento = new FechamentoComissao(vendedor, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 10_000m, 300m,
            new DateOnly(2026, 2, 5));

        Assert.Equal(CategoriaDespesa.Comissoes, fechamento.ContaPagar!.Categoria);
        Assert.Equal(300m, fechamento.ContaPagar.Valor);
        Assert.True(fechamento.Sobrepoe(new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15)));
        Assert.False(fechamento.Sobrepoe(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
        Assert.Throws<RegraNegocioException>(() =>
            new FechamentoComissao(vendedor, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 0m, 0m, new DateOnly(2026, 3, 5)));
    }

    [Fact]
    public void Comissao_do_usuario_e_limitada_e_so_comercial_ou_administrador_vende()
    {
        var usuario = new Usuario("Fábio", "fabio", PerfilUsuario.Financeiro, "hash");
        Assert.Throws<RegraNegocioException>(() => usuario.DefinirComissao(-1));
        Assert.Throws<RegraNegocioException>(() => usuario.DefinirComissao(Usuario.ComissaoMaximaPercentual + 0.01m));
        Assert.False(usuario.PodeVender);
        Assert.True(new Usuario("Carla", "carla", PerfilUsuario.Comercial, "hash").PodeVender);
    }

    // ------------------------- Envio de proposta -------------------------

    [Fact]
    public void Envio_da_proposta_marca_rascunho_como_enviado_e_fica_no_historico()
    {
        var orcamento = new Orcamento(1, 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), 100, DataEvento.AddDays(-30));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 5000 }, 1));
        orcamento.DefinirPacote(ComId(new Domain.Cardapio.Pacote { Nome = "Prata", PrecoPorPessoa = 100, MinimoConvidados = 50 }, 1));

        orcamento.RegistrarEnvio(CanalEnvio.WhatsApp, "(11) 98888-7777", "carla", Agora);
        orcamento.RegistrarEnvio(CanalEnvio.Email, "ana@email.com", "carla", Agora.AddHours(1));

        Assert.Equal(StatusOrcamento.Enviado, orcamento.Status);
        Assert.Equal(Agora, orcamento.EnviadoEm);
        Assert.Equal(2, orcamento.Envios.Count);

        orcamento.Recusar("Fechou com outro", Agora.AddDays(1));
        Assert.Throws<RegraNegocioException>(() => orcamento.RegistrarEnvio(CanalEnvio.Email, "ana@email.com", null, Agora.AddDays(2)));
    }

    [Theory]
    [InlineData("(11) 98888-7777", "5511988887777")]
    [InlineData("5511988887777", "5511988887777")]
    public void Link_do_whatsapp_usa_ddi_e_texto_codificado(string telefone, string esperado)
    {
        var mensagem = new MensagemWhatsApp(telefone, "Olá! Proposta & valores");
        Assert.Equal(esperado, mensagem.TelefoneInternacional);
        Assert.Equal($"https://wa.me/{esperado}?text=Ol%C3%A1%21%20Proposta%20%26%20valores", mensagem.Link);
    }

    // ------------------------- Auditoria -------------------------

    [Fact]
    public void Alteracoes_da_auditoria_sao_serializadas_com_acentos_legiveis()
    {
        var json = AlteracaoCampo.Serializar([new AlteracaoCampo("Endereco.Cidade", "São Paulo", null)]);
        Assert.Contains("São Paulo", json);

        var lidas = AlteracaoCampo.Ler(json);
        Assert.Equal(new AlteracaoCampo("Endereco.Cidade", "São Paulo", null), Assert.Single(lidas));
        Assert.Empty(AlteracaoCampo.Ler(null));
    }
}
