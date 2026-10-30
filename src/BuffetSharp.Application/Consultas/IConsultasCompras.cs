using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.Application.Consultas;

// Compras: cotação -> pedido de compra -> recebimento (estoque) -> nota fiscal de entrada (contas a pagar).

public sealed record FiltroCotacoes
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusCotacao? Status { get; init; }
}

public sealed record CotacaoResumo
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public DateTime? PrazoResposta { get; init; }
    public int? EventoId { get; init; }
    public string? Evento { get; init; }
    public StatusCotacao Status { get; init; }
    public int Itens { get; init; }
    public int Participantes { get; init; }

    /// <summary>Preços informados (de produtos x participantes possíveis).</summary>
    public int Precos { get; init; }

    /// <summary>Total da compra pelos vencedores (menor preço ou escolhidos).</summary>
    public decimal TotalVencedores { get; init; }

    /// <summary>Produtos em que o vencedor não é o menor preço (escolha por marca/qualidade).</summary>
    public int EscolhasManuais { get; init; }

    public int Pedidos { get; init; }
    public string? Observacoes { get; init; }
    public string? MotivoCancelamento { get; init; }
    public string Codigo => $"COT-{Id:D5}";
    public string Respostas => Itens * Participantes == 0 ? "-" : $"{Precos} de {Itens * Participantes}";
}

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

    /// <summary>Valor do que já chegou (recebimentos não estornados), pelo custo do pedido.</summary>
    public decimal Recebido { get; init; }

    /// <summary>Recebimentos ainda sem nota fiscal de entrada.</summary>
    public int AFaturar { get; init; }

    public int? CotacaoCompraId { get; init; }
    public string? CondicaoPagamento { get; init; }
    public string? FormaPagamento { get; init; }
    public string? Observacoes { get; init; }
    public string? MotivoCancelamento { get; init; }
    public string Codigo => $"PC-{Id:D5}";

    /// <summary>"28 dias • Boleto", só a condição, só a forma ou vazio.</summary>
    public string Pagamento => string.Join(" • ", new[] { CondicaoPagamento, FormaPagamento }.Where(t => !string.IsNullOrWhiteSpace(t)));
    public string? Cotacao => CotacaoCompraId is { } id ? $"COT-{id:D5}" : null;
    public decimal PercentualRecebido => Total == 0 ? 0 : Math.Min(100, Math.Round(Recebido / Total * 100, 0));
}

/// <summary>Itens de um pedido (na unidade de compra) com o que já chegou.</summary>
public sealed record PedidoCompraItemResumo
{
    public int Id { get; init; }
    public string Produto { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public decimal QuantidadeRecebida { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public decimal CustoUnitario { get; init; }

    /// <summary>O produto controla validade: o recebimento pede o lote e a validade.</summary>
    public bool ControlaValidade { get; init; }

    public decimal Saldo => Quantidade - QuantidadeRecebida;
    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

public sealed record FiltroRecebimentos
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusRecebimento? Status { get; init; }
    public int? FornecedorId { get; init; }
}

public sealed record RecebimentoResumo
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public int PedidoCompraId { get; init; }
    public int FornecedorId { get; init; }
    public string Fornecedor { get; init; } = string.Empty;

    /// <summary>Condição e forma de pagamento do pedido (sugerem os vencimentos e a forma da nota).</summary>
    public int? CondicaoPagamentoId { get; init; }
    public int? FormaPagamentoId { get; init; }

    public StatusRecebimento Status { get; init; }
    public int Itens { get; init; }
    public decimal Total { get; init; }
    public int? NotaFiscalEntradaId { get; init; }
    public string? NotaFiscal { get; init; }
    public string? Usuario { get; init; }
    public string? Observacoes { get; init; }
    public string? MotivoEstorno { get; init; }
    public string Codigo => $"REC-{Id:D5}";
    public string Pedido => $"PC-{PedidoCompraId:D5}";
}

/// <summary>Item recebido, na unidade de compra do pedido.</summary>
/// <param name="QuantidadeDevolvida">Quanto já voltou ao fornecedor (devoluções), na unidade de compra.</param>
public sealed record RecebimentoItemResumo(string Produto, decimal Quantidade, string Unidade, decimal CustoUnitario, decimal QuantidadeDevolvida = 0)
{
    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

public sealed record FiltroNotasEntrada
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusNotaEntrada? Status { get; init; }
}

public sealed record NotaEntradaResumo
{
    public int Id { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string? Serie { get; init; }
    public DateTime DataEmissao { get; init; }
    public int FornecedorId { get; init; }
    public string Fornecedor { get; init; } = string.Empty;
    public StatusNotaEntrada Status { get; init; }
    public decimal ValorProdutos { get; init; }
    public decimal ValorTotal { get; init; }
    public int Recebimentos { get; init; }

    /// <summary>Contas da nota (a única ou as parcelas) e quantas já foram pagas.</summary>
    public int Contas { get; init; }
    public int ContasPagas { get; init; }

    public string? Observacoes { get; init; }
    public string? MotivoCancelamento { get; init; }
    public string Descricao => Serie is null ? Numero : $"{Numero}/{Serie}";

    public string Pagamento => Status != StatusNotaEntrada.Lancada || Contas == 0
        ? string.Empty
        : ContasPagas == Contas ? "Pago" : Contas == 1 ? "Em aberto" : $"{ContasPagas} de {Contas} paga(s)";
}

public interface IConsultasCompras
{
    Task<IReadOnlyList<CotacaoResumo>> ListarCotacoesAsync(FiltroCotacoes filtro, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoCompraResumo>> ListarPedidosAsync(FiltroPedidosCompra filtro, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoCompraItemResumo>> ItensAsync(int pedidoId, CancellationToken ct = default);
    Task<IReadOnlyList<RecebimentoResumo>> ListarRecebimentosAsync(FiltroRecebimentos filtro, CancellationToken ct = default);
    Task<IReadOnlyList<RecebimentoItemResumo>> ItensRecebimentoAsync(int recebimentoId, CancellationToken ct = default);
    Task<IReadOnlyList<NotaEntradaResumo>> ListarNotasAsync(FiltroNotasEntrada filtro, CancellationToken ct = default);
}
