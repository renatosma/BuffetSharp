using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

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
        return await ComItens(db).AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    /// <summary>
    /// Listas que fazem parte do cadastro (contatos e endereços adicionais, embalagens do produto...): coleções cujos
    /// itens são excluídos junto com o dono. São carregadas para edição e gravadas junto com o cadastro.
    /// </summary>
    private static List<INavigation> Itens(IBuffetDbContext db) =>
        db.Set<T>().EntityType.GetNavigations()
            .Where(n => n.IsCollection && n.ForeignKey.DeleteBehavior == DeleteBehavior.Cascade && n.PropertyInfo is not null)
            .ToList();

    private static IQueryable<T> ComItens(IBuffetDbContext db)
    {
        IQueryable<T> consulta = db.Set<T>();
        foreach (var navegacao in Itens(db))
            consulta = consulta.Include(navegacao.Name);
        return consulta;
    }

    public async Task<T> SalvarAsync(T entidade, CancellationToken ct = default)
    {
        Validador.Validar(entidade);

        await using var db = fabrica.Criar();
        if (entidade.EhNovo)
        {
            db.Set<T>().Add(entidade);

            // Somente o cadastro em edição (e os itens das suas listas) é gravado; entidades relacionadas carregadas
            // para exibição (ex.: Funcionario.Cargo) não devem ser inseridas.
            var itens = Itens(db).SelectMany(n => (IEnumerable<object>)n.PropertyInfo!.GetValue(entidade)!).ToHashSet(ReferenceEqualityComparer.Instance);
            foreach (var relacionada in db.ChangeTracker.Entries().Where(e => !ReferenceEquals(e.Entity, entidade) && !itens.Contains(e.Entity)))
                relacionada.State = EntityState.Unchanged;
        }
        else
        {
            // Os valores da tela são copiados para a versão do banco: o EF grava somente o que mudou e a
            // auditoria registra o valor anterior de cada campo. Campos controlados pelo sistema
            // (ex.: saldo de estoque) mantêm o valor atual do banco.
            var atual = await ComItens(db).FirstOrDefaultAsync(e => e.Id == entidade.Id, ct)
                        ?? throw new RegraNegocioException("O registro não existe mais. Atualize a lista.");
            var controlados = (entidade as ICamposControlados)?.CamposControladosPeloSistema ?? [];
            CopiarValores(db.Entry(atual), entidade, controlados);
            foreach (var navegacao in Itens(db))
                SincronizarItens(db, navegacao, atual, entidade);
        }

        if (entidade is IPadraoUnico { Padrao: true })
        {
            var outros = await db.Set<T>()
                .Where(e => e.Id != entidade.Id && EF.Property<bool>(e, nameof(IPadraoUnico.Padrao)))
                .ToListAsync(ct);
            foreach (var outro in outros.Cast<IPadraoUnico>())
                outro.Padrao = false;
        }

        await db.SaveChangesAsync(ct);
        return entidade;
    }

    /// <summary>
    /// Aplica na lista gravada o que foi feito na tela: itens novos (Id 0) são incluídos, os existentes têm os valores
    /// copiados (a auditoria registra o que mudou) e os que saíram da lista são excluídos.
    /// </summary>
    private static void SincronizarItens(IBuffetDbContext db, INavigation navegacao, T atual, T editado)
    {
        var gravados = (System.Collections.IList)navegacao.PropertyInfo!.GetValue(atual)!;
        var naTela = ((IEnumerable<object>)navegacao.PropertyInfo.GetValue(editado)!).Cast<Entidade>().ToList();

        foreach (var gravado in gravados.Cast<Entidade>().ToList())
        {
            if (naTela.Any(i => i.Id == gravado.Id))
                continue;
            db.ChangeTracker.Entries().First(e => ReferenceEquals(e.Entity, gravado)).State = EntityState.Deleted;
        }

        foreach (var item in naTela)
        {
            if (item.EhNovo)
            {
                gravados.Add(item);
                continue;
            }
            var gravado = gravados.Cast<Entidade>().FirstOrDefault(g => g.Id == item.Id);
            if (gravado is not null)
                CopiarValores(db.ChangeTracker.Entries().First(e => ReferenceEquals(e.Entity, gravado)), item, []);
        }
    }

    private static readonly string[] CamposDoSistema = [nameof(Entidade.CriadoEm), nameof(Entidade.AtualizadoEm), nameof(Entidade.Versao)];

    private static void CopiarValores(EntityEntry destino, object origem, IReadOnlyCollection<string> ignorar)
    {
        // Concorrência: o registro só é gravado se ninguém o alterou desde que a tela o leu.
        if (destino.Entity is Entidade atual && origem is Entidade lida)
            Concorrencia.Conferir(atual, lida.Versao);

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
