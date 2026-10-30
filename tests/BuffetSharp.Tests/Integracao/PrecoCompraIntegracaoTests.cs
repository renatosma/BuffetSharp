using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Histórico de preços de compra no banco: histórico, resumo, comparativo, maiores altas, sugestão no pedido e aviso de alta.</summary>
[Collection(ColecaoBanco.Nome)]
public class PrecoCompraIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Produto novo (com embalagem de 5 KG) e dois fornecedores da base, para as contas não dependerem dos outros testes.</summary>
    private async Task<(Produto Produto, int Pacote, (int Id, string Nome) A, (int Id, string Nome) B)> CenarioAsync()
    {
        int categoria, kg, pct;
        List<(int Id, string Nome)> fornecedores;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            categoria = await db.CategoriasProduto.Where(c => c.Nome == "Mercearia").Select(c => c.Id).FirstAsync(Ct);
            kg = await db.UnidadesMedida.Where(u => u.Sigla == "KG").Select(u => u.Id).FirstAsync(Ct);
            pct = await db.UnidadesMedida.Where(u => u.Sigla == "PCT").Select(u => u.Id).FirstAsync(Ct);
            fornecedores = (await db.Fornecedores.OrderBy(f => f.Id).Take(2).Select(f => new { f.Id, f.NomeFantasia }).ToListAsync(Ct))
                .Select(f => (f.Id, f.NomeFantasia!)).ToList();
        }
        var produto = new Produto { Nome = $"Farinha {Guid.NewGuid():N}"[..20], CategoriaProdutoId = categoria, UnidadeMedidaId = kg, EstoqueMinimo = 1 };
        produto.Embalagens.Add(new ProdutoEmbalagem { UnidadeMedidaId = pct, Fator = 5 });
        produto = await banco.Obter<ICadastroService<Produto>>().SalvarAsync(produto, Ct);
        return (produto, produto.Embalagens.Single().Id, fornecedores[0], fornecedores[1]);
    }

    private Task<MovimentacaoEstoque> ComprarAsync(int produtoId, int fornecedorId, decimal custo) =>
        banco.Obter<EstoqueService>().RegistrarCompraAsync(new EntradaEstoqueInput
        {
            ProdutoId = produtoId, FornecedorId = fornecedorId, Quantidade = 1, CustoUnitario = custo, GerarContaPagar = false
        }, Ct);

    [Fact]
    public async Task Historico_resumo_comparativo_e_altas_vem_das_entradas_sem_as_estornadas()
    {
        var precos = banco.Obter<PrecoCompraService>();
        var (produto, _, a, b) = await CenarioAsync();
        await ComprarAsync(produto.Id, a.Id, 10m);
        await ComprarAsync(produto.Id, b.Id, 9m);
        await ComprarAsync(produto.Id, a.Id, 12m);
        var errada = await ComprarAsync(produto.Id, a.Id, 50m);
        await ComprarAsync(produto.Id, b.Id, 9.5m);
        await banco.Obter<EstoqueService>().EstornarEntradaAsync(errada.Id, "Preço digitado errado", Ct);

        var historico = await precos.HistoricoAsync(produto.Id, Hoje, Hoje, Ct);
        Assert.Equal([(a.Nome, 10m, (decimal?)null), (b.Nome, 9m, -10m), (a.Nome, 12m, 33.3m), (b.Nome, 9.5m, -20.8m)],
            historico.Select(c => (c.Fornecedor, c.CustoUnitario, c.Variacao)));

        var resumo = Assert.Single(await precos.ResumoAsync(Hoje, Hoje, Ct), r => r.ProdutoId == produto.Id);
        Assert.Equal((4, 10m, 9.5m, 9m, 12m, 10.125m, -5m), (resumo.Compras, resumo.Primeiro, resumo.Ultimo, resumo.Menor, resumo.Maior, resumo.Medio,
            resumo.Variacao));
        Assert.Equal((b.Nome, 9.5m), (resumo.MelhorFornecedor, resumo.MelhorPreco));

        var comparativo = (await precos.ComparativoAsync(Hoje, Hoje, Ct)).Where(p => p.ProdutoId == produto.Id).ToList();
        Assert.Equal([(b.Nome, 9.5m, true, 0m), (a.Nome, 12m, false, 26.3m)],
            comparativo.Select(p => (p.Fornecedor, p.Ultimo, p.MaisBarato, p.AcimaDoMelhor)));
        Assert.All(comparativo, p => Assert.Equal((2, 2), (p.Compras, p.FornecedoresDoProduto)));

        var alta = Assert.Single(await precos.MaioresAltasAsync(Hoje, Hoje, Ct), x => x.ProdutoId == produto.Id);
        Assert.Equal((4, 10m, 9.5m, -5m, -2m), (alta.Compras, alta.Primeiro, alta.Ultimo, alta.Variacao, alta.ImpactoMes)); // 4 KG no mês
    }

    [Fact]
    public async Task Pedido_sugere_o_ultimo_preco_do_fornecedor_e_avisa_a_alta_acima_do_limite()
    {
        var precos = banco.Obter<PrecoCompraService>();
        var (produto, pacote, a, b) = await CenarioAsync();
        Assert.Empty(await precos.AvisosAsync([new ItemPreco(produto.Id, null, 100m)], Ct)); // nunca comprado: sem referência
        await ComprarAsync(produto.Id, a.Id, 12m);
        await ComprarAsync(produto.Id, b.Id, 10m);

        var doA = (await banco.Obter<CompraService>().ProdutosAsync(a.Id, Ct)).Single(p => p.Id == produto.Id);
        Assert.Equal((10m, b.Nome, 12m), (doA.Ultimo!.Custo, doA.Ultimo.Fornecedor, doA.UltimoDoFornecedor!.Custo));
        Assert.Equal(60m, doA.CustoSugerido(5)); // o pacote de 5 KG pelo último preço pago ao fornecedor A
        Assert.Equal(10m, (await precos.UltimoAsync(produto.Id, Ct))!.Custo);

        // Limite padrão de 10% sobre o último pago (10,00 o KG), na unidade do estoque: o pacote de 5 KG é convertido.
        Assert.Equal(10m, precos.LimiteAlta);
        Assert.Empty(await precos.AvisosAsync([new ItemPreco(produto.Id, null, 11m), new ItemPreco(produto.Id, pacote, 55m)], Ct));
        var avisos = await banco.Obter<CompraService>().AvisosPrecoAsync([new ItemPreco(produto.Id, null, 11.1m), new ItemPreco(produto.Id, pacote, 60m)], Ct);
        Assert.Equal([(11.1m, 11m), (12m, 20m)], avisos.Select(x => (x.Novo, x.Variacao)));
        Assert.All(avisos, x => Assert.Equal((10m, b.Nome), (x.Anterior.Custo, x.Anterior.Fornecedor)));
        Assert.Contains("+20,0% sobre o último pago", avisos[1].Texto);
    }

    [Fact]
    public async Task Demonstracao_tem_historico_com_dois_fornecedores_e_altas()
    {
        var precos = banco.Obter<PrecoCompraService>();
        var (de, ate) = precos.UltimoAno;

        var file = (await precos.ComparativoAsync(de, ate, Ct)).Where(p => p.Produto == "Filé mignon").ToList();
        Assert.Equal(["Atacadão Central", "Boi Nobre"], file.Select(p => p.Fornecedor).Order());
        Assert.Contains(await precos.MaioresAltasAsync(ate.AddMonths(-6), ate, Ct), x => x.Produto == "Chocolate meio amargo" && x.Variacao > 10);
        Assert.DoesNotContain(await precos.HistoricoAsync(file[0].ProdutoId, de, ate, Ct), c => c.Fornecedor == PrecoCompraService.SemFornecedor);
    }
}
