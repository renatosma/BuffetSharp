using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasComissoes(FabricaConexao conexoes) : IConsultasComissoes
{
    // A comissão de cada parcela usa o percentual congelado no evento e é arredondada parcela a parcela.
    private const string ParcelasDoPeriodo = """
        SELECT p.Id AS ParcelaId, e.Id AS EventoId, e.VendedorId, e.Titulo AS Evento, c.Nome AS Cliente, p.Descricao,
               p.DataPagamento, p.ValorPago, e.PercentualComissao AS Percentual,
               CAST(ROUND(p.ValorPago * e.PercentualComissao / 100, 2) AS DECIMAL(18, 2)) AS Comissao
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
                   f.Id AS FechamentoId, f.De AS FechamentoDe, f.Ate AS FechamentoAte,
                   f.Valor AS FechamentoValor, cp.Status AS SituacaoConta
              FROM Base b
              JOIN Usuarios u ON u.Id = b.VendedorId
              LEFT JOIN (SELECT x.Id, x.VendedorId, x.De, x.Ate, x.Valor, x.ContaPagarId,
                                ROW_NUMBER() OVER (PARTITION BY x.VendedorId ORDER BY x.De) AS Ordem
                           FROM FechamentosComissao x
                          WHERE x.De <= @Ate AND @De <= x.Ate) f ON f.VendedorId = u.Id AND f.Ordem = 1
              LEFT JOIN ContasPagar cp ON cp.Id = f.ContaPagarId
             GROUP BY u.Id, u.Nome, f.Id, f.De, f.Ate, f.Valor, cp.Status
             ORDER BY Comissao DESC
            """;

        return await conexoes.ListarAsync<ComissaoVendedor>(sql, Periodo(de, ate), ct);
    }

    public async Task<IReadOnlyList<ComissaoParcela>> DetalharAsync(int vendedorId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        const string sql = ParcelasDoPeriodo + """
               AND e.VendedorId = @VendedorId
             ORDER BY p.DataPagamento, e.Id
            """;

        return await conexoes.ListarAsync<ComissaoParcela>(sql, new
        {
            De = Filtros.Data(de),
            Ate = Filtros.Data(ate),
            VendedorId = vendedorId
        }, ct);
    }

    public async Task<IReadOnlyList<FechamentoComissaoResumo>> ListarFechamentosAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT f.Id, f.VendedorId, u.Nome AS Vendedor, f.De, f.Ate,
                   f.BaseCalculo, f.Valor, f.CriadoEm, f.ContaPagarId, cp.Status AS SituacaoConta
              FROM FechamentosComissao f
              JOIN Usuarios u ON u.Id = f.VendedorId
              JOIN ContasPagar cp ON cp.Id = f.ContaPagarId
             ORDER BY f.Ate DESC, u.Nome
            """;

        return await conexoes.ListarAsync<FechamentoComissaoResumo>(sql, ct: ct);
    }

    private static object Periodo(DateOnly de, DateOnly ate) => new { De = Filtros.Data(de), Ate = Filtros.Data(ate) };
}

internal sealed class ConsultasAuditoria(FabricaConexao conexoes) : IConsultasAuditoria
{
    public async Task<IReadOnlyList<RegistroAuditoriaResumo>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, DataHora, Usuario, Entidade, RegistroId, Descricao, Acao, Alteracoes
              FROM Auditoria
             WHERE DataHora >= @De AND DataHora < @AteExclusivo
               AND (@Usuario IS NULL OR Usuario = @Usuario)
               AND (@Entidade IS NULL OR Entidade = @Entidade)
               AND (@Acao IS NULL OR Acao = @Acao)
               AND (@RegistroId IS NULL OR RegistroId = @RegistroId)
               AND (@Texto IS NULL OR UPPER(Descricao) LIKE @Texto ESCAPE '!' OR UPPER(Alteracoes) LIKE @Texto ESCAPE '!')
             ORDER BY DataHora DESC, Id DESC
             {limite:@Limite}
            """;

        return await conexoes.ListarAsync<RegistroAuditoriaResumo>(sql, new
        {
            filtro.Limite,
            De = filtro.De.Date,
            AteExclusivo = filtro.Ate.Date.AddDays(1),
            filtro.Usuario,
            filtro.Entidade,
            Acao = filtro.Acao?.ToString(),
            filtro.RegistroId,
            Texto = Filtros.Like(filtro.Texto)
        }, ct);
    }

    public async Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Usuarios)> OpcoesFiltroAsync(CancellationToken ct = default)
    {
        await using var lote = await conexoes.LoteAsync(
        [
            "SELECT DISTINCT Entidade FROM Auditoria ORDER BY Entidade",
            "SELECT DISTINCT Usuario FROM Auditoria ORDER BY Usuario"
        ], ct: ct);
        return (await lote.LerAsync<string>(), await lote.LerAsync<string>());
    }
}
