using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

/// <summary>
/// Painel inicial em uma única ida ao banco (lote de consultas) quando o banco permite; no Oracle, as consultas
/// rodam em sequência na mesma conexão. Períodos (mês atual, últimos 90 dias...) são calculados em C#.
/// </summary>
internal sealed class ConsultasDashboard(FabricaConexao conexoes) : IConsultasDashboard
{
    private const string Indicadores = """
        SELECT
            (SELECT COUNT(*) FROM Eventos
              WHERE Status <> 'Cancelado' AND Inicio >= @IniMes AND Inicio < @FimMes)                AS EventosNoMes,
            (SELECT COALESCE(SUM(ValorTotal), 0) FROM Eventos
              WHERE Status <> 'Cancelado' AND Inicio >= @IniMes AND Inicio < @FimMes)                AS FaturamentoPrevistoMes,
            (SELECT COALESCE(SUM(ValorPago), 0) FROM Parcelas
              WHERE Status = 'Pago' AND DataPagamento >= @IniMes AND DataPagamento < @FimMes)        AS RecebidoNoMes,
            (SELECT COALESCE(SUM(Valor), 0) FROM Parcelas
              WHERE Status = 'Aberto' AND Vencimento < @Hoje)                                         AS AReceberVencido,
            (SELECT COALESCE(SUM(Valor), 0) FROM ContasPagar
              WHERE Status = 'Aberto' AND Vencimento <= @Hoje7)                                       AS APagarProximos7Dias,
            (SELECT COUNT(*) FROM Orcamentos
              WHERE Status IN ('Rascunho', 'Enviado') AND ValidoAte >= @Hoje)                         AS OrcamentosEmAberto,
            (SELECT COALESCE(SUM(ValorTotal), 0) FROM Orcamentos
              WHERE Status IN ('Rascunho', 'Enviado') AND ValidoAte >= @Hoje)                         AS ValorOrcamentosEmAberto,
            (SELECT CASE WHEN COUNT(*) = 0 THEN 0
                         ELSE CAST(SUM(CASE WHEN Status = 'Aprovado' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS DECIMAL(5, 1)) END
               FROM Orcamentos
              WHERE Status IN ('Aprovado', 'Recusado') AND DecididoEm >= @Hoje90)                    AS TaxaConversao90Dias,
            (SELECT COUNT(*) FROM Produtos WHERE Ativo = {verdadeiro} AND EstoqueAtual < EstoqueMinimo) AS ProdutosAbaixoMinimo
        {semTabela}
        """;

    private const string ProximosEventos = """
        SELECT e.Id, e.Titulo, c.Nome AS Cliente, es.Nome AS Espaco, e.Inicio, e.QuantidadeConvidados, e.ValorTotal,
               e.ValorTotal - COALESCE(pg.Pago, 0) AS SaldoAReceber,
               (SELECT COUNT(*) FROM EventoEquipe q WHERE q.EventoId = e.Id) AS EquipeEscalada
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN Espacos es ON es.Id = e.EspacoId
          LEFT JOIN (SELECT p.EventoId, SUM(p.Valor) AS Pago FROM Parcelas p WHERE p.Status = 'Pago' GROUP BY p.EventoId) pg
                 ON pg.EventoId = e.Id
         WHERE e.Status = 'Confirmado' AND e.Inicio >= @Hoje
         ORDER BY e.Inicio
         {limite:10}
        """;

    // Faturamento mensal (8 meses para trás e 3 para frente): valores lidos do período e agrupados em C#,
    // sem funções de data específicas de cada banco.
    private const string EventosDoPeriodo = """
        SELECT Inicio AS Data, ValorTotal AS Valor FROM Eventos
         WHERE Status <> 'Cancelado' AND Inicio >= @IniGrafico AND Inicio < @FimGrafico
        """;

    private const string RecebimentosDoPeriodo = """
        SELECT DataPagamento AS Data, ValorPago AS Valor FROM Parcelas
         WHERE Status = 'Pago' AND DataPagamento >= @IniGrafico AND DataPagamento < @FimGrafico
        """;

    private const string ParcelasVencidas = """
        SELECT p.Id, p.EventoId, CONCAT(CONCAT(p.Descricao, ' - '), e.Titulo) AS Descricao, c.Nome AS Cliente,
               p.Vencimento, p.Valor
          FROM Parcelas p
          JOIN Eventos e ON e.Id = p.EventoId
          JOIN Clientes c ON c.Id = e.ClienteId
         WHERE p.Status = 'Aberto' AND p.Vencimento < @Hoje
         ORDER BY p.Vencimento
         {limite:10}
        """;

    private const string EstoqueBaixo = """
        SELECT p.Id, p.Nome, u.Sigla AS Unidade, p.EstoqueAtual, p.EstoqueMinimo
          FROM Produtos p
          JOIN UnidadesMedida u ON u.Id = p.UnidadeMedidaId
         WHERE p.Ativo = {verdadeiro} AND p.EstoqueAtual < p.EstoqueMinimo
         ORDER BY p.EstoqueAtual / NULLIF(p.EstoqueMinimo, 0)
         {limite:10}
        """;

    private sealed class ValorDatado
    {
        public DateTime Data { get; init; }
        public decimal Valor { get; init; }
    }

    public async Task<ResumoDashboard> ObterAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
        var parametros = new
        {
            Hoje = Filtros.Data(hoje),
            Hoje7 = Filtros.Data(hoje.AddDays(7)),
            Hoje90 = Filtros.Data(hoje.AddDays(-90)),
            IniMes = inicioMes,
            FimMes = inicioMes.AddMonths(1),
            IniGrafico = inicioMes.AddMonths(-8),
            FimGrafico = inicioMes.AddMonths(4)
        };

        await using var lote = await conexoes.LoteAsync(
            [Indicadores, ProximosEventos, EventosDoPeriodo, RecebimentosDoPeriodo, ParcelasVencidas, EstoqueBaixo], parametros, ct);

        var resumo = await lote.LerUnicoAsync<ResumoDashboard>();
        resumo.ProximosEventos = await lote.LerAsync<ProximoEvento>();
        var contratado = await lote.LerAsync<ValorDatado>();
        var recebido = await lote.LerAsync<ValorDatado>();
        resumo.FaturamentoMensal = Enumerable.Range(-8, 12).Select(n => inicioMes.AddMonths(n)).Select(mes => new ValorMensal
        {
            Ano = mes.Year,
            Mes = mes.Month,
            Valor = contratado.Where(v => v.Data >= mes && v.Data < mes.AddMonths(1)).Sum(v => v.Valor),
            Valor2 = recebido.Where(v => v.Data >= mes && v.Data < mes.AddMonths(1)).Sum(v => v.Valor)
        }).ToList();
        resumo.ParcelasVencidas = (await lote.LerAsync<TituloVencido>())
            .Select(t => t with { DiasAtraso = Filtros.DiasAtraso(t.Vencimento, hoje) }).ToList();
        resumo.ProdutosEmAlerta = await lote.LerAsync<ProdutoAlerta>();
        return resumo;
    }
}
