using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class ComprasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 21);

    private static PedidoCompra Pedido(out Produto produto)
    {
        produto = new Produto { Nome = "Refrigerante" };
        var pedido = new PedidoCompra(7, Hoje);
        pedido.DefinirItens([(produto, null, 10m, 8.5m), (new Produto { Nome = "Água" }, null, 20m, 2m)]);
        return pedido;
    }

    [Fact]
    public void Pedido_soma_os_itens_e_recusa_embalagem_de_outro_fornecedor()
    {
        var pedido = Pedido(out var produto);
        Assert.Equal(125m, pedido.Total);

        var deOutro = new ProdutoEmbalagem { ProdutoId = produto.Id, FornecedorId = 99, Fator = 6 };
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(produto, deOutro, 1m, 50m)]));
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([]));
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(produto, null, 0m, 50m)]));
    }

    [Fact]
    public void Recebimento_confere_nota_data_e_soma_dos_vencimentos()
    {
        var pedido = Pedido(out _);

        Assert.Throws<RegraNegocioException>(() => pedido.ConferirRecebimento(" ", Hoje, [(Hoje.AddDays(28), 125m)], Hoje));
        Assert.Throws<RegraNegocioException>(() => pedido.ConferirRecebimento("123", Hoje.AddDays(1), [(Hoje.AddDays(28), 125m)], Hoje));
        Assert.Throws<RegraNegocioException>(() => pedido.ConferirRecebimento("123", Hoje, [(Hoje.AddDays(-1), 125m)], Hoje));
        var soma = Assert.Throws<RegraNegocioException>(() =>
            pedido.ConferirRecebimento("123", Hoje, [(Hoje.AddDays(28), 60m), (Hoje.AddDays(35), 60m)], Hoje));
        Assert.Contains("soma", soma.Message);

        pedido.ConferirRecebimento("123", Hoje, [(Hoje.AddDays(28), 60m), (Hoje.AddDays(35), 65m)], Hoje);
    }

    [Fact]
    public void Parcelamento_aceita_vencimentos_e_valores_informados()
    {
        var parcelamento = new ParcelamentoPagar("Compra NF 123", CategoriaDespesa.Insumos, 125m, 7, documento: "123");

        Assert.Throws<RegraNegocioException>(() => parcelamento.GerarParcelas([(Hoje.AddDays(28), 100m)]));
        var parcelas = parcelamento.GerarParcelas([(Hoje.AddDays(42), 25m), (Hoje.AddDays(28), 100m)]);

        Assert.Equal([Hoje.AddDays(28), Hoje.AddDays(42)], parcelas.Select(p => p.Vencimento));
        Assert.Equal([100m, 25m], parcelas.Select(p => p.Valor));
        Assert.All(parcelas, p => Assert.Equal("123", p.Documento));
    }

    [Fact]
    public void Pedido_cancelado_nao_pode_ser_alterado_nem_recebido()
    {
        var pedido = Pedido(out var produto);
        pedido.Cancelar("Fornecedor sem estoque");

        Assert.Equal(StatusPedidoCompra.Cancelado, pedido.Status);
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(produto, null, 1m, 1m)]));
        Assert.Throws<RegraNegocioException>(() => pedido.ConferirRecebimento("1", Hoje, [(Hoje, 125m)], Hoje));
    }
}
