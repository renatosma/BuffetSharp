using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Cotação -> pedido de compra -> recebimentos (estoque, em partes) -> nota fiscal de entrada (contas a pagar).</summary>
[Collection(ColecaoBanco.Nome)]
public class ComprasIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool> filtro) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro);

    private async Task<decimal> SaldoAsync(int produtoId) =>
        (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produtoId, Ct))!.EstoqueAtual;

    private async Task<PedidoCompraResumo> PedidoAsync(int id) =>
        (await banco.Obter<IConsultasCompras>().ListarPedidosAsync(new FiltroPedidosCompra(), Ct)).Single(p => p.Id == id);

    private async Task<RecebimentoInput> TudoAsync(int pedidoId, Func<PedidoCompraItemResumo, decimal>? quantidade = null) => new()
    {
        PedidoId = pedidoId,
        Data = Hoje,
        Itens = (await banco.Obter<IConsultasCompras>().ItensAsync(pedidoId, Ct))
            .Select(i => new RecebimentoItemInput(i.Id, quantidade?.Invoke(i) ?? i.Saldo)).ToList()
    };

    [Fact]
    public async Task Pedido_recebido_em_partes_entra_no_estoque_e_a_nota_junta_os_recebimentos_nas_contas()
    {
        var compras = banco.Obter<CompraService>();
        var consultas = banco.Obter<IConsultasCompras>();
        var refrigerante = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Refrigerante 2L")).Id;
        var fardo = (await banco.Obter<EstoqueService>().ListarEmbalagensAsync(refrigerante, Ct)).Single(e => e.Fator == 6);
        var outro = (await Lookup(TipoLookup.Produtos, p => p.Id != refrigerante)).Id;
        var saldoRefrigerante = await SaldoAsync(refrigerante);
        var saldoOutro = await SaldoAsync(outro);

        var pedido = compras.Novo(fardo.FornecedorId!.Value);
        pedido.Itens =
        [
            new PedidoCompraItemInput { ProdutoId = refrigerante, ProdutoEmbalagemId = fardo.Id, Quantidade = 4, CustoUnitario = 51m },
            new PedidoCompraItemInput { ProdutoId = outro, Quantidade = 2.5m, CustoUnitario = 10m }
        ];
        var id = await compras.SalvarAsync(pedido, Ct);
        Assert.Equal(229m, (await compras.ObterAsync(id, Ct)).Total);

        // 1ª entrega: 3 dos 4 fardos e todo o outro produto.
        var primeiro = await compras.ReceberAsync(await TudoAsync(id, i => i.Quantidade == 4 ? 3 : i.Saldo), Ct);
        Assert.Equal(saldoRefrigerante + 18, await SaldoAsync(refrigerante)); // 3 fardos de 6
        Assert.Equal(saldoOutro + 2.5m, await SaldoAsync(outro));
        var parcial = await PedidoAsync(id);
        Assert.Equal(StatusPedidoCompra.ParcialmenteRecebido, parcial.Status);
        Assert.Equal(178m, parcial.Recebido);
        Assert.Equal(1, parcial.AFaturar);

        // Pedido com recebimento não muda mais; a entrada do recebimento não é estornada sozinha.
        var recebido = await compras.ObterAsync(id, Ct);
        var alterar = await Assert.ThrowsAsync<RegraNegocioException>(() => compras.SalvarAsync(recebido, Ct));
        Assert.Contains("recebimento", alterar.Message);
        var kardex = await banco.Obter<IConsultasEstoqueCardapio>().ListarMovimentacoesAsync(DateTime.Today, DateTime.Today, refrigerante, ct: Ct);
        var entrada = kardex.First(m => m.RecebimentoCompraId == primeiro);
        Assert.Equal($"REC-{primeiro:D5} (PC-{id:D5})", entrada.ContaPagar);
        await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<EstoqueService>().EstornarEntradaAsync(entrada.Id, "Engano", Ct));

        // 2ª entrega: o fardo que faltava.
        var excesso = await TudoAsync(id, i => i.Saldo + 1);
        await Assert.ThrowsAsync<RegraNegocioException>(() => compras.ReceberAsync(excesso, Ct));
        var segundo = await compras.ReceberAsync(await TudoAsync(id), Ct);
        Assert.Equal(saldoRefrigerante + 24, await SaldoAsync(refrigerante));
        Assert.Equal(StatusPedidoCompra.Recebido, (await PedidoAsync(id)).Status);

        // A nota junta as duas entregas, com R$ 10 de frete, em dois boletos.
        var numero = $"NF{Guid.NewGuid():N}"[..14];
        var nota = new NotaEntradaInput
        {
            FornecedorId = fardo.FornecedorId.Value, Numero = numero, DataEmissao = Hoje, ValorTotal = 239m, Recebimentos = [primeiro, segundo],
            Vencimentos = [new() { Vencimento = Hoje.AddDays(28), Valor = 139m }, new() { Vencimento = Hoje.AddDays(42), Valor = 100m }]
        };
        var notaId = await compras.LancarNotaAsync(nota, Ct);
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var parcelas = await db.ContasPagar.Where(c => c.Documento == numero).OrderBy(c => c.Vencimento).ToListAsync(Ct);
            Assert.Equal([139m, 100m], parcelas.Select(p => p.Valor));
            Assert.All(parcelas, p => Assert.Equal(fardo.FornecedorId, p.FornecedorId));
            Assert.Contains("Nota fiscal de entrada", (await banco.Obter<FinanceiroService>().ObterContaAsync(parcelas[0].Id, Ct)).OrigemEstoque);
        }
        var lancada = (await consultas.ListarNotasAsync(new FiltroNotasEntrada(), Ct)).Single(n => n.Id == notaId);
        Assert.Equal((229m, 239m, 2, "0 de 2 paga(s)"), (lancada.ValorProdutos, lancada.ValorTotal, lancada.Recebimentos, lancada.Pagamento));
        Assert.Equal(0, (await PedidoAsync(id)).AFaturar);

        // Recebimento faturado não é estornado; a mesma nota não entra duas vezes.
        await Assert.ThrowsAsync<RegraNegocioException>(() => compras.EstornarRecebimentoAsync(segundo, "Devolução", Ct));
        var repetida = await Assert.ThrowsAsync<RegraNegocioException>(() => compras.LancarNotaAsync(nota, Ct));
        Assert.Contains("já foi lançada", repetida.Message);

        // Nota cancelada: contas canceladas e recebimentos aguardando nota de novo; aí o estorno é possível.
        await compras.CancelarNotaAsync(notaId, "Número digitado errado", Ct);
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            Assert.All(await db.ContasPagar.Where(c => c.Documento == numero).ToListAsync(Ct), c => Assert.Equal(StatusTitulo.Cancelado, c.Status));
        Assert.Equal(2, (await PedidoAsync(id)).AFaturar);

        await compras.EstornarRecebimentoAsync(segundo, "Fardo amassado, devolvido", Ct);
        Assert.Equal(saldoRefrigerante + 18, await SaldoAsync(refrigerante));
        Assert.Equal(StatusPedidoCompra.ParcialmenteRecebido, (await PedidoAsync(id)).Status);
        var recebimentos = await consultas.ListarRecebimentosAsync(new FiltroRecebimentos(), Ct);
        Assert.Equal(StatusRecebimento.Estornado, recebimentos.Single(r => r.Id == segundo).Status);

        await compras.EncerrarSaldoAsync(id, "Fornecedor sem estoque", Ct);
        Assert.Equal(StatusPedidoCompra.Encerrado, (await PedidoAsync(id)).Status);
    }

    [Fact]
    public async Task Cotacao_com_participante_sem_cadastro_vira_um_pedido_por_vencedor()
    {
        var cotacoes = banco.Obter<CotacaoService>();
        var refrigerante = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Refrigerante 2L")).Id;
        var outro = (await Lookup(TipoLookup.Produtos, p => p.Id != refrigerante)).Id;
        var cadastrado = await Lookup(TipoLookup.Fornecedores, _ => true);
        var novo = $"Mercado {Guid.NewGuid():N}"[..20];

        var input = cotacoes.Nova();
        input.Descricao = "Festa de 500 convidados";
        input.Itens =
        [
            new CotacaoItemInput { ProdutoId = refrigerante, Quantidade = 120, Especificacao = "Marca líder, a pedido do cliente" },
            new CotacaoItemInput { ProdutoId = outro, Quantidade = 10 }
        ];
        input.Participantes =
        [
            new CotacaoParticipanteInput { Nome = cadastrado.Nome, FornecedorId = cadastrado.Id },
            new CotacaoParticipanteInput { Nome = novo, Contato = "Carlos", Telefone = "11988887777", CondicaoPagamento = "28 dias" }
        ];
        var id = await cotacoes.SalvarAsync(input, Ct);

        var mapa = await cotacoes.ObterMapaAsync(id, Ct);
        var (itemRefri, itemOutro) = (mapa.Itens[0], mapa.Itens[1]);
        var (pCadastrado, pNovo) = (mapa.Participantes.Single(p => p.FornecedorId == cadastrado.Id), mapa.Participantes.Single(p => p.Nome == novo));
        mapa.Precos =
        [
            new() { ItemId = itemRefri.ItemId, ParticipanteId = pCadastrado.Id, PrecoUnitario = 9.5m, Observacao = "Marca líder" },
            new() { ItemId = itemRefri.ItemId, ParticipanteId = pNovo.Id, PrecoUnitario = 8m, Observacao = "Marca própria" },
            new() { ItemId = itemOutro.ItemId, ParticipanteId = pCadastrado.Id, PrecoUnitario = 5m },
            new() { ItemId = itemOutro.ItemId, ParticipanteId = pNovo.Id, PrecoUnitario = 4m }
        ];

        // O refrigerante mais caro só vence com o motivo.
        itemRefri.Escolhido = pCadastrado.Id;
        await Assert.ThrowsAsync<RegraNegocioException>(() => cotacoes.SalvarMapaAsync(mapa, Ct));
        itemRefri.Motivo = "Cliente pediu a marca líder";
        await cotacoes.SalvarMapaAsync(mapa, Ct);

        var resumo = (await banco.Obter<IConsultasCompras>().ListarCotacoesAsync(new FiltroCotacoes(), Ct)).Single(c => c.Id == id);
        Assert.Equal((1180m, 1, "4 de 4"), (resumo.TotalVencedores, resumo.EscolhasManuais, resumo.Respostas)); // 120 x 9,50 + 10 x 4,00
        Assert.Equal(pCadastrado.Id, (await cotacoes.ObterMapaAsync(id, Ct)).Itens[0].Escolhido);

        // O vencedor do outro produto não é cadastrado: o pedido só sai com o cadastro.
        var semCadastro = await Assert.ThrowsAsync<RegraNegocioException>(() => cotacoes.GerarPedidosAsync(id, cadastrarFornecedores: false, Ct));
        Assert.Contains(novo, semCadastro.Message);
        var pedidos = await cotacoes.GerarPedidosAsync(id, cadastrarFornecedores: true, Ct);

        Assert.Equal(2, pedidos.Count);
        var gerados = (await banco.Obter<IConsultasCompras>().ListarPedidosAsync(new FiltroPedidosCompra(), Ct)).Where(p => pedidos.Contains(p.Id)).ToList();
        Assert.Equal(1140m, gerados.Single(p => p.FornecedorId == cadastrado.Id).Total);
        var doNovo = gerados.Single(p => p.FornecedorId != cadastrado.Id);
        Assert.Equal((novo, "11988887777", 40m), (doNovo.Fornecedor, doNovo.TelefoneFornecedor, doNovo.Total));
        Assert.All(gerados, p => Assert.Equal($"COT-{id:D5}", p.Cotacao));
        Assert.Contains("28 dias", doNovo.Observacoes);

        var concluida = (await banco.Obter<IConsultasCompras>().ListarCotacoesAsync(new FiltroCotacoes(), Ct)).Single(c => c.Id == id);
        Assert.Equal((StatusCotacao.Concluida, 2), (concluida.Status, concluida.Pedidos));
        await Assert.ThrowsAsync<RegraNegocioException>(async () => await cotacoes.SalvarAsync(await cotacoes.ObterAsync(id, Ct), Ct));
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

    [Fact]
    public async Task Cotacao_gerada_pela_lista_traz_os_produtos_e_os_fornecedores_padrao()
    {
        var lista = await banco.Obter<PlanejamentoService>().ListaComprasAsync(Hoje, Hoje.AddDays(60), reporMinimo: true, Ct);
        var cotacoes = banco.Obter<CotacaoService>();

        var id = await cotacoes.GerarDaListaAsync("Compras do mês", lista.Itens, Ct);
        var cotacao = await cotacoes.ObterAsync(id, Ct);

        Assert.Equal(lista.Itens.Where(i => i.AComprar > 0).Select(i => i.ProdutoId).Distinct().Order(), cotacao.Itens.Select(i => i.ProdutoId).Order());
        Assert.Equal(lista.Itens.Where(i => i.FornecedorId is not null).Select(i => i.FornecedorId).Distinct().Order(),
            cotacao.Participantes.Select(p => p.FornecedorId).Order());
        await cotacoes.CancelarAsync(id, "Teste", Ct);
        Assert.Equal(StatusCotacao.Cancelada, (await cotacoes.ObterAsync(id, Ct)).Status);
    }
}
