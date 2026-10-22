using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.Application.Consultas;

public sealed record FiltroPedidosCompra
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusPedidoCompra? Status { get; init; }
}

public sealed record PedidoCompraResumo
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public DateTime? PrevisaoEntrega { get; init; }
    public int FornecedorId { get; init; }
    public string Fornecedor { get; init; } = string.Empty;
    public string? TelefoneFornecedor { get; init; }
    public string? EmailFornecedor { get; init; }
    public StatusPedidoCompra Status { get; init; }
    public int Itens { get; init; }
    public decimal Total { get; init; }
    public string? NotaFiscal { get; init; }
    public DateTime? DataCompra { get; init; }

    /// <summary>Contas da nota (a única ou as parcelas) e quantas já foram pagas.</summary>
    public int Contas { get; init; }
    public int ContasPagas { get; init; }

    public string? Observacoes { get; init; }
    public string? MotivoCancelamento { get; init; }
    public string Codigo => $"PC-{Id:D5}";

    public string Pagamento => Status != StatusPedidoCompra.Recebido || Contas == 0
        ? string.Empty
        : ContasPagas == Contas ? "Pago" : Contas == 1 ? "Em aberto" : $"{ContasPagas} de {Contas} paga(s)";
}

/// <summary>Itens de um pedido para impressão e envio ao fornecedor.</summary>
public sealed record PedidoCompraItemResumo(string Produto, decimal Quantidade, string Unidade, decimal CustoUnitario)
{
    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

public interface IConsultasCompras
{
    Task<IReadOnlyList<PedidoCompraResumo>> ListarPedidosAsync(FiltroPedidosCompra filtro, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoCompraItemResumo>> ItensAsync(int pedidoId, CancellationToken ct = default);
}
