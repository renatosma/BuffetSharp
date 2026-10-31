using BuffetSharp.Domain.Cardapio;

namespace BuffetSharp.Domain.Comercial;

/// <summary>
/// Cenário do simulador: quantas pessoas, por quanto tempo e quantos adultos (bebidas por hora e alcoólicas), o custo dos
/// insumos da composição, a equipe para essa quantidade e os percentuais sobre a receita.
/// </summary>
public sealed record CenarioPreco(ContextoConsumo Consumo, CustoInsumos Insumos, decimal Equipe, PercentuaisReceita Percentuais);

/// <summary>Custo e preços por pessoa de um cenário, e a margem do preço atual (se houver).</summary>
public sealed record ResultadoSimulacao(int Convidados, decimal InsumosPorPessoa, decimal EquipePorPessoa, decimal? PrecoMinimo,
    decimal? PrecoSugerido, decimal? PrecoAtual, decimal? MargemAtual)
{
    public decimal CustoDiretoPorPessoa => InsumosPorPessoa + EquipePorPessoa;
}

/// <summary>
/// Simulador de preço do pacote: com os custos diretos (insumos e equipe) fixos para o cenário e os percentuais acompanhando
/// a receita, o preço por pessoa que dá a margem m é P = diretos / (n · (1 − p − m)). Considera que todos pagam inteira.
/// </summary>
public static class SimuladorPreco
{
    /// <summary>Quantidades de convidados da tabela do simulador.</summary>
    public static readonly int[] Faixas = [50, 80, 100, 150, 200, 250, 300];

    public static ResultadoSimulacao Simular(CenarioPreco cenario, decimal margemMinima, decimal margemAlvo, decimal? precoAtual)
    {
        var pessoas = Math.Max(1, cenario.Consumo.Pessoas);
        var insumos = cenario.Insumos.Calcular(cenario.Consumo);
        var diretos = insumos + cenario.Equipe;
        return new ResultadoSimulacao(pessoas, Math.Round(insumos / pessoas, 2), Math.Round(cenario.Equipe / pessoas, 2),
            PrecoPara(diretos, pessoas, cenario.Percentuais, margemMinima), PrecoPara(diretos, pessoas, cenario.Percentuais, margemAlvo),
            precoAtual, precoAtual is > 0 and var preco ? Margem(preco, diretos, pessoas, cenario.Percentuais) : null);
    }

    /// <summary>Preço por pessoa para a margem (%), arredondado para cima nos 10 centavos; nulo se os percentuais já consomem a margem.</summary>
    public static decimal? PrecoPara(decimal diretos, int pessoas, PercentuaisReceita percentuais, decimal margem)
    {
        var fator = 1 - percentuais.Total / 100 - margem / 100;
        return fator <= 0 || pessoas <= 0 ? null : Math.Ceiling(diretos / (pessoas * fator) * 10) / 10;
    }

    /// <summary>Margem (%) com o preço por pessoa informado.</summary>
    public static decimal Margem(decimal preco, decimal diretos, int pessoas, PercentuaisReceita percentuais)
    {
        var receita = preco * pessoas;
        return Math.Round((receita - diretos - receita * percentuais.Total / 100) / receita * 100, 1);
    }
}
