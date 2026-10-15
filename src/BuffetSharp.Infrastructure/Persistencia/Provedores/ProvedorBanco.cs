using System.Data.Common;
using System.Text.RegularExpressions;
using BuffetSharp.Application.Abstracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BuffetSharp.Infrastructure.Persistencia.Provedores;

/// <summary>Bancos de dados suportados (seção "BancoDeDados:Provedor" do appsettings.json).</summary>
public enum TipoBancoDados
{
    SqlServer,
    PostgreSql,
    MySql,
    Oracle
}

/// <summary>Violações de integridade reconhecidas em qualquer banco, convertidas em mensagens de negócio.</summary>
public enum ErroBanco
{
    Duplicidade,
    RegistroEmUso,
    ReferenciaInvalida
}

/// <summary>
/// Tudo o que muda de um banco para outro: o provedor do EF Core (e suas migrations), a conexão ADO.NET das
/// consultas Dapper, o dialeto SQL, a tradução de erros e os ajustes do modelo (nomes físicos, índices filtrados).
/// Cada banco tem a sua implementação em um projeto próprio (BuffetSharp.Infrastructure.{Tipo}).
/// </summary>
public interface IProvedorBanco
{
    TipoBancoDados Tipo { get; }

    /// <summary>Nome para exibição (ex.: "PostgreSQL").</summary>
    string Nome { get; }

    DialetoSql Dialeto { get; }

    /// <summary>Conexão usada quando nenhuma é configurada (somente o SQL Server LocalDB tem uma).</summary>
    string? ConexaoPadrao { get; }

    /// <summary>Configura o EF Core: provedor, assembly das migrations e comportamento de consultas.</summary>
    void Configurar(DbContextOptionsBuilder opcoes, string conexao);

    DbConnection CriarConexao(string conexao);

    /// <summary>Classifica o erro do driver (exceção interna do DbUpdateException); nulo se não for de integridade.</summary>
    ErroBanco? Classificar(Exception erro);

    void ConfigurarConvencoes(ModelConfigurationBuilder convencoes);

    void AjustarModelo(ModelBuilder modelo);

    /// <summary>"BuffetSharp @ servidor (PostgreSQL)", para a barra de status.</summary>
    string DescreverConexao(string conexao);
}

/// <summary>Comportamento comum dos provedores; cada banco sobrescreve só o que difere.</summary>
public abstract partial class ProvedorBanco : IProvedorBanco
{
    public abstract TipoBancoDados Tipo { get; }
    public abstract string Nome { get; }
    public virtual DialetoSql Dialeto { get; } = new();
    public virtual string? ConexaoPadrao => null;

    public abstract void Configurar(DbContextOptionsBuilder opcoes, string conexao);
    public abstract DbConnection CriarConexao(string conexao);
    public abstract ErroBanco? Classificar(Exception erro);

    /// <summary>Assembly onde ficam as migrations deste banco (o próprio projeto do provedor).</summary>
    protected string AssemblyMigrations => GetType().Assembly.GetName().Name!;

    /// <summary>
    /// Nome físico de tabelas, colunas e restrições. PostgreSQL e Oracle convertem identificadores sem aspas para
    /// minúsculas/maiúsculas; gravando os nomes já assim, o mesmo SQL (sem aspas) das consultas Dapper funciona em todos.
    /// </summary>
    protected virtual string NomeFisico(string nome) => nome;

    /// <summary>Índices com filtro (ex.: único só entre os não nulos). SQL Server e PostgreSQL suportam.</summary>
    protected virtual bool SuportaIndiceFiltrado => false;

    /// <summary>Identificador entre aspas no filtro de índice (os filtros são escritos como [Coluna]).</summary>
    protected virtual string Citar(string identificador) => $"[{identificador}]";

    public virtual void ConfigurarConvencoes(ModelConfigurationBuilder convencoes)
    {
    }

    /// <summary>
    /// Grava datas sem hora (DateOnly) como DateTime na coluna indicada, para drivers que não leem DateOnly
    /// (MySql.Data) ou provedores que as guardariam como texto (Oracle).
    /// </summary>
    protected static void DatasComoDateTime(ModelConfigurationBuilder convencoes, string tipoColuna)
    {
        convencoes.Properties<DateOnly>().HaveConversion<DataParaDateTime>().HaveColumnType(tipoColuna);
        convencoes.Properties<DateOnly?>().HaveConversion<DataParaDateTime>().HaveColumnType(tipoColuna);
    }

    private sealed class DataParaDateTime() : ValueConverter<DateOnly, DateTime>(
        data => data.ToDateTime(TimeOnly.MinValue), dataHora => DateOnly.FromDateTime(dataHora));

    public virtual void AjustarModelo(ModelBuilder modelo)
    {
        // Só renomeia quando o banco exige (PostgreSQL e Oracle): nos demais os nomes ficam como o EF gera.
        var renomear = NomeFisico("Ab") != "Ab";
        foreach (var entidade in modelo.Model.GetEntityTypes())
        {
            if (renomear)
                RenomearObjetos(entidade);

            foreach (var indice in entidade.GetIndexes())
            {
                if (indice.GetFilter() is not { } filtro)
                    continue;

                if (SuportaIndiceFiltrado)
                {
                    indice.SetFilter(Colchetes().Replace(filtro, m => Citar(NomeFisico(m.Groups[1].Value))));
                    continue;
                }

                // Sem índice filtrado: "único entre os não nulos" já é o comportamento padrão de MySQL e Oracle
                // (vários NULL são permitidos). Outras condições (ex.: um contrato vigente por evento) ficam só
                // na regra de negócio, e o índice deixa de ser único.
                indice.SetFilter(null);
                if (!SomenteNaoNulos().IsMatch(filtro))
                    indice.IsUnique = false;
            }
        }
    }

    private void RenomearObjetos(IMutableEntityType entidade)
    {
        if (entidade.GetTableName() is { } tabela)
            entidade.SetTableName(NomeFisico(tabela));
        foreach (var propriedade in entidade.GetProperties())
            propriedade.SetColumnName(NomeFisico(propriedade.GetColumnName()));

        // Colunas de tipos complexos seguem o padrão do EF: Propriedade_Campo (ex.: Endereco_Cep).
        foreach (var complexa in entidade.GetComplexProperties())
            foreach (var propriedade in complexa.ComplexType.GetProperties())
                propriedade.SetColumnName(NomeFisico($"{complexa.Name}_{propriedade.Name}"));

        foreach (var chave in entidade.GetKeys())
            chave.SetName(NomeFisico(chave.GetName()!));
        foreach (var estrangeira in entidade.GetForeignKeys())
            estrangeira.SetConstraintName(NomeFisico(estrangeira.GetConstraintName()!));
        foreach (var indice in entidade.GetIndexes())
            indice.SetDatabaseName(NomeFisico(indice.GetDatabaseName()!));
    }

    public virtual string DescreverConexao(string conexao)
    {
        try
        {
            var partes = new DbConnectionStringBuilder { ConnectionString = conexao };
            string? Valor(params string[] chaves) =>
                chaves.Select(c => partes.TryGetValue(c, out var v) ? v?.ToString() : null).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            var banco = Valor("Database", "Initial Catalog", "User Id", "User ID", "Username");
            var servidor = Valor("Server", "Data Source", "Host");
            return $"{banco ?? "?"} @ {servidor ?? "?"} ({Nome})";
        }
        catch (ArgumentException)
        {
            return Nome;
        }
    }

    /// <summary>Cria um contexto com este provedor (migrations em tempo de design e ferramentas).</summary>
    public BuffetDbContext CriarContexto(string conexao, ISessaoUsuario? sessao = null)
    {
        var opcoes = new DbContextOptionsBuilder<BuffetDbContext>();
        Configurar(opcoes, conexao);
        opcoes.ReplaceService<IModelCacheKeyFactory, ChaveModeloPorProvedor>();
        return new BuffetDbContext(opcoes.Options, this, sessao);
    }

    [GeneratedRegex(@"\[(\w+)\]")]
    private static partial Regex Colchetes();

    [GeneratedRegex(@"^\[\w+\] IS NOT NULL$")]
    private static partial Regex SomenteNaoNulos();
}

/// <summary>
/// O EF Core guarda o modelo em cache por tipo de contexto. Como o modelo muda conforme o banco (nomes físicos,
/// índices), a chave inclui o provedor: permite usar mais de um banco no mesmo processo (ex.: nos testes).
/// </summary>
internal sealed class ChaveModeloPorProvedor : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is BuffetDbContext buffet ? (context.GetType(), buffet.Provedor.Tipo, designTime) : (context.GetType(), designTime);
}
