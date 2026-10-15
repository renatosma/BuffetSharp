using BuffetSharp.Application;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using BuffetSharp.Infrastructure.Persistencia.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuffetSharp.Tests.Integracao;

/// <summary>
/// Cria um banco temporário, aplica as migrations e carrega a base de demonstração. O banco é excluído ao
/// final da execução dos testes.
/// <para>
/// Por padrão usa o SQL Server LocalDB. Para rodar a mesma suíte em outro banco, defina
/// BUFFETSHARP_TEST_PROVIDER (SqlServer, PostgreSql, MySql ou Oracle) e BUFFETSHARP_TEST_CONNECTION, usando
/// {banco} no lugar do nome do banco temporário, ex.: "Host=localhost;Username=postgres;Database={banco}".
/// </para>
/// </summary>
public sealed class BancoFixture : IAsyncLifetime
{
    public string NomeBanco { get; } = $"buffetsharp_testes_{Guid.NewGuid():N}"[..36];
    public TipoBancoDados Banco { get; } = CatalogoProvedores.Interpretar(Environment.GetEnvironmentVariable("BUFFETSHARP_TEST_PROVIDER"));
    public ServiceProvider Servicos { get; private set; } = null!;

    public T Obter<T>() where T : notnull => Servicos.GetRequiredService<T>();

    public async ValueTask InitializeAsync()
    {
        var conexao = (Environment.GetEnvironmentVariable("BUFFETSHARP_TEST_CONNECTION")
                       ?? @"Server=(localdb)\MSSQLLocalDB;Database={banco};Trusted_Connection=True;TrustServerCertificate=True")
            .Replace("{banco}", NomeBanco);

        var services = new ServiceCollection();
        services.AddSingleton(new BuffetOptions());
        services.AddAplicacao();
        services.AddInfraestrutura(conexao, Banco);
        Servicos = services.BuildServiceProvider();

        try
        {
            await Obter<InicializadorBanco>().InicializarAsync(dadosDemonstracao: true);
        }
        catch
        {
            // Se a inicialização falhar o xUnit não chama DisposeAsync: remove o banco aqui.
            await DisposeAsync();
            throw;
        }

        // Os serviços de usuários exigem sessão de administrador.
        Obter<ISessaoUsuario>().Iniciar(new UsuarioLogado(1, "Administrador", "admin", Domain.Seguranca.PerfilUsuario.Administrador));
    }

    public async ValueTask DisposeAsync()
    {
        await using (var db = await Obter<IDbContextFactory<BuffetDbContext>>().CreateDbContextAsync())
            await db.Database.EnsureDeletedAsync();
        await Servicos.DisposeAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoBanco : ICollectionFixture<BancoFixture>
{
    public const string Nome = "Banco LocalDB";
}
