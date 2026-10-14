using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// CRUD genérico (EF Core) para os cadastros de nível 1 e 2, cujas regras cabem em
/// DataAnnotations/<see cref="System.ComponentModel.DataAnnotations.IValidatableObject"/>.
/// </summary>
public interface ICadastroService<T> where T : Entidade
{
    Task<List<T>> ListarAsync(Func<IQueryable<T>, IQueryable<T>>? consulta = null, CancellationToken ct = default);
    Task<T?> ObterAsync(int id, CancellationToken ct = default);
    Task<T> SalvarAsync(T entidade, CancellationToken ct = default);
    Task ExcluirAsync(int id, CancellationToken ct = default);
}

internal sealed class CadastroService<T>(IBuffetDbContextFactory fabrica) : ICadastroService<T> where T : Entidade
{
    public async Task<List<T>> ListarAsync(Func<IQueryable<T>, IQueryable<T>>? consulta = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var query = db.Set<T>().AsNoTracking();
        if (consulta is not null)
            query = consulta(query);
        return await query.ToListAsync(ct);
    }

    public async Task<T?> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<T> SalvarAsync(T entidade, CancellationToken ct = default)
    {
        Validador.Validar(entidade);

        await using var db = fabrica.Criar();
        if (entidade.EhNovo)
            db.Set<T>().Add(entidade);
        else
            db.Set<T>().Update(entidade);

        // Somente o cadastro em edição é gravado; entidades relacionadas carregadas para exibição
        // (ex.: Funcionario.Cargo) não devem ser alteradas.
        foreach (var relacionada in db.ChangeTracker.Entries().Where(e => !ReferenceEquals(e.Entity, entidade)))
            relacionada.State = EntityState.Unchanged;

        if (!entidade.EhNovo && entidade is ICamposControlados controlados)
        {
            var entry = db.Entry(entidade);
            foreach (var campo in controlados.CamposControladosPeloSistema)
                entry.Property(campo).IsModified = false;
        }

        await db.SaveChangesAsync(ct);
        return entidade;
    }

    public async Task ExcluirAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var entidade = await db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct)
                       ?? throw new RegraNegocioException("Registro não encontrado.");
        db.Set<T>().Remove(entidade);
        await db.SaveChangesAsync(ct);
    }
}
