using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Devolução parcial ao fornecedor no banco: antes da nota, abatida das contas, como crédito e o crédito usado na próxima nota.</summary>
[Collection(ColecaoBanco.Nome)]
public class DevolucaoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Fornecedor e produto novos (KG), só deste teste.</summary>
    private async Task<(int Fornecedor, Produto Produto)> CenarioAsync(bool perecivel = false)
    {
        int categoria, kg, fornecedorId;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            categoria = await db.CategoriasProduto.Where(c => c.Nome == "Mercearia").Select(c => c.Id).FirstAsync(Ct);
            kg = await db.UnidadesMedida.Where(u => u.Sigla == "KG").Select(u => u.Id).FirstAsync(Ct);
            var fornecedor = new Fornecedor { RazaoSocial = $"Distribuidora {Guid.NewGuid():N}"[..30], NomeFantasia = $"Dist {Guid.NewGuid():N}"[..14],
                Documento = NfeExemplo.NovoCnpj() };
            db.Fornecedores.Add(fornecedor);
            await db.SaveChangesAsync(Ct);
            fornecedorId = fornecedor.Id;
        }
        var produto = await banco.Obter<ICadastroService<Produto>>().SalvarAsync(new Produto
        {
            Nome = $"Farinha {Guid.NewGuid():N}"[..20], CategoriaProdutoId = categoria, UnidadeMedidaId = kg, EstoqueMinimo = 1, ControlaValidade = perecivel
        }, Ct);
        return (fornecedorId, produto);
    }

    /// <summary>Pedido de <paramref name="quantidade"/> KG a 20,00, recebido inteiro. Devolve o Id do recebimento e o do item.</summary>
    private async Task<(int Recebimento, int Item, int Pedido)> ReceberAsync(int fornecedorId, int produtoId, decimal quantidade, string? lote = null)
    {
        var compras = banco.Obter<CompraService>();
        var pedido = compras.Novo(fornecedorId);
        pedido.Itens = [new PedidoCompraItemInput { ProdutoId = produtoId, Quantidade = quantidade, CustoUnitario = 20m }];
        var pedidoId = await compras.SalvarAsync(pedido, Ct);
        var item = Assert.Single(await banco.Obter<IConsultasCompras>().ItensAsync(pedidoId, Ct));
        var recebimentoId = await compras.ReceberAsync(new RecebimentoInput
        {
            PedidoId = pedidoId, Data = Hoje, Itens = [new(item.Id, quantidade, lote, lote is null ? null : Hoje.AddDays(10))]
        }, Ct);
        var situacao = await banco.Obter<DevolucaoService>().SituacaoAsync(recebimentoId, Ct);
        return (recebimentoId, situacao.Itens.Single().RecebimentoItemId, pedidoId);
    }

    private Task<int> DevolverAsync(int recebimento, int item, decimal quantidade, bool repoe = false, MotivoDevolucao motivo = MotivoDevolucao.Avariado) =>
        banco.Obter<DevolucaoService>().DevolverAsync(new DevolucaoInput
        {
            RecebimentoId = recebimento, Data = Hoje, Motivo = motivo, FornecedorRepoe = repoe, NotaDevolucao = "D-77", Observacoes = "Sacos furados",
            Itens = [new DevolucaoItemInput(item, quantidade)]
        }, Ct);

    private async Task<decimal> SaldoAsync(int produtoId) =>
        (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produtoId, Ct))!.EstoqueAtual;

    [Fact]
    public async Task Devolve_antes_da_nota_abate_das_contas_vira_credito_e_o_credito_paga_parte_da_proxima_nota()
    {
        var compras = banco.Obter<CompraService>();
        var devolucoes = banco.Obter<DevolucaoService>();
        var consultas = banco.Obter<IConsultasCompras>();
        var (fornecedor, produto) = await CenarioAsync();
        var (recebimento, item, pedido) = await ReceberAsync(fornecedor, produto.Id, 10);

        // 1) Antes da nota: 2 KG avariados, o fornecedor repõe.
        var primeira = await DevolverAsync(recebimento, item, 2, repoe: true);
        Assert.Equal(8m, await SaldoAsync(produto.Id));
        Assert.Equal(2m, Assert.Single(await consultas.ItensAsync(pedido, Ct)).Saldo);
        Assert.Equal(160m, (await consultas.ListarRecebimentosAsync(new FiltroRecebimentos { FornecedorId = fornecedor }, Ct)).Single().Total);
        var situacao = await devolucoes.SituacaoAsync(recebimento, Ct);
        Assert.Equal((2m, 8m), (situacao.Itens[0].Devolvido, situacao.Itens[0].Disponivel));
        Assert.StartsWith("O recebimento ainda não tem nota", situacao.Destino(40));
        await Assert.ThrowsAsync<RegraNegocioException>(() => compras.EstornarRecebimentoAsync(recebimento, "Lançado errado", Ct));

        // 2) A nota vem sem o devolvido; depois dela, 3 KG (60,00) saem da última conta em aberto.
        var numero = $"NF{Guid.NewGuid():N}"[..14];
        var notaId = await compras.LancarNotaAsync(new NotaEntradaInput
        {
            FornecedorId = fornecedor, Numero = numero, DataEmissao = Hoje, ValorTotal = 160m, Recebimentos = [recebimento],
            Vencimentos = [new() { Vencimento = Hoje.AddDays(28), Valor = 100m }, new() { Vencimento = Hoje.AddDays(56), Valor = 60m }]
        }, Ct);
        Assert.Equal(160m, (await consultas.ListarNotasAsync(new FiltroNotasEntrada(), Ct)).Single(n => n.Id == notaId).ValorProdutos);
        var destino = (await devolucoes.SituacaoAsync(recebimento, Ct)).Destino(60);
        Assert.StartsWith("Abate R$", destino);
        Assert.Contains("60,00 das contas em aberto", destino);
        var segunda = await DevolverAsync(recebimento, item, 3);
        List<ContaPagar> contas;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            contas = await db.ContasPagar.Where(c => c.Documento == numero).OrderBy(c => c.Vencimento).ToListAsync(Ct);
        Assert.Equal([(100m, StatusTitulo.Aberto), (60m, StatusTitulo.Cancelado)], contas.Select(c => (c.Valor, c.Status)));

        // 3) Nota paga: 1 KG (20,00) vira crédito com o fornecedor.
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            (await db.ContasPagar.FirstAsync(c => c.Id == contas[0].Id, Ct)).RegistrarPagamento(Hoje, 100m, null);
            await db.SaveChangesAsync(Ct);
        }
        var terceira = await DevolverAsync(recebimento, item, 1, motivo: MotivoDevolucao.Qualidade);
        Assert.Equal(20m, await devolucoes.CreditoDisponivelAsync(fornecedor, Ct));
        Assert.Equal(20m, (await compras.CreditosFornecedoresAsync(Ct))[fornecedor]);
        Assert.Equal(4m, await SaldoAsync(produto.Id));

        var lista = (await devolucoes.ListarAsync(Hoje, Hoje, fornecedor, Ct)).OrderBy(d => d.Id).ToList();
        Assert.Equal([primeira, segunda, terceira], lista.Select(d => d.Id));
        Assert.Equal([("Antes da nota", 40m, 0m, 0m), ($"Abatido da NF {numero}", 60m, 60m, 0m), ("Crédito", 20m, 0m, 20m)],
            lista.Select(d => (d.Destino, d.Valor, d.ValorAbatido, d.ValorCredito)));
        Assert.Equal(("D-77", true, "Sacos furados"), (lista[0].NotaDevolucao, lista[0].FornecedorRepoe, lista[0].Observacoes));
        var itens = await devolucoes.ItensAsync(primeira, Ct);
        Assert.Equal((produto.Nome, 2m, "KG", 40m), (itens[0].Produto, itens[0].Quantidade, itens[0].Unidade, itens[0].Total));
        Assert.Contains(lista[2].Codigo, devolucoes.Mensagem(lista[2], await devolucoes.ItensAsync(terceira, Ct)));

        var porFornecedor = Assert.Single(await devolucoes.PorFornecedorAsync(Hoje, Hoje, Ct), f => f.FornecedorId == fornecedor);
        Assert.Equal((3, 120m, 200m, 60m, MotivoDevolucao.Avariado, 20m),
            (porFornecedor.Devolucoes, porFornecedor.Valor, porFornecedor.Recebido, porFornecedor.Percentual, porFornecedor.MotivoPrincipal,
                porFornecedor.CreditoDisponivel));

        // 4) Próxima nota do fornecedor usa o crédito; cancelada, o crédito volta.
        var (outro, _, _) = await ReceberAsync(fornecedor, produto.Id, 5);
        var proxima = $"NF{Guid.NewGuid():N}"[..14];
        var input = new NotaEntradaInput
        {
            FornecedorId = fornecedor, Numero = proxima, DataEmissao = Hoje, ValorTotal = 100m, Recebimentos = [outro], CreditoUsado = 20m,
            Vencimentos = [new() { Vencimento = Hoje.AddDays(28), Valor = 100m }]
        };
        await Assert.ThrowsAsync<RegraNegocioException>(() => compras.LancarNotaAsync(input, Ct)); // as contas são o total menos o crédito
        input.Vencimentos = [new() { Vencimento = Hoje.AddDays(28), Valor = 80m }];
        var notaCredito = await compras.LancarNotaAsync(input, Ct);
        Assert.Equal(0m, await devolucoes.CreditoDisponivelAsync(fornecedor, Ct));
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var nota = await db.NotasFiscaisEntrada.Include(n => n.ContaPagar).Include(n => n.CreditosUsados).FirstAsync(n => n.Id == notaCredito, Ct);
            Assert.Equal((20m, 80m, 80m), (nota.CreditoUsado, nota.ValorAPagar, nota.ContaPagar!.Valor));
            Assert.Equal(terceira, Assert.Single(nota.CreditosUsados).DevolucaoCompraId);
        }
        await compras.CancelarNotaAsync(notaCredito, "Lançada errada", Ct);
        Assert.Equal(20m, await devolucoes.CreditoDisponivelAsync(fornecedor, Ct));
    }

    [Fact]
    public async Task Perecivel_devolvido_sai_do_lote_do_recebimento()
    {
        var (fornecedor, produto) = await CenarioAsync(perecivel: true);
        var (recebimento, item, _) = await ReceberAsync(fornecedor, produto.Id, 6, lote: "L-DEV");

        await DevolverAsync(recebimento, item, 2);

        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var lote = await db.LotesEstoque.SingleAsync(l => l.ProdutoId == produto.Id, Ct);
        Assert.Equal(("L-DEV", 6m, 4m), (lote.Numero, lote.Quantidade, lote.Saldo));
        var saida = await db.MovimentacoesEstoque.Include(m => m.Lotes).SingleAsync(m => m.ProdutoId == produto.Id && m.Tipo == TipoMovimentacao.Devolucao, Ct);
        Assert.Equal((2m, 2m), (saida.Quantidade, Assert.Single(saida.Lotes).Quantidade));
    }

    [Fact]
    public async Task Demonstracao_tem_devolucao_antes_da_nota_e_com_credito()
    {
        var devolucoes = await banco.Obter<DevolucaoService>().ListarAsync(Hoje.AddDays(-60), Hoje, ct: Ct);

        Assert.Contains(devolucoes, d => d.Fornecedor == "Vale Verde" && d.Destino == "Antes da nota" && d.FornecedorRepoe);
        Assert.Contains(devolucoes, d => d.Fornecedor == "Festa & Cia" && d.CreditoDisponivel > 0 && d.NotaDevolucao == "312");
    }
}
