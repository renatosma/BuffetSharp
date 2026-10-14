using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

public enum TipoMovimentacao
{
    Entrada,
    Saida,
    Ajuste
}

/// <summary>
/// Histórico imutável de movimentações. No ajuste, a quantidade pode ser negativa (diferença).
/// </summary>
public class MovimentacaoEstoque : Entidade
{
    private MovimentacaoEstoque()
    {
    }

    internal MovimentacaoEstoque(Produto produto, TipoMovimentacao tipo, decimal quantidade, decimal custoUnitario,
        DateTime data, string? observacao, int? eventoId = null)
    {
        Produto = produto;
        ProdutoId = produto.Id;
        Tipo = tipo;
        Quantidade = quantidade;
        CustoUnitario = custoUnitario;
        Data = data;
        Observacao = observacao;
        EventoId = eventoId;
        SaldoApos = produto.EstoqueAtual;
    }

    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public TipoMovimentacao Tipo { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal CustoUnitario { get; private set; }
    public decimal SaldoApos { get; private set; }
    public DateTime Data { get; private set; }
    public string? Observacao { get; private set; }
    public int? EventoId { get; private set; }

    public decimal ValorTotal => Math.Round(Math.Abs(Quantidade) * CustoUnitario, 2);
}
