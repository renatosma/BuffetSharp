using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

/// <summary>
/// Quantos convidados há em cada faixa de cobrança: inteira (adultos e crianças acima da idade da meia), meia e
/// isentos (crianças pequenas). O total é a quantidade de pessoas (capacidade do espaço, insumos, materiais).
/// Um inteiro é convertido em "todos pagam inteira".
/// </summary>
public readonly record struct QuantidadesConvidados(int Total, int Meia = 0, int Isentos = 0)
{
    public int Inteira => Total - Meia - Isentos;

    /// <summary>Convidados que pagam alguma coisa (inteira ou meia): base do mínimo do pacote e dos serviços por pessoa.</summary>
    public int Pagantes => Total - Isentos;

    public bool TemCriancas => Meia > 0 || Isentos > 0;

    public static implicit operator QuantidadesConvidados(int total) => new(total);

    public void Validar()
    {
        Validador.Garantir(Total > 0, "Informe a quantidade de convidados.");
        Validador.Garantir(Meia >= 0 && Isentos >= 0, "A quantidade de crianças não pode ser negativa.");
        Validador.Garantir(Meia + Isentos <= Total,
            "As crianças (meia e isentas) não podem passar do total de convidados.");
        Validador.Garantir(Pagantes > 0, "Informe ao menos um convidado pagante.");
    }
}

/// <summary>
/// Regra de cobrança por idade do pacote: crianças até <see cref="IdadeMaximaIsento"/> anos não pagam e, daí até
/// <see cref="IdadeMaximaMeia"/> anos, pagam <see cref="PercentualMeia"/>% do valor por pessoa. Fica congelada no
/// orçamento e no evento, como os valores negociados.
/// </summary>
public sealed record RegraFaixasEtarias(int IdadeMaximaIsento, int IdadeMaximaMeia, decimal PercentualMeia)
{
    public const int IdadeMaximaCrianca = 17;

    /// <summary>Usual dos buffets: até 5 anos isentos, de 6 a 10 anos meia (50%).</summary>
    public static readonly RegraFaixasEtarias Padrao = new(5, 10, 50m);

    public bool TemMeia => IdadeMaximaMeia > IdadeMaximaIsento;

    /// <summary>Valor cobrado por criança na faixa da meia.</summary>
    public decimal ValorMeia(decimal valorPorPessoa) => Math.Round(valorPorPessoa * PercentualMeia / 100m, 2);

    /// <summary>"até 5 anos" (ou "menores de 1 ano").</summary>
    public string FaixaIsentos => IdadeMaximaIsento == 0 ? "menores de 1 ano" : $"até {IdadeMaximaIsento} anos";

    /// <summary>"de 6 a 10 anos".</summary>
    public string FaixaMeia => IdadeMaximaIsento + 1 == IdadeMaximaMeia
        ? $"de {IdadeMaximaMeia} anos"
        : $"de {IdadeMaximaIsento + 1} a {IdadeMaximaMeia} anos";

    /// <summary>"Crianças até 5 anos não pagam; de 6 a 10 anos pagam 50% do valor por pessoa."</summary>
    public string Descricao => TemMeia
        ? $"Crianças {FaixaIsentos} não pagam; {FaixaMeia} pagam {PercentualMeia:0.##}% do valor por pessoa."
        : $"Crianças {FaixaIsentos} não pagam.";

    public void Validar()
    {
        Validador.Garantir(IdadeMaximaIsento is >= 0 and <= IdadeMaximaCrianca,
            $"A idade máxima dos isentos deve estar entre 0 e {IdadeMaximaCrianca} anos.");
        Validador.Garantir(IdadeMaximaMeia >= IdadeMaximaIsento && IdadeMaximaMeia <= IdadeMaximaCrianca,
            $"A idade máxima da meia deve estar entre a dos isentos e {IdadeMaximaCrianca} anos.");
        Validador.Garantir(PercentualMeia is >= 0 and <= 100, "O percentual da meia deve estar entre 0% e 100%.");
    }
}
