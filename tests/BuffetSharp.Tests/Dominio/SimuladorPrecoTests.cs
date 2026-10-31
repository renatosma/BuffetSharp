using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;

namespace BuffetSharp.Tests.Dominio;

public class SimuladorPrecoTests
{
    private static readonly PercentuaisReceita DezPorCento = new(2, 8, 0);

    [Fact]
    public void Preco_para_a_margem_cobre_os_diretos_e_os_percentuais()
    {
        // 6.000 de custos diretos para 100 pessoas, 10% sobre a receita e margem de 20%: 6.000 / (100 × 0,7) = 85,71 → 85,80.
        var preco = SimuladorPreco.PrecoPara(6_000m, 100, DezPorCento, 20);
        Assert.Equal(85.8m, preco);
        Assert.True(SimuladorPreco.Margem(preco!.Value, 6_000m, 100, DezPorCento) >= 20);
        Assert.Equal(30m, SimuladorPreco.Margem(100m, 6_000m, 100, DezPorCento)); // 10.000 − 6.000 − 1.000 = 3.000
        Assert.Null(SimuladorPreco.PrecoPara(6_000m, 100, new PercentuaisReceita(40, 45, 0), 20)); // os percentuais já consomem a margem
    }

    [Fact]
    public void Cenario_soma_insumos_pela_duracao_e_adultos_e_a_equipe()
    {
        // Por pessoa: 20 fixos, 1 por hora; por adulto: 3 por hora (cerveja). 100 pessoas, 80 adultos, 4 horas; equipe de 2.000.
        var cenario = new CenarioPreco(new ContextoConsumo(100, 80, 4), new CustoInsumos(20, 1, 0, 3), 2_000m, DezPorCento);

        var resultado = SimuladorPreco.Simular(cenario, margemMinima: 20, margemAlvo: 30, precoAtual: 90m);

        Assert.Equal((33.6m, 20m, 53.6m), (resultado.InsumosPorPessoa, resultado.EquipePorPessoa, resultado.CustoDiretoPorPessoa)); // 2.000 + 400 + 960
        Assert.Equal(SimuladorPreco.PrecoPara(5_360m, 100, DezPorCento, 20), resultado.PrecoMinimo);
        Assert.Equal(SimuladorPreco.PrecoPara(5_360m, 100, DezPorCento, 30), resultado.PrecoSugerido);
        Assert.True(resultado.PrecoSugerido > resultado.PrecoMinimo);
        Assert.Equal(SimuladorPreco.Margem(90m, 5_360m, 100, DezPorCento), resultado.MargemAtual);
        Assert.Null(SimuladorPreco.Simular(cenario, 20, 30, null).MargemAtual);
    }
}
