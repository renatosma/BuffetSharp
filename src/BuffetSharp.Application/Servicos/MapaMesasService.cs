using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Mapa padrão de mesas do espaço: montado no cadastro do espaço ou salvo a partir do mapa de um evento.</summary>
public sealed class MapaMesasService(IBuffetDbContextFactory fabrica)
{
    public async Task<IReadOnlyList<MesaInput>> ObterDoEspacoAsync(int espacoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return (await db.EspacoMesas.AsNoTracking().Where(m => m.EspacoId == espacoId).OrderBy(m => m.Numero).ToListAsync(ct))
            .Select(m => MesaInput.De(m, m.Id)).ToList();
    }

    /// <summary>Grava o mapa do espaço. Mesas novas sem posição vão para o primeiro lugar livre da planta.</summary>
    public async Task SalvarDoEspacoAsync(int espacoId, IReadOnlyList<MesaInput> mesas, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        Validador.Garantir(await db.Espacos.AnyAsync(e => e.Id == espacoId, ct), "Espaço não encontrado.");
        await GravarAsync(db, espacoId, mesas, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>O mapa do evento (sem os convidados) passa a ser o padrão do espaço dele. Retorna o nome do espaço.</summary>
    public async Task<string> SalvarDoEventoComoPadraoAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Mesas).Include(e => e.Espaco).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        Validador.Garantir(evento.Mesas.Count > 0, "O mapa do evento ainda não tem mesas.");
        await GravarAsync(db, evento.EspacoId, evento.Mesas.Select(m => MesaInput.De(m)).ToList(), ct);
        await db.SaveChangesAsync(ct);
        return evento.Espaco?.Nome ?? "";
    }

    /// <summary>As mesas são casadas pelo número: as que continuam são atualizadas e as que saíram, removidas.</summary>
    private static async Task GravarAsync(IBuffetDbContext db, int espacoId, IReadOnlyList<MesaInput> mesas, CancellationToken ct)
    {
        var posicionadas = mesas.Where(m => m.X is not null && m.Y is not null).Cast<IMesaPlanta>().ToList();
        foreach (var mesa in mesas.Where(m => m.X is null || m.Y is null))
        {
            (mesa.X, mesa.Y) = PlantaSalao.ProximaPosicao(posicionadas);
            posicionadas.Add(mesa);
        }
        PlantaSalao.Validar(mesas);

        var atuais = await db.EspacoMesas.Where(m => m.EspacoId == espacoId).ToListAsync(ct);
        foreach (var atual in atuais.Where(a => mesas.All(m => m.Numero != a.Numero)))
            db.EspacoMesas.Remove(atual);
        foreach (var mesa in mesas)
        {
            if (atuais.FirstOrDefault(a => a.Numero == mesa.Numero) is { } atual)
                atual.Alterar(mesa);
            else
                db.EspacoMesas.Add(new EspacoMesa(espacoId, mesa));
        }
    }
}
