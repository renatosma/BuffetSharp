using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Financeiro;
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

    private sealed class ValorDre
    {
        public DateTime Data { get; init; }
        public decimal Valor { get; init; }
        public string Grupo { get; init; } = string.Empty;
    }

    /// <summary>
    /// Centros de custo (categorias de despesa) e onde entram no demonstrativo. A fatura do cartão fica de fora: a despesa já
    /// foi contada na conta original.
    /// </summary>
    private static readonly (CategoriaDespesa Categoria, string Nome, bool CustoDoEvento)[] Centros =
    [
        (CategoriaDespesa.Insumos, "Insumos (alimentos e bebidas)", true),
        (CategoriaDespesa.Pessoal, "Pessoal (equipe dos eventos)", true),
        (CategoriaDespesa.LocacaoMateriais, "Locação de materiais", true),
        (CategoriaDespesa.Comissoes, "Comissões de vendas", true),
        (CategoriaDespesa.Aluguel, "Aluguel", false),
        (CategoriaDespesa.Servicos, "Serviços (energia, água, internet...)", false),
        (CategoriaDespesa.Manutencao, "Manutenção", false),
        (CategoriaDespesa.Marketing, "Marketing", false),
        (CategoriaDespesa.Outros, "Outras despesas", false)
    ];

    /// <summary>
    /// DRE gerencial do ano, mês a mês. Regime de caixa: parcelas recebidas e contas pagas, pela data do pagamento. Competência:
    /// valor dos eventos (não cancelados) no mês do evento e contas (não canceladas) no mês do vencimento.
    /// </summary>
    public async Task<Dre> DreAsync(int ano, RegimeDre regime, CancellationToken ct = default)
    {
        var caixa = regime == RegimeDre.Caixa;
        var receitas = caixa
            ? """
              SELECT p.DataPagamento AS Data, p.ValorPago AS Valor, t.Nome AS Grupo
                FROM Parcelas p
                JOIN Eventos e ON e.Id = p.EventoId
                JOIN TiposEvento t ON t.Id = e.TipoEventoId
               WHERE p.Status = 'Pago' AND p.DataPagamento >= @Inicio AND p.DataPagamento < @Fim
              """
            : """
              SELECT e.Inicio AS Data, e.ValorTotal AS Valor, t.Nome AS Grupo
                FROM Eventos e
                JOIN TiposEvento t ON t.Id = e.TipoEventoId
               WHERE e.Status <> 'Cancelado' AND e.Inicio >= @Inicio AND e.Inicio < @Fim
              """;
        var despesas = caixa
            ? """
              SELECT DataPagamento AS Data, ValorPago AS Valor, Categoria AS Grupo
                FROM ContasPagar
               WHERE Status = 'Pago' AND Categoria <> 'FaturaCartao' AND DataPagamento >= @Inicio AND DataPagamento < @Fim
              """
            : """
              SELECT Vencimento AS Data, Valor, Categoria AS Grupo
                FROM ContasPagar
               WHERE Status <> 'Cancelado' AND Categoria <> 'FaturaCartao' AND Vencimento >= @Inicio AND Vencimento < @Fim
              """;

        await using var lote = await conexoes.LoteAsync([receitas, despesas],
            new { Inicio = new DateTime(ano, 1, 1), Fim = new DateTime(ano + 1, 1, 1) }, ct);
        var entradas = await lote.LerAsync<ValorDre>();
        var saidas = await lote.LerAsync<ValorDre>();

        static decimal[] PorMes(IEnumerable<ValorDre> valores, int sinal)
        {
            var meses = new decimal[12];
            foreach (var v in valores)
                meses[v.Data.Month - 1] += sinal * v.Valor;
            return meses;
        }
        static decimal[] Somar(params decimal[][] parcelas) =>
            Enumerable.Range(0, 12).Select(m => parcelas.Sum(l => l[m])).ToArray();

        var linhas = new List<(string Descricao, int Nivel, decimal[] Meses)>();
        var receita = PorMes(entradas, 1);
        linhas.Add(("Receita bruta de eventos", 1, receita));
        foreach (var tipo in entradas.GroupBy(e => e.Grupo).OrderByDescending(g => g.Sum(v => v.Valor)))
            linhas.Add(($"    {tipo.Key}", 2, PorMes(tipo, 1)));

        decimal[] Grupo(string titulo, bool custoDoEvento)
        {
            var centros = Centros.Where(c => c.CustoDoEvento == custoDoEvento).ToList();
            var total = PorMes(saidas.Where(s => centros.Any(c => c.Categoria.ToString() == s.Grupo)), -1);
            linhas.Add((titulo, 1, total));
            foreach (var centro in centros)
            {
                var valores = PorMes(saidas.Where(s => s.Grupo == centro.Categoria.ToString()), -1);
                if (valores.Any(v => v != 0))
                    linhas.Add(($"    {centro.Nome}", 2, valores));
            }
            return total;
        }

        var custos = Grupo("(-) Custos dos eventos", custoDoEvento: true);
        var margem = Somar(receita, custos);
        linhas.Add(("= Margem de contribuição", 0, margem));
        var operacionais = Grupo("(-) Despesas operacionais", custoDoEvento: false);
        var impostos = PorMes(saidas.Where(s => s.Grupo == nameof(CategoriaDespesa.Impostos)), -1);
        linhas.Add(("(-) Impostos", 1, impostos));
        linhas.Add(("= Resultado do período", 0, Somar(margem, operacionais, impostos)));

        var receitaAno = receita.Sum();
        return new Dre(ano, regime, linhas.Select(l => new LinhaDre
        {
            Descricao = l.Descricao,
            Nivel = l.Nivel,
            Meses = l.Meses,
            PercentualReceita = receitaAno == 0 ? null : Math.Round(l.Meses.Sum() / receitaAno * 100, 1)
        }).ToList());
    }

    private async Task<IReadOnlyList<T>> ListarAsync<T>(string sql, DateTime de, DateTime ate, CancellationToken ct) =>
        await conexoes.ListarAsync<T>(sql, new { De = de.Date, AteExclusivo = ate.Date.AddDays(1) }, ct);
}
