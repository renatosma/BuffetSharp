using BuffetSharp.Domain.Fiscal;

namespace BuffetSharp.Application.Consultas;

public sealed record FiltroNotasServico
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public StatusNfse? Status { get; init; }
}

public sealed record NotaServicoResumo
{
    public int Id { get; init; }
    public string Referencia { get; init; } = string.Empty;
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public DateTime DataEvento { get; init; }
    public AmbienteNfse Ambiente { get; init; }
    public StatusNfse Status { get; init; }
    public string? Numero { get; init; }
    public string? CodigoVerificacao { get; init; }
    public DateTime? DataEmissao { get; init; }
    public DateTime SolicitadaEm { get; init; }
    public string TomadorDocumento { get; init; } = string.Empty;
    public string TomadorNome { get; init; } = string.Empty;
    public decimal ValorServicos { get; init; }
    public decimal AliquotaIss { get; init; }
    public string? Url { get; init; }
    public string? Mensagem { get; init; }
    public string? MotivoCancelamento { get; init; }
    public string? Usuario { get; init; }

    public decimal ValorIss => Math.Round(ValorServicos * AliquotaIss / 100, 2, MidpointRounding.AwayFromZero);
}

public interface IConsultasFiscais
{
    Task<IReadOnlyList<NotaServicoResumo>> ListarNotasServicoAsync(FiltroNotasServico filtro, CancellationToken ct = default);
}
