using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;
using Dapper;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasComissoes(FabricaConexao conexoes) : IConsultasComissoes
{
    // A comissão de cada parcela usa o percentual congelado no evento e é arredondada parcela a parcela.
    private const string ParcelasDoPeriodo = """
        SELECT p.Id AS ParcelaId, e.Id AS EventoId, e.VendedorId, e.Titulo AS Evento, c.Nome AS Cliente, p.Descricao,
               CAST(p.DataPagamento AS datetime2) AS DataPagamento, p.ValorPago, e.PercentualComissao AS Percentual,
               CAST(ROUND(p.ValorPago * e.PercentualComissao / 100, 2) AS decimal(18, 2)) AS Comissao
          FROM Parcelas p
          JOIN Eventos e ON e.Id = p.EventoId
          JOIN Clientes c ON c.Id = e.ClienteId
         WHERE p.Status = 'Pago' AND e.VendedorId IS NOT NULL
           AND p.DataPagamento >= @De AND p.DataPagamento <= @Ate
        """;

    public async Task<IReadOnlyList<ComissaoVendedor>> ApurarAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        const string sql = "WITH Base AS (" + ParcelasDoPeriodo + """
            )
            SELECT u.Id AS VendedorId, u.Nome AS Vendedor,
                   COUNT(b.ParcelaId) AS QuantidadeParcelas, COUNT(DISTINCT b.EventoId) AS QuantidadeEventos,
                   SUM(b.ValorPago) AS BaseRecebida, SUM(b.Comissao) AS Comissao,
                   f.Id AS FechamentoId, CAST(f.De AS datetime2) AS FechamentoDe, CAST(f.Ate AS datetime2) AS FechamentoAte,
                   f.Valor AS FechamentoValor, cp.Status AS SituacaoConta
              FROM Base b
              JOIN Usuarios u ON u.Id = b.VendedorId
              OUTER APPLY (SELECT TOP 1 x.Id, x.De, x.Ate, x.Valor, x.ContaPagarId FROM FechamentosComissao x
                            WHERE x.VendedorId = u.Id AND x.De <= @Ate AND @De <= x.Ate ORDER BY x.De) f
              LEFT JOIN ContasPagar cp ON cp.Id = f.ContaPagarId
             GROUP BY u.Id, u.Nome, f.Id, f.De, f.Ate, f.Valor, cp.Status
             ORDER BY Comissao DESC;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<ComissaoVendedor>(new CommandDefinition(sql, Periodo(de, ate), cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<IReadOnlyList<ComissaoParcela>> DetalharAsync(int vendedorId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        const string sql = ParcelasDoPeriodo + """
               AND e.VendedorId = @VendedorId
             ORDER BY p.DataPagamento, e.Id;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<ComissaoParcela>(new CommandDefinition(sql, new
        {
            De = de.ToDateTime(TimeOnly.MinValue),
            Ate = ate.ToDateTime(TimeOnly.MinValue),
            VendedorId = vendedorId
        }, cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<IReadOnlyList<FechamentoComissaoResumo>> ListarFechamentosAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT f.Id, f.VendedorId, u.Nome AS Vendedor, CAST(f.De AS datetime2) AS De, CAST(f.Ate AS datetime2) AS Ate,
                   f.BaseCalculo, f.Valor, f.CriadoEm, f.ContaPagarId, cp.Status AS SituacaoConta
              FROM FechamentosComissao f
              JOIN Usuarios u ON u.Id = f.VendedorId
              JOIN ContasPagar cp ON cp.Id = f.ContaPagarId
             ORDER BY f.Ate DESC, u.Nome;
            """;

        await using var conexao = conexoes.Criar();
        return (await conexao.QueryAsync<FechamentoComissaoResumo>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static object Periodo(DateOnly de, DateOnly ate) =>
        new { De = de.ToDateTime(TimeOnly.MinValue), Ate = ate.ToDateTime(TimeOnly.MinValue) };
}

internal sealed class ConsultasAuditoria(FabricaConexao conexoes) : IConsultasAuditoria
{
    public async Task<IReadOnlyList<RegistroAuditoriaResumo>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@Limite) Id, DataHora, Usuario, Entidade, RegistroId, Descricao, Acao, Alteracoes
              FROM Auditoria
             WHERE DataHora >= @De AND DataHora < DATEADD(DAY, 1, @Ate)
               AND (@Usuario IS NULL OR Usuario = @Usuario)
               AND (@Entidade IS NULL OR Entidade = @Entidade)
               AND (@Acao IS NULL OR Acao = @Acao)
               AND (@RegistroId IS NULL OR RegistroId = @RegistroId)
               AND (@Texto IS NULL OR Descricao LIKE @Texto OR Alteracoes LIKE @Texto)
             ORDER BY DataHora DESC, Id DESC;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<RegistroAuditoriaResumo>(new CommandDefinition(sql, new
        {
            filtro.Limite,
            De = filtro.De.Date,
            Ate = filtro.Ate.Date,
            filtro.Usuario,
            filtro.Entidade,
            Acao = filtro.Acao?.ToString(),
            filtro.RegistroId,
            Texto = Filtros.Like(filtro.Texto)
        }, cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Usuarios)> OpcoesFiltroAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT Entidade FROM Auditoria ORDER BY Entidade;
            SELECT DISTINCT Usuario FROM Auditoria ORDER BY Usuario;
            """;

        await using var conexao = conexoes.Criar();
        await using var multi = await conexao.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: ct));
        var entidades = (await multi.ReadAsync<string>()).AsList();
        var usuarios = (await multi.ReadAsync<string>()).AsList();
        return (entidades, usuarios);
    }
}
