using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comercial;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Ajuste de um dia no calendário de preços.</summary>
public sealed record DiaPreco(DateOnly Data, AjustePreco Ajuste);

/// <summary>Preço por data: o ajuste das regras (dia da semana, temporada e data especial) para o orçamento e o calendário.</summary>
public sealed class TabelaPrecoService(IBuffetDbContextFactory fabrica)
{
    /// <summary>Ajuste que vale na data para o pacote e o espaço.</summary>
    public async Task<AjustePreco> AjusteAsync(DateOnly data, int? pacoteId, int? espacoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return AjustePreco.Calcular(await RegrasAtivasAsync(db, ct), data, pacoteId, espacoId);
    }

    /// <summary>Ajuste de cada dia do mês, para o vendedor consultar ao negociar.</summary>
    public async Task<IReadOnlyList<DiaPreco>> CalendarioAsync(DateOnly mes, int? pacoteId, int? espacoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var regras = await RegrasAtivasAsync(db, ct);
        var inicio = new DateOnly(mes.Year, mes.Month, 1);
        return Enumerable.Range(0, DateTime.DaysInMonth(mes.Year, mes.Month))
            .Select(i => inicio.AddDays(i))
            .Select(dia => new DiaPreco(dia, AjustePreco.Calcular(regras, dia, pacoteId, espacoId)))
            .ToList();
    }

    internal static async Task<List<RegraPreco>> RegrasAtivasAsync(IBuffetDbContext db, CancellationToken ct) =>
        await db.RegrasPreco.AsNoTracking().Where(r => r.Ativo).ToListAsync(ct);
}
