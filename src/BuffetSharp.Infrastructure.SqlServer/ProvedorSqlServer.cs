using System.Data.Common;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BuffetSharp.Infrastructure.SqlServer;

/// <summary>Microsoft SQL Server (2016 ou superior) e Azure SQL. Padrão: SQL Server LocalDB.</summary>
public sealed class ProvedorSqlServer : ProvedorBanco
{
    public override TipoBancoDados Tipo => TipoBancoDados.SqlServer;
    public override string Nome => "SQL Server";

    public override string ConexaoPadrao =>
        @"Server=(localdb)\MSSQLLocalDB;Database=BuffetSharp;Trusted_Connection=True;TrustServerCertificate=True";

    protected override bool SuportaIndiceFiltrado => true;

    public override void Configurar(DbContextOptionsBuilder opcoes, string conexao) =>
        opcoes.UseSqlServer(conexao, sql => sql
            .MigrationsAssembly(AssemblyMigrations)
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

    public override DbConnection CriarConexao(string conexao) => new SqlConnection(conexao);

    public override ErroBanco? Classificar(Exception erro) => erro is SqlException sql
        ? sql.Number switch
        {
            2601 or 2627 => ErroBanco.Duplicidade,
            547 when sql.Message.Contains("DELETE", StringComparison.OrdinalIgnoreCase) => ErroBanco.RegistroEmUso,
            547 => ErroBanco.ReferenciaInvalida,
            _ => null
        }
        : null;

    public override string ExtensaoBackup => ".bak";

    /// <summary>
    /// BACKUP DATABASE nativo (cópia avulsa, sem afetar a sequência de backups do servidor). O arquivo é gravado pelo
    /// próprio SQL Server: a pasta é a do servidor e precisa permitir gravação pela conta do serviço (no LocalDB, o usuário).
    /// </summary>
    public override async Task FazerBackupAsync(string conexao, string arquivo, string? ferramenta, CancellationToken ct = default)
    {
        await using var sql = new SqlConnection(conexao);
        await sql.OpenAsync(ct);
        await using var comando = sql.CreateCommand();
        comando.CommandText = """
            DECLARE @banco sysname = DB_NAME();
            BACKUP DATABASE @banco TO DISK = @arquivo WITH COPY_ONLY, INIT, NAME = @nome
            """;
        comando.CommandTimeout = 0;
        comando.Parameters.AddWithValue("@arquivo", arquivo);
        comando.Parameters.AddWithValue("@nome", $"BuffetSharp {DateTime.Now:dd/MM/yyyy HH:mm}");
        await comando.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Usado pelo "dotnet ef" para gerar as migrations deste banco. Não precisa de um servidor ativo; a conexão pode
/// ser trocada pela variável de ambiente BUFFETSHARP_CONNECTION (ex.: para "dotnet ef database update").
/// </summary>
internal sealed class FabricaDesign : IDesignTimeDbContextFactory<BuffetDbContext>
{
    public BuffetDbContext CreateDbContext(string[] args) =>
        new ProvedorSqlServer().CriarContexto(Environment.GetEnvironmentVariable("BUFFETSHARP_CONNECTION")
                                         ?? "Server=(localdb)\\MSSQLLocalDB;Database=BuffetSharp;Trusted_Connection=True;TrustServerCertificate=True");
}
