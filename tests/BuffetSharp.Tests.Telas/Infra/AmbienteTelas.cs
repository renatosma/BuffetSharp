using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(BuffetSharp.Tests.Telas.Infra.AmbienteTelas))]

// As telas compartilham estado estático do Windows Forms (formulários abertos, interceptador de mensagens):
// os testes rodam um de cada vez.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BuffetSharp.Tests.Telas.Infra;

/// <summary>
/// Banco temporário no LocalDB com a base de demonstração, criado uma vez para todos os testes de tela
/// e excluído no final. Cada teste monta o próprio contêiner de serviços, como o Program faz.
/// </summary>
public sealed class AmbienteTelas : IAsyncLifetime
{
    public string NomeBanco { get; } = $"buffetsharp_telas_{Guid.NewGuid():N}"[..34];

    public string Conexao { get; private set; } = string.Empty;

    /// <summary>Pasta temporária onde os e-mails são gravados (modo "Pasta").</summary>
    public string PastaEmails { get; } = Path.Combine(Path.GetTempPath(), $"BuffetSharp_Emails_{Guid.NewGuid():N}");

    /// <summary>Pasta temporária onde as mensagens de WhatsApp são gravadas (modo "Pasta": envio direto, sem abrir o aplicativo).</summary>
    public string PastaWhatsApp { get; } = Path.Combine(Path.GetTempPath(), $"BuffetSharp_WhatsApp_{Guid.NewGuid():N}");

    public async ValueTask InitializeAsync()
    {
        // Mesmas variáveis dos testes de integração (BUFFETSHARP_TEST_PROVIDER / BUFFETSHARP_TEST_CONNECTION com {banco}).
        Conexao = (Environment.GetEnvironmentVariable("BUFFETSHARP_TEST_CONNECTION")
                   ?? @"Server=(localdb)\MSSQLLocalDB;Database={banco};Trusted_Connection=True;TrustServerCertificate=True")
            .Replace("{banco}", NomeBanco);

        await using var servicos = CriarServicos();
        try
        {
            await servicos.GetRequiredService<InicializadorBanco>().InicializarAsync(dadosDemonstracao: true);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>Mesmos registros do aplicativo (Program.ConfigurarServicos), apontando para o banco de teste.</summary>
    public ServiceProvider CriarServicos()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Buffet"] = Conexao,
                ["BancoDeDados:Provedor"] = Environment.GetEnvironmentVariable("BUFFETSHARP_TEST_PROVIDER"),
                ["Email:Modo"] = nameof(ModoEnvioEmail.Pasta),
                ["Email:Pasta"] = PastaEmails,
                ["WhatsApp:Modo"] = nameof(ModoEnvioWhatsApp.Pasta),
                ["WhatsApp:Pasta"] = PastaWhatsApp,
                // A janela principal faria a cópia de segurança do banco de teste a cada login.
                ["Backup:Automatico"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        WinForms.Program.ConfigurarServicos(services, configuracao);
        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await using (var servicos = CriarServicos())
        await using (var db = await servicos.GetRequiredService<IDbContextFactory<BuffetDbContext>>().CreateDbContextAsync())
            await db.Database.EnsureDeletedAsync();

        if (Directory.Exists(PastaEmails))
            Directory.Delete(PastaEmails, recursive: true);
        if (Directory.Exists(PastaWhatsApp))
            Directory.Delete(PastaWhatsApp, recursive: true);
    }
}
