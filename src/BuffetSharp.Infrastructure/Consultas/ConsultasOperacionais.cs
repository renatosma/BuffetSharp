using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Infrastructure.Persistencia;
using Dapper;

namespace BuffetSharp.Infrastructure.Consultas;

internal static class Filtros
{
    public static string? Like(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : $"%{texto.Trim().Replace("[", "[[]").Replace("%", "[%]")}%";
}

internal sealed class ConsultasComercial(FabricaConexao conexoes) : IConsultasComercial
{
    private const string SelectOrcamento = """
        SELECT o.Id, c.Nome AS Cliente, t.Nome AS TipoEvento, p.Nome AS Pacote, es.Nome AS Espaco, o.Inicio,
               o.QuantidadeConvidados, o.ValorTotal, o.Status, o.ValidoAte, ev.Id AS EventoId, o.CriadoEm
          FROM Orcamentos o
          JOIN Clientes c ON c.Id = o.ClienteId
          JOIN TiposEvento t ON t.Id = o.TipoEventoId
          LEFT JOIN Pacotes p ON p.Id = o.PacoteId
          LEFT JOIN Espacos es ON es.Id = o.EspacoId
          LEFT JOIN Eventos ev ON ev.OrcamentoId = o.Id
        """;

    public async Task<IReadOnlyList<OrcamentoResumo>> ListarOrcamentosAsync(FiltroOrcamentos filtro, CancellationToken ct = default)
    {
        const string sql = SelectOrcamento + """
             WHERE (@De IS NULL OR o.Inicio >= @De)
               AND (@Ate IS NULL OR o.Inicio < DATEADD(DAY, 1, @Ate))
               AND (@Status IS NULL OR o.Status = @Status)
               AND (@Texto IS NULL OR c.Nome LIKE @Texto OR CONCAT('ORC-', FORMAT(o.Id, 'D5')) LIKE @Texto)
             ORDER BY o.Inicio DESC;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<OrcamentoResumo>(new CommandDefinition(sql, new
        {
            filtro.De,
            filtro.Ate,
            Status = filtro.Status?.ToString(),
            Texto = Filtros.Like(filtro.Texto)
        }, cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<HistoricoCliente> HistoricoClienteAsync(int clienteId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(*) AS QuantidadeEventos,
                   COALESCE(SUM(e.ValorTotal), 0) AS TotalContratado,
                   COALESCE((SELECT SUM(p.ValorPago) FROM Parcelas p JOIN Eventos x ON x.Id = p.EventoId
                              WHERE x.ClienteId = @ClienteId AND p.Status = 'Pago'), 0) AS TotalRecebido,
                   COALESCE((SELECT SUM(p.Valor) FROM Parcelas p JOIN Eventos x ON x.Id = p.EventoId
                              WHERE x.ClienteId = @ClienteId AND p.Status = 'Aberto'), 0) AS SaldoEmAberto,
                   MAX(e.Inicio) AS UltimoEvento
              FROM Eventos e
             WHERE e.ClienteId = @ClienteId AND e.Status <> 'Cancelado';
            """ + SelectOrcamento + """
             WHERE o.ClienteId = @ClienteId
             ORDER BY o.Inicio DESC;
            """ + ConsultasEventos.SelectEvento + """
             WHERE e.ClienteId = @ClienteId
             ORDER BY e.Inicio DESC;
            """;

        await using var conexao = conexoes.Criar();
        await using var multi = await conexao.QueryMultipleAsync(
            new CommandDefinition(sql, new { ClienteId = clienteId }, cancellationToken: ct));
        var historico = await multi.ReadSingleAsync<HistoricoCliente>();
        return historico with
        {
            Orcamentos = (await multi.ReadAsync<OrcamentoResumo>()).AsList(),
            Eventos = (await multi.ReadAsync<EventoResumo>()).AsList()
        };
    }
}

internal sealed class ConsultasEventos(FabricaConexao conexoes) : IConsultasEventos
{
    internal const string SelectEvento = """
        SELECT e.Id, e.Titulo, c.Nome AS Cliente, t.Nome AS TipoEvento, es.Nome AS Espaco, e.Inicio,
               e.QuantidadeConvidados, e.ValorTotal, e.Status,
               COALESCE(pg.Recebido, 0) AS Recebido,
               CASE WHEN e.Status = 'Cancelado' THEN 0 ELSE e.ValorTotal - COALESCE(pg.Quitado, 0) END AS SaldoAReceber
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN TiposEvento t ON t.Id = e.TipoEventoId
          JOIN Espacos es ON es.Id = e.EspacoId
          OUTER APPLY (SELECT SUM(p.ValorPago) AS Recebido, SUM(p.Valor) AS Quitado
                         FROM Parcelas p WHERE p.EventoId = e.Id AND p.Status = 'Pago') pg
        """;

    public async Task<IReadOnlyList<EventoResumo>> ListarAsync(FiltroEventos filtro, CancellationToken ct = default)
    {
        const string sql = SelectEvento + """
             WHERE (@De IS NULL OR e.Inicio >= @De)
               AND (@Ate IS NULL OR e.Inicio < DATEADD(DAY, 1, @Ate))
               AND (@Status IS NULL OR e.Status = @Status)
               AND (@EspacoId IS NULL OR e.EspacoId = @EspacoId)
               AND (@Texto IS NULL OR e.Titulo LIKE @Texto OR c.Nome LIKE @Texto OR CONCAT('EVT-', FORMAT(e.Id, 'D5')) LIKE @Texto)
             ORDER BY e.Inicio DESC;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<EventoResumo>(new CommandDefinition(sql, new
        {
            filtro.De,
            filtro.Ate,
            Status = filtro.Status?.ToString(),
            filtro.EspacoId,
            Texto = Filtros.Like(filtro.Texto)
        }, cancellationToken: ct));
        return itens.AsList();
    }
}

internal sealed class ConsultasFinanceiro(FabricaConexao conexoes) : IConsultasFinanceiro
{
    public async Task<IReadOnlyList<ParcelaResumo>> ListarParcelasAsync(FiltroTitulos filtro, DateOnly hoje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id, p.EventoId, e.Titulo AS EventoTitulo, c.Nome AS Cliente, p.Numero, p.Descricao, p.Vencimento,
                   p.Valor, p.Status, p.DataPagamento, p.ValorPago, f.Nome AS FormaPagamento,
                   CASE WHEN p.Status = 'Aberto' AND p.Vencimento < @Hoje THEN DATEDIFF(DAY, p.Vencimento, @Hoje) ELSE 0 END AS DiasAtraso
              FROM Parcelas p
              JOIN Eventos e ON e.Id = p.EventoId
              JOIN Clientes c ON c.Id = e.ClienteId
              LEFT JOIN FormasPagamento f ON f.Id = p.FormaPagamentoId
             WHERE (@VencimentoDe IS NULL OR p.Vencimento >= @VencimentoDe)
               AND (@VencimentoAte IS NULL OR p.Vencimento <= @VencimentoAte)
               AND (@Status IS NULL OR p.Status = @Status)
               AND (@SomenteVencidos = 0 OR (p.Status = 'Aberto' AND p.Vencimento < @Hoje))
               AND (@Texto IS NULL OR c.Nome LIKE @Texto OR e.Titulo LIKE @Texto)
             ORDER BY p.Vencimento, p.EventoId, p.Numero;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<ParcelaResumo>(new CommandDefinition(sql, Parametros(filtro, hoje), cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<IReadOnlyList<ContaPagarResumo>> ListarContasPagarAsync(FiltroTitulos filtro, DateOnly hoje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT cp.Id, cp.Descricao, cp.Categoria, COALESCE(fo.NomeFantasia, fo.RazaoSocial) AS Fornecedor,
                   cp.EventoId, CONCAT('EVT-', FORMAT(e.Id, 'D5'), ' - ', e.Titulo) AS Evento,
                   cp.Vencimento, cp.Valor, cp.Status, cp.DataPagamento, cp.ValorPago, f.Nome AS FormaPagamento,
                   CASE WHEN cp.Status = 'Aberto' AND cp.Vencimento < @Hoje THEN DATEDIFF(DAY, cp.Vencimento, @Hoje) ELSE 0 END AS DiasAtraso
              FROM ContasPagar cp
              LEFT JOIN Fornecedores fo ON fo.Id = cp.FornecedorId
              LEFT JOIN Eventos e ON e.Id = cp.EventoId
              LEFT JOIN FormasPagamento f ON f.Id = cp.FormaPagamentoId
             WHERE (@VencimentoDe IS NULL OR cp.Vencimento >= @VencimentoDe)
               AND (@VencimentoAte IS NULL OR cp.Vencimento <= @VencimentoAte)
               AND (@Status IS NULL OR cp.Status = @Status)
               AND (@SomenteVencidos = 0 OR (cp.Status = 'Aberto' AND cp.Vencimento < @Hoje))
               AND (@Texto IS NULL OR cp.Descricao LIKE @Texto OR fo.RazaoSocial LIKE @Texto OR fo.NomeFantasia LIKE @Texto)
             ORDER BY cp.Vencimento, cp.Id;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<ContaPagarResumo>(new CommandDefinition(sql, Parametros(filtro, hoje), cancellationToken: ct));
        return itens.AsList();
    }

    private static object Parametros(FiltroTitulos filtro, DateOnly hoje) => new
    {
        filtro.VencimentoDe,
        filtro.VencimentoAte,
        Status = filtro.Status?.ToString(),
        filtro.SomenteVencidos,
        Texto = Filtros.Like(filtro.Texto),
        Hoje = hoje.ToDateTime(TimeOnly.MinValue)
    };
}

internal sealed class ConsultasEstoqueCardapio(FabricaConexao conexoes) : IConsultasEstoqueCardapio
{
    public async Task<IReadOnlyList<MovimentacaoResumo>> ListarMovimentacoesAsync(DateTime de, DateTime ate, int? produtoId = null,
        TipoMovimentacao? tipo = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT m.Id, m.Data, m.ProdutoId, p.Nome AS Produto, u.Sigla AS Unidade, m.Tipo, m.Quantidade,
                   m.CustoUnitario, m.SaldoApos, m.Observacao, m.EventoId
              FROM MovimentacoesEstoque m
              JOIN Produtos p ON p.Id = m.ProdutoId
              JOIN UnidadesMedida u ON u.Id = p.UnidadeMedidaId
             WHERE m.Data >= @De AND m.Data < DATEADD(DAY, 1, @Ate)
               AND (@ProdutoId IS NULL OR m.ProdutoId = @ProdutoId)
               AND (@Tipo IS NULL OR m.Tipo = @Tipo)
             ORDER BY m.Data DESC, m.Id DESC;
            """;

        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<MovimentacaoResumo>(new CommandDefinition(sql,
            new { De = de, Ate = ate, ProdutoId = produtoId, Tipo = tipo?.ToString() }, cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<IReadOnlyList<ItemCardapioResumo>> ListarItensCardapioAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT i.Id, i.Nome, c.Nome AS Categoria, c.Ordem AS OrdemCategoria, i.Ativo,
                   COUNT(f.ProdutoId) AS QuantidadeIngredientes,
                   CAST(COALESCE(SUM(f.QuantidadePorPessoa * p.CustoMedio), 0) AS decimal(18, 4)) AS CustoPorPessoa
              FROM ItensCardapio i
              JOIN CategoriasCardapio c ON c.Id = i.CategoriaCardapioId
              LEFT JOIN FichasTecnicas f ON f.ItemCardapioId = i.Id
              LEFT JOIN Produtos p ON p.Id = f.ProdutoId
             GROUP BY i.Id, i.Nome, c.Nome, c.Ordem, i.Ativo
             ORDER BY c.Ordem, i.Nome;
            """;

        await using var conexao = conexoes.Criar();
        return (await conexao.QueryAsync<ItemCardapioResumo>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<PacoteResumo>> ListarPacotesAsync(CancellationToken ct = default)
    {
        const string sql = """
            WITH CustoItem AS (
                SELECT f.ItemCardapioId, SUM(f.QuantidadePorPessoa * p.CustoMedio) AS Custo
                  FROM FichasTecnicas f JOIN Produtos p ON p.Id = f.ProdutoId
                 GROUP BY f.ItemCardapioId
            )
            SELECT pc.Id, pc.Nome, pc.PrecoPorPessoa, pc.MinimoConvidados, pc.Ativo,
                   COUNT(pi.ItemCardapioId) AS QuantidadeItens,
                   CAST(COALESCE(SUM(ci.Custo), 0) AS decimal(18, 4)) AS CustoPorPessoa
              FROM Pacotes pc
              LEFT JOIN PacoteItens pi ON pi.PacoteId = pc.Id
              LEFT JOIN CustoItem ci ON ci.ItemCardapioId = pi.ItemCardapioId
             GROUP BY pc.Id, pc.Nome, pc.PrecoPorPessoa, pc.MinimoConvidados, pc.Ativo
             ORDER BY pc.PrecoPorPessoa;
            """;

        await using var conexao = conexoes.Criar();
        return (await conexao.QueryAsync<PacoteResumo>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<ItemComposicao>> ComposicaoPacoteAsync(int pacoteId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.Nome AS Categoria, i.Nome AS Item, i.Descricao
              FROM PacoteItens pi
              JOIN ItensCardapio i ON i.Id = pi.ItemCardapioId
              JOIN CategoriasCardapio c ON c.Id = i.CategoriaCardapioId
             WHERE pi.PacoteId = @PacoteId
             ORDER BY c.Ordem, i.Nome;
            """;

        await using var conexao = conexoes.Criar();
        return (await conexao.QueryAsync<ItemComposicao>(
            new CommandDefinition(sql, new { PacoteId = pacoteId }, cancellationToken: ct))).AsList();
    }
}
