using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Infrastructure.Persistencia;

/// <param name="provedor">Banco de dados em uso: ajusta o modelo (nomes físicos, índices) e traduz os erros.</param>
/// <param name="sessao">Usuário logado, gravado na auditoria (nulo em tempo de design/migrations).</param>
public sealed class BuffetDbContext(DbContextOptions<BuffetDbContext> options, IProvedorBanco provedor, ISessaoUsuario? sessao = null)
    : DbContext(options), IBuffetDbContext
{
    public IProvedorBanco Provedor { get; } = provedor;

    public DbSet<TipoEvento> TiposEvento => Set<TipoEvento>();
    public DbSet<CategoriaProduto> CategoriasProduto => Set<CategoriaProduto>();
    public DbSet<CategoriaCardapio> CategoriasCardapio => Set<CategoriaCardapio>();
    public DbSet<UnidadeMedida> UnidadesMedida => Set<UnidadeMedida>();
    public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();
    public DbSet<CondicaoPagamento> CondicoesPagamento => Set<CondicaoPagamento>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<Espaco> Espacos => Set<Espaco>();
    public DbSet<ServicoAdicional> ServicosAdicionais => Set<ServicoAdicional>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<ProdutoEmbalagem> ProdutoEmbalagens => Set<ProdutoEmbalagem>();
    public DbSet<ItemCardapio> ItensCardapio => Set<ItemCardapio>();
    public DbSet<Pacote> Pacotes => Set<Pacote>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Parcela> Parcelas => Set<Parcela>();
    public DbSet<ContaPagar> ContasPagar => Set<ContaPagar>();
    public DbSet<ParcelamentoPagar> ParcelamentosPagar => Set<ParcelamentoPagar>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Material> Materiais => Set<Material>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<ModeloContrato> ModelosContrato => Set<ModeloContrato>();
    public DbSet<Degustacao> Degustacoes => Set<Degustacao>();
    public DbSet<FechamentoComissao> FechamentosComissao => Set<FechamentoComissao>();
    public DbSet<ContaFinanceira> ContasFinanceiras => Set<ContaFinanceira>();
    public DbSet<MovimentoConta> MovimentosConta => Set<MovimentoConta>();
    public DbSet<FechamentoConta> FechamentosConta => Set<FechamentoConta>();
    public DbSet<CartaoCredito> CartoesCredito => Set<CartaoCredito>();
    public DbSet<Baixa> Baixas => Set<Baixa>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    /// <summary>
    /// Desligada somente em cargas em massa (base de demonstração), em que o histórico não tem valor.
    /// </summary>
    public bool AuditoriaHabilitada { get; set; } = true;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Valores monetários com 2 casas por padrão (quantidades e custos unitários sobrescrevem no mapeamento).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Enums gravados como texto: o banco fica legível e as consultas Dapper ficam auto-explicativas.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);

        Provedor.ConfigurarConvencoes(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuffetDbContext).Assembly);

        // Concorrência otimista: o UPDATE só grava se a versão no banco ainda for a que foi lida.
        foreach (var entidade in modelBuilder.Model.GetEntityTypes()
                     .Where(t => t.BaseType is null && typeof(Entidade).IsAssignableFrom(t.ClrType)))
            modelBuilder.Entity(entidade.ClrType).Property(nameof(Entidade.Versao)).IsConcurrencyToken();

        Provedor.AjustarModelo(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreencherDatas();
        var pendentes = AuditoriaHabilitada ? Auditoria.Coletar(this) : [];
        try
        {
            if (pendentes.Count == 0)
                return base.SaveChanges(acceptAllChangesOnSuccess);

            // Dados e histórico na mesma transação: ou grava tudo, ou nada.
            using var transacao = Database.CurrentTransaction is null ? Database.BeginTransaction() : null;
            var resultado = base.SaveChanges(acceptAllChangesOnSuccess);
            RegistrosAuditoria.AddRange(Auditoria.Finalizar(pendentes, sessao?.Usuario, DateTime.Now));
            base.SaveChanges(acceptAllChangesOnSuccess);
            transacao?.Commit();
            return resultado;
        }
        catch (DbUpdateException ex) when (Traduzir(ex, Provedor) is { } traduzida)
        {
            throw traduzida;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreencherDatas();
        var pendentes = AuditoriaHabilitada ? Auditoria.Coletar(this) : [];
        try
        {
            if (pendentes.Count == 0)
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            // Dados e histórico na mesma transação: ou grava tudo, ou nada.
            await using var transacao = Database.CurrentTransaction is null
                ? await Database.BeginTransactionAsync(cancellationToken)
                : null;
            var resultado = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            RegistrosAuditoria.AddRange(Auditoria.Finalizar(pendentes, sessao?.Usuario, DateTime.Now));
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (transacao is not null)
                await transacao.CommitAsync(cancellationToken);
            return resultado;
        }
        catch (DbUpdateException ex) when (Traduzir(ex, Provedor) is { } traduzida)
        {
            throw traduzida;
        }
    }

    private void PreencherDatas()
    {
        // Horário UTC sem a marcação de fuso: colunas de data/hora não guardam fuso em nenhum dos bancos suportados
        // (o PostgreSQL recusa DateTime "Utc" em timestamp without time zone).
        var agora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
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
                    if (AlteradoPeloUsuario(entry))
                        entry.Entity.Versao = entry.Property(e => e.Versao).OriginalValue + 1;
                    break;
            }
        }
    }

    private static readonly HashSet<string> CamposSemVersao =
        [nameof(Entidade.CriadoEm), nameof(Entidade.AtualizadoEm), nameof(Entidade.Versao)];

    /// <summary>
    /// A versão só muda quando muda um dado do cadastro. Campos mantidos pelo sistema (saldo de estoque, último acesso...)
    /// mudam a toda hora e não devem impedir quem está com a tela aberta de salvar.
    /// </summary>
    private static bool AlteradoPeloUsuario(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Entidade> entry)
    {
        var controlados = (entry.Entity as ICamposControlados)?.CamposControladosPeloSistema ?? [];
        return entry.Properties.Any(p => p.IsModified && !CamposSemVersao.Contains(p.Metadata.Name) && !controlados.Contains(p.Metadata.Name)) ||
               entry.ComplexProperties.Any(c => c.Properties.Any(p => p.IsModified));
    }

    /// <summary>Converte erros de integridade do banco (classificados pelo provedor) em mensagens de negócio.</summary>
    private static RegraNegocioException? Traduzir(DbUpdateException ex, IProvedorBanco provedor)
    {
        if (ex is DbUpdateConcurrencyException)
            return new RegraNegocioException(Application.Servicos.Concorrencia.Mensagem);

        return (ex.InnerException is null ? null : provedor.Classificar(ex.InnerException)) switch
        {
            ErroBanco.Duplicidade => new RegraNegocioException("Já existe um registro com estes dados (valor duplicado)."),
            ErroBanco.RegistroEmUso =>
                new RegraNegocioException("Este registro está sendo utilizado e não pode ser excluído. Se preferir, inative-o."),
            ErroBanco.ReferenciaInvalida => new RegraNegocioException("Há uma referência inválida a outro cadastro."),
            _ => null
        };
    }
}
