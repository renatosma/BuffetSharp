namespace BuffetSharp.Domain.Cardapio;

/// <summary>
/// Para quantas pessoas, quantos adultos e quantas horas se calcula o consumo: as bebidas da ficha técnica podem ser "por
/// hora" (refrigerante, água, cerveja) e "só adultos" (alcoólicas: as crianças ficam de fora).
/// </summary>
public sealed record ContextoConsumo(int Pessoas, int Adultos, decimal Horas)
{
    /// <summary>Duração de referência dos custos por pessoa (listas de itens e pacotes), quando o evento ainda não tem horário.</summary>
    public const decimal HorasReferencia = 5;

    /// <summary>Sem horário nem faixas: todos adultos, na duração de referência.</summary>
    public static ContextoConsumo Padrao(int pessoas) => new(pessoas, pessoas, HorasReferencia);

    /// <param name="adultos">Os que pagam inteira (meia e isentos são crianças).</param>
    public static ContextoConsumo De(int pessoas, int adultos, DateTime inicio, DateTime fim) =>
        new(pessoas, Math.Clamp(adultos, 0, pessoas), Math.Max(1, Math.Round((decimal)(fim - inicio).TotalHours, 2)));

    /// <summary>Parte das porções que é de adultos (para os insumos "só adultos").</summary>
    public decimal FatorAdultos => Pessoas <= 0 ? 0 : (decimal)Adultos / Pessoas;
}

/// <summary>
/// Custo dos insumos de um pacote separado pelo que muda com a duração e com os adultos, para calcular o custo de qualquer
/// evento: por pessoa + por pessoa e hora + por adulto + por adulto e hora.
/// </summary>
public sealed record CustoInsumos(decimal PorPessoa, decimal PorPessoaHora, decimal PorAdulto, decimal PorAdultoHora)
{
    public static readonly CustoInsumos Nenhum = new(0, 0, 0, 0);

    public decimal Calcular(ContextoConsumo c) =>
        PorPessoa * c.Pessoas + PorPessoaHora * c.Pessoas * c.Horas + PorAdulto * c.Adultos + PorAdultoHora * c.Adultos * c.Horas;

    /// <summary>Por pessoa na referência (todos adultos, <see cref="ContextoConsumo.HorasReferencia"/>), para as listas.</summary>
    public decimal Referencia => Calcular(ContextoConsumo.Padrao(1));
}
