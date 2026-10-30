using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Validade e lote no banco: entradas com lote, saída pelo que vence primeiro, descarte, recebimento, NF-e e lembretes.</summary>
[Collection(ColecaoBanco.Nome)]
public class ValidadeLoteIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Perecível novo, só deste teste (os da demonstração mudam com os outros testes).</summary>
    private async Task<Produto> PerecivelAsync()
    {
        int categoria, unidade, fornecedor;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            categoria = await db.CategoriasProduto.Where(c => c.Nome == "Laticínios").Select(c => c.Id).FirstAsync(Ct);
            unidade = await db.UnidadesMedida.Where(u => u.Sigla == "KG").Select(u => u.Id).FirstAsync(Ct);
            fornecedor = await db.Fornecedores.OrderBy(f => f.Id).Select(f => f.Id).FirstAsync(Ct);
        }
        return await banco.Obter<ICadastroService<Produto>>().SalvarAsync(new Produto
        {
            Nome = $"Ricota {Guid.NewGuid():N}"[..20], CategoriaProdutoId = categoria, UnidadeMedidaId = unidade, FornecedorId = fornecedor,
            ControlaValidade = true, EstoqueMinimo = 1
        }, Ct);
    }

    private async Task<List<LoteEstoque>> LotesAsync(int produtoId)
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        return await db.LotesEstoque.AsNoTracking().Where(l => l.ProdutoId == produtoId).OrderBy(l => l.Validade).ToListAsync(Ct);
    }

    [Fact]
    public async Task Saida_usa_o_lote_que_vence_primeiro_e_o_vencido_vai_para_as_perdas()
    {
        var estoque = banco.Obter<EstoqueService>();
        var validades = banco.Obter<ValidadeService>();
        var produto = await PerecivelAsync();

        await Assert.ThrowsAsync<RegraNegocioException>(() => estoque.RegistrarEntradaAsync(produto.Id, 5, 20m, "Sem validade", ct: Ct));
        await estoque.RegistrarEntradaAsync(produto.Id, 5, 20m, "Lote bom", [new LoteInformado("BOM", Hoje.AddDays(40))], Ct);
        await estoque.RegistrarEntradaAsync(produto.Id, 3, 22m, "Lote vencendo", [new LoteInformado("CEDO", Hoje.AddDays(2))], Ct);
        await estoque.RegistrarEntradaAsync(produto.Id, 2, 18m, "Lote vencido", [new LoteInformado("VENCIDO", Hoje.AddDays(-1))], Ct);

        // Só 8 dos 10 podem sair (2 vencidos); a saída começa pelo que vence em 2 dias.
        await Assert.ThrowsAsync<RegraNegocioException>(() => estoque.RegistrarSaidaAsync(produto.Id, 9, "Evento", ct: Ct));
        var saida = await estoque.RegistrarSaidaAsync(produto.Id, 4, "Evento", ct: Ct);
        Assert.Equal([("VENCIDO", 2m), ("CEDO", 0m), ("BOM", 4m)], (await LotesAsync(produto.Id)).Select(l => (l.Numero, l.Saldo)));
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var usados = await db.Set<MovimentacaoLote>().Where(m => m.MovimentacaoEstoqueId == saida.Id).Include(m => m.Lote)
                .OrderBy(m => m.Lote!.Validade).Select(m => new { m.Lote!.Numero, m.Quantidade }).ToListAsync(Ct);
            Assert.Equal([("CEDO", 3m), ("BOM", 1m)], usados.Select(u => (u.Numero, u.Quantidade)));
        }

        // Tela de validades e lembretes: o vencido e o lote bom (fora do prazo de alerta) com saldo.
        var lotes = (await validades.ListarLotesAsync(ct: Ct)).Where(l => l.ProdutoId == produto.Id).ToList();
        Assert.Equal([(SituacaoLote.Vencido, -1), (SituacaoLote.Ok, 40)], lotes.Select(l => (l.Situacao, l.DiasParaVencer)));
        Assert.Equal(36m, lotes[0].Valor);
        Assert.Equal(3, (await validades.ListarLotesAsync(somenteComSaldo: false, Ct)).Count(l => l.ProdutoId == produto.Id));
        var vencido = lotes[0];
        var lembrete = Assert.Single(await validades.LembretesAsync(Ct), l => l.ReferenciaId == vencido.Id);
        Assert.Equal((TipoLembrete.ValidadeLote, $"validade:{vencido.Id}:vencido"), (lembrete.Tipo, lembrete.Chave));
        Assert.Contains("vencido", lembrete.Titulo);

        // Descarte do vencido: sai como perda pelo custo do lote e some dos lembretes.
        await Assert.ThrowsAsync<RegraNegocioException>(() => estoque.DescartarAsync(produto.Id, vencido.Id, 3, "Vencido", Ct));
        var perda = await estoque.DescartarAsync(produto.Id, vencido.Id, 2, "Produto vencido", Ct);
        Assert.Equal((TipoMovimentacao.Perda, 18m), (perda.Tipo, perda.CustoUnitario));
        Assert.DoesNotContain(await validades.LembretesAsync(Ct), l => l.ReferenciaId == vencido.Id);
        var perdas = await validades.PerdasAsync(Hoje, Hoje, Ct);
        var registrada = Assert.Single(perdas, p => p.MovimentacaoId == perda.Id);
        Assert.Equal((produto.Nome, 2m, 36m), (registrada.Produto, registrada.Quantidade, registrada.Valor));
        Assert.Contains("Produto vencido", registrada.Observacao);

        var salvo = (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produto.Id, Ct))!;
        Assert.Equal(4m, salvo.EstoqueAtual);
    }

    [Fact]
    public async Task Lote_vencendo_vira_lembrete_e_entrada_estornada_baixa_o_lote()
    {
        var estoque = banco.Obter<EstoqueService>();
        var produto = await PerecivelAsync();

        var entrada = await estoque.RegistrarEntradaAsync(produto.Id, 4, 10m, "Lote vencendo", [new LoteInformado(null, Hoje.AddDays(3))], Ct);
        var lote = Assert.Single(await LotesAsync(produto.Id));
        var lembrete = Assert.Single(await banco.Obter<LembreteService>().ListarAsync(Ct), l => l.Tipo == TipoLembrete.ValidadeLote && l.ReferenciaId == lote.Id);
        Assert.Equal($"validade:{lote.Id}:vencendo", lembrete.Chave);
        Assert.Contains("vence em 3 dia(s)", lembrete.Titulo);

        await estoque.EstornarEntradaAsync(entrada.Id, "Lançada errada", Ct);
        Assert.Equal(0m, Assert.Single(await LotesAsync(produto.Id)).Saldo);
        Assert.DoesNotContain(await banco.Obter<ValidadeService>().LembretesAsync(Ct), l => l.ReferenciaId == lote.Id);
    }

    [Fact]
    public async Task Recebimento_do_pedido_pede_a_validade_dos_pereciveis_e_cria_o_lote()
    {
        var compras = banco.Obter<CompraService>();
        var produto = await PerecivelAsync();
        var pedido = compras.Novo(produto.FornecedorId!.Value);
        pedido.Itens = [new PedidoCompraItemInput { ProdutoId = produto.Id, Quantidade = 6, CustoUnitario = 15m }];
        var pedidoId = await compras.SalvarAsync(pedido, Ct);
        var item = Assert.Single(await banco.Obter<IConsultasCompras>().ItensAsync(pedidoId, Ct));
        Assert.True(item.ControlaValidade);
        Assert.True((await compras.ProdutosAsync(produto.FornecedorId.Value, Ct)).Single(p => p.Id == produto.Id).ControlaValidade);

        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            compras.ReceberAsync(new RecebimentoInput { PedidoId = pedidoId, Data = Hoje, Itens = [new(item.Id, 6)] }, Ct));
        await compras.ReceberAsync(new RecebimentoInput { PedidoId = pedidoId, Data = Hoje, Itens = [new(item.Id, 6, "NF-123", Hoje.AddDays(12))] }, Ct);

        var lote = Assert.Single(await LotesAsync(produto.Id));
        Assert.Equal(("NF-123", Hoje.AddDays(12), 6m, 15m), (lote.Numero, lote.Validade, lote.Saldo, lote.CustoUnitario));
    }

    [Fact]
    public async Task Nfe_sem_pedido_traz_os_lotes_do_rastro_ou_a_validade_informada()
    {
        var importacao = banco.Obter<ImportacaoNfeService>();
        var ricota = await PerecivelAsync();
        var outra = await PerecivelAsync();
        var numero = Random.Shared.Next(100_000, 999_999).ToString();
        var xml = NfeExemplo.Xml(numero, NfeExemplo.NovoCnpj(), $"Laticínios {numero}",
        [
            new("R1", "RICOTA FRESCA", "KG", 5, 30m, Rastros: [("RA", 2, Hoje.AddDays(6)), ("RB", 3, Hoje.AddDays(15))]),
            new("R2", "RICOTA TEMPERADA", "KG", 2, 34m)
        ]);
        var analise = await importacao.AnalisarAsync(LeitorNfe.Ler(xml), Ct);
        NotaSemPedidoInput Input(DateOnly? validade) => new()
        {
            Nota = analise.Nota, DataRecebimento = Hoje,
            Itens = [new(1, ricota.Id, 0), new(2, outra.Id, 0, "MANUAL", validade)],
            Vencimentos = [new VencimentoInput { Vencimento = Hoje.AddDays(28), Valor = analise.Nota.ValorTotal }]
        };

        // O segundo item não tem rastro no XML: sem a validade informada, a nota não entra.
        await Assert.ThrowsAsync<RegraNegocioException>(() => importacao.LancarSemPedidoAsync(Input(null), Ct));
        await importacao.LancarSemPedidoAsync(Input(Hoje.AddDays(20)), Ct);

        Assert.Equal([("RA", 2m, Hoje.AddDays(6)), ("RB", 3m, Hoje.AddDays(15))],
            (await LotesAsync(ricota.Id)).Select(l => (l.Numero, l.Saldo, l.Validade)));
        Assert.Equal(("MANUAL", 2m, Hoje.AddDays(20)), (await LotesAsync(outra.Id)).Select(l => (l.Numero, l.Saldo, l.Validade)).Single());
    }

    [Fact]
    public async Task Demonstracao_tem_lotes_vencendo_vencido_e_perdas()
    {
        var validades = banco.Obter<ValidadeService>();
        var lotes = await validades.ListarLotesAsync(ct: Ct);

        Assert.Contains(lotes, l => l.Produto == "Queijo muçarela" && l.Situacao == SituacaoLote.Vencido);
        Assert.Contains(lotes, l => l.Situacao == SituacaoLote.Vencendo);
        Assert.Contains(await validades.PerdasAsync(Hoje.AddDays(-30), Hoje, Ct), p => p.Produto == "Morango");
    }
}
