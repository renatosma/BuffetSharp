using System.Data.Common;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Infrastructure.Persistencia;

/// <summary>Adapta o IDbContextFactory do EF Core para a abstração usada pela aplicação.</summary>
internal sealed class FabricaBuffetDbContext(IDbContextFactory<BuffetDbContext> fabrica) : IBuffetDbContextFactory
{
    public IBuffetDbContext Criar() => fabrica.CreateDbContext();
}

/// <summary>
/// Conexões ADO.NET para as consultas Dapper (lado de leitura), no banco configurado. As consultas passam pelo
/// <see cref="DialetoSql"/> antes de executar.
/// </summary>
public sealed class FabricaConexao(string connectionString, IProvedorBanco provedor)
{
    public string ConnectionString { get; } = connectionString;

    public IProvedorBanco Provedor { get; } = provedor;

    public DialetoSql Dialeto => Provedor.Dialeto;

    public DbConnection Criar() => Provedor.CriarConexao(ConnectionString);

    public async Task<List<T>> ListarAsync<T>(string sql, object? parametros = null, CancellationToken ct = default)
    {
        await using var conexao = Criar();
        return (await conexao.QueryAsync<T>(Comando(sql, parametros, ct))).AsList();
    }

    public async Task<T?> PrimeiroAsync<T>(string sql, object? parametros = null, CancellationToken ct = default)
    {
        await using var conexao = Criar();
        return await conexao.QueryFirstOrDefaultAsync<T>(Comando(sql, parametros, ct));
    }

    /// <summary>
    /// Várias consultas com os mesmos parâmetros: em uma única ida ao banco quando o dialeto permite
    /// (QueryMultiple), ou em sequência na mesma conexão (Oracle).
    /// </summary>
    public async Task<LoteConsultas> LoteAsync(IReadOnlyList<string> sqls, object? parametros = null, CancellationToken ct = default)
    {
        var conexao = Criar();
        try
        {
            await conexao.OpenAsync(ct);
            parametros = Dialeto.AjustarParametros(parametros);
            if (!Dialeto.SuportaLote)
                return new LoteConsultas(conexao, null, sqls.Select(Dialeto.Preparar).ToList(), parametros, ct);

            var lote = string.Join(";\n", sqls.Select(Dialeto.Preparar));
            var leitor = await conexao.QueryMultipleAsync(new CommandDefinition(lote, parametros, cancellationToken: ct));
            return new LoteConsultas(conexao, leitor, [], parametros, ct);
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }
    }

    private CommandDefinition Comando(string sql, object? parametros, CancellationToken ct) =>
        new(Dialeto.Preparar(sql), Dialeto.AjustarParametros(parametros), cancellationToken: ct);
}

/// <summary>Leitura, na ordem, dos resultados de <see cref="FabricaConexao.LoteAsync"/>.</summary>
public sealed class LoteConsultas : IAsyncDisposable
{
    private readonly DbConnection _conexao;
    private readonly SqlMapper.GridReader? _leitor;
    private readonly Queue<string> _pendentes;
    private readonly object? _parametros;
    private readonly CancellationToken _ct;

    internal LoteConsultas(DbConnection conexao, SqlMapper.GridReader? leitor, List<string> pendentes, object? parametros,
        CancellationToken ct)
    {
        _conexao = conexao;
        _leitor = leitor;
        _pendentes = new Queue<string>(pendentes);
        _parametros = parametros;
        _ct = ct;
    }

    public async Task<List<T>> LerAsync<T>() => _leitor is not null
        ? (await _leitor.ReadAsync<T>()).AsList()
        : (await _conexao.QueryAsync<T>(new CommandDefinition(_pendentes.Dequeue(), _parametros, cancellationToken: _ct))).AsList();

    public async Task<T> LerUnicoAsync<T>() => _leitor is not null
        ? await _leitor.ReadSingleAsync<T>()
        : await _conexao.QuerySingleAsync<T>(new CommandDefinition(_pendentes.Dequeue(), _parametros, cancellationToken: _ct));

    public async ValueTask DisposeAsync()
    {
        _leitor?.Dispose();
        await _conexao.DisposeAsync();
    }
}
