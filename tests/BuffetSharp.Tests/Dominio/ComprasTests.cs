using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class ComprasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 21);
    private static readonly DateTime Agora = Hoje.ToDateTime(new TimeOnly(10, 0));

    private static PedidoCompra Pedido(out Produto refrigerante, out Produto agua)
    {
        refrigerante = new Produto { Nome = "Refrigerante" };
        agua = new Produto { Nome = "Água" };
        var pedido = new PedidoCompra(7, Hoje);
        pedido.DefinirItens([(refrigerante, null, 10m, 8.5m), (agua, null, 20m, 2m)]);
        return pedido;
    }

    private static Dictionary<PedidoCompraItem, decimal> Quantidades(PedidoCompra pedido, decimal refrigerante, decimal agua) => new()
    {
        [pedido.Itens.First()] = refrigerante,
        [pedido.Itens.Last()] = agua
    };

    [Fact]
    public void Pedido_soma_os_itens_e_recusa_embalagem_de_outro_fornecedor()
    {
        var pedido = Pedido(out var produto, out _);
        Assert.Equal(125m, pedido.Total);

        var deOutro = new ProdutoEmbalagem { ProdutoId = produto.Id, FornecedorId = 99, Fator = 6 };
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(produto, deOutro, 1m, 50m)]));
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([]));
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(produto, null, 0m, 50m)]));
    }

    [Fact]
    public void Recebimento_parcial_entra_no_estoque_e_deixa_o_saldo_no_pedido()
    {
        var pedido = Pedido(out var refrigerante, out var agua);

        var primeiro = pedido.Receber(Hoje, Quantidades(pedido, 4, 20), "Faltou refrigerante", "ana", Agora);
        var entradas = primeiro.DarEntrada(Agora);

        Assert.Equal(StatusPedidoCompra.ParcialmenteRecebido, pedido.Status);
        Assert.Equal(4m, refrigerante.EstoqueAtual);
        Assert.Equal(20m, agua.EstoqueAtual);
        Assert.Equal(2, entradas.Count);
        Assert.All(entradas, m => Assert.True(m.DePedidoCompra));
        Assert.Equal(74m, primeiro.Total); // 4 x 8,50 + 20 x 2,00
        Assert.Equal([6m, 0m], pedido.Itens.Select(i => i.Saldo));
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(refrigerante, null, 1m, 1m)]));

        // Mais do que o saldo não entra; o resto completa o pedido.
        var excesso = Assert.Throws<RegraNegocioException>(() => pedido.Receber(Hoje, Quantidades(pedido, 7, 0), null, null, Agora));
        Assert.Contains("saldo", excesso.Message);
        pedido.Receber(Hoje, Quantidades(pedido, 6, 0), null, null, Agora).DarEntrada(Agora);
        Assert.Equal(StatusPedidoCompra.Recebido, pedido.Status);
        Assert.Equal(10m, refrigerante.EstoqueAtual);
        Assert.Throws<RegraNegocioException>(() => pedido.Receber(Hoje, Quantidades(pedido, 1, 0), null, null, Agora));
    }

    [Fact]
    public void Estorno_do_recebimento_tira_do_estoque_e_volta_ao_saldo_do_pedido()
    {
        var pedido = Pedido(out var refrigerante, out _);
        var recebimento = pedido.Receber(Hoje, Quantidades(pedido, 10, 20), null, null, Agora);
        recebimento.DarEntrada(Agora);
        Assert.Equal(StatusPedidoCompra.Recebido, pedido.Status);

        Assert.Throws<RegraNegocioException>(() => recebimento.Estornar(" ", Agora));
        var estornos = recebimento.Estornar("Mercadoria devolvida", Agora);

        Assert.Equal(2, estornos.Count);
        Assert.Equal(0m, refrigerante.EstoqueAtual);
        Assert.Equal(StatusRecebimento.Estornado, recebimento.Status);
        Assert.Equal(StatusPedidoCompra.Aberto, pedido.Status);
        Assert.All(pedido.Itens, i => Assert.Equal(i.Quantidade, i.Saldo));
        Assert.Throws<RegraNegocioException>(() => recebimento.Estornar("De novo", Agora));
    }

    [Fact]
    public void Saldo_encerrado_fecha_o_pedido_recebido_em_parte()
    {
        var pedido = Pedido(out _, out _);
        Assert.Throws<RegraNegocioException>(() => pedido.EncerrarSaldo("Sem estoque")); // nada recebido: cancele

        pedido.Receber(Hoje, Quantidades(pedido, 5, 0), null, null, Agora).DarEntrada(Agora);
        Assert.Throws<RegraNegocioException>(() => pedido.Cancelar("Desisti"));
        pedido.EncerrarSaldo("Fornecedor sem estoque");

        Assert.Equal(StatusPedidoCompra.Encerrado, pedido.Status);
        Assert.Throws<RegraNegocioException>(() => pedido.Receber(Hoje, Quantidades(pedido, 1, 0), null, null, Agora));
    }

    [Fact]
    public void Nota_de_entrada_junta_recebimentos_e_confere_os_vencimentos()
    {
        var pedido = Pedido(out _, out _);
        var primeiro = pedido.Receber(Hoje, Quantidades(pedido, 4, 20), null, null, Agora);
        var segundo = pedido.Receber(Hoje, Quantidades(pedido, 6, 0), null, null, Agora);

        Assert.Throws<RegraNegocioException>(() =>
            new NotaFiscalEntrada(8, "123", null, Hoje, CategoriaDespesa.Insumos, [primeiro], 74m, null, Hoje)); // outro fornecedor
        Assert.Throws<RegraNegocioException>(() =>
            new NotaFiscalEntrada(7, "123", null, Hoje.AddDays(1), CategoriaDespesa.Insumos, [primeiro], 74m, null, Hoje));

        // Frete de R$ 10: a nota vale mais que a mercadoria.
        var nota = new NotaFiscalEntrada(7, "123", "1", Hoje, CategoriaDespesa.Insumos, [primeiro, segundo], 135m, null, Hoje);
        Assert.Equal(125m, nota.ValorProdutos);
        Assert.All([primeiro, segundo], r => Assert.Equal(StatusRecebimento.Faturado, r.Status));
        Assert.Throws<RegraNegocioException>(() => primeiro.Estornar("Engano", Agora));

        var soma = Assert.Throws<RegraNegocioException>(() => nota.LancarContas([(Hoje.AddDays(28), 125m)], "Atacadão"));
        Assert.Contains("soma", soma.Message);
        Assert.Throws<RegraNegocioException>(() => nota.LancarContas([(Hoje.AddDays(-1), 135m)], "Atacadão"));
        var (conta, parcelamento) = nota.LancarContas([(Hoje.AddDays(28), 100m), (Hoje.AddDays(42), 35m)], "Atacadão");

        Assert.Null(conta);
        Assert.Equal([100m, 35m], parcelamento!.Parcelas.Select(p => p.Valor));
        Assert.All(parcelamento.Parcelas, p => Assert.Equal("123", p.Documento));
        Assert.Contains("NF 123/1", parcelamento.Descricao);

        nota.Cancelar("Número errado", Agora);
        Assert.Equal(StatusNotaEntrada.Cancelada, nota.Status);
        Assert.All(parcelamento.Parcelas, p => Assert.Equal(StatusTitulo.Cancelado, p.Status));
        Assert.All([primeiro, segundo], r => Assert.Equal(StatusRecebimento.AFaturar, r.Status));
    }

    [Fact]
    public void Nota_paga_nao_pode_ser_cancelada()
    {
        var pedido = Pedido(out _, out _);
        var recebimento = pedido.Receber(Hoje, Quantidades(pedido, 10, 20), null, null, Agora);
        var nota = new NotaFiscalEntrada(7, "9", null, Hoje, CategoriaDespesa.Insumos, [recebimento], 125m, null, Hoje);
        var (conta, _) = nota.LancarContas([(Hoje.AddDays(10), 125m)], null);
        conta!.RegistrarPagamento(Hoje, 125m, null);

        Assert.Throws<RegraNegocioException>(() => nota.Cancelar("Engano", Agora));
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
        var pedido = Pedido(out var produto, out _);
        pedido.Cancelar("Fornecedor sem estoque");

        Assert.Equal(StatusPedidoCompra.Cancelado, pedido.Status);
        Assert.Throws<RegraNegocioException>(() => pedido.DefinirItens([(produto, null, 1m, 1m)]));
        Assert.Throws<RegraNegocioException>(() => pedido.Receber(Hoje, Quantidades(pedido, 1, 0), null, null, Agora));
    }

    // ------------------------------- Cotação -------------------------------

    private static CotacaoCompra Cotacao(out CotacaoItem picanha, out CotacaoItem refrigerante, out CotacaoParticipante atacadao,
        out CotacaoParticipante acougue)
    {
        var cotacao = new CotacaoCompra("Casamento Silva", Hoje);
        cotacao.DefinirItens([(new Produto { Nome = "Picanha" }, 30m, "Maturada"), (new Produto { Nome = "Refrigerante" }, 100m, null)]);
        picanha = cotacao.Itens.First();
        refrigerante = cotacao.Itens.Last();
        atacadao = cotacao.AdicionarParticipante("Atacadão", null, "11999990000", null, 7);
        acougue = cotacao.AdicionarParticipante("Açougue do Zé", "Zé", null, null, null); // não cadastrado
        cotacao.DefinirPreco(picanha, atacadao, 69.9m, "Marca X");
        cotacao.DefinirPreco(picanha, acougue, 79.9m, "Angus, maturada 21 dias");
        cotacao.DefinirPreco(refrigerante, atacadao, 6.5m, null);
        cotacao.DefinirPreco(refrigerante, acougue, 7m, null);
        cotacao.AtualizarVencedores();
        return cotacao;
    }

    [Fact]
    public void Cotacao_escolhe_o_menor_preco_por_padrao()
    {
        var cotacao = Cotacao(out var picanha, out var refrigerante, out var atacadao, out var acougue);

        Assert.Same(atacadao, picanha.Vencedor);
        Assert.Same(atacadao, refrigerante.Vencedor);
        Assert.False(picanha.EscolhaManual);
        Assert.Equal(2747m, cotacao.TotalVencedores); // 30 x 69,90 + 100 x 6,50

        // O preço mudou: o vencedor automático acompanha o menor preço.
        cotacao.DefinirPreco(refrigerante, acougue, 6m, "Promoção");
        cotacao.AtualizarVencedores();
        Assert.Same(acougue, refrigerante.Vencedor);
        Assert.Throws<RegraNegocioException>(() => cotacao.DefinirPreco(refrigerante, acougue, 0m, null));
        Assert.Throws<RegraNegocioException>(() => cotacao.AdicionarParticipante("atacadão", null, null, null, null));
    }

    [Fact]
    public void Vencedor_mais_caro_exige_motivo_e_continua_escolhido()
    {
        var cotacao = Cotacao(out var picanha, out _, out var atacadao, out var acougue);

        var semMotivo = Assert.Throws<RegraNegocioException>(() => cotacao.EscolherVencedor(picanha, acougue, " "));
        Assert.Contains("motivo", semMotivo.Message);
        cotacao.EscolherVencedor(picanha, acougue, "Cliente pediu Angus");

        Assert.Same(acougue, picanha.Vencedor);
        Assert.True(picanha.EscolhaManual);
        Assert.Equal("Cliente pediu Angus", picanha.MotivoEscolha);

        // Mudar preços não desfaz a escolha manual; "menor preço em tudo" desfaz.
        cotacao.DefinirPreco(picanha, atacadao, 59.9m, null);
        cotacao.AtualizarVencedores();
        Assert.Same(acougue, picanha.Vencedor);
        cotacao.EscolherMenoresPrecos();
        Assert.Same(atacadao, picanha.Vencedor);
        Assert.Null(picanha.MotivoEscolha);

        // Sem preço, não dá para vencer.
        cotacao.DefinirPreco(picanha, acougue, null, null);
        Assert.Throws<RegraNegocioException>(() => cotacao.EscolherVencedor(picanha, acougue, "Qualidade"));
    }

    [Fact]
    public void Gerar_pedidos_exige_fornecedor_cadastrado_e_separa_por_vencedor()
    {
        var cotacao = Cotacao(out var picanha, out var refrigerante, out _, out var acougue);
        cotacao.EscolherVencedor(picanha, acougue, "Cliente pediu Angus");

        var semCadastro = Assert.Throws<RegraNegocioException>(() => cotacao.GerarPedidos(Hoje, Agora));
        Assert.Contains("Açougue do Zé", semCadastro.Message);
        Assert.Equal([acougue], cotacao.VencedoresSemCadastro());

        cotacao.AlterarParticipante(acougue, acougue.Nome, acougue.Contato, acougue.Telefone, null, 9);
        var pedidos = cotacao.GerarPedidos(Hoje, Agora);

        Assert.Equal(StatusCotacao.Concluida, cotacao.Status);
        Assert.Equal(2, pedidos.Count);
        var doAcougue = pedidos.Single(p => p.FornecedorId == 9);
        Assert.Equal(2397m, doAcougue.Total); // 30 x 79,90
        Assert.Equal(650m, pedidos.Single(p => p.FornecedorId == 7).Total);
        Assert.All(pedidos, p => Assert.Same(cotacao, p.CotacaoCompra));
        Assert.Contains(refrigerante.Produto!, pedidos.Single(p => p.FornecedorId == 7).Itens.Select(i => i.Produto));
        Assert.Throws<RegraNegocioException>(() => cotacao.DefinirPreco(picanha, acougue, 1m, null));
    }

    [Fact]
    public void Retirar_participante_leva_os_precos_e_devolve_o_produto_ao_menor_preco()
    {
        var cotacao = Cotacao(out var picanha, out _, out var atacadao, out var acougue);
        cotacao.EscolherVencedor(picanha, acougue, "Qualidade");

        cotacao.RemoverParticipante(acougue);

        Assert.Single(cotacao.Participantes);
        Assert.DoesNotContain(cotacao.Precos, p => p.Participante == acougue);
        Assert.Same(atacadao, picanha.Vencedor);
        Assert.False(picanha.EscolhaManual);
    }

    [Fact]
    public void Trocar_os_produtos_mantem_os_precos_de_quem_continua()
    {
        var cotacao = Cotacao(out var picanha, out var refrigerante, out var atacadao, out _);

        cotacao.DefinirItens([(picanha.Produto!, 40m, "Maturada"), (new Produto { Nome = "Gelo" }, 50m, null)]);

        Assert.Equal(2, cotacao.Itens.Count);
        Assert.DoesNotContain(refrigerante, cotacao.Itens);
        Assert.Equal(40m, picanha.Quantidade);
        Assert.Equal(69.9m, cotacao.PrecoDe(picanha, atacadao)!.PrecoUnitario);
        Assert.DoesNotContain(cotacao.Precos, p => p.Item == refrigerante);
    }
}
