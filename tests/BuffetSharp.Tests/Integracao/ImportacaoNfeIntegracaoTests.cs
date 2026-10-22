using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Importação do XML da NF-e: compra sem pedido (estoque + nota de uma vez) e nota de pedido já recebido.</summary>
[Collection(ColecaoBanco.Nome)]
public class ImportacaoNfeIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool> filtro) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro);

    private async Task<decimal> SaldoAsync(int produtoId) =>
        (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produtoId, Ct))!.EstoqueAtual;

    [Fact]
    public async Task Compra_sem_pedido_cadastra_o_fornecedor_da_entrada_no_estoque_e_lembra_os_produtos()
    {
        var importacao = banco.Obter<ImportacaoNfeService>();
        var arroz = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Arroz agulhinha")).Id;
        var oleo = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Óleo de soja")).Id;
        var saldoArroz = await SaldoAsync(arroz);
        var saldoOleo = await SaldoAsync(oleo);
        var cnpj = NfeExemplo.NovoCnpj();
        var numero = Random.Shared.Next(100_000, 999_999).ToString();

        var xml = NfeExemplo.Xml(numero, cnpj, $"Mercado {numero}",
        [
            new("771", "ARROZ AGULHINHA TIPO 1 PCT 5KG", "KG", 25, 6.10m),
            new("772", "OLEO SOJA 900ML", "UN", 12, 8.50m, Desconto: 6m),
            new("990", "SACOLA PLASTICA", "UN", 2, 0.25m)
        ], frete: 15m, duplicatas: [(Hoje.AddDays(14), 130m), (Hoje.AddDays(28), 134m)]);
        var analise = await importacao.AnalisarAsync(LeitorNfe.Ler(xml), Ct);

        // Fornecedor novo; o arroz é reconhecido pelo nome, o óleo e a sacola não.
        Assert.Null(analise.FornecedorId);
        Assert.Empty(analise.RecebimentosAFaturar);
        Assert.Equal((arroz, OrigemVinculo.Nome), (analise.Itens[0].ProdutoId, analise.Itens[0].Origem));
        Assert.Null(analise.Itens[1].ProdutoId);
        Assert.Equal(264m, analise.Nota.ValorTotal); // 152,50 + 102,00 + 0,50 - 6,00 de desconto + 15,00 de frete

        var input = new NotaSemPedidoInput
        {
            Nota = analise.Nota, DataRecebimento = Hoje,
            Itens = [new(1, arroz, 0), new(2, oleo, 0), new(3, null, 0)],
            Vencimentos = analise.Nota.Duplicatas.Select(d => new VencimentoInput { Vencimento = d.Vencimento, Valor = d.Valor }).ToList()
        };
        var semItens = new NotaSemPedidoInput { Nota = analise.Nota, DataRecebimento = Hoje, Itens = [new(1, null, 0), new(2, null, 0), new(3, null, 0)] };
        await Assert.ThrowsAsync<RegraNegocioException>(() => importacao.LancarSemPedidoAsync(semItens, Ct));
        var notaId = await importacao.LancarSemPedidoAsync(input, Ct);

        Assert.Equal(saldoArroz + 25, await SaldoAsync(arroz));
        Assert.Equal(saldoOleo + 12, await SaldoAsync(oleo));
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var fornecedor = await db.Fornecedores.SingleAsync(f => f.Documento == cnpj, Ct);
            Assert.Equal(($"Mercado {numero} Ltda", "São Paulo", "01001-000"), (fornecedor.RazaoSocial, fornecedor.Endereco.Cidade, fornecedor.Endereco.Cep));
            var nota = await db.NotasFiscaisEntrada.Include(n => n.Recebimentos).SingleAsync(n => n.Id == notaId, Ct);
            Assert.Equal((analise.Nota.ChaveAcesso, 264m, 248.50m), (nota.ChaveAcesso, nota.ValorTotal, nota.ValorProdutos)); // estoque: arroz 152,50 + óleo com desconto 96,00
            Assert.Equal(StatusRecebimento.Faturado, Assert.Single(nota.Recebimentos).Status);
            var parcelas = await db.ContasPagar.Where(c => c.Documento == numero && c.FornecedorId == fornecedor.Id).OrderBy(c => c.Vencimento).ToListAsync(Ct);
            Assert.Equal([130m, 134m], parcelas.Select(p => p.Valor));
            Assert.Equal(2, await db.CodigosProdutoFornecedor.CountAsync(v => v.FornecedorId == fornecedor.Id, Ct));
        }

        // A mesma NF-e não entra de novo.
        var repetida = await Assert.ThrowsAsync<RegraNegocioException>(() => importacao.AnalisarAsync(LeitorNfe.Ler(xml), Ct));
        Assert.Contains("já foi lançada", repetida.Message);

        // Na próxima nota do fornecedor, os códigos já vêm ligados aos produtos.
        var proxima = NfeExemplo.Xml((int.Parse(numero) + 1).ToString(), cnpj, $"Mercado {numero}",
            [new("772", "OLEO SOJA 900ML", "UN", 6, 8.40m), new("771", "ARROZ AGULHINHA TIPO 1 PCT 5KG", "KG", 10, 6m)]);
        var segunda = await importacao.AnalisarAsync(LeitorNfe.Ler(proxima), Ct);
        Assert.NotNull(segunda.FornecedorId);
        Assert.All(segunda.Itens, i => Assert.Equal(OrigemVinculo.Vinculo, i.Origem));
        Assert.Equal([oleo, arroz], segunda.Itens.Select(i => i.ProdutoId!.Value));
    }

    [Fact]
    public async Task NFe_de_pedido_recebido_e_conferida_e_lancada_com_a_chave()
    {
        var compras = banco.Obter<CompraService>();
        var importacao = banco.Obter<ImportacaoNfeService>();
        var refrigerante = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Refrigerante 2L")).Id;
        var fardo = (await banco.Obter<EstoqueService>().ListarEmbalagensAsync(refrigerante, Ct)).Single(e => e.Fator == 6);
        var fornecedor = (await banco.Obter<ICadastroService<Fornecedor>>().ObterAsync(fardo.FornecedorId!.Value, Ct))!;

        var pedido = compras.Novo(fornecedor.Id);
        pedido.Itens = [new PedidoCompraItemInput { ProdutoId = refrigerante, ProdutoEmbalagemId = fardo.Id, Quantidade = 5, CustoUnitario = 51m }];
        var pedidoId = await compras.SalvarAsync(pedido, Ct);
        var item = Assert.Single(await banco.Obter<IConsultasCompras>().ItensAsync(pedidoId, Ct));
        var recebimento = await compras.ReceberAsync(new RecebimentoInput { PedidoId = pedidoId, Data = Hoje, Itens = [new(item.Id, 5)] }, Ct);

        var numero = Random.Shared.Next(100_000, 999_999).ToString();
        var xml = NfeExemplo.Xml(numero, fornecedor.Documento!, fornecedor.NomeExibicao, [new("R2L", "REFRIGERANTE 2L COLA", "PCT", 4, 51m)], frete: 12m);
        var analise = await importacao.AnalisarAsync(LeitorNfe.Ler(xml), Ct);

        // Reconhece o fornecedor pelo CNPJ e o refrigerante no fardo de 6; a nota tem 4 fardos e chegaram 5.
        Assert.Equal(fornecedor.Id, analise.FornecedorId);
        Assert.Contains(recebimento, analise.RecebimentosAFaturar);
        Assert.Equal((refrigerante, fardo.Id, 24m), (analise.Itens[0].ProdutoId, analise.Itens[0].ProdutoEmbalagemId, analise.Itens[0].QuantidadeEstoque));
        Assert.Contains("Refrigerante 2L: nota 24 UN, recebido 30 UN", analise.Divergencias([recebimento]));

        var nota = new NotaEntradaInput
        {
            FornecedorId = fornecedor.Id, Numero = analise.Nota.Numero, Serie = analise.Nota.Serie, DataEmissao = analise.Nota.DataEmissao,
            ValorTotal = analise.Nota.ValorTotal, ChaveAcesso = analise.Nota.ChaveAcesso, Recebimentos = [recebimento],
            Vencimentos = [new() { Vencimento = Hoje.AddDays(30), Valor = analise.Nota.ValorTotal }]
        };
        var notaId = await compras.LancarNotaAsync(nota, Ct);
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            Assert.Equal(analise.Nota.ChaveAcesso, (await db.NotasFiscaisEntrada.SingleAsync(n => n.Id == notaId, Ct)).ChaveAcesso);
        await Assert.ThrowsAsync<RegraNegocioException>(() => importacao.AnalisarAsync(LeitorNfe.Ler(xml), Ct));

        // Cancelada, a mesma NF-e pode ser importada de novo.
        await compras.CancelarNotaAsync(notaId, "Lançada no pedido errado", Ct);
        Assert.Equal(fornecedor.Id, (await importacao.AnalisarAsync(LeitorNfe.Ler(xml), Ct)).FornecedorId);
    }
}
