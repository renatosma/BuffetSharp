using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Tests.Dominio;

public class AlcadaDescontoTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private const decimal LimiteComercial = 5;

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    /// <summary>100 convidados a R$ 100 (tabela) e salão de R$ 2.000: tabela de R$ 12.000.</summary>
    private static Orcamento Orcamento(decimal? valorPorPessoa = null, decimal desconto = 0)
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = ComId(new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), data.AddDays(-30)), 42);
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1), valorPorPessoa);
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 2_000m }, 1));
        orcamento.AplicarDesconto(desconto);
        orcamento.DefinirValorTabela(12_000m);
        return orcamento;
    }

    [Fact]
    public void Desconto_efetivo_soma_o_campo_e_o_preco_abaixo_da_tabela()
    {
        Assert.Equal((10m, 1_200m), (Orcamento(desconto: 10).DescontoEfetivoPercentual, Orcamento(desconto: 10).DescontoEfetivoValor));
        Assert.Equal(10m, Orcamento(valorPorPessoa: 88m).DescontoEfetivoPercentual); // 8.800 + 2.000 = 10.800: 1.200 abaixo
        Assert.Equal(15.4m, Orcamento(valorPorPessoa: 88m, desconto: 6).DescontoEfetivoPercentual); // 10.800 - 6% = 10.152
        Assert.Equal(0m, Orcamento(valorPorPessoa: 110m, desconto: 5).DescontoEfetivoPercentual); // acima da tabela compensa
    }

    [Fact]
    public void Dentro_da_alcada_fica_autorizado_por_quem_deu()
    {
        var orcamento = Orcamento(desconto: 4);

        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));
        Assert.Equal((SituacaoDesconto.DentroDaAlcada, 4m, "carla"),
            (orcamento.SituacaoDesconto, orcamento.PercentualDescontoAutorizado, orcamento.DescontoConcedidoPor));
        orcamento.Enviar(Agora);
        Assert.Equal(StatusOrcamento.Enviado, orcamento.Status);
    }

    [Fact]
    public void Acima_da_alcada_aguarda_a_aprovacao_e_nao_vai_ao_cliente_nem_vira_evento()
    {
        var orcamento = Orcamento(desconto: 12);
        Assert.Equal(ConferenciaAlcada.PrecisaAprovacao, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));
        Assert.Throws<RegraNegocioException>(() => orcamento.SolicitarAprovacaoDesconto(" ", "carla", Agora)); // motivo obrigatório

        orcamento.SolicitarAprovacaoDesconto("Cliente fechando com a concorrência", "carla", Agora);

        Assert.Equal((SituacaoDesconto.Aguardando, 12m), (orcamento.SituacaoDesconto, orcamento.PercentualDescontoAutorizado));
        Assert.Contains("aguardando", Assert.Throws<RegraNegocioException>(() => orcamento.Enviar(Agora)).Message);
        Assert.Throws<RegraNegocioException>(() => orcamento.RegistrarEnvio(CanalEnvio.Email, "cliente@exemplo.com", "carla", Agora));
        Assert.Throws<RegraNegocioException>(() => orcamento.Aprovar("Casamento", Agora));
        Assert.Equal(ConferenciaAlcada.Pendente, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora)); // salvar de novo não pede outra vez

        Assert.Throws<RegraNegocioException>(() => orcamento.AprovarDesconto("gerente", 10, null, Agora)); // passa da alçada de quem aprova
        orcamento.AprovarDesconto("admin", 100, "Cliente antigo", Agora);

        Assert.Equal((SituacaoDesconto.Aprovado, "admin", "Cliente antigo"),
            (orcamento.SituacaoDesconto, orcamento.DescontoDecididoPor, orcamento.RespostaDesconto));
        orcamento.Enviar(Agora);
    }

    [Fact]
    public void Desconto_aprovado_vale_ate_o_percentual_aprovado()
    {
        var orcamento = Orcamento(desconto: 12);
        orcamento.ConferirAlcada(LimiteComercial, "carla", Agora);
        orcamento.SolicitarAprovacaoDesconto("Data de baixa procura", "carla", Agora);
        orcamento.AprovarDesconto("admin", 100, null, Agora);

        orcamento.AplicarDesconto(10);
        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));
        Assert.Equal(SituacaoDesconto.Aprovado, orcamento.SituacaoDesconto);

        orcamento.AplicarDesconto(15); // subiu além do aprovado: precisa de nova aprovação
        Assert.Equal(ConferenciaAlcada.PrecisaAprovacao, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));

        orcamento.AplicarDesconto(3); // voltou para dentro da alçada de quem salva
        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));
    }

    [Fact]
    public void Recusado_bloqueia_ate_baixar_o_desconto()
    {
        var orcamento = Orcamento(valorPorPessoa: 80m); // R$ 2.000 abaixo da tabela pelo preço negociado
        Assert.Equal(ConferenciaAlcada.PrecisaAprovacao, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));
        orcamento.SolicitarAprovacaoDesconto("Pedido do cliente", "carla", Agora);
        Assert.Throws<RegraNegocioException>(() => orcamento.RecusarDesconto("admin", null, Agora));
        orcamento.RecusarDesconto("admin", "Margem muito baixa", Agora);

        Assert.Contains("recusado", Assert.Throws<RegraNegocioException>(() => orcamento.Enviar(Agora)).Message);
        Assert.Equal(ConferenciaAlcada.Pendente, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));

        orcamento.DefinirPacote(orcamento.Pacote, 97m); // 11.700: 2,5% abaixo da tabela
        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(LimiteComercial, "carla", Agora));
        Assert.Equal(SituacaoDesconto.DentroDaAlcada, orcamento.SituacaoDesconto);
        orcamento.Enviar(Agora);
    }

    [Fact]
    public void Recebimento_aponta_o_maior_desconto_e_a_aprovacao_exige_motivo()
    {
        var orcamento = Orcamento();
        orcamento.Enviar(Agora);
        var evento = ComId(orcamento.Aprovar("Casamento", Agora), 7);
        var parcelas = evento.GerarParcelas(2, new DateOnly(2026, 4, 10));
        var baixa = new Baixa(TipoBaixa.Recebimento, DateOnly.FromDateTime(Agora), DateOnly.FromDateTime(Agora), null, "fabio");
        baixa.AdicionarTitulo(parcelas[0], parcelas[0].Valor, 0, 300);
        baixa.AdicionarTitulo(parcelas[1], parcelas[1].Valor, 0, 900);

        Assert.Equal(15m, baixa.MaiorDescontoPercentual); // 900 de 6.000
        Assert.Throws<RegraNegocioException>(() => baixa.AprovarDesconto("admin", ""));
        baixa.AprovarDesconto("admin", "Pagamento à vista antecipado");
        Assert.Equal(("admin", "Pagamento à vista antecipado"), (baixa.DescontoAprovadoPor, baixa.MotivoDesconto));
    }

    [Fact]
    public void Usuario_tem_limite_proprio_e_contato_para_os_avisos()
    {
        var usuario = new Usuario("Carla", "carla", PerfilUsuario.Comercial, "hash");
        usuario.DefinirDescontoMaximo(8);
        usuario.DefinirContato(" carla@exemplo.com ", "(11) 98888-7777");
        Assert.Equal((8m, "carla@exemplo.com", "(11) 98888-7777"), (usuario.DescontoMaximo, usuario.Email, usuario.Celular));

        usuario.DefinirDescontoMaximo(null); // volta ao padrão do perfil
        Assert.Null(usuario.DescontoMaximo);
        Assert.Throws<RegraNegocioException>(() => usuario.DefinirDescontoMaximo(120));
        Assert.Throws<RegraNegocioException>(() => usuario.DefinirContato("carla", null));
        Assert.Throws<RegraNegocioException>(() => usuario.DefinirContato(null, "9888"));
    }
}
