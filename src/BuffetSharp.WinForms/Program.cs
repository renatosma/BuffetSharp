using System.Globalization;
using BuffetSharp.Application;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure;
using BuffetSharp.Infrastructure.Manutencao;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using BuffetSharp.WinForms.Formularios;
using BuffetSharp.WinForms.Infra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;

        using var host = CriarHost();
        var servicos = host.Services;

        // Erros inesperados vão para o log em arquivo, além da mensagem na tela.
        var log = servicos.GetRequiredService<ILoggerFactory>().CreateLogger("BuffetSharp.Telas");
        Mensagens.Log = log;
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => Mensagens.Erro(Form.ActiveForm, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception ex)
                return;
            log.LogCritical(ex, "Erro não tratado (encerrando: {Encerrando})", e.IsTerminating);
            Mensagens.Erro(null, ex);
        };
        log.LogInformation("BuffetSharp {Versao} iniciado", typeof(Program).Assembly.GetName().Version?.ToString(3));

        using (var inicializacao = ActivatorUtilities.CreateInstance<FormInicializacao>(servicos))
        {
            if (inicializacao.ShowDialog() != DialogResult.OK)
                return;
        }

        // Ciclo login → janela principal (permite "trocar de usuário" sem fechar o sistema).
        while (true)
        {
            using (var login = ActivatorUtilities.CreateInstance<FormLogin>(servicos))
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            using var principal = ActivatorUtilities.CreateInstance<FormPrincipal>(servicos);
            System.Windows.Forms.Application.Run(principal);
            if (!principal.TrocarUsuario)
                return;
            servicos.GetRequiredService<ISessaoUsuario>().Encerrar();
        }
    }

    private static IHost CriarHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = "BuffetSharp"
        });

        // Configuração só desta máquina (ex.: conexão com usuário e senha), fora do git.
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

        // Log em arquivo (um por dia); os níveis gravados vêm da seção "Logging".
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ProvedorLogArquivo(builder.Configuration.GetSection(LogOptions.Secao).Get<LogOptions>() ?? new LogOptions()));

        ConfigurarServicos(builder.Services, builder.Configuration);
        return builder.Build();
    }

    /// <summary>Registros de DI da aplicação desktop (também usados pelos testes das telas).</summary>
    internal static void ConfigurarServicos(IServiceCollection services, IConfiguration configuracao)
    {
        services.AddSingleton(configuracao);
        services.AddSingleton(configuracao.GetSection(BuffetOptions.Secao).Get<BuffetOptions>() ?? new BuffetOptions());
        services.AddSingleton(configuracao.GetSection(EmailOptions.Secao).Get<EmailOptions>() ?? new EmailOptions());
        services.AddSingleton(configuracao.GetSection(BackupOptions.Secao).Get<BackupOptions>() ?? new BackupOptions());
        services.AddSingleton(configuracao.GetSection(LogOptions.Secao).Get<LogOptions>() ?? new LogOptions());
        services.AddSingleton(configuracao.GetSection(LembretesOptions.Secao).Get<LembretesOptions>() ?? new LembretesOptions());
        services.AddAplicacao();
        services.AddInfraestrutura(configuracao.GetConnectionString("Buffet"),
            CatalogoProvedores.Interpretar(configuracao["BancoDeDados:Provedor"]));
        services.AddSingleton<Navegador>();
    }
}
