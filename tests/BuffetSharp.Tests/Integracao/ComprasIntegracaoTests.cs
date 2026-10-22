using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Pedido de compra: vários itens numa nota, recebidos de uma vez com os vencimentos.</summary>
[Collection(ColecaoBanco.Nome)]
public class ComprasIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool> filtro) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro);

    private async Task<decimal> SaldoAsync(int produtoId) =>
        (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produtoId, Ct))!.EstoqueAtual;

    [Fact]
    public async Task Nota_com_varios_itens_entra_no_estoque_e_vira_parcelas_com_os_vencimentos_informados()
    {
        var compras = banco.Obter<CompraService>();
        var refrigerante = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Refrigerante 2L")).Id;
        var fardo = (await banco.Obter<EstoqueService>().ListarEmbalagensAsync(refrigerante, Ct)).Single(e => e.Fator == 6);
        var outro = (await Lookup(TipoLookup.Produtos, p => p.Id != refrigerante)).Id;
        var saldoRefrigerante = await SaldoAsync(refrigerante);
        var saldoOutro = await SaldoAsync(outro);

        var produtos = await compras.ProdutosAsync(fardo.FornecedorId!.Value, Ct);
        Assert.Contains(produtos.Single(p => p.Id == refrigerante).Unidades, u => u.Id == fardo.Id && u.Fator == 6);

        var pedido = compras.Novo(fardo.FornecedorId.Value);
        pedido.Itens =
        [
            new PedidoCompraItemInput { ProdutoId = refrigerante, ProdutoEmbalagemId = fardo.Id, Quantidade = 4, CustoUnitario = 51m },
            new PedidoCompraItemInput { ProdutoId = outro, Quantidade = 2.5m, CustoUnitario = 10m }
        ];
        var id = await compras.SalvarAsync(pedido, Ct);
        Assert.Equal(229m, (await compras.ObterAsync(id, Ct)).Total);

        var nota = $"NF{Guid.NewGuid():N}"[..14];
        await compras.ReceberAsync(new RecebimentoPedidoInput
        {
            PedidoId = id, NotaFiscal = nota, DataCompra = Hoje,
            Vencimentos = [new() { Vencimento = Hoje.AddDays(28), Valor = 129m }, new() { Vencimento = Hoje.AddDays(42), Valor = 100m }]
        }, Ct);

        Assert.Equal(saldoRefrigerante + 24, await SaldoAsync(refrigerante)); // 4 fardos de 6
        Assert.Equal(saldoOutro + 2.5m, await SaldoAsync(outro));
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var parcelas = await db.ContasPagar.Where(c => c.Documento == nota).OrderBy(c => c.Vencimento).ToListAsync(Ct);
            Assert.Equal([129m, 100m], parcelas.Select(p => p.Valor));
            Assert.Equal([Hoje.AddDays(28), Hoje.AddDays(42)], parcelas.Select(p => p.Vencimento));
            Assert.All(parcelas, p => Assert.Equal(fardo.FornecedorId, p.FornecedorId));
            Assert.Contains("Recebimento do pedido de compra", (await banco.Obter<FinanceiroService>().ObterContaAsync(parcelas[0].Id, Ct)).OrigemEstoque);
        }

        var resumo = (await banco.Obter<IConsultasCompras>().ListarPedidosAsync(new FiltroPedidosCompra(), Ct)).Single(p => p.Id == id);
        Assert.Equal(StatusPedidoCompra.Recebido, resumo.Status);
        Assert.Equal("0 de 2 paga(s)", resumo.Pagamento);
        Assert.Equal(229m, resumo.Total);

        // A entrada da nota não é estornada sozinha: o estorno é pelo pedido.
        var kardex = await banco.Obter<IConsultasEstoqueCardapio>().ListarMovimentacoesAsync(DateTime.Today, DateTime.Today, refrigerante, ct: Ct);
        var entrada = kardex.First(m => m.PedidoCompraId == id);
        Assert.Equal($"Pedido PC-{id:D5}", entrada.ContaPagar);
        await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<EstoqueService>().EstornarEntradaAsync(entrada.Id, "Engano", Ct));
        var recebido = await compras.ObterAsync(id, Ct);
        var alterar = await Assert.ThrowsAsync<RegraNegocioException>(() => compras.SalvarAsync(recebido, Ct));
        Assert.Contains("estorne o recebimento", alterar.Message);

        // Estornado, o pedido volta a ficar aberto e pode ser recebido de novo (agora com vencimento único).
        await compras.EstornarRecebimentoAsync(id, "Nota lançada com valor errado", Ct);
        Assert.Equal(saldoRefrigerante, await SaldoAsync(refrigerante));
        Assert.Equal(StatusPedidoCompra.Aberto, (await compras.ObterAsync(id, Ct)).Status);
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            Assert.All(await db.ContasPagar.Where(c => c.Documento == nota).ToListAsync(Ct), c => Assert.Equal(StatusTitulo.Cancelado, c.Status));

        await compras.ReceberAsync(new RecebimentoPedidoInput
        {
            PedidoId = id, NotaFiscal = nota + "B", DataCompra = Hoje, Vencimentos = [new() { Vencimento = Hoje.AddDays(30), Valor = 229m }]
        }, Ct);
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            Assert.Equal(229m, (await db.ContasPagar.SingleAsync(c => c.Documento == nota + "B", Ct)).Valor);
    }

    [Fact]
    public async Task Pedido_gerado_pela_lista_de_compras_traz_os_itens_do_fornecedor_na_embalagem_sugerida()
    {
        var lista = await banco.Obter<PlanejamentoService>().ListaComprasAsync(Hoje, Hoje.AddDays(60), reporMinimo: true, Ct);
        var fornecedorId = lista.Itens.First(i => i.FornecedorId is not null).FornecedorId!.Value;
        var doFornecedor = lista.Itens.Where(i => i.FornecedorId == fornecedorId).ToList();

        var id = await banco.Obter<CompraService>().GerarDaListaAsync(fornecedorId, lista.Itens, Ct);
        var pedido = await banco.Obter<CompraService>().ObterAsync(id, Ct);

        Assert.Equal(fornecedorId, pedido.FornecedorId);
        Assert.Equal(doFornecedor.Count, pedido.Itens.Count);
        foreach (var item in doFornecedor)
        {
            var linha = pedido.Itens.Single(i => i.ProdutoId == item.ProdutoId);
            Assert.Equal(item.EmbalagemId ?? 0, linha.ProdutoEmbalagemId);
            Assert.True(linha.Quantidade * (item.FatorEmbalagem ?? 1) >= item.AComprar);
        }
        await banco.Obter<CompraService>().CancelarAsync(id, "Teste", Ct);
    }
}
