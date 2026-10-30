namespace BuffetSharp.Domain.Comercial;

/// <summary>
/// Custos previstos de um orçamento: os diretos (insumos do cardápio, equipe estimada, serviços e espaço), que não mudam com o
/// desconto, e os que são um percentual da receita (comissão do vendedor, impostos e taxa da forma de pagamento).
/// </summary>
public sealed record CustosOrcamento(decimal Insumos, decimal Equipe, decimal Servicos, decimal Espaco, decimal Comissao, decimal Impostos,
    decimal Taxa)
{
    public static readonly CustosOrcamento Nenhum = new(0, 0, 0, 0, 0, 0, 0);

    public decimal Diretos => Insumos + Equipe + Servicos + Espaco;
    public decimal SobreReceita => Comissao + Impostos + Taxa;
    public decimal Total => Diretos + SobreReceita;
}

/// <summary>Percentuais que incidem sobre a receita do orçamento.</summary>
public sealed record PercentuaisReceita(decimal Comissao, decimal Impostos, decimal Taxa)
{
    public decimal Total => Comissao + Impostos + Taxa;
}

/// <summary>Faixa da margem para o semáforo: abaixo da mínima, entre a mínima e a meta, ou na meta.</summary>
public enum FaixaMargem
{
    SemCusto,
    AbaixoDaMinima,
    Atencao,
    Boa
}

/// <summary>Margem do orçamento: custos previstos, margem e o desconto máximo que ainda mantém a margem mínima.</summary>
public static class CalculadoraMargem
{
    public static CustosOrcamento Calcular(decimal receita, decimal insumos, decimal equipe, decimal servicos, decimal espaco,
        PercentuaisReceita percentuais) =>
        new(Math.Round(insumos, 2), Math.Round(equipe, 2), Math.Round(servicos, 2), Math.Round(espaco, 2),
            Math.Round(receita * percentuais.Comissao / 100, 2), Math.Round(receita * percentuais.Impostos / 100, 2),
            Math.Round(receita * percentuais.Taxa / 100, 2));

    /// <summary>Margem (%) sobre a receita; nula sem receita ou sem nenhum custo informado.</summary>
    public static decimal? Percentual(decimal receita, CustosOrcamento custos) =>
        receita <= 0 || custos.Total <= 0 ? null : Math.Round((receita - custos.Total) / receita * 100, 1);

    public static FaixaMargem Faixa(decimal? margem, decimal minima, decimal meta) => margem switch
    {
        null => FaixaMargem.SemCusto,
        var m when m < minima => FaixaMargem.AbaixoDaMinima,
        var m when m < meta => FaixaMargem.Atencao,
        _ => FaixaMargem.Boa
    };

    /// <summary>
    /// Menor receita que mantém a margem mínima: os custos diretos não mudam com o preço e os percentuais acompanham a receita,
    /// então R − diretos − p·R ≥ m·R, ou seja, R ≥ diretos / (1 − p − m). Nula se os percentuais já consomem a margem.
    /// </summary>
    public static decimal? ReceitaMinima(decimal diretos, PercentuaisReceita percentuais, decimal margemMinima)
    {
        var fator = 1 - percentuais.Total / 100 - margemMinima / 100;
        return fator <= 0 ? null : Math.Round(diretos / fator, 2);
    }

    /// <summary>Desconto máximo (o campo %, sobre o subtotal) que mantém a margem mínima; zero se nem sem desconto ela é atingida.</summary>
    public static decimal DescontoMaximo(decimal subtotal, decimal diretos, PercentuaisReceita percentuais, decimal margemMinima)
    {
        if (subtotal <= 0 || ReceitaMinima(diretos, percentuais, margemMinima) is not { } minima)
            return 0;
        var desconto = Math.Floor((1 - minima / subtotal) * 10000) / 100;
        return Math.Clamp(desconto, 0, CalculadoraValores.DescontoMaximoPercentual);
    }
}
