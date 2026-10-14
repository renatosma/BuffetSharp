using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;
using Dapper;

namespace BuffetSharp.Infrastructure.Consultas;

/// <summary>
/// Painel inicial em uma única ida ao banco (QueryMultiple com 5 conjuntos de resultados).
/// </summary>
internal sealed class ConsultasDashboard(FabricaConexao conexoes) : IConsultasDashboard
{
    private const string Sql = """
        DECLARE @IniMes date = DATEFROMPARTS(YEAR(@Hoje), MONTH(@Hoje), 1);
        DECLARE @FimMes date = DATEADD(MONTH, 1, @IniMes);

        -- 1) Indicadores
        SELECT
            (SELECT COUNT(*) FROM Eventos
              WHERE Status <> 'Cancelado' AND Inicio >= @IniMes AND Inicio < @FimMes)              AS EventosNoMes,
            (SELECT COALESCE(SUM(ValorTotal), 0) FROM Eventos
              WHERE Status <> 'Cancelado' AND Inicio >= @IniMes AND Inicio < @FimMes)              AS FaturamentoPrevistoMes,
            (SELECT COALESCE(SUM(ValorPago), 0) FROM Parcelas
              WHERE Status = 'Pago' AND DataPagamento >= @IniMes AND DataPagamento < @FimMes)      AS RecebidoNoMes,
            (SELECT COALESCE(SUM(Valor), 0) FROM Parcelas
              WHERE Status = 'Aberto' AND Vencimento < @Hoje)                                       AS AReceberVencido,
            (SELECT COALESCE(SUM(Valor), 0) FROM ContasPagar
              WHERE Status = 'Aberto' AND Vencimento <= DATEADD(DAY, 7, @Hoje))                     AS APagarProximos7Dias,
            (SELECT COUNT(*) FROM Orcamentos
              WHERE Status IN ('Rascunho', 'Enviado') AND ValidoAte >= @Hoje)                       AS OrcamentosEmAberto,
            (SELECT COALESCE(SUM(ValorTotal), 0) FROM Orcamentos
              WHERE Status IN ('Rascunho', 'Enviado') AND ValidoAte >= @Hoje)                       AS ValorOrcamentosEmAberto,
            (SELECT CASE WHEN COUNT(*) = 0 THEN 0
                         ELSE CAST(SUM(CASE WHEN Status = 'Aprovado' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS decimal(5, 1)) END
               FROM Orcamentos
              WHERE Status IN ('Aprovado', 'Recusado') AND DecididoEm >= DATEADD(DAY, -90, @Hoje)) AS TaxaConversao90Dias,
            (SELECT COUNT(*) FROM Produtos WHERE Ativo = 1 AND EstoqueAtual < EstoqueMinimo)        AS ProdutosAbaixoMinimo;

        -- 2) Próximos eventos
        SELECT TOP (10) e.Id, e.Titulo, c.Nome AS Cliente, es.Nome AS Espaco, e.Inicio, e.QuantidadeConvidados, e.ValorTotal,
               e.ValorTotal - COALESCE(pg.Pago, 0) AS SaldoAReceber,
               (SELECT COUNT(*) FROM EventoEquipe q WHERE q.EventoId = e.Id) AS EquipeEscalada
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN Espacos es ON es.Id = e.EspacoId
          OUTER APPLY (SELECT SUM(p.Valor) AS Pago FROM Parcelas p WHERE p.EventoId = e.Id AND p.Status = 'Pago') pg
         WHERE e.Status = 'Confirmado' AND e.Inicio >= @Hoje
         ORDER BY e.Inicio;

        -- 3) Faturamento mensal: 8 meses para trás e 3 para frente (buffets vendem com antecedência)
        WITH Meses AS (
            SELECT DATEADD(MONTH, v.n, @IniMes) AS Inicio
              FROM (VALUES (-8), (-7), (-6), (-5), (-4), (-3), (-2), (-1), (0), (1), (2), (3)) v(n)
        )
        SELECT YEAR(m.Inicio) AS Ano, MONTH(m.Inicio) AS Mes,
               COALESCE((SELECT SUM(e.ValorTotal) FROM Eventos e
                          WHERE e.Status <> 'Cancelado' AND e.Inicio >= m.Inicio AND e.Inicio < DATEADD(MONTH, 1, m.Inicio)), 0) AS Valor,
               COALESCE((SELECT SUM(p.ValorPago) FROM Parcelas p
                          WHERE p.Status = 'Pago' AND p.DataPagamento >= m.Inicio AND p.DataPagamento < DATEADD(MONTH, 1, m.Inicio)), 0) AS Valor2
          FROM Meses m
         ORDER BY m.Inicio;

        -- 4) Parcelas vencidas
        SELECT TOP (10) p.Id, p.EventoId, CONCAT(p.Descricao, ' - ', e.Titulo) AS Descricao, c.Nome AS Cliente,
               p.Vencimento, p.Valor, DATEDIFF(DAY, p.Vencimento, @Hoje) AS DiasAtraso
          FROM Parcelas p
          JOIN Eventos e ON e.Id = p.EventoId
          JOIN Clientes c ON c.Id = e.ClienteId
         WHERE p.Status = 'Aberto' AND p.Vencimento < @Hoje
         ORDER BY p.Vencimento;

        -- 5) Estoque abaixo do mínimo
        SELECT TOP (10) p.Id, p.Nome, u.Sigla AS Unidade, p.EstoqueAtual, p.EstoqueMinimo
          FROM Produtos p
          JOIN UnidadesMedida u ON u.Id = p.UnidadeMedidaId
         WHERE p.Ativo = 1 AND p.EstoqueAtual < p.EstoqueMinimo
         ORDER BY p.EstoqueAtual / NULLIF(p.EstoqueMinimo, 0);
        """;

    public async Task<ResumoDashboard> ObterAsync(DateOnly hoje, CancellationToken ct = default)
    {
        await using var conexao = conexoes.Criar();
        await using var multi = await conexao.QueryMultipleAsync(
            new CommandDefinition(Sql, new { Hoje = hoje.ToDateTime(TimeOnly.MinValue) }, cancellationToken: ct));

        var resumo = await multi.ReadSingleAsync<ResumoDashboard>();
        resumo.ProximosEventos = (await multi.ReadAsync<ProximoEvento>()).AsList();
        resumo.FaturamentoMensal = (await multi.ReadAsync<ValorMensal>()).AsList();
        resumo.ParcelasVencidas = (await multi.ReadAsync<TituloVencido>()).AsList();
        resumo.ProdutosEmAlerta = (await multi.ReadAsync<ProdutoAlerta>()).AsList();
        return resumo;
    }
}
