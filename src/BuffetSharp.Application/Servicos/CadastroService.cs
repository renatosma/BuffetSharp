using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
        {
            db.Set<T>().Add(entidade);

            // Somente o cadastro em edição é gravado; entidades relacionadas carregadas para exibição
            // (ex.: Funcionario.Cargo) não devem ser inseridas.
            foreach (var relacionada in db.ChangeTracker.Entries().Where(e => !ReferenceEquals(e.Entity, entidade)))
                relacionada.State = EntityState.Unchanged;
        }
        else
        {
            // Os valores da tela são copiados para a versão do banco: o EF grava somente o que mudou e a
            // auditoria registra o valor anterior de cada campo. Campos controlados pelo sistema
            // (ex.: saldo de estoque) mantêm o valor atual do banco.
            var atual = await db.Set<T>().FirstOrDefaultAsync(e => e.Id == entidade.Id, ct)
                        ?? throw new RegraNegocioException("O registro não existe mais. Atualize a lista.");
            var controlados = (entidade as ICamposControlados)?.CamposControladosPeloSistema ?? [];
            CopiarValores(db.Entry(atual), entidade, controlados);
        }

        await db.SaveChangesAsync(ct);
        return entidade;
    }

    private static readonly string[] CamposDoSistema = [nameof(Entidade.CriadoEm), nameof(Entidade.AtualizadoEm)];

    private static void CopiarValores(EntityEntry destino, object origem, IReadOnlyCollection<string> ignorar)
    {
        foreach (var propriedade in destino.Properties)
        {
            var nome = propriedade.Metadata.Name;
            if (propriedade.Metadata.IsPrimaryKey() || propriedade.Metadata.PropertyInfo is not { } info ||
                CamposDoSistema.Contains(nome) || ignorar.Contains(nome))
                continue;
            propriedade.CurrentValue = info.GetValue(origem);
        }

        foreach (var complexa in destino.ComplexProperties)
        {
            var valor = complexa.Metadata.PropertyInfo?.GetValue(origem);
            if (valor is null)
                continue;
            foreach (var propriedade in complexa.Properties)
            {
                if (propriedade.Metadata.PropertyInfo is { } info)
                    propriedade.CurrentValue = info.GetValue(valor);
            }
        }
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
