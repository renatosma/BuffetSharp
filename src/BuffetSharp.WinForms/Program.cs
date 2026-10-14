using System.Globalization;
using BuffetSharp.Application;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure;
using BuffetSharp.WinForms.Formularios;
using BuffetSharp.WinForms.Infra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => Mensagens.Erro(Form.ActiveForm, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Mensagens.Erro(null, ex);
        };

        using var host = CriarHost();
        var servicos = host.Services;

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

        var opcoes = builder.Configuration.GetSection(BuffetOptions.Secao).Get<BuffetOptions>() ?? new BuffetOptions();
        builder.Services.AddSingleton(opcoes);
        builder.Services.AddAplicacao();
        builder.Services.AddInfraestrutura(builder.Configuration.GetConnectionString("Buffet"));
        builder.Services.AddSingleton<Navegador>();
        return builder.Build();
    }
}
