using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;
using Dapper;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasRelatorios(FabricaConexao conexoes) : IConsultasRelatorios
{
    public Task<IReadOnlyList<FaturamentoTipoEvento>> FaturamentoPorTipoAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<FaturamentoTipoEvento>("""
            SELECT t.Nome AS TipoEvento, COUNT(*) AS QuantidadeEventos,
                   SUM(e.QuantidadeConvidados) AS Convidados, SUM(e.ValorTotal) AS ValorTotal
              FROM Eventos e
              JOIN TiposEvento t ON t.Id = e.TipoEventoId
             WHERE e.Status <> 'Cancelado' AND e.Inicio >= @De AND e.Inicio < DATEADD(DAY, 1, @Ate)
             GROUP BY t.Nome
             ORDER BY ValorTotal DESC;
            """, de, ate, ct);

    public Task<IReadOnlyList<RankingPacote>> RankingPacotesAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<RankingPacote>("""
            SELECT COALESCE(p.Nome, '(Sem pacote)') AS Pacote, COUNT(*) AS QuantidadeEventos,
                   SUM(e.QuantidadeConvidados) AS Convidados, SUM(e.ValorTotal) AS ValorTotal
              FROM Eventos e
              LEFT JOIN Pacotes p ON p.Id = e.PacoteId
             WHERE e.Status <> 'Cancelado' AND e.Inicio >= @De AND e.Inicio < DATEADD(DAY, 1, @Ate)
             GROUP BY p.Nome
             ORDER BY QuantidadeEventos DESC, ValorTotal DESC;
            """, de, ate, ct);

    /// <summary>
    /// Receita x custos por evento. Para eventos com estoque já baixado, usa o custo real das
    /// movimentações; para os demais, estima pela ficha técnica do pacote. Despesas das categorias
    /// Pessoal e Locação de materiais não entram em "Despesas" porque já são custeadas pela escala e
    /// pela reserva de materiais do evento.
    /// </summary>
    public Task<IReadOnlyList<LucratividadeEvento>> LucratividadeAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<LucratividadeEvento>("""
            WITH CustoPacote AS (
                SELECT pi.PacoteId, SUM(f.QuantidadePorPessoa * p.CustoMedio) AS CustoPorPessoa
                  FROM PacoteItens pi
                  JOIN FichasTecnicas f ON f.ItemCardapioId = pi.ItemCardapioId
                  JOIN Produtos p ON p.Id = f.ProdutoId
                 GROUP BY pi.PacoteId
            )
            SELECT e.Id, e.Titulo, c.Nome AS Cliente, e.Inicio, e.Status, e.QuantidadeConvidados AS Convidados,
                   e.ValorTotal AS Receita,
                   CAST(CASE WHEN e.EstoqueBaixado = 1
                             THEN COALESCE((SELECT SUM(m.Quantidade * m.CustoUnitario) FROM MovimentacoesEstoque m
                                             WHERE m.EventoId = e.Id AND m.Tipo = 'Saida'), 0)
                             ELSE COALESCE(cp.CustoPorPessoa, 0) * e.QuantidadeConvidados END AS decimal(18, 2)) AS CustoInsumos,
                   COALESCE((SELECT SUM(q.ValorDiaria) FROM EventoEquipe q WHERE q.EventoId = e.Id), 0) AS CustoEquipe,
                   COALESCE((SELECT SUM(m.QuantidadeAlugada * m.CustoLocacaoUnitario + m.QuantidadePerdida * m.ValorReposicaoUnitario)
                               FROM EventoMateriais m WHERE m.EventoId = e.Id), 0) AS CustoMateriais,
                   COALESCE((SELECT SUM(d.Valor) FROM ContasPagar d WHERE d.EventoId = e.Id AND d.Status <> 'Cancelado'
                               AND d.Categoria NOT IN ('Pessoal', 'LocacaoMateriais')), 0) AS Despesas
              FROM Eventos e
              JOIN Clientes c ON c.Id = e.ClienteId
              LEFT JOIN CustoPacote cp ON cp.PacoteId = e.PacoteId
             WHERE e.Status <> 'Cancelado' AND e.Inicio >= @De AND e.Inicio < DATEADD(DAY, 1, @Ate)
             ORDER BY e.Inicio;
            """, de, ate, ct);

    public Task<IReadOnlyList<FluxoCaixaMes>> FluxoCaixaAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<FluxoCaixaMes>("""
            WITH Meses AS (
                SELECT DATEFROMPARTS(YEAR(@De), MONTH(@De), 1) AS Inicio
                UNION ALL
                SELECT DATEADD(MONTH, 1, Inicio) FROM Meses WHERE DATEADD(MONTH, 1, Inicio) <= @Ate
            )
            SELECT YEAR(m.Inicio) AS Ano, MONTH(m.Inicio) AS Mes,
                   COALESCE((SELECT SUM(Valor) FROM Parcelas
                              WHERE Status <> 'Cancelado' AND Vencimento >= m.Inicio AND Vencimento < DATEADD(MONTH, 1, m.Inicio)), 0) AS ReceberPrevisto,
                   COALESCE((SELECT SUM(ValorPago) FROM Parcelas
                              WHERE Status = 'Pago' AND DataPagamento >= m.Inicio AND DataPagamento < DATEADD(MONTH, 1, m.Inicio)), 0) AS Recebido,
                   COALESCE((SELECT SUM(Valor) FROM ContasPagar
                              WHERE Status <> 'Cancelado' AND Vencimento >= m.Inicio AND Vencimento < DATEADD(MONTH, 1, m.Inicio)), 0) AS PagarPrevisto,
                   COALESCE((SELECT SUM(ValorPago) FROM ContasPagar
                              WHERE Status = 'Pago' AND DataPagamento >= m.Inicio AND DataPagamento < DATEADD(MONTH, 1, m.Inicio)), 0) AS Pago
              FROM Meses m
             ORDER BY m.Inicio
            OPTION (MAXRECURSION 240);
            """, de, ate, ct);

    public Task<IReadOnlyList<ConversaoOrcamentos>> ConversaoOrcamentosAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<ConversaoOrcamentos>("""
            SELECT Status, COUNT(*) AS Quantidade, SUM(ValorTotal) AS Valor
              FROM Orcamentos
             WHERE CriadoEm >= @De AND CriadoEm < DATEADD(DAY, 1, @Ate)
             GROUP BY Status
             ORDER BY Quantidade DESC;
            """, de, ate, ct);

    private async Task<IReadOnlyList<T>> ListarAsync<T>(string sql, DateTime de, DateTime ate, CancellationToken ct)
    {
        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<T>(new CommandDefinition(sql, new { De = de.Date, Ate = ate.Date }, cancellationToken: ct));
        return itens.AsList();
    }
}
