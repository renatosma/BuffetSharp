using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasCompras(FabricaConexao conexoes) : IConsultasCompras
{
    public async Task<IReadOnlyList<CotacaoResumo>> ListarCotacoesAsync(FiltroCotacoes filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.Id, c.Data, c.Descricao, c.PrazoResposta, c.EventoId, e.Titulo AS Evento, c.Status, c.Observacoes,
                   c.MotivoCancelamento,
                   (SELECT COUNT(*) FROM CotacaoItens i WHERE i.CotacaoCompraId = c.Id) AS Itens,
                   (SELECT COUNT(*) FROM CotacaoParticipantes pa WHERE pa.CotacaoCompraId = c.Id) AS Participantes,
                   (SELECT COUNT(*) FROM CotacaoPrecos pr WHERE pr.CotacaoCompraId = c.Id) AS Precos,
                   COALESCE((SELECT SUM(ROUND(i.Quantidade * pr.PrecoUnitario, 2)) FROM CotacaoItens i
                               JOIN CotacaoPrecos pr ON pr.CotacaoItemId = i.Id AND pr.CotacaoParticipanteId = i.VencedorId
                              WHERE i.CotacaoCompraId = c.Id), 0) AS TotalVencedores,
                   (SELECT COUNT(*) FROM CotacaoItens i WHERE i.CotacaoCompraId = c.Id AND i.EscolhaManual = {verdadeiro}) AS EscolhasManuais,
                   (SELECT COUNT(*) FROM PedidosCompra p WHERE p.CotacaoCompraId = c.Id) AS Pedidos
              FROM CotacoesCompra c
              LEFT JOIN Eventos e ON e.Id = c.EventoId
             WHERE (@De IS NULL OR c.Data >= @De)
               AND (@AteExclusivo IS NULL OR c.Data < @AteExclusivo)
               AND (@Status IS NULL OR c.Status = @Status)
             ORDER BY c.Data DESC, c.Id DESC
            """;
        return await conexoes.ListarAsync<CotacaoResumo>(sql, new
        {
            De = filtro.De?.Date,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString()
        }, ct);
    }

    public async Task<IReadOnlyList<PedidoCompraResumo>> ListarPedidosAsync(FiltroPedidosCompra filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id, p.Data, p.PrevisaoEntrega, p.FornecedorId, COALESCE(f.NomeFantasia, f.RazaoSocial) AS Fornecedor,
                   f.Telefone AS TelefoneFornecedor, f.Email AS EmailFornecedor, p.Status, p.CotacaoCompraId,
                   p.Observacoes, p.MotivoCancelamento,
                   (SELECT COUNT(*) FROM PedidoCompraItens i WHERE i.PedidoCompraId = p.Id) AS Itens,
                   COALESCE((SELECT SUM(ROUND(i.Quantidade * i.CustoUnitario, 2)) FROM PedidoCompraItens i
                              WHERE i.PedidoCompraId = p.Id), 0) AS Total,
                   COALESCE((SELECT SUM(ROUND(i.QuantidadeRecebida * i.CustoUnitario, 2)) FROM PedidoCompraItens i
                              WHERE i.PedidoCompraId = p.Id), 0) AS Recebido,
                   (SELECT COUNT(*) FROM RecebimentosCompra r WHERE r.PedidoCompraId = p.Id AND r.Status = 'AFaturar') AS AFaturar
              FROM PedidosCompra p
              JOIN Fornecedores f ON f.Id = p.FornecedorId
             WHERE (@De IS NULL OR p.Data >= @De)
               AND (@AteExclusivo IS NULL OR p.Data < @AteExclusivo)
               AND (@Status IS NULL OR p.Status = @Status)
             ORDER BY p.Data DESC, p.Id DESC
            """;
        return await conexoes.ListarAsync<PedidoCompraResumo>(sql, new
        {
            De = filtro.De?.Date,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString()
        }, ct);
    }

    public async Task<IReadOnlyList<PedidoCompraItemResumo>> ItensAsync(int pedidoId, CancellationToken ct = default) =>
        await conexoes.ListarAsync<PedidoCompraItemResumo>("""
            SELECT i.Id, pr.Nome AS Produto, i.Quantidade, i.QuantidadeRecebida, COALESCE(ue.Sigla, u.Sigla) AS Unidade, i.CustoUnitario
              FROM PedidoCompraItens i
              JOIN Produtos pr ON pr.Id = i.ProdutoId
              JOIN UnidadesMedida u ON u.Id = pr.UnidadeMedidaId
              LEFT JOIN ProdutoEmbalagens e ON e.Id = i.ProdutoEmbalagemId
              LEFT JOIN UnidadesMedida ue ON ue.Id = e.UnidadeMedidaId
             WHERE i.PedidoCompraId = @PedidoId
             ORDER BY i.Id
            """, new { PedidoId = pedidoId }, ct);

    public async Task<IReadOnlyList<RecebimentoResumo>> ListarRecebimentosAsync(FiltroRecebimentos filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.Id, r.Data, r.PedidoCompraId, r.FornecedorId, COALESCE(f.NomeFantasia, f.RazaoSocial) AS Fornecedor, r.Status,
                   r.NotaFiscalEntradaId, n.Numero AS NotaFiscal, r.Usuario, r.Observacoes, r.MotivoEstorno,
                   (SELECT COUNT(*) FROM RecebimentoItens i WHERE i.RecebimentoCompraId = r.Id) AS Itens,
                   COALESCE((SELECT SUM(ROUND(i.Quantidade * i.CustoUnitario, 2)) FROM RecebimentoItens i
                              WHERE i.RecebimentoCompraId = r.Id), 0) AS Total
              FROM RecebimentosCompra r
              JOIN Fornecedores f ON f.Id = r.FornecedorId
              LEFT JOIN NotasFiscaisEntrada n ON n.Id = r.NotaFiscalEntradaId
             WHERE (@De IS NULL OR r.Data >= @De)
               AND (@AteExclusivo IS NULL OR r.Data < @AteExclusivo)
               AND (@Status IS NULL OR r.Status = @Status)
               AND (@FornecedorId IS NULL OR r.FornecedorId = @FornecedorId)
             ORDER BY r.Data DESC, r.Id DESC
            """;
        return await conexoes.ListarAsync<RecebimentoResumo>(sql, new
        {
            De = filtro.De?.Date,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString(),
            filtro.FornecedorId
        }, ct);
    }

    public async Task<IReadOnlyList<RecebimentoItemResumo>> ItensRecebimentoAsync(int recebimentoId, CancellationToken ct = default) =>
        await conexoes.ListarAsync<RecebimentoItemResumo>("""
            SELECT pr.Nome AS Produto, i.Quantidade, COALESCE(ue.Sigla, u.Sigla) AS Unidade, i.CustoUnitario
              FROM RecebimentoItens i
              JOIN PedidoCompraItens pi ON pi.Id = i.PedidoCompraItemId
              JOIN Produtos pr ON pr.Id = pi.ProdutoId
              JOIN UnidadesMedida u ON u.Id = pr.UnidadeMedidaId
              LEFT JOIN ProdutoEmbalagens e ON e.Id = pi.ProdutoEmbalagemId
              LEFT JOIN UnidadesMedida ue ON ue.Id = e.UnidadeMedidaId
             WHERE i.RecebimentoCompraId = @RecebimentoId
             ORDER BY i.Id
            """, new { RecebimentoId = recebimentoId }, ct);

    public async Task<IReadOnlyList<NotaEntradaResumo>> ListarNotasAsync(FiltroNotasEntrada filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT n.Id, n.Numero, n.Serie, n.DataEmissao, n.FornecedorId, COALESCE(f.NomeFantasia, f.RazaoSocial) AS Fornecedor,
                   n.Status, n.ValorProdutos, n.ValorTotal, n.Observacoes, n.MotivoCancelamento,
                   (SELECT COUNT(*) FROM RecebimentosCompra r WHERE r.NotaFiscalEntradaId = n.Id) AS Recebimentos,
                   (SELECT COUNT(*) FROM ContasPagar c
                     WHERE c.Id = n.ContaPagarId OR c.ParcelamentoId = n.ParcelamentoPagarId) AS Contas,
                   (SELECT COUNT(*) FROM ContasPagar c
                     WHERE (c.Id = n.ContaPagarId OR c.ParcelamentoId = n.ParcelamentoPagarId) AND c.Status = 'Pago') AS ContasPagas
              FROM NotasFiscaisEntrada n
              JOIN Fornecedores f ON f.Id = n.FornecedorId
             WHERE (@De IS NULL OR n.DataEmissao >= @De)
               AND (@AteExclusivo IS NULL OR n.DataEmissao < @AteExclusivo)
               AND (@Status IS NULL OR n.Status = @Status)
             ORDER BY n.DataEmissao DESC, n.Id DESC
            """;
        return await conexoes.ListarAsync<NotaEntradaResumo>(sql, new
        {
            De = filtro.De?.Date,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString()
        }, ct);
    }
}
