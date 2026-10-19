using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Consultas;

// ============================ Comercial ============================

public sealed record FiltroOrcamentos
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusOrcamento? Status { get; init; }
    public string? Texto { get; init; }
}

public sealed record OrcamentoResumo
{
    public int Id { get; init; }
    public int ClienteId { get; init; }
    public string Cliente { get; init; } = string.Empty;
    public string TipoEvento { get; init; } = string.Empty;
    public string? Pacote { get; init; }
    public string? Espaco { get; init; }
    public DateTime Inicio { get; init; }
    public int QuantidadeConvidados { get; init; }
    public decimal ValorTotal { get; init; }
    public StatusOrcamento Status { get; init; }
    public DateTime ValidoAte { get; init; }
    public int? EventoId { get; init; }
    public DateTime CriadoEm { get; init; }
    public string? Vendedor { get; init; }
    public DateTime? UltimoEnvio { get; init; }
    public CanalEnvio? CanalUltimoEnvio { get; init; }
    public string Codigo => $"ORC-{Id:D5}";
}

public sealed record HistoricoCliente
{
    public int QuantidadeEventos { get; init; }
    public decimal TotalContratado { get; init; }
    public decimal TotalRecebido { get; init; }
    public decimal SaldoEmAberto { get; init; }
    public DateTime? UltimoEvento { get; init; }
    public IReadOnlyList<OrcamentoResumo> Orcamentos { get; init; } = [];
    public IReadOnlyList<EventoResumo> Eventos { get; init; } = [];
}

public sealed record FiltroDegustacoes
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusDegustacao? Status { get; init; }
}

public sealed record DegustacaoResumo
{
    public int Id { get; init; }
    public DateTime DataHora { get; init; }
    public int ClienteId { get; init; }
    public string Cliente { get; init; } = string.Empty;
    public string? Contato { get; init; }
    public int Pessoas { get; init; }
    public string? Pacote { get; init; }
    public int? OrcamentoId { get; init; }
    public StatusDegustacao Status { get; init; }
    public int? Avaliacao { get; init; }
    public string? Comentario { get; init; }
    public string? Observacoes { get; init; }

    /// <summary>Verdadeiro quando o cliente aprovou algum orçamento a partir da data da degustação.</summary>
    public bool Convertida { get; init; }

    public string? OrcamentoCodigo => OrcamentoId is { } id ? $"ORC-{id:D5}" : null;
}

public sealed record ContratoResumo
{
    public int Id { get; init; }
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public DateTime DataEvento { get; init; }
    public StatusContrato Status { get; init; }
    public DateTime EmitidoEm { get; init; }
    public DateTime? AssinadoEm { get; init; }
    public string? NomeSignatario { get; init; }
    public string? MotivoCancelamento { get; init; }
    public string Codigo => $"CTR-{Id:D5}";
    public string EventoCodigo => $"EVT-{EventoId:D5}";
}

public interface IConsultasComercial
{
    Task<IReadOnlyList<OrcamentoResumo>> ListarOrcamentosAsync(FiltroOrcamentos filtro, CancellationToken ct = default);
    Task<HistoricoCliente> HistoricoClienteAsync(int clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<DegustacaoResumo>> ListarDegustacoesAsync(FiltroDegustacoes filtro, CancellationToken ct = default);

    /// <summary>Contratos emitidos; <paramref name="status"/> nulo traz todos.</summary>
    Task<IReadOnlyList<ContratoResumo>> ListarContratosAsync(StatusContrato? status, CancellationToken ct = default);
}

// ============================ Eventos ============================

public sealed record FiltroEventos
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusEvento? Status { get; init; }
    public int? EspacoId { get; init; }
    public string? Texto { get; init; }
}

public sealed record EventoResumo
{
    public int Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string TipoEvento { get; init; } = string.Empty;
    public string Espaco { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public int QuantidadeConvidados { get; init; }
    public decimal ValorTotal { get; init; }
    public decimal Recebido { get; init; }
    public decimal SaldoAReceber { get; init; }
    public StatusEvento Status { get; init; }

    /// <summary>Situação do contrato vigente (nulo quando não há contrato emitido).</summary>
    public StatusContrato? Contrato { get; init; }

    public string Codigo => $"EVT-{Id:D5}";
}

public interface IConsultasEventos
{
    Task<IReadOnlyList<EventoResumo>> ListarAsync(FiltroEventos filtro, CancellationToken ct = default);
}

// ============================ Financeiro ============================

public sealed record FiltroTitulos
{
    public DateTime? VencimentoDe { get; init; }
    public DateTime? VencimentoAte { get; init; }
    public StatusTitulo? Status { get; init; }
    public bool SomenteVencidos { get; init; }
    public string? Texto { get; init; }
}

public sealed record ParcelaResumo
{
    public int Id { get; init; }
    public int EventoId { get; init; }
    public string EventoTitulo { get; init; } = string.Empty;
    public int ClienteId { get; init; }
    public string Cliente { get; init; } = string.Empty;
    public int Numero { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public DateTime Vencimento { get; init; }
    public decimal Valor { get; init; }
    public StatusTitulo Status { get; init; }
    public DateTime? DataPagamento { get; init; }
    public decimal? ValorPago { get; init; }
    public string? FormaPagamento { get; init; }
    public int DiasAtraso { get; init; }
    public string EventoCodigo => $"EVT-{EventoId:D5}";
}

public sealed record ContaPagarResumo
{
    public int Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public CategoriaDespesa Categoria { get; init; }
    public int? FornecedorId { get; init; }
    public string? Fornecedor { get; init; }
    public int? EventoId { get; init; }
    public string? Evento { get; init; }
    public DateTime Vencimento { get; init; }
    public decimal Valor { get; init; }
    public StatusTitulo Status { get; init; }
    public DateTime? DataPagamento { get; init; }
    public decimal? ValorPago { get; init; }
    public string? FormaPagamento { get; init; }
    public int DiasAtraso { get; init; }
}

public interface IConsultasFinanceiro
{
    Task<IReadOnlyList<ParcelaResumo>> ListarParcelasAsync(FiltroTitulos filtro, DateOnly hoje, CancellationToken ct = default);
    Task<IReadOnlyList<ContaPagarResumo>> ListarContasPagarAsync(FiltroTitulos filtro, DateOnly hoje, CancellationToken ct = default);
}

// ============================ Estoque e Cardápio ============================

public sealed record MovimentacaoResumo
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public int ProdutoId { get; init; }
    public string Produto { get; init; } = string.Empty;
    public string Unidade { get; init; } = string.Empty;
    public TipoMovimentacao Tipo { get; init; }
    public decimal Quantidade { get; init; }
    public decimal CustoUnitario { get; init; }
    public decimal SaldoApos { get; init; }
    public string? Observacao { get; init; }
    public int? EventoId { get; init; }
    public string? Fornecedor { get; init; }

    /// <summary>Unidade e quantidade da compra (ex.: 2 CX), quando a entrada foi feita numa embalagem do fornecedor.</summary>
    public string? UnidadeCompra { get; init; }
    public decimal? QuantidadeCompra { get; init; }

    public string Compra => QuantidadeCompra is { } quantidade && UnidadeCompra is not null ? $"{quantidade:0.###} {UnidadeCompra}" : string.Empty;
    public decimal ValorTotal => Math.Round(Math.Abs(Quantidade) * CustoUnitario, 2);
}

public sealed record ItemCardapioResumo
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public int OrdemCategoria { get; init; }
    public int QuantidadeIngredientes { get; init; }
    public decimal CustoPorPessoa { get; init; }
    public bool Ativo { get; init; }
}

public sealed record PacoteResumo
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public decimal PrecoPorPessoa { get; init; }
    public decimal CustoPorPessoa { get; init; }
    public int MinimoConvidados { get; init; }
    public int IdadeMaximaIsento { get; init; }
    public int IdadeMaximaMeia { get; init; }
    public decimal PercentualMeia { get; init; }
    public int QuantidadeItens { get; init; }
    public bool Ativo { get; init; }
    public decimal MargemPercentual => PrecoPorPessoa == 0 ? 0 : Math.Round((PrecoPorPessoa - CustoPorPessoa) / PrecoPorPessoa * 100, 1);
    public RegraFaixasEtarias RegraFaixas => new(IdadeMaximaIsento, IdadeMaximaMeia, PercentualMeia);
}

public sealed record ItemComposicao
{
    public string Categoria { get; init; } = string.Empty;
    public string Item { get; init; } = string.Empty;
    public string? Descricao { get; init; }
}

/// <summary>Patrimônio de um material e quanto dele já está comprometido com eventos próximos à data.</summary>
public sealed record DisponibilidadeMaterial
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public CategoriaMaterial Categoria { get; init; }
    public int QuantidadePropria { get; init; }

    /// <summary>Unidades próprias reservadas por outros eventos (um dia antes ao dia seguinte) ainda não devolvidas.</summary>
    public int Reservada { get; init; }

    public decimal CustoLocacao { get; init; }
    public int Disponivel => Math.Max(0, QuantidadePropria - Reservada);
}

public interface IConsultasEstoqueCardapio
{
    /// <summary>
    /// Disponibilidade dos materiais ativos para um evento na data informada. O material fica comprometido do dia
    /// anterior ao dia seguinte ao evento, ou até a devolução ser registrada.
    /// </summary>
    Task<IReadOnlyList<DisponibilidadeMaterial>> DisponibilidadeMateriaisAsync(DateOnly data, int? ignorarEventoId,
        CancellationToken ct = default);

    Task<IReadOnlyList<MovimentacaoResumo>> ListarMovimentacoesAsync(DateTime de, DateTime ate, int? produtoId = null,
        TipoMovimentacao? tipo = null, CancellationToken ct = default);

    Task<IReadOnlyList<ItemCardapioResumo>> ListarItensCardapioAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PacoteResumo>> ListarPacotesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ItemComposicao>> ComposicaoPacoteAsync(int pacoteId, CancellationToken ct = default);
}
