using System.Data.Common;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.Data.MySqlClient;

namespace BuffetSharp.Infrastructure.MySql;

/// <summary>MySQL 8.0 ou superior (provedor oficial MySql.EntityFrameworkCore).</summary>
public sealed class ProvedorMySql : ProvedorBanco
{
    public override TipoBancoDados Tipo => TipoBancoDados.MySql;
    public override string Nome => "MySQL";
    public override DialetoSql Dialeto { get; } = new DialetoMySql();

    // O driver MySql.Data não lê colunas DATE como DateOnly: gravadas/lidas como DateTime.
    public override void ConfigurarConvencoes(ModelConfigurationBuilder convencoes) => DatasComoDateTime(convencoes, "date");

    public override void Configurar(DbContextOptionsBuilder opcoes, string conexao) =>
        opcoes.UseMySQL(conexao, sql => sql
            .MigrationsAssembly(AssemblyMigrations)
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

    public override DbConnection CriarConexao(string conexao) => new MySqlConnection(conexao);

    public override ErroBanco? Classificar(Exception erro) => erro is MySqlException mysql
        ? mysql.Number switch
        {
            1062 => ErroBanco.Duplicidade,
            1451 => ErroBanco.RegistroEmUso,
            1452 => ErroBanco.ReferenciaInvalida,
            _ => null
        }
        : null;

    public override string ExtensaoBackup => ".sql";

    /// <summary>
    /// mysqldump numa transação consistente (InnoDB). Usuário e senha vão num arquivo de opções temporário, para a senha
    /// não aparecer na linha de comando.
    /// </summary>
    public override async Task FazerBackupAsync(string conexao, string arquivo, string? ferramenta, CancellationToken ct = default)
    {
        var dados = new MySqlConnectionStringBuilder(conexao);
        static string Valor(string? texto) => $"\"{(texto ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        var opcoes = Path.Combine(Path.GetTempPath(), $"buffetsharp-{Guid.NewGuid():N}.cnf");
        await File.WriteAllLinesAsync(opcoes,
            ["[client]", $"host={Valor(dados.Server)}", $"port={dados.Port}", $"user={Valor(dados.UserID)}", $"password={Valor(dados.Password)}"], ct);
        try
        {
            await ExecutarFerramentaAsync(LocalizarFerramenta(ferramenta, "mysqldump", "MySQL"),
                [$"--defaults-extra-file={opcoes}", "--single-transaction", "--routines", "--triggers", "--no-tablespaces",
                    $"--result-file={arquivo}", dados.Database], null, ct);
        }
        finally
        {
            File.Delete(opcoes);
        }
    }

    private sealed class DialetoMySql : DialetoSql
    {
        public override string Limite(string quantidade) => $"LIMIT {quantidade}";
    }
}

/// <summary>
/// Usado pelo "dotnet ef" para gerar as migrations deste banco. Não precisa de um servidor ativo; a conexão pode
/// ser trocada pela variável de ambiente BUFFETSHARP_CONNECTION (ex.: para "dotnet ef database update").
/// </summary>
internal sealed class FabricaDesign : IDesignTimeDbContextFactory<BuffetDbContext>
{
    public BuffetDbContext CreateDbContext(string[] args) =>
        new ProvedorMySql().CriarContexto(Environment.GetEnvironmentVariable("BUFFETSHARP_CONNECTION")
                                         ?? "Server=localhost;Database=buffetsharp;User Id=root;Password=root");
}
