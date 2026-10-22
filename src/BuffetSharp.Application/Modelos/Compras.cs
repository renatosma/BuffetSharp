using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Modelos;

// Pedido de compra: vários produtos de um fornecedor, recebidos de uma vez com a nota fiscal.

public sealed class PedidoCompraInput
{
    public int Id { get; set; }

    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public int FornecedorId { get; set; }
    public DateOnly Data { get; set; }
    public DateOnly? PrevisaoEntrega { get; set; }
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public string? Observacoes { get; set; }
    public List<PedidoCompraItemInput> Itens { get; set; } = [];

    // Somente leitura
    public string Codigo { get; set; } = "(novo)";
    public StatusPedidoCompra Status { get; set; } = StatusPedidoCompra.Aberto;
    public string? NotaFiscal { get; set; }
    public DateOnly? DataCompra { get; set; }
    public decimal Total => Itens.Sum(i => i.Total);
}

public sealed class PedidoCompraItemInput
{
    public int ProdutoId { get; set; }

    /// <summary>Unidade de compra (0 = unidade do estoque).</summary>
    public int ProdutoEmbalagemId { get; set; }

    public decimal Quantidade { get; set; }

    /// <summary>Custo de uma unidade de compra.</summary>
    public decimal CustoUnitario { get; set; }

    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

/// <summary>Recebimento da nota: número, data da compra e os vencimentos (um ou vários boletos).</summary>
public sealed class RecebimentoPedidoInput
{
    public int PedidoId { get; set; }
    public string NotaFiscal { get; set; } = string.Empty;
    public DateOnly DataCompra { get; set; }
    public List<VencimentoInput> Vencimentos { get; set; } = [];
}

public sealed class VencimentoInput
{
    public DateOnly Vencimento { get; set; }
    public decimal Valor { get; set; }
}

/// <summary>Produto que pode ser comprado, com as unidades de compra (embalagens) que valem para o fornecedor.</summary>
public sealed record ProdutoCompra(int Id, string Nome, string Unidade, decimal CustoMedio, int? FornecedorPadraoId,
    IReadOnlyList<UnidadeCompra> Unidades);

/// <param name="Id">0 = unidade do estoque.</param>
/// <param name="Fator">Unidades do estoque em uma unidade de compra.</param>
public sealed record UnidadeCompra(int Id, string Nome, decimal Fator);
