using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class DevolucaoCompraTests
{
    private static readonly DateTime Agora = new(2026, 10, 20, 10, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);
    private const int Fornecedor = 7;

    /// <summary>Pedido de 10 caixas (a 60,00) de um produto que já tinha 10 UN a 5,00; tudo recebido e no estoque.</summary>
    private static (Produto Produto, PedidoCompra Pedido, RecebimentoCompra Recebimento) Recebido(bool perecivel = false)
    {
        var produto = new Produto { Nome = "Refrigerante", ControlaValidade = perecivel };
        produto.RegistrarEntrada(10, 5m, Agora.AddDays(-30), null, [new LoteInformado("ANTIGO", Hoje.AddDays(60))]);
        var caixa = new ProdutoEmbalagem { Fator = 12 };
        produto.Embalagens.Add(caixa);
        var pedido = new PedidoCompra(Fornecedor, Hoje.AddDays(-3));
        pedido.DefinirItens([(produto, caixa, 10m, 60m)]);
        var item = pedido.Itens.Single();
        var recebimento = pedido.Receber(Hoje.AddDays(-2), new Dictionary<PedidoCompraItem, decimal> { [item] = 10 }, null, "admin", Agora,
            perecivel ? new Dictionary<PedidoCompraItem, IReadOnlyList<LoteInformado>> { [item] = [new LoteInformado("NOVO", Hoje.AddDays(90))] } : null);
        recebimento.DarEntrada(Agora.AddDays(-2));
        return (produto, pedido, recebimento);
    }

    private static DevolucaoCompra Devolver(RecebimentoCompra recebimento, decimal caixas, bool repoe = true, MotivoDevolucao motivo = MotivoDevolucao.Avariado,
        string? observacoes = "Caixas amassadas") =>
        recebimento.Devolver(new Dictionary<RecebimentoItem, decimal> { [recebimento.Itens.Single()] = caixas }, Hoje, motivo, repoe, "D-1", observacoes,
            "admin", Agora);

    [Fact]
    public void Devolucao_antes_da_nota_tira_do_estoque_pelo_custo_da_entrada_e_volta_ao_saldo_do_pedido()
    {
        var (produto, pedido, recebimento) = Recebido();
        Assert.Equal((130m, StatusPedidoCompra.Recebido), (produto.EstoqueAtual, pedido.Status));

        var devolucao = Devolver(recebimento, 2);

        var saida = Assert.Single(devolucao.Itens).Saida!;
        Assert.Equal((TipoMovimentacao.Devolucao, 24m, 5m), (saida.Tipo, saida.Quantidade, saida.CustoUnitario)); // 2 caixas de 12 a 5,00 a UN
        Assert.Equal(106m, produto.EstoqueAtual);
        Assert.Equal(5m, produto.CustoMedio); // tudo a 5,00 a UN: o custo médio não muda
        Assert.Equal((120m, 0m, 0m), (devolucao.Valor, devolucao.ValorAbatido, devolucao.ValorCredito));
        Assert.Null(devolucao.NotaFiscalEntrada);
        Assert.Equal((8m, 480m), (recebimento.Itens.Single().Disponivel, recebimento.Total)); // a nota já virá sem o devolvido
        Assert.Equal((2m, StatusPedidoCompra.ParcialmenteRecebido), (pedido.Itens.Single().Saldo, pedido.Status));
    }

    [Fact]
    public void Sem_reposicao_o_pedido_continua_recebido_e_o_custo_medio_sai_sem_o_devolvido()
    {
        var produto = new Produto { Nome = "Arroz" };
        produto.RegistrarEntrada(10, 4m, Agora.AddDays(-30), null);
        var pedido = new PedidoCompra(Fornecedor, Hoje.AddDays(-3));
        pedido.DefinirItens([(produto, null, 10m, 8m)]);
        var recebimento = pedido.Receber(Hoje.AddDays(-2), pedido.Itens.ToDictionary(i => i, i => i.Quantidade), null, null, Agora);
        recebimento.DarEntrada(Agora.AddDays(-2));
        Assert.Equal(6m, produto.CustoMedio);

        Devolver(recebimento, 5, repoe: false);

        Assert.Equal((15m, 5.3333m), (produto.EstoqueAtual, produto.CustoMedio)); // (20 x 6 - 5 x 8) / 15
        Assert.Equal((0m, StatusPedidoCompra.Recebido), (pedido.Itens.Single().Saldo, pedido.Status));
    }

    [Fact]
    public void Nao_devolve_mais_que_o_recebido_nem_o_que_ja_foi_usado()
    {
        var (produto, _, recebimento) = Recebido();
        Devolver(recebimento, 4);

        var erro = Assert.Throws<RegraNegocioException>(() => Devolver(recebimento, 7));
        Assert.Contains("passa do que ainda pode ser devolvido (6)", erro.Message);
        produto.RegistrarSaida(80, Agora, "Evento");
        Assert.Throws<RegraNegocioException>(() => Devolver(recebimento, 6)); // 72 UN, mas só restam 82 - 80 = 2 no estoque
        Assert.Throws<RegraNegocioException>(() => Devolver(recebimento, 0));
        Assert.Throws<RegraNegocioException>(() => Devolver(recebimento, 1, motivo: MotivoDevolucao.Outro, observacoes: null));
    }

    [Fact]
    public void Recebimento_com_devolucao_nao_pode_ser_estornado()
    {
        var (_, _, recebimento) = Recebido();
        Devolver(recebimento, 1);

        var erro = Assert.Throws<RegraNegocioException>(() => recebimento.Estornar("Lançado errado", Agora));
        Assert.Contains("devolução", erro.Message);
    }

    [Fact]
    public void Perecivel_sai_do_lote_que_veio_no_recebimento()
    {
        var (produto, _, recebimento) = Recebido(perecivel: true);
        var devolucao = Devolver(recebimento, 3);

        Assert.Equal("NOVO", Assert.Single(Assert.Single(devolucao.Itens).Saida!.Lotes).Lote!.Numero);
        Assert.Equal([("ANTIGO", 10m), ("NOVO", 84m)], produto.Lotes.Select(l => (l.Numero, l.Saldo)));

        produto.RegistrarSaida(85, Agora, "Evento"); // usa o antigo e quase todo o novo (vence depois): sobram 9 do novo
        produto.RegistrarEntrada(50, 5m, Agora, null, [new LoteInformado("OUTRO", Hoje.AddDays(120))]);
        var erro = Assert.Throws<RegraNegocioException>(() => Devolver(recebimento, 1));
        Assert.Contains("Do lote recebido", erro.Message);
    }

    [Fact]
    public void Depois_da_nota_abate_das_contas_em_aberto_da_ultima_para_a_primeira()
    {
        var (_, _, recebimento) = Recebido();
        var nota = new NotaFiscalEntrada(Fornecedor, "123", null, Hoje.AddDays(-1), CategoriaDespesa.Insumos, [recebimento], 600m, null, Hoje);
        var (_, parcelamento) = nota.LancarContas([(Hoje.AddDays(28), 300m), (Hoje.AddDays(56), 300m)], "Bebidas");

        var devolucao = Devolver(recebimento, 6); // 360,00

        Assert.Equal((360m, 360m, 0m), (devolucao.Valor, devolucao.ValorAbatido, devolucao.ValorCredito));
        Assert.Same(nota, devolucao.NotaFiscalEntrada);
        var parcelas = parcelamento!.Parcelas.OrderBy(p => p.Vencimento).ToList();
        Assert.Equal([(240m, StatusTitulo.Aberto), (300m, StatusTitulo.Cancelado)], parcelas.Select(p => (p.Valor, p.Status)));
        Assert.Contains("Abatido", parcelas[0].Observacao);
        Assert.Contains("devolução de itens do", parcelas[0].Observacao);
        Assert.Equal((240m, 360m, 600m), (parcelamento.ValorTotal, nota.ValorDevolvido, nota.ValorTotal));
    }

    [Fact]
    public void Nota_ja_paga_vira_credito_usado_na_proxima_nota_e_devolvido_se_ela_for_cancelada()
    {
        var (_, _, recebimento) = Recebido();
        var nota = new NotaFiscalEntrada(Fornecedor, "123", null, Hoje.AddDays(-1), CategoriaDespesa.Insumos, [recebimento], 600m, null, Hoje);
        nota.LancarContas([(Hoje.AddDays(28), 400m), (Hoje.AddDays(56), 200m)], "Bebidas");
        var parcelas = nota.Contas().OrderBy(x => x.Vencimento).ToList();
        parcelas[1].RegistrarPagamento(Hoje, 200m, null); // a última já foi paga; a primeira está em aberto

        var devolucao = Devolver(recebimento, 8); // 480,00: 400 abatidos da conta em aberto e 80 de crédito
        Assert.Equal((400m, 80m, 80m), (devolucao.ValorAbatido, devolucao.ValorCredito, devolucao.CreditoDisponivel));
        Assert.Equal(StatusTitulo.Cancelado, parcelas[0].Status);

        // Próxima compra do mesmo fornecedor: o crédito sai da nota e as contas ficam com o resto.
        var (_, _, outro) = Recebido();
        var proxima = new NotaFiscalEntrada(Fornecedor, "124", null, Hoje, CategoriaDespesa.Insumos, [outro], 600m, null, Hoje);
        Assert.Throws<RegraNegocioException>(() => proxima.UsarCredito([devolucao], 81m));
        proxima.UsarCredito([devolucao], 80m);
        Assert.Throws<RegraNegocioException>(() => proxima.LancarContas([(Hoje.AddDays(28), 600m)], "Bebidas"));
        var (contaProxima, _) = proxima.LancarContas([(Hoje.AddDays(28), 520m)], "Bebidas");
        Assert.Equal((520m, 80m, 0m), (contaProxima!.Valor, proxima.CreditoUsado, devolucao.CreditoDisponivel));

        proxima.Cancelar("Lançada errada", Agora);
        Assert.Equal(80m, devolucao.CreditoDisponivel);
    }
}
