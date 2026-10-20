using System.Data.Common;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace BuffetSharp.Infrastructure.PostgreSql;

/// <summary>PostgreSQL 13 ou superior (Npgsql).</summary>
public sealed class ProvedorPostgreSql : ProvedorBanco
{
    public override TipoBancoDados Tipo => TipoBancoDados.PostgreSql;
    public override string Nome => "PostgreSQL";
    public override DialetoSql Dialeto { get; } = new DialetoPostgreSql();

    protected override bool SuportaIndiceFiltrado => true;

    /// <summary>Identificadores sem aspas viram minúsculas no PostgreSQL: as tabelas são criadas assim.</summary>
    protected override string NomeFisico(string nome) => nome.ToLowerInvariant();

    protected override string Citar(string identificador) => $"\"{identificador}\"";

    public override void ConfigurarConvencoes(ModelConfigurationBuilder convencoes)
    {
        // O sistema trabalha com data/hora local (sem fuso), como nos demais bancos.
        convencoes.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        convencoes.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
    }

    public override void Configurar(DbContextOptionsBuilder opcoes, string conexao) =>
        opcoes.UseNpgsql(conexao, sql => sql
            .MigrationsAssembly(AssemblyMigrations)
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

    public override DbConnection CriarConexao(string conexao) => new NpgsqlConnection(conexao);

    public override ErroBanco? Classificar(Exception erro) => erro is PostgresException pg
        ? pg.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => ErroBanco.Duplicidade,
            // As chaves do sistema são ON DELETE RESTRICT: excluir um registro referenciado gera restrict_violation.
            PostgresErrorCodes.RestrictViolation => ErroBanco.RegistroEmUso,
            PostgresErrorCodes.ForeignKeyViolation => ErroBanco.ReferenciaInvalida,
            _ => null
        }
        : null;

    public override string ExtensaoBackup => ".dump";

    /// <summary>pg_dump no formato personalizado (restaurável com pg_restore). A senha vai pela variável PGPASSWORD.</summary>
    public override Task FazerBackupAsync(string conexao, string arquivo, string? ferramenta, CancellationToken ct = default)
    {
        var dados = new NpgsqlConnectionStringBuilder(conexao);
        var argumentos = new List<string> { "--format=custom", "--no-password", $"--file={arquivo}" };
        if (!string.IsNullOrWhiteSpace(dados.Host))
            argumentos.Add($"--host={dados.Host}");
        argumentos.Add($"--port={dados.Port}");
        if (!string.IsNullOrWhiteSpace(dados.Username))
            argumentos.Add($"--username={dados.Username}");
        argumentos.Add(dados.Database ?? dados.Username ?? "postgres");

        var ambiente = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(dados.Password))
            ambiente["PGPASSWORD"] = dados.Password;
        return ExecutarFerramentaAsync(LocalizarFerramenta(ferramenta, "pg_dump", "PostgreSQL"), argumentos, ambiente, ct);
    }

    private sealed class DialetoPostgreSql : DialetoSql
    {
        public override bool TipaParametrosNulos => true;
        public override string Verdadeiro => "TRUE";
        public override string Falso => "FALSE";
    }
}

/// <summary>
/// Usado pelo "dotnet ef" para gerar as migrations deste banco. Não precisa de um servidor ativo; a conexão pode
/// ser trocada pela variável de ambiente BUFFETSHARP_CONNECTION (ex.: para "dotnet ef database update").
/// </summary>
internal sealed class FabricaDesign : IDesignTimeDbContextFactory<BuffetDbContext>
{
    public BuffetDbContext CreateDbContext(string[] args) =>
        new ProvedorPostgreSql().CriarContexto(Environment.GetEnvironmentVariable("BUFFETSHARP_CONNECTION")
                                         ?? "Host=localhost;Database=buffetsharp;Username=postgres;Password=postgres");
}
