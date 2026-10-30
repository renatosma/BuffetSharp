using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Dominio;

public class MargemOrcamentoTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly PercentuaisReceita DezPorCento = new(2, 8, 0);

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    [Fact]
    public void Custos_sobre_a_receita_acompanham_o_total_e_a_margem_sai_em_percentual()
    {
        var custos = CalculadoraMargem.Calcular(10_000m, 3_000m, 2_000m, 500m, 500m, new PercentuaisReceita(3, 8, 1.5m));

        Assert.Equal((6_000m, 300m, 800m, 150m), (custos.Diretos, custos.Comissao, custos.Impostos, custos.Taxa));
        Assert.Equal(7_250m, custos.Total);
        Assert.Equal(27.5m, CalculadoraMargem.Percentual(10_000m, custos));
        Assert.Null(CalculadoraMargem.Percentual(10_000m, CustosOrcamento.Nenhum)); // sem custos não há margem
        Assert.Equal(FaixaMargem.Atencao, CalculadoraMargem.Faixa(27.5m, 20, 30));
        Assert.Equal(FaixaMargem.AbaixoDaMinima, CalculadoraMargem.Faixa(19.9m, 20, 30));
        Assert.Equal(FaixaMargem.Boa, CalculadoraMargem.Faixa(30m, 20, 30));
        Assert.Equal(FaixaMargem.SemCusto, CalculadoraMargem.Faixa(null, 20, 30));
    }

    [Fact]
    public void Desconto_maximo_mantem_a_margem_minima()
    {
        // R − 6.000 − 10%·R ≥ 20%·R  →  R ≥ 6.000 / 0,7 = 8.571,43: até 14,28% de desconto sobre 10.000.
        Assert.Equal(8_571.43m, CalculadoraMargem.ReceitaMinima(6_000m, DezPorCento, 20));
        Assert.Equal(14.28m, CalculadoraMargem.DescontoMaximo(10_000m, 6_000m, DezPorCento, 20));
        var receita = 10_000m * (1 - 0.1428m);
        Assert.True(CalculadoraMargem.Percentual(receita, CalculadoraMargem.Calcular(receita, 6_000m, 0, 0, 0, DezPorCento)) >= 20);

        Assert.Equal(0m, CalculadoraMargem.DescontoMaximo(7_000m, 6_000m, DezPorCento, 20)); // nem sem desconto chega lá
        Assert.Equal(30m, CalculadoraMargem.DescontoMaximo(100_000m, 6_000m, DezPorCento, 20)); // limitado ao máximo do campo
        Assert.Null(CalculadoraMargem.ReceitaMinima(6_000m, new PercentuaisReceita(50, 40, 0), 20));
    }

    /// <summary>Orçamento de R$ 12.000 (tabela) com os custos diretos informados e 10% sobre a receita.</summary>
    private static Orcamento Orcamento(decimal custosDiretos, decimal desconto = 0)
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = ComId(new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), data.AddDays(-30)), 42);
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 2_000m }, 1));
        orcamento.AplicarDesconto(desconto);
        orcamento.DefinirValorTabela(12_000m);
        DefinirCustos(orcamento, custosDiretos);
        return orcamento;
    }

    private static void DefinirCustos(Orcamento orcamento, decimal custosDiretos) =>
        orcamento.DefinirCustos(CalculadoraMargem.Calcular(orcamento.ValorTotal, custosDiretos, 0, 0, 0, DezPorCento));

    [Fact]
    public void Margem_abaixo_da_minima_precisa_do_administrador_mesmo_sem_desconto()
    {
        var orcamento = Orcamento(custosDiretos: 9_000m); // 12.000 − 9.000 − 1.200 = 1.800: 15%
        Assert.Equal(15m, orcamento.MargemPercentual);

        Assert.Equal(ConferenciaAlcada.PrecisaAprovacao, orcamento.ConferirAlcada(5, "carla", Agora, margemMinima: 20));
        orcamento.SolicitarAprovacaoDesconto("Cliente estratégico", "carla", Agora, margemMinima: 20);
        Assert.Equal((SituacaoDesconto.Aguardando, (decimal?)15m), (orcamento.SituacaoDesconto, orcamento.MargemAprovada));
        Assert.Contains("margem", Assert.Throws<RegraNegocioException>(() => orcamento.Enviar(Agora)).Message);
        Assert.Throws<RegraNegocioException>(() => orcamento.AprovarDesconto("gerente", 100, null, Agora, aprovaMargem: false));

        orcamento.AprovarDesconto("admin", 100, null, Agora, aprovaMargem: true);
        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(5, "carla", Agora, margemMinima: 20));

        DefinirCustos(orcamento, 9_600m); // a margem caiu de novo (10%): nova aprovação
        Assert.Equal(ConferenciaAlcada.PrecisaAprovacao, orcamento.ConferirAlcada(5, "carla", Agora, margemMinima: 20));
        DefinirCustos(orcamento, 6_000m); // voltou para 40%: a aprovação anterior (até 15%) continua valendo
        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(5, "carla", Agora, margemMinima: 20));
        Assert.Equal(SituacaoDesconto.Aprovado, orcamento.SituacaoDesconto);
        orcamento.Enviar(Agora);
    }

    [Fact]
    public void Administrador_aceita_margem_baixa_ao_salvar_e_a_copia_leva_os_custos()
    {
        var orcamento = Orcamento(custosDiretos: 9_000m);

        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(100, "admin", Agora, margemMinima: 20, aprovaMargem: true));
        Assert.Equal((SituacaoDesconto.DentroDaAlcada, (decimal?)15m, "admin"),
            (orcamento.SituacaoDesconto, orcamento.MargemAprovada, orcamento.DescontoConcedidoPor));
        Assert.Equal(ConferenciaAlcada.Liberado, orcamento.ConferirAlcada(5, "carla", Agora, margemMinima: 20)); // já autorizada

        var copia = orcamento.Duplicar(DateOnly.FromDateTime(Agora), 15);
        Assert.Equal(orcamento.Custos, copia.Custos);
        Assert.Equal(ConferenciaAlcada.PrecisaAprovacao, copia.ConferirAlcada(5, "carla", Agora, margemMinima: 20)); // a aprovação não vai junto
    }
}
