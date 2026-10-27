using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Consultas;

public sealed record FiltroCobrancas
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusCobranca? Status { get; init; }
}

public sealed record CobrancaResumo
{
    public int Id { get; init; }
    public int? ParcelaId { get; init; }
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public int ClienteId { get; init; }
    public string Cliente { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public AmbienteCobranca Ambiente { get; init; }
    public TipoCobranca Tipo { get; init; }
    public StatusCobranca Status { get; init; }
    public decimal Valor { get; init; }
    public DateTime Vencimento { get; init; }
    public DateTime? DataPagamento { get; init; }
    public decimal? ValorPago { get; init; }
    public decimal? ValorLiquido { get; init; }
    public TipoFormaPagamento? PagaCom { get; init; }
    public bool Baixada { get; init; }
    public string? Mensagem { get; init; }
    public string? IdExterno { get; init; }
    public string? UrlBoleto { get; init; }
    public string? UrlFatura { get; init; }
    public DateTime SolicitadaEm { get; init; }
    public string? Usuario { get; init; }

    public string EventoCodigo => $"EVT-{EventoId:D5}";
    public bool Vencida(DateOnly hoje) => Status == StatusCobranca.Pendente && DateOnly.FromDateTime(Vencimento) < hoje;

    /// <summary>Taxa do provedor (o que foi pago menos o que caiu na conta).</summary>
    public decimal? Taxa => ValorPago is { } pago && ValorLiquido is { } liquido ? pago - liquido : null;
}

public interface IConsultasCobrancas
{
    /// <summary>Cobranças emitidas no período (pela data de emissão) ou ainda pendentes.</summary>
    Task<IReadOnlyList<CobrancaResumo>> ListarAsync(FiltroCobrancas filtro, CancellationToken ct = default);
}
