using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Application.Consultas;

public sealed record ItemAgenda
{
    public int Id { get; init; }

    /// <summary>"Evento" (confirmado/realizado) ou "Orcamento" (pré-reserva enviada ao cliente).</summary>
    public string Origem { get; init; } = "Evento";

    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public int EspacoId { get; init; }
    public string Espaco { get; init; } = string.Empty;
    public string TipoEvento { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public DateTime Fim { get; init; }
    public int Convidados { get; init; }
    public StatusEvento Status { get; init; }

    public bool Provisorio => Origem == "Orcamento";
    public string Codigo => Provisorio ? $"ORC-{Id:D5}" : $"EVT-{Id:D5}";
}

public interface IConsultasAgenda
{
    /// <summary>Eventos (não cancelados) e pré-reservas no intervalo.</summary>
    Task<IReadOnlyList<ItemAgenda>> ListarAsync(DateTime inicio, DateTime fim, int? espacoId = null,
        bool incluirPreReservas = true, CancellationToken ct = default);

    /// <summary>
    /// Retorna o primeiro evento confirmado que conflita com o período no espaço, considerando
    /// o intervalo mínimo de montagem/desmontagem.
    /// </summary>
    Task<ItemAgenda?> BuscarConflitoAsync(int espacoId, DateTime inicio, DateTime fim, int intervaloMinutos,
        int? ignorarEventoId = null, CancellationToken ct = default);
}
