using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasCompras(FabricaConexao conexoes) : IConsultasCompras
{
    public async Task<IReadOnlyList<PedidoCompraResumo>> ListarPedidosAsync(FiltroPedidosCompra filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id, p.Data, p.PrevisaoEntrega, p.FornecedorId, COALESCE(f.NomeFantasia, f.RazaoSocial) AS Fornecedor,
                   f.Telefone AS TelefoneFornecedor, f.Email AS EmailFornecedor, p.Status, p.NotaFiscal, p.DataCompra,
                   p.Observacoes, p.MotivoCancelamento,
                   (SELECT COUNT(*) FROM PedidoCompraItens i WHERE i.PedidoCompraId = p.Id) AS Itens,
                   COALESCE((SELECT SUM(ROUND(i.Quantidade * i.CustoUnitario, 2)) FROM PedidoCompraItens i
                              WHERE i.PedidoCompraId = p.Id), 0) AS Total,
                   (SELECT COUNT(*) FROM ContasPagar c
                     WHERE c.Id = p.ContaPagarId OR c.ParcelamentoId = p.ParcelamentoPagarId) AS Contas,
                   (SELECT COUNT(*) FROM ContasPagar c
                     WHERE (c.Id = p.ContaPagarId OR c.ParcelamentoId = p.ParcelamentoPagarId) AND c.Status = 'Pago') AS ContasPagas
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
            SELECT pr.Nome AS Produto, i.Quantidade, COALESCE(ue.Sigla, u.Sigla) AS Unidade, i.CustoUnitario
              FROM PedidoCompraItens i
              JOIN Produtos pr ON pr.Id = i.ProdutoId
              JOIN UnidadesMedida u ON u.Id = pr.UnidadeMedidaId
              LEFT JOIN ProdutoEmbalagens e ON e.Id = i.ProdutoEmbalagemId
              LEFT JOIN UnidadesMedida ue ON ue.Id = e.UnidadeMedidaId
             WHERE i.PedidoCompraId = @PedidoId
             ORDER BY i.Id
            """, new { PedidoId = pedidoId }, ct);
}
