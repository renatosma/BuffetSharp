using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

// As consultas usam SQL padrão para funcionar em SQL Server, PostgreSQL, MySQL e Oracle: datas e intervalos são
// calculados em C# e passados como parâmetros, e o que não tem forma comum usa os marcadores do DialetoSql.

internal static class Filtros
{
    /// <summary>
    /// Padrão para <c>UPPER(coluna) LIKE @Texto ESCAPE '!'</c>: em maiúsculas (PostgreSQL e Oracle diferenciam
    /// maiúsculas de minúsculas) e com "!" como caractere de escape, aceito igualmente por todos os bancos.
    /// </summary>
    public static string? Like(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : $"%{texto.Trim().ToUpperInvariant().Replace("!", "!!").Replace("%", "!%").Replace("_", "!_")}%";

    /// <summary>Código digitado na pesquisa ("ORC-00042" ou "42") convertido no Id; nulo se não for um código.</summary>
    public static int? Codigo(string? texto, string prefixo)
    {
        var valor = texto?.Trim().ToUpperInvariant();
        if (valor is null)
            return null;
        if (valor.StartsWith(prefixo + "-", StringComparison.Ordinal))
            valor = valor[(prefixo.Length + 1)..];
        return int.TryParse(valor, out var id) ? id : null;
    }

    /// <summary>Limite superior exclusivo de um filtro "até" (inclui o dia inteiro).</summary>
    public static DateTime? FimDoDia(DateTime? data) => data?.Date.AddDays(1);

    public static DateTime Data(DateOnly data) => data.ToDateTime(TimeOnly.MinValue);

    public static int DiasAtraso(DateTime vencimento, DateOnly hoje) => Math.Max(0, hoje.DayNumber - DateOnly.FromDateTime(vencimento).DayNumber);
}

internal sealed class ConsultasComercial(FabricaConexao conexoes) : IConsultasComercial
{
    private const string SelectOrcamento = """
        SELECT o.Id, o.ClienteId, c.Nome AS Cliente, t.Nome AS TipoEvento, p.Nome AS Pacote, es.Nome AS Espaco, o.Inicio,
               o.QuantidadeConvidados, o.ValorTotal, o.Status, o.ValidoAte, ev.Id AS EventoId, o.CriadoEm,
               u.Nome AS Vendedor, ue.EnviadoEm AS UltimoEnvio, ue.Canal AS CanalUltimoEnvio
          FROM Orcamentos o
          JOIN Clientes c ON c.Id = o.ClienteId
          JOIN TiposEvento t ON t.Id = o.TipoEventoId
          LEFT JOIN Pacotes p ON p.Id = o.PacoteId
          LEFT JOIN Espacos es ON es.Id = o.EspacoId
          LEFT JOIN Eventos ev ON ev.OrcamentoId = o.Id
          LEFT JOIN Usuarios u ON u.Id = o.VendedorId
          LEFT JOIN (SELECT x.OrcamentoId, x.EnviadoEm, x.Canal,
                            ROW_NUMBER() OVER (PARTITION BY x.OrcamentoId ORDER BY x.EnviadoEm DESC) AS Ordem
                       FROM EnviosProposta x) ue ON ue.OrcamentoId = o.Id AND ue.Ordem = 1
        """;

    public async Task<IReadOnlyList<OrcamentoResumo>> ListarOrcamentosAsync(FiltroOrcamentos filtro, CancellationToken ct = default)
    {
        const string sql = SelectOrcamento + """
             WHERE (@De IS NULL OR o.Inicio >= @De)
               AND (@AteExclusivo IS NULL OR o.Inicio < @AteExclusivo)
               AND (@Status IS NULL OR o.Status = @Status)
               AND (@Texto IS NULL OR UPPER(c.Nome) LIKE @Texto ESCAPE '!' OR o.Id = @CodigoId)
             ORDER BY o.Inicio DESC
            """;

        return await conexoes.ListarAsync<OrcamentoResumo>(sql, new
        {
            filtro.De,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString(),
            Texto = Filtros.Like(filtro.Texto),
            CodigoId = Filtros.Codigo(filtro.Texto, "ORC")
        }, ct);
    }

    public async Task<HistoricoCliente> HistoricoClienteAsync(int clienteId, CancellationToken ct = default)
    {
        const string resumo = """
            SELECT COUNT(*) AS QuantidadeEventos,
                   COALESCE(SUM(e.ValorTotal), 0) AS TotalContratado,
                   COALESCE((SELECT SUM(p.ValorPago) FROM Parcelas p JOIN Eventos x ON x.Id = p.EventoId
                              WHERE x.ClienteId = @ClienteId AND p.Status = 'Pago'), 0) AS TotalRecebido,
                   COALESCE((SELECT SUM(p.Valor) FROM Parcelas p JOIN Eventos x ON x.Id = p.EventoId
                              WHERE x.ClienteId = @ClienteId AND p.Status = 'Aberto'), 0) AS SaldoEmAberto,
                   MAX(e.Inicio) AS UltimoEvento
              FROM Eventos e
             WHERE e.ClienteId = @ClienteId AND e.Status <> 'Cancelado'
            """;
        const string orcamentos = SelectOrcamento + " WHERE o.ClienteId = @ClienteId ORDER BY o.Inicio DESC";
        const string eventos = ConsultasEventos.SelectEvento + " WHERE e.ClienteId = @ClienteId ORDER BY e.Inicio DESC";

        await using var lote = await conexoes.LoteAsync([resumo, orcamentos, eventos], new { ClienteId = clienteId }, ct);
        var historico = await lote.LerUnicoAsync<HistoricoCliente>();
        return historico with
        {
            Orcamentos = await lote.LerAsync<OrcamentoResumo>(),
            Eventos = await lote.LerAsync<EventoResumo>()
        };
    }

    public async Task<IReadOnlyList<DegustacaoResumo>> ListarDegustacoesAsync(FiltroDegustacoes filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.Id, d.DataHora, d.ClienteId, c.Nome AS Cliente, COALESCE(c.Celular, c.Telefone, c.Email) AS Contato,
                   d.Pessoas, p.Nome AS Pacote, d.OrcamentoId, d.Status, d.Avaliacao, d.Comentario, d.Observacoes,
                   CASE WHEN EXISTS (SELECT 1 FROM Orcamentos o
                                      WHERE o.ClienteId = d.ClienteId AND o.Status = 'Aprovado' AND o.DecididoEm >= d.DataHora)
                        THEN 1 ELSE 0 END AS Convertida
              FROM Degustacoes d
              JOIN Clientes c ON c.Id = d.ClienteId
              LEFT JOIN Pacotes p ON p.Id = d.PacoteId
             WHERE (@De IS NULL OR d.DataHora >= @De)
               AND (@AteExclusivo IS NULL OR d.DataHora < @AteExclusivo)
               AND (@Status IS NULL OR d.Status = @Status)
             ORDER BY d.DataHora DESC
            """;

        return await conexoes.ListarAsync<DegustacaoResumo>(sql, new
        {
            filtro.De,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString()
        }, ct);
    }

    public async Task<IReadOnlyList<ContratoResumo>> ListarContratosAsync(StatusContrato? status, CancellationToken ct = default)
    {
        const string sql = """
            SELECT k.Id, k.EventoId, e.Titulo AS Evento, c.Nome AS Cliente, e.Inicio AS DataEvento, k.Status,
                   k.EmitidoEm, k.AssinadoEm, k.NomeSignatario, k.MotivoCancelamento
              FROM Contratos k
              JOIN Eventos e ON e.Id = k.EventoId
              JOIN Clientes c ON c.Id = e.ClienteId
             WHERE @Status IS NULL OR k.Status = @Status
             ORDER BY k.EmitidoEm DESC
            """;

        return await conexoes.ListarAsync<ContratoResumo>(sql, new { Status = status?.ToString() }, ct);
    }
}

internal sealed class ConsultasEventos(FabricaConexao conexoes) : IConsultasEventos
{
    internal const string SelectEvento = """
        SELECT e.Id, e.Titulo, c.Nome AS Cliente, t.Nome AS TipoEvento, es.Nome AS Espaco, e.Inicio,
               e.QuantidadeConvidados, e.ValorTotal, e.Status,
               COALESCE(pg.Recebido, 0) AS Recebido,
               CASE WHEN e.Status = 'Cancelado' THEN 0 ELSE e.ValorTotal - COALESCE(pg.Quitado, 0) END AS SaldoAReceber,
               ct.Status AS Contrato
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN TiposEvento t ON t.Id = e.TipoEventoId
          JOIN Espacos es ON es.Id = e.EspacoId
          LEFT JOIN (SELECT p.EventoId, SUM(p.ValorPago) AS Recebido, SUM(p.Valor) AS Quitado
                       FROM Parcelas p WHERE p.Status = 'Pago' GROUP BY p.EventoId) pg ON pg.EventoId = e.Id
          LEFT JOIN Contratos ct ON ct.EventoId = e.Id AND ct.Status <> 'Cancelado'
        """;

    public async Task<IReadOnlyList<EventoResumo>> ListarAsync(FiltroEventos filtro, CancellationToken ct = default)
    {
        const string sql = SelectEvento + """
             WHERE (@De IS NULL OR e.Inicio >= @De)
               AND (@AteExclusivo IS NULL OR e.Inicio < @AteExclusivo)
               AND (@Status IS NULL OR e.Status = @Status)
               AND (@EspacoId IS NULL OR e.EspacoId = @EspacoId)
               AND (@Texto IS NULL OR UPPER(e.Titulo) LIKE @Texto ESCAPE '!' OR UPPER(c.Nome) LIKE @Texto ESCAPE '!'
                    OR e.Id = @CodigoId)
             ORDER BY e.Inicio DESC
            """;

        return await conexoes.ListarAsync<EventoResumo>(sql, new
        {
            filtro.De,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString(),
            filtro.EspacoId,
            Texto = Filtros.Like(filtro.Texto),
            CodigoId = Filtros.Codigo(filtro.Texto, "EVT")
        }, ct);
    }
}

internal sealed class ConsultasFinanceiro(FabricaConexao conexoes) : IConsultasFinanceiro
{
    public async Task<IReadOnlyList<ParcelaResumo>> ListarParcelasAsync(FiltroTitulos filtro, DateOnly hoje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id, p.EventoId, e.Titulo AS EventoTitulo, e.ClienteId, c.Nome AS Cliente, p.Numero, p.Descricao, p.Vencimento,
                   p.Valor, p.Status, p.DataPagamento, p.ValorPago, f.Nome AS FormaPagamento
              FROM Parcelas p
              JOIN Eventos e ON e.Id = p.EventoId
              JOIN Clientes c ON c.Id = e.ClienteId
              LEFT JOIN FormasPagamento f ON f.Id = p.FormaPagamentoId
             WHERE (@VencimentoDe IS NULL OR p.Vencimento >= @VencimentoDe)
               AND (@VencimentoAte IS NULL OR p.Vencimento <= @VencimentoAte)
               AND (@Status IS NULL OR p.Status = @Status)
               AND (@SomenteVencidos = 0 OR (p.Status = 'Aberto' AND p.Vencimento < @Hoje))
               AND (@Texto IS NULL OR UPPER(c.Nome) LIKE @Texto ESCAPE '!' OR UPPER(e.Titulo) LIKE @Texto ESCAPE '!')
             ORDER BY p.Vencimento, p.EventoId, p.Numero
            """;

        var itens = await conexoes.ListarAsync<ParcelaResumo>(sql, Parametros(filtro, hoje), ct);
        return itens.Select(p => p.Status == Domain.Financeiro.StatusTitulo.Aberto
            ? p with { DiasAtraso = Filtros.DiasAtraso(p.Vencimento, hoje) }
            : p).ToList();
    }

    public async Task<IReadOnlyList<ContaPagarResumo>> ListarContasPagarAsync(FiltroTitulos filtro, DateOnly hoje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT cp.Id, cp.Descricao, cp.Categoria, cp.FornecedorId, COALESCE(fo.NomeFantasia, fo.RazaoSocial) AS Fornecedor,
                   cp.EventoId, e.Titulo AS Evento,
                   cp.Vencimento, cp.Valor, cp.Status, cp.DataPagamento, cp.ValorPago, f.Nome AS FormaPagamento
              FROM ContasPagar cp
              LEFT JOIN Fornecedores fo ON fo.Id = cp.FornecedorId
              LEFT JOIN Eventos e ON e.Id = cp.EventoId
              LEFT JOIN FormasPagamento f ON f.Id = cp.FormaPagamentoId
             WHERE (@VencimentoDe IS NULL OR cp.Vencimento >= @VencimentoDe)
               AND (@VencimentoAte IS NULL OR cp.Vencimento <= @VencimentoAte)
               AND (@Status IS NULL OR cp.Status = @Status)
               AND (@SomenteVencidos = 0 OR (cp.Status = 'Aberto' AND cp.Vencimento < @Hoje))
               AND (@Texto IS NULL OR UPPER(cp.Descricao) LIKE @Texto ESCAPE '!' OR UPPER(fo.RazaoSocial) LIKE @Texto ESCAPE '!'
                    OR UPPER(fo.NomeFantasia) LIKE @Texto ESCAPE '!')
             ORDER BY cp.Vencimento, cp.Id
            """;

        var itens = await conexoes.ListarAsync<ContaPagarResumo>(sql, Parametros(filtro, hoje), ct);
        return itens.Select(c => c with
        {
            Evento = c.EventoId is { } id ? $"EVT-{id:D5} - {c.Evento}" : null,
            DiasAtraso = c.Status == Domain.Financeiro.StatusTitulo.Aberto ? Filtros.DiasAtraso(c.Vencimento, hoje) : 0
        }).ToList();
    }

    private static object Parametros(FiltroTitulos filtro, DateOnly hoje) => new
    {
        filtro.VencimentoDe,
        filtro.VencimentoAte,
        Status = filtro.Status?.ToString(),
        SomenteVencidos = filtro.SomenteVencidos ? 1 : 0,
        Texto = Filtros.Like(filtro.Texto),
        Hoje = Filtros.Data(hoje)
    };
}

internal sealed class ConsultasEstoqueCardapio(FabricaConexao conexoes) : IConsultasEstoqueCardapio
{
    public async Task<IReadOnlyList<DisponibilidadeMaterial>> DisponibilidadeMateriaisAsync(DateOnly data, int? ignorarEventoId,
        CancellationToken ct = default)
    {
        // Comprometido: eventos do dia anterior ao dia seguinte cujo material ainda não voltou.
        const string sql = """
            SELECT m.Id, m.Nome, m.Categoria, m.QuantidadePropria, m.CustoLocacao,
                   COALESCE((SELECT SUM(em.Quantidade - em.QuantidadeAlugada)
                               FROM EventoMateriais em
                               JOIN Eventos e ON e.Id = em.EventoId
                              WHERE em.MaterialId = m.Id AND em.DevolvidoEm IS NULL AND e.Status <> 'Cancelado'
                                AND (@Ignorar IS NULL OR e.Id <> @Ignorar)
                                AND e.Inicio >= @Inicio AND e.Inicio < @Fim), 0) AS Reservada
              FROM Materiais m
             WHERE m.Ativo = {verdadeiro}
             ORDER BY m.Categoria, m.Nome
            """;

        return await conexoes.ListarAsync<DisponibilidadeMaterial>(sql, new
        {
            Inicio = Filtros.Data(data.AddDays(-1)),
            Fim = Filtros.Data(data.AddDays(2)),
            Ignorar = ignorarEventoId
        }, ct);
    }

    public async Task<IReadOnlyList<MovimentacaoResumo>> ListarMovimentacoesAsync(DateTime de, DateTime ate, int? produtoId = null,
        TipoMovimentacao? tipo = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT m.Id, m.Data, m.ProdutoId, p.Nome AS Produto, u.Sigla AS Unidade, m.Tipo, m.Quantidade,
                   m.CustoUnitario, m.SaldoApos, m.Observacao, m.EventoId,
                   COALESCE(f.NomeFantasia, f.RazaoSocial) AS Fornecedor, m.UnidadeCompra, m.QuantidadeCompra
              FROM MovimentacoesEstoque m
              JOIN Produtos p ON p.Id = m.ProdutoId
              JOIN UnidadesMedida u ON u.Id = p.UnidadeMedidaId
              LEFT JOIN Fornecedores f ON f.Id = m.FornecedorId
             WHERE m.Data >= @De AND m.Data < @AteExclusivo
               AND (@ProdutoId IS NULL OR m.ProdutoId = @ProdutoId)
               AND (@Tipo IS NULL OR m.Tipo = @Tipo)
             ORDER BY m.Data DESC, m.Id DESC
            """;

        return await conexoes.ListarAsync<MovimentacaoResumo>(sql, new
        {
            De = de.Date,
            AteExclusivo = Filtros.FimDoDia(ate),
            ProdutoId = produtoId,
            Tipo = tipo?.ToString()
        }, ct);
    }

    public async Task<IReadOnlyList<ItemCardapioResumo>> ListarItensCardapioAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT i.Id, i.Nome, c.Nome AS Categoria, c.Ordem AS OrdemCategoria, i.Ativo,
                   COUNT(f.ProdutoId) AS QuantidadeIngredientes,
                   CAST(COALESCE(SUM(f.QuantidadePorPessoa * p.CustoMedio), 0) AS DECIMAL(18, 4)) AS CustoPorPessoa
              FROM ItensCardapio i
              JOIN CategoriasCardapio c ON c.Id = i.CategoriaCardapioId
              LEFT JOIN FichasTecnicas f ON f.ItemCardapioId = i.Id
              LEFT JOIN Produtos p ON p.Id = f.ProdutoId
             GROUP BY i.Id, i.Nome, c.Nome, c.Ordem, i.Ativo
             ORDER BY c.Ordem, i.Nome
            """;

        return await conexoes.ListarAsync<ItemCardapioResumo>(sql, ct: ct);
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
                   CAST(COALESCE(SUM(ci.Custo), 0) AS DECIMAL(18, 4)) AS CustoPorPessoa
              FROM Pacotes pc
              LEFT JOIN PacoteItens pi ON pi.PacoteId = pc.Id
              LEFT JOIN CustoItem ci ON ci.ItemCardapioId = pi.ItemCardapioId
             GROUP BY pc.Id, pc.Nome, pc.PrecoPorPessoa, pc.MinimoConvidados, pc.Ativo
             ORDER BY pc.PrecoPorPessoa
            """;

        return await conexoes.ListarAsync<PacoteResumo>(sql, ct: ct);
    }

    public async Task<IReadOnlyList<ItemComposicao>> ComposicaoPacoteAsync(int pacoteId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.Nome AS Categoria, i.Nome AS Item, i.Descricao
              FROM PacoteItens pi
              JOIN ItensCardapio i ON i.Id = pi.ItemCardapioId
              JOIN CategoriasCardapio c ON c.Id = i.CategoriaCardapioId
             WHERE pi.PacoteId = @PacoteId
             ORDER BY c.Ordem, i.Nome
            """;

        return await conexoes.ListarAsync<ItemComposicao>(sql, new { PacoteId = pacoteId }, ct);
    }
}
