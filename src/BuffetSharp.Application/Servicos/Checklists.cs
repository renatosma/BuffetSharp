using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Escolha do modelo de checklist de um evento: o do tipo de evento tem prioridade sobre o geral.</summary>
internal static class Checklists
{
    public static async Task<ModeloChecklist?> ModeloSugeridoAsync(IBuffetDbContext db, int tipoEventoId, CancellationToken ct) =>
        await db.ModelosChecklist.Include(m => m.Itens)
            .Where(m => m.Ativo && (m.TipoEventoId == tipoEventoId || m.TipoEventoId == null))
            .OrderBy(m => m.TipoEventoId == null ? 1 : 0).ThenBy(m => m.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Evento novo já nasce com o checklist e o cronograma dos modelos sugeridos e com o mapa de mesas padrão do espaço
    /// (quando houver).
    /// </summary>
    public static async Task AplicarSugeridoAsync(IBuffetDbContext db, Evento evento, CancellationToken ct)
    {
        if (await ModeloSugeridoAsync(db, evento.TipoEventoId, ct) is { } modelo)
            evento.AplicarChecklist(modelo);
        if (await CronogramaSugeridoAsync(db, evento.TipoEventoId, ct) is { } cronograma)
            evento.AplicarCronograma(cronograma);
        if (await db.EspacoMesas.Where(m => m.EspacoId == evento.EspacoId).ToListAsync(ct) is { Count: > 0 } planta)
            evento.UsarPlanta(planta);
    }

    /// <summary>Modelo de cronograma do tipo de evento ou, na falta dele, o geral.</summary>
    public static async Task<ModeloCronograma?> CronogramaSugeridoAsync(IBuffetDbContext db, int tipoEventoId, CancellationToken ct) =>
        await db.ModelosCronograma.Include(m => m.Itens)
            .Where(m => m.Ativo && (m.TipoEventoId == tipoEventoId || m.TipoEventoId == null))
            .OrderBy(m => m.TipoEventoId == null ? 1 : 0).ThenBy(m => m.Id)
            .FirstOrDefaultAsync(ct);
}
