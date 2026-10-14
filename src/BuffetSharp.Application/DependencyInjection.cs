using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace BuffetSharp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISessaoUsuario, SessaoUsuario>();

        services.AddTransient(typeof(ICadastroService<>), typeof(CadastroService<>));
        services.AddTransient<DisponibilidadeService>();
        services.AddTransient<EstoqueService>();
        services.AddTransient<CardapioService>();
        services.AddTransient<OrcamentoService>();
        services.AddTransient<EventoService>();
        services.AddTransient<FinanceiroService>();
        services.AddTransient<UsuarioService>();
        return services;
    }
}
