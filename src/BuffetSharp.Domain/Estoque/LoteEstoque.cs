using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

/// <summary>Lote informado na entrada: número (opcional), validade e, quando a entrada vem em vários lotes, a quantidade de cada um.</summary>
/// <param name="Quantidade">Na unidade do estoque; nula = o que sobrar da entrada.</param>
public sealed record LoteInformado(string? Numero, DateOnly Validade, decimal? Quantidade = null);

public enum SituacaoLote
{
    Ok,
    Vencendo,
    Vencido
}

/// <summary>
/// Lote de um produto que controla validade: nasce numa entrada (compra, recebimento, nota) e é consumido pelas saídas, do
/// que vence primeiro para o que vence depois. Lote vencido não sai para eventos: é descartado como perda.
/// </summary>
public class LoteEstoque : Entidade
{
    public const int TamanhoNumero = 40;

    private LoteEstoque()
    {
    }

    internal LoteEstoque(Produto produto, string? numero, DateOnly validade, decimal quantidade, decimal custoUnitario, DateTime entrada)
    {
        Validador.Garantir((numero?.Trim().Length ?? 0) <= TamanhoNumero, $"O número do lote pode ter no máximo {TamanhoNumero} caracteres.");
        Produto = produto;
        ProdutoId = produto.Id;
        Numero = string.IsNullOrWhiteSpace(numero) ? null : numero.Trim();
        Validade = validade;
        Quantidade = quantidade;
        Saldo = quantidade;
        CustoUnitario = custoUnitario;
        EntradaEm = entrada;
    }

    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Número do lote do fabricante (pode não ter).</summary>
    public string? Numero { get; private set; }

    public DateOnly Validade { get; private set; }

    /// <summary>Quantidade que entrou (na unidade do estoque).</summary>
    public decimal Quantidade { get; private set; }

    public decimal Saldo { get; internal set; }
    public decimal CustoUnitario { get; private set; }
    public DateTime EntradaEm { get; private set; }

    public string Descricao => (Numero is null ? "Lote sem número" : $"Lote {Numero}") + $", validade {Validade:dd/MM/yyyy}";

    public bool Vencido(DateOnly hoje) => Validade < hoje;

    public SituacaoLote Situacao(DateOnly hoje, int diasAlerta) =>
        Validade < hoje ? SituacaoLote.Vencido : Validade <= hoje.AddDays(diasAlerta) ? SituacaoLote.Vencendo : SituacaoLote.Ok;
}

/// <summary>Quanto a movimentação tirou (ou pôs) de cada lote.</summary>
public class MovimentacaoLote
{
    private MovimentacaoLote()
    {
    }

    internal MovimentacaoLote(LoteEstoque lote, decimal quantidade)
    {
        Lote = lote;
        Quantidade = quantidade;
    }

    public int Id { get; private set; }
    public int MovimentacaoEstoqueId { get; private set; }
    public int LoteEstoqueId { get; private set; }
    public LoteEstoque? Lote { get; private set; }
    public decimal Quantidade { get; private set; }
}
