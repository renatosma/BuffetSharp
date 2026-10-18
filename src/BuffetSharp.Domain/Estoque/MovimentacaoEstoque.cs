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

    /// <summary>Fornecedor da compra (entradas).</summary>
    public int? FornecedorId { get; private set; }

    /// <summary>Unidade em que foi comprado (ex.: CX), quando diferente da unidade do estoque.</summary>
    public string? UnidadeCompra { get; private set; }

    /// <summary>Quantidade comprada na unidade de compra (ex.: 2 CX).</summary>
    public decimal? QuantidadeCompra { get; private set; }

    /// <summary>Unidades do estoque em cada unidade de compra, na data da compra.</summary>
    public decimal? FatorConversao { get; private set; }

    /// <summary>Custo de cada unidade de compra (ex.: R$ 45,00 a caixa).</summary>
    public decimal? CustoUnitarioCompra { get; private set; }

    public decimal ValorTotal => Math.Round(Math.Abs(Quantidade) * CustoUnitario, 2);

    internal void RegistrarCompra(int? fornecedorId, string? unidade, decimal? quantidade, decimal? fator, decimal? custoUnitario)
    {
        FornecedorId = fornecedorId;
        UnidadeCompra = unidade;
        QuantidadeCompra = quantidade;
        FatorConversao = fator;
        CustoUnitarioCompra = custoUnitario;
    }
}
