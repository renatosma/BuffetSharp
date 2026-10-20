using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Pagamento da equipe: as diárias dos eventos realizados (escalas com presença) viram uma conta a pagar por funcionário,
/// como no fechamento de comissões. Desfazer só é possível enquanto a conta não foi paga.
/// </summary>
public sealed class EquipeService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    /// <summary>Fecha as diárias em aberto do funcionário no período. Retorna o Id do fechamento.</summary>
    public async Task<int> FecharAsync(int funcionarioId, DateOnly de, DateOnly ate, DateOnly? vencimento = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var fechamento = await CriarAsync(db, funcionarioId, de, ate, vencimento ?? Hoje, ct)
                         ?? throw new RegraNegocioException("Não há diárias em aberto (com presença registrada) para este funcionário no período.");
        await db.SaveChangesAsync(ct);
        return fechamento.Id;
    }

    /// <summary>Fecha as diárias em aberto de todos os funcionários do período. Retorna quantas contas foram geradas.</summary>
    public async Task<int> FecharTodosAsync(DateOnly de, DateOnly ate, DateOnly? vencimento = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var funcionarios = await EscalasEmAberto(db, de, ate).Select(q => q.FuncionarioId).Distinct().ToListAsync(ct);
        var gerados = 0;
        foreach (var funcionarioId in funcionarios)
        {
            if (await CriarAsync(db, funcionarioId, de, ate, vencimento ?? Hoje, ct) is not null)
                gerados++;
        }
        Validador.Garantir(gerados > 0, "Não há diárias em aberto (com presença registrada) no período.");
        await db.SaveChangesAsync(ct);
        return gerados;
    }

    /// <summary>Exclui o fechamento e a conta a pagar gerada; as diárias voltam a ficar em aberto.</summary>
    public async Task DesfazerAsync(int fechamentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var fechamento = await db.FechamentosEquipe.Include(f => f.ContaPagar).Include(f => f.Escalas)
                             .FirstOrDefaultAsync(f => f.Id == fechamentoId, ct)
                         ?? throw new RegraNegocioException("Fechamento não encontrado.");
        fechamento.Desfazer();
        db.FechamentosEquipe.Remove(fechamento);
        db.ContasPagar.Remove(fechamento.ContaPagar!);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<FechamentoEquipe?> CriarAsync(IBuffetDbContext db, int funcionarioId, DateOnly de, DateOnly ate,
        DateOnly vencimento, CancellationToken ct)
    {
        Validador.Garantir(de <= ate, "O início do período deve ser anterior ao fim.");
        var escalas = await EscalasEmAberto(db, de, ate).Where(q => q.FuncionarioId == funcionarioId).ToListAsync(ct);
        if (escalas.Count == 0)
            return null;

        var funcionario = await db.Funcionarios.FirstOrDefaultAsync(f => f.Id == funcionarioId, ct)
                          ?? throw new RegraNegocioException("Funcionário não encontrado.");
        var fechamento = new FechamentoEquipe(funcionario, de, ate, escalas, vencimento);
        db.FechamentosEquipe.Add(fechamento);
        return fechamento;
    }

    /// <summary>Escalas com presença, ainda não pagas, dos eventos realizados com início no período.</summary>
    private static IQueryable<EventoFuncionario> EscalasEmAberto(IBuffetDbContext db, DateOnly de, DateOnly ate)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return db.Eventos
            .Where(e => e.Status == StatusEvento.Realizado && e.Inicio >= inicio && e.Inicio < fim)
            .SelectMany(e => e.Equipe)
            .Where(q => q.Presente && q.FechamentoEquipeId == null);
    }
}
