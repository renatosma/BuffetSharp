using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Infrastructure.Persistencia;

public sealed class BuffetDbContext(DbContextOptions<BuffetDbContext> options) : DbContext(options), IBuffetDbContext
{
    public DbSet<TipoEvento> TiposEvento => Set<TipoEvento>();
    public DbSet<CategoriaProduto> CategoriasProduto => Set<CategoriaProduto>();
    public DbSet<CategoriaCardapio> CategoriasCardapio => Set<CategoriaCardapio>();
    public DbSet<UnidadeMedida> UnidadesMedida => Set<UnidadeMedida>();
    public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<Espaco> Espacos => Set<Espaco>();
    public DbSet<ServicoAdicional> ServicosAdicionais => Set<ServicoAdicional>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<ItemCardapio> ItensCardapio => Set<ItemCardapio>();
    public DbSet<Pacote> Pacotes => Set<Pacote>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Parcela> Parcelas => Set<Parcela>();
    public DbSet<ContaPagar> ContasPagar => Set<ContaPagar>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Valores monetários com 2 casas por padrão (quantidades e custos unitários sobrescrevem no mapeamento).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Enums gravados como texto: o banco fica legível e as consultas Dapper ficam auto-explicativas.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuffetDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarAuditoria();
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex) when (Traduzir(ex) is { } traduzida)
        {
            throw traduzida;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarAuditoria();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (Traduzir(ex) is { } traduzida)
        {
            throw traduzida;
        }
    }

    private void AplicarAuditoria()
    {
        var agora = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entidade>())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.CriadoEm == default:
                    entry.Entity.CriadoEm = agora;
                    break;
                case EntityState.Modified:
                    entry.Entity.AtualizadoEm = agora;
                    entry.Property(e => e.CriadoEm).IsModified = false;
                    break;
            }
        }
    }

    /// <summary>Converte erros de integridade do SQL Server em mensagens de negócio.</summary>
    private static RegraNegocioException? Traduzir(DbUpdateException ex)
    {
        if (ex is DbUpdateConcurrencyException)
            return new RegraNegocioException("O registro foi alterado ou excluído por outro usuário. Recarregue e tente novamente.");

        if (ex.InnerException is not SqlException sql)
            return null;

        return sql.Number switch
        {
            2601 or 2627 => new RegraNegocioException("Já existe um registro com estes dados (valor duplicado)."),
            547 when sql.Message.Contains("DELETE", StringComparison.OrdinalIgnoreCase) =>
                new RegraNegocioException("Este registro está sendo utilizado e não pode ser excluído. Se preferir, inative-o."),
            547 => new RegraNegocioException("Há uma referência inválida a outro cadastro."),
            _ => null
        };
    }
}
