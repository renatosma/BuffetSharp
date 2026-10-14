using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Consultas;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Seed;
using BuffetSharp.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuffetSharp.Infrastructure;

public static class DependencyInjection
{
    public const string ConexaoPadrao =
        @"Server=(localdb)\MSSQLLocalDB;Database=BuffetSharp;Trusted_Connection=True;TrustServerCertificate=True";

    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, string? connectionString)
    {
        var conexao = string.IsNullOrWhiteSpace(connectionString) ? ConexaoPadrao : connectionString;

        // Escrita: EF Core 10 (um DbContext por operação, criado pela fábrica).
        services.AddDbContextFactory<BuffetDbContext>(options => options
            .UseSqlServer(conexao, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddSingleton<IBuffetDbContextFactory, FabricaBuffetDbContext>();

        // Leitura: Dapper sobre conexões ADO.NET.
        services.AddSingleton(new FabricaConexao(conexao));
        services.AddSingleton<IConsultasLookup, ConsultasLookup>();
        services.AddSingleton<IConsultasDashboard, ConsultasDashboard>();
        services.AddSingleton<IConsultasAgenda, ConsultasAgenda>();
        services.AddSingleton<IConsultasComercial, ConsultasComercial>();
        services.AddSingleton<IConsultasEventos, ConsultasEventos>();
        services.AddSingleton<IConsultasFinanceiro, ConsultasFinanceiro>();
        services.AddSingleton<IConsultasEstoqueCardapio, ConsultasEstoqueCardapio>();
        services.AddSingleton<IConsultasRelatorios, ConsultasRelatorios>();

        services.AddSingleton<IHasherSenha, HasherSenhaPbkdf2>();
        services.AddTransient<InicializadorBanco>();
        return services;
    }
}
