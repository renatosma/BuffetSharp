using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasRelatorios(FabricaConexao conexoes) : IConsultasRelatorios
{
    public Task<IReadOnlyList<FaturamentoTipoEvento>> FaturamentoPorTipoAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<FaturamentoTipoEvento>("""
            SELECT t.Nome AS TipoEvento, COUNT(*) AS QuantidadeEventos,
                   SUM(e.QuantidadeConvidados) AS Convidados, SUM(e.ValorTotal) AS ValorTotal
              FROM Eventos e
              JOIN TiposEvento t ON t.Id = e.TipoEventoId
             WHERE e.Status <> 'Cancelado' AND e.Inicio >= @De AND e.Inicio < @AteExclusivo
             GROUP BY t.Nome
             ORDER BY ValorTotal DESC
            """, de, ate, ct);

    public Task<IReadOnlyList<RankingPacote>> RankingPacotesAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<RankingPacote>("""
            SELECT COALESCE(p.Nome, '(Sem pacote)') AS Pacote, COUNT(*) AS QuantidadeEventos,
                   SUM(e.QuantidadeConvidados) AS Convidados, SUM(e.ValorTotal) AS ValorTotal
              FROM Eventos e
              LEFT JOIN Pacotes p ON p.Id = e.PacoteId
             WHERE e.Status <> 'Cancelado' AND e.Inicio >= @De AND e.Inicio < @AteExclusivo
             GROUP BY p.Nome
             ORDER BY QuantidadeEventos DESC, ValorTotal DESC
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
                   CAST(CASE WHEN e.EstoqueBaixado = {verdadeiro}
                             THEN COALESCE((SELECT SUM(m.Quantidade * m.CustoUnitario) FROM MovimentacoesEstoque m
                                             WHERE m.EventoId = e.Id AND m.Tipo = 'Saida'), 0)
                             ELSE COALESCE(cp.CustoPorPessoa, 0) * e.QuantidadeConvidados END AS DECIMAL(18, 2)) AS CustoInsumos,
                   COALESCE((SELECT SUM(q.ValorDiaria) FROM EventoEquipe q WHERE q.EventoId = e.Id), 0) AS CustoEquipe,
                   COALESCE((SELECT SUM(m.QuantidadeAlugada * m.CustoLocacaoUnitario + m.QuantidadePerdida * m.ValorReposicaoUnitario)
                               FROM EventoMateriais m WHERE m.EventoId = e.Id), 0) AS CustoMateriais,
                   COALESCE((SELECT SUM(d.Valor) FROM ContasPagar d WHERE d.EventoId = e.Id AND d.Status <> 'Cancelado'
                               AND d.Categoria NOT IN ('Pessoal', 'LocacaoMateriais', 'FaturaCartao')), 0) AS Despesas
              FROM Eventos e
              JOIN Clientes c ON c.Id = e.ClienteId
              LEFT JOIN CustoPacote cp ON cp.PacoteId = e.PacoteId
             WHERE e.Status <> 'Cancelado' AND e.Inicio >= @De AND e.Inicio < @AteExclusivo
             ORDER BY e.Inicio
            """, de, ate, ct);

    private sealed class Lancamento
    {
        public DateTime Vencimento { get; init; }
        public decimal Valor { get; init; }
        public DateTime? DataPagamento { get; init; }
        public decimal? ValorPago { get; init; }
    }

    private sealed class PagamentoCartao
    {
        public DateTime Data { get; init; }
        public decimal Valor { get; init; }
    }

    /// <summary>
    /// Previsto (por vencimento) x realizado (por pagamento), mês a mês. Os lançamentos do período são lidos e
    /// agrupados em C#: evita gerar a série de meses e extrair ano/mês com funções diferentes em cada banco.
    /// A parte de um pagamento feita com o cartão de crédito da empresa não é saída de dinheiro no dia: ela sai do
    /// mês do pagamento (previsto e realizado) e entra quando vencem/são pagas as parcelas da fatura.
    /// </summary>
    public async Task<IReadOnlyList<FluxoCaixaMes>> FluxoCaixaAsync(DateTime de, DateTime ate, CancellationToken ct = default)
    {
        const string condicao = """
             WHERE Status <> 'Cancelado'
               AND ((Vencimento >= @Inicio AND Vencimento < @Fim) OR (DataPagamento >= @Inicio AND DataPagamento < @Fim))
            """;
        var inicio = new DateTime(de.Year, de.Month, 1);
        var fim = new DateTime(ate.Year, ate.Month, 1).AddMonths(1);
        var parametros = new { Inicio = inicio, Fim = fim };

        await using var lote = await conexoes.LoteAsync(
        [
            "SELECT Vencimento, Valor, DataPagamento, ValorPago FROM Parcelas" + condicao,
            "SELECT Vencimento, Valor, DataPagamento, ValorPago FROM ContasPagar" + condicao,
            """
            SELECT b.Data, f.Valor FROM BaixaFormas f JOIN Baixas b ON b.Id = f.BaixaId
             WHERE b.Tipo = 'Pagamento' AND f.CartaoCreditoId IS NOT NULL AND b.Data >= @Inicio AND b.Data < @Fim
            """
        ], parametros, ct);
        var receber = await lote.LerAsync<Lancamento>();
        var pagar = await lote.LerAsync<Lancamento>();
        var cartao = await lote.LerAsync<PagamentoCartao>();

        static bool NoMes(DateTime? data, DateTime mes) => data >= mes && data < mes.AddMonths(1);

        var meses = new List<FluxoCaixaMes>();
        for (var mes = inicio; mes < fim; mes = mes.AddMonths(1))
        {
            meses.Add(new FluxoCaixaMes
            {
                Ano = mes.Year,
                Mes = mes.Month,
                ReceberPrevisto = receber.Where(l => NoMes(l.Vencimento, mes)).Sum(l => l.Valor),
                Recebido = receber.Where(l => NoMes(l.DataPagamento, mes)).Sum(l => l.ValorPago ?? 0),
                PagarPrevisto = pagar.Where(l => NoMes(l.Vencimento, mes)).Sum(l => l.Valor) - cartao.Where(c => NoMes(c.Data, mes)).Sum(c => c.Valor),
                Pago = pagar.Where(l => NoMes(l.DataPagamento, mes)).Sum(l => l.ValorPago ?? 0) - cartao.Where(c => NoMes(c.Data, mes)).Sum(c => c.Valor)
            });
        }
        return meses;
    }

    public Task<IReadOnlyList<ConversaoOrcamentos>> ConversaoOrcamentosAsync(DateTime de, DateTime ate, CancellationToken ct = default) =>
        ListarAsync<ConversaoOrcamentos>("""
            SELECT Status, COUNT(*) AS Quantidade, SUM(ValorTotal) AS Valor
              FROM Orcamentos
             WHERE CriadoEm >= @De AND CriadoEm < @AteExclusivo
             GROUP BY Status
             ORDER BY Quantidade DESC
            """, de, ate, ct);

    private async Task<IReadOnlyList<T>> ListarAsync<T>(string sql, DateTime de, DateTime ate, CancellationToken ct) =>
        await conexoes.ListarAsync<T>(sql, new { De = de.Date, AteExclusivo = ate.Date.AddDays(1) }, ct);
}
