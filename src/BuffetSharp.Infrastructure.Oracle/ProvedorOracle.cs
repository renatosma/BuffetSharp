using System.Data.Common;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Oracle.EntityFrameworkCore.Infrastructure;
using Oracle.ManagedDataAccess.Client;

namespace BuffetSharp.Infrastructure.Oracle;

/// <summary>
/// Oracle Database 19c ou superior (ODP.NET). O "banco" é o esquema do usuário da connection string, que precisa
/// existir com permissão para criar tabelas.
/// </summary>
public sealed class ProvedorOracle : ProvedorBanco
{
    public override TipoBancoDados Tipo => TipoBancoDados.Oracle;
    public override string Nome => "Oracle";
    public override DialetoSql Dialeto { get; } = new DialetoOracle();

    /// <summary>Identificadores sem aspas viram maiúsculas no Oracle: as tabelas são criadas assim.</summary>
    protected override string NomeFisico(string nome) => nome.ToUpperInvariant();

    public override void Configurar(DbContextOptionsBuilder opcoes, string conexao) =>
        opcoes.UseOracle(conexao, sql => sql
            .MigrationsAssembly(AssemblyMigrations)
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            // Tipos e SQL compatíveis com o 19c (versão de suporte longo mais usada); o 23ai também aceita.
            .UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));

    public override void ConfigurarConvencoes(ModelConfigurationBuilder convencoes)
    {
        // Datas sem hora gravadas como DATE (o provedor as guardaria como texto).
        DatasComoDateTime(convencoes, "DATE");
    }

    public override void AjustarModelo(ModelBuilder modelo)
    {
        base.AjustarModelo(modelo);

        // Sem tamanho máximo (texto do contrato, auditoria, assinaturas): NCLOB/BLOB, pois NVARCHAR2 e RAW
        // param em 2000 caracteres/bytes.
        foreach (var propriedade in modelo.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            if (propriedade.GetMaxLength() is not null || propriedade.GetColumnType() is not null)
                continue;
            if (propriedade.ClrType == typeof(string))
                propriedade.SetColumnType("NCLOB");
            else if (propriedade.ClrType == typeof(byte[]))
                propriedade.SetColumnType("BLOB");
        }
    }

    public override DbConnection CriarConexao(string conexao) => new OracleConnection(conexao);

    public override ErroBanco? Classificar(Exception erro) => erro is OracleException oracle
        ? oracle.Number switch
        {
            1 => ErroBanco.Duplicidade,          // ORA-00001
            2292 => ErroBanco.RegistroEmUso,     // ORA-02292
            2291 => ErroBanco.ReferenciaInvalida, // ORA-02291
            _ => null
        }
        : null;

    private sealed class DialetoOracle : DialetoSql
    {
        public override bool SuportaLote => false;
        public override string SemTabela => " FROM DUAL";
        public override char PrefixoParametro => ':';
    }
}

/// <summary>
/// Usado pelo "dotnet ef" para gerar as migrations deste banco. Não precisa de um servidor ativo; a conexão pode
/// ser trocada pela variável de ambiente BUFFETSHARP_CONNECTION (ex.: para "dotnet ef database update").
/// </summary>
internal sealed class FabricaDesign : IDesignTimeDbContextFactory<BuffetDbContext>
{
    public BuffetDbContext CreateDbContext(string[] args) =>
        new ProvedorOracle().CriarContexto(Environment.GetEnvironmentVariable("BUFFETSHARP_CONNECTION")
                                         ?? "User Id=buffetsharp;Password=buffetsharp;Data Source=localhost:1521/FREEPDB1");
}
