using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Consultas;
using BuffetSharp.Infrastructure.Email;
using BuffetSharp.Infrastructure.Manutencao;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using BuffetSharp.Infrastructure.Persistencia.Seed;
using BuffetSharp.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuffetSharp.Infrastructure;

public static class DependencyInjection
{
    /// <param name="connectionString">Conexão do banco; vazia usa a padrão do provedor (somente SQL Server LocalDB tem).</param>
    /// <param name="banco">Banco de dados: SQL Server, PostgreSQL, MySQL ou Oracle.</param>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, string? connectionString,
        TipoBancoDados banco = TipoBancoDados.SqlServer)
    {
        var provedor = CatalogoProvedores.Obter(banco);
        var conexao = !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : provedor.ConexaoPadrao ?? throw new InvalidOperationException(
                $"Informe a connection string \"Buffet\" do {provedor.Nome} no appsettings.json.");
        services.AddSingleton(provedor);

        // Escrita: EF Core 10 (um DbContext por operação, criado pela fábrica).
        services.AddDbContextFactory<BuffetDbContext>(options =>
        {
            provedor.Configurar(options, conexao);
            options.ReplaceService<IModelCacheKeyFactory, ChaveModeloPorProvedor>();
        });
        services.AddSingleton<IBuffetDbContextFactory, FabricaBuffetDbContext>();

        // Leitura: Dapper sobre conexões ADO.NET do mesmo banco.
        services.AddSingleton(new FabricaConexao(conexao, provedor));
        services.AddSingleton<IConsultasLookup, ConsultasLookup>();
        services.AddSingleton<IConsultasDashboard, ConsultasDashboard>();
        services.AddSingleton<IConsultasAgenda, ConsultasAgenda>();
        services.AddSingleton<IConsultasComercial, ConsultasComercial>();
        services.AddSingleton<IConsultasEventos, ConsultasEventos>();
        services.AddSingleton<IConsultasFinanceiro, ConsultasFinanceiro>();
        services.AddSingleton<IConsultasContas, ConsultasContas>();
        services.AddSingleton<IConsultasEstoqueCardapio, ConsultasEstoqueCardapio>();
        services.AddSingleton<IConsultasRelatorios, ConsultasRelatorios>();
        services.AddSingleton<IConsultasComissoes, ConsultasComissoes>();
        services.AddSingleton<IConsultasAuditoria, ConsultasAuditoria>();
        services.AddSingleton<IConsultasLeads, ConsultasLeads>();
        services.AddSingleton<IConsultasLembretes, ConsultasLembretes>();
        services.AddSingleton<IConsultasEquipe, ConsultasEquipe>();

        // E-mail: usa a configuração registrada pela aplicação ou, na falta dela, grava em pasta local.
        services.TryAddSingleton(new EmailOptions());
        services.AddSingleton<IEnvioEmail, EnvioEmailSmtp>();

        // Cópia de segurança: usa a configuração registrada pela aplicação ou, na falta dela, a padrão.
        services.TryAddSingleton(new BackupOptions());
        services.AddSingleton<IBackupBanco, BackupBanco>();

        services.AddSingleton<IGeradorQrCode, GeradorQrCode>();
        services.AddSingleton<IHasherSenha, HasherSenhaPbkdf2>();
        services.AddTransient<InicializadorBanco>();
        return services;
    }
}
