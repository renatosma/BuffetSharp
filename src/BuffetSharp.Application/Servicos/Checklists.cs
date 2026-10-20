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

    /// <summary>Evento novo já nasce com o checklist do modelo sugerido (quando houver).</summary>
    public static async Task AplicarSugeridoAsync(IBuffetDbContext db, Evento evento, CancellationToken ct)
    {
        if (await ModeloSugeridoAsync(db, evento.TipoEventoId, ct) is { } modelo)
            evento.AplicarChecklist(modelo);
    }
}
