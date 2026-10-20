using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuffetSharp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISessaoUsuario, SessaoUsuario>();

        services.AddTransient(typeof(ICadastroService<>), typeof(CadastroService<>));
        services.AddTransient<DisponibilidadeService>();
        services.AddTransient<EstoqueService>();
        services.AddTransient<CardapioService>();
        services.AddTransient<OrcamentoService>();
        services.AddTransient<EventoService>();
        services.AddTransient<FinanceiroService>();
        services.AddTransient<BaixaService>();
        services.AddTransient<ContaFinanceiraService>();
        services.AddTransient<UsuarioService>();
        services.AddTransient<ContratoService>();
        services.AddTransient<PropostaService>();
        services.AddTransient<DegustacaoService>();
        services.AddTransient<ComissaoService>();
        services.AddTransient<LeadService>();
        services.AddTransient<LembreteService>();
        services.AddTransient<ReajusteService>();
        services.AddTransient<PlanejamentoService>();
        services.AddTransient<EquipeService>();

        // Configurações com valor padrão quando a aplicação não as registra (ex.: testes).
        services.TryAddSingleton(new LembretesOptions());
        return services;
    }
}
