using BuffetSharp.Application.Abstracoes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BuffetSharp.Infrastructure.Persistencia;

/// <summary>Adapta o IDbContextFactory do EF Core para a abstração usada pela aplicação.</summary>
internal sealed class FabricaBuffetDbContext(IDbContextFactory<BuffetDbContext> fabrica) : IBuffetDbContextFactory
{
    public IBuffetDbContext Criar() => fabrica.CreateDbContext();
}

/// <summary>Conexões ADO.NET para as consultas Dapper (lado de leitura).</summary>
public sealed class FabricaConexao(string connectionString)
{
    public string ConnectionString { get; } = connectionString;

    public SqlConnection Criar() => new(ConnectionString);
}

/// <summary>
/// Usado pelo "dotnet ef" em tempo de design (migrations). A conexão pode ser sobrescrita
/// pela variável de ambiente BUFFETSHARP_CONNECTION.
/// </summary>
internal sealed class BuffetDbContextDesignFactory : IDesignTimeDbContextFactory<BuffetDbContext>
{
    public BuffetDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("BUFFETSHARP_CONNECTION")
                         ?? DependencyInjection.ConexaoPadrao;
        var options = new DbContextOptionsBuilder<BuffetDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new BuffetDbContext(options);
    }
}
