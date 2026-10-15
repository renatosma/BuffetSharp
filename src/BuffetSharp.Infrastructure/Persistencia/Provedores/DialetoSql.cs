using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;

namespace BuffetSharp.Infrastructure.Persistencia.Provedores;

/// <summary>
/// Diferenças de SQL entre os bancos nas consultas Dapper. As consultas são escritas em SQL padrão (ANSI) e usam
/// marcadores para o que não tem forma comum:
/// <list type="bullet">
///   <item><c>{verdadeiro}</c> / <c>{falso}</c>: literais booleanos (1/0 ou TRUE/FALSE);</item>
///   <item><c>{semTabela}</c>: SELECT sem tabela (o Oracle exige FROM DUAL);</item>
///   <item><c>{limite:@Parametro}</c>: no fim da consulta (após o ORDER BY), limita a quantidade de linhas.</item>
/// </list>
/// Os parâmetros são escritos com @ e convertidos para o prefixo do banco (":" no Oracle).
/// </summary>
public partial class DialetoSql
{
    /// <summary>Vários comandos em uma única ida ao banco (o Oracle não aceita).</summary>
    public virtual bool SuportaLote => true;

    public virtual string Verdadeiro => "1";
    public virtual string Falso => "0";
    public virtual string SemTabela => string.Empty;
    public virtual char PrefixoParametro => '@';

    /// <summary>
    /// Parâmetros nulos precisam de tipo explícito (o PostgreSQL não deduz o tipo de "@De IS NULL" quando o
    /// valor é nulo, e o Dapper não informa o tipo de DateTime).
    /// </summary>
    public virtual bool TipaParametrosNulos => false;

    /// <summary>Padrão SQL:2008, aceito por SQL Server (com ORDER BY), PostgreSQL e Oracle 12c+.</summary>
    public virtual string Limite(string quantidade) => $"OFFSET 0 ROWS FETCH NEXT {quantidade} ROWS ONLY";

    public string Preparar(string sql)
    {
        var texto = Marcadores().Replace(sql, m => m.Groups[1].Value switch
        {
            "verdadeiro" => Verdadeiro,
            "falso" => Falso,
            "semTabela" => SemTabela,
            _ => Limite(m.Groups[2].Value)
        });
        if (PrefixoParametro != '@')
            texto = Parametros().Replace(texto, PrefixoParametro.ToString());
        return texto.TrimEnd().TrimEnd(';');
    }

    public object? AjustarParametros(object? parametros)
    {
        if (!TipaParametrosNulos || parametros is null or DynamicParameters)
            return parametros;

        var ajustados = new DynamicParameters();
        foreach (var propriedade in parametros.GetType().GetProperties())
        {
            var valor = propriedade.GetValue(parametros);
            var tipo = Nullable.GetUnderlyingType(propriedade.PropertyType) ?? propriedade.PropertyType;
            ajustados.Add(propriedade.Name, valor, valor is not null ? null : TiposNulos.GetValueOrDefault(tipo));
        }
        return ajustados;
    }

    private static readonly Dictionary<Type, DbType> TiposNulos = new()
    {
        [typeof(DateTime)] = DbType.DateTime,
        [typeof(string)] = DbType.String,
        [typeof(int)] = DbType.Int32,
        [typeof(long)] = DbType.Int64,
        [typeof(decimal)] = DbType.Decimal,
        [typeof(bool)] = DbType.Boolean
    };

    [GeneratedRegex(@"\{(verdadeiro|falso|semTabela|limite):?(@?\w+)?\}")]
    private static partial Regex Marcadores();

    [GeneratedRegex(@"(?<![\w@])@(?=\w)")]
    private static partial Regex Parametros();
}

/// <summary>Localiza a implementação do banco escolhido no assembly BuffetSharp.Infrastructure.{Tipo}.</summary>
public static class CatalogoProvedores
{
    public static IProvedorBanco Obter(TipoBancoDados tipo)
    {
        var nome = $"BuffetSharp.Infrastructure.{tipo}";
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(nome);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"O suporte ao banco {tipo} não está disponível: {nome}.dll não foi encontrado junto ao sistema.");
        }

        var implementacao = assembly.GetTypes().Single(t => typeof(IProvedorBanco).IsAssignableFrom(t) && !t.IsAbstract);
        return (IProvedorBanco)Activator.CreateInstance(implementacao)!;
    }

    /// <summary>Aceita o nome do enum ou apelidos comuns ("Postgres", "MSSQL"...).</summary>
    public static TipoBancoDados Interpretar(string? valor) => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" or "sqlserver" or "mssql" or "sql server" => TipoBancoDados.SqlServer,
        "postgresql" or "postgres" or "pgsql" => TipoBancoDados.PostgreSql,
        "mysql" => TipoBancoDados.MySql,
        "oracle" => TipoBancoDados.Oracle,
        _ => throw new InvalidOperationException(
            $"Banco de dados \"{valor}\" não suportado. Use SqlServer, PostgreSql, MySql ou Oracle.")
    };
}
