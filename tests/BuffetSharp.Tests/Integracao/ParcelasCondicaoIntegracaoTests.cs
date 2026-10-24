using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Condição de pagamento com cada parcela definida (prazo e %): cadastro, nota de entrada, evento e contrato.</summary>
[Collection(ColecaoBanco.Nome)]
public class ParcelasCondicaoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private Task<CondicaoPagamento> SalvarAsync(string nome, params (int Dias, decimal Percentual)[] parcelas) =>
        banco.Obter<ICadastroService<CondicaoPagamento>>().SalvarAsync(new CondicaoPagamento
        {
            Nome = $"{nome} {Guid.NewGuid():N}"[..30],
            Parcelas = parcelas.Select(p => new CondicaoPagamentoParcela { Dias = p.Dias, Percentual = p.Percentual }).ToList()
        }, Ct);

    [Fact]
    public async Task Cadastro_grava_altera_e_recusa_parcelas_que_nao_somam_cem()
    {
        var cadastro = banco.Obter<ICadastroService<CondicaoPagamento>>();
        var condicao = await SalvarAsync("40/30/30", (0, 40), (30, 30), (60, 30));

        var lida = (await cadastro.ObterAsync(condicao.Id, Ct))!;
        Assert.Equal([0, 30, 60], lida.Parcelas.OrderBy(p => p.Dias).Select(p => p.Dias));
        Assert.Equal("40% à vista, 30% em 30 dias e 30% em 60 dias", lida.Resumo);

        // Tira a última e passa os 30% para a segunda.
        lida.Parcelas.RemoveAll(p => p.Dias == 60);
        lida.Parcelas.Single(p => p.Dias == 30).Percentual = 60;
        await cadastro.SalvarAsync(lida, Ct);
        Assert.Equal([40m, 60m], (await cadastro.ObterAsync(condicao.Id, Ct))!.Parcelas.OrderBy(p => p.Dias).Select(p => p.Percentual));

        lida.Parcelas.Single(p => p.Dias == 30).Percentual = 50;
        await Assert.ThrowsAsync<ValidacaoException>(() => cadastro.SalvarAsync(lida, Ct));
    }

    [Fact]
    public async Task Nota_de_entrada_usa_os_prazos_e_percentuais_da_condicao()
    {
        var condicao = await SalvarAsync("28/35/42", (28, 30), (35, 30), (42, 40));

        var vencimentos = await banco.Obter<CompraService>().VencimentosAsync(condicao.Id, 1_000m, Hoje, Ct);

        Assert.Equal([Hoje.AddDays(28), Hoje.AddDays(35), Hoje.AddDays(42)], vencimentos.Select(v => v.Vencimento));
        Assert.Equal([300m, 300m, 400m], vencimentos.Select(v => v.Valor));
        var (condicoes, _) = await banco.Obter<CompraService>().OpcoesPagamentoAsync(Ct);
        Assert.Equal(3, condicoes.Single(c => c.Id == condicao.Id).Parcelas.Count);
    }

    [Fact]
    public async Task Plano_do_evento_e_contrato_seguem_as_parcelas_da_condicao()
    {
        var condicao = await SalvarAsync("Metade e metade", (0, 50), (30, 50));
        var lookups = banco.Obter<IConsultasLookup>();
        var espaco = (await lookups.ListarAsync(TipoLookup.Espacos, ct: Ct)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
        var eventos = banco.Obter<EventoService>();
        var eventoId = await eventos.SalvarAsync(new EventoInput
        {
            Titulo = "Evento com parcelas definidas",
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espaco.Id,
            Data = Hoje.AddYears(7).AddDays(11),
            HoraInicio = new TimeOnly(9, 0),
            HoraFim = new TimeOnly(11, 0),
            Convidados = 40,
            ValorTotal = 8_000m,
            CondicaoPagamentoId = condicao.Id
        }, Ct);

        var plano = await eventos.SugerirPlanoAsync(eventoId, condicao.Id, Ct);
        Assert.Equal([Hoje, Hoje.AddDays(30)], plano.Vencimentos!.Select(v => v.Vencimento));
        Assert.Equal([4_000m, 4_000m], plano.Vencimentos!.Select(v => v.Valor));

        await eventos.GerarParcelasAsync(eventoId, plano, Ct);
        var parcelas = (await eventos.ObterDetalheAsync(eventoId, Ct)).Evento.Parcelas.OrderBy(p => p.Vencimento).ToList();
        Assert.Equal([4_000m, 4_000m], parcelas.Select(p => p.Valor));
        Assert.Equal([Hoje, Hoje.AddDays(30)], parcelas.Select(p => p.Vencimento));

        Assert.Equal("50% à vista e 50% em 30 dias",
            await banco.Obter<ContratoService>().PreVisualizarAsync(eventoId, "{{CONDICAO_PAGAMENTO}}", Ct));
    }

    [Fact]
    public async Task Nota_de_entrada_com_forma_por_parcela_gera_contas_com_formas_diferentes()
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento, ct: Ct);
        var (pix, boleto) = (formas.Single(f => f.Nome == "PIX").Id, formas.Single(f => f.Nome == "Boleto Bancário").Id);
        var condicao = await banco.Obter<ICadastroService<CondicaoPagamento>>().SalvarAsync(new CondicaoPagamento
        {
            Nome = $"PIX + boleto {Guid.NewGuid():N}"[..30],
            Parcelas =
            [
                new CondicaoPagamentoParcela { Dias = 0, Percentual = 20, FormaPagamentoId = pix },
                new CondicaoPagamentoParcela { Dias = 28, Percentual = 80 }
            ]
        }, Ct);
        var compras = banco.Obter<CompraService>();
        var vencimentos = await compras.VencimentosAsync(condicao.Id, 500m, Hoje, Ct);
        Assert.Equal([pix, (int?)null], vencimentos.Select(v => v.FormaPagamentoId));

        var fornecedor = (await lookups.ListarAsync(TipoLookup.Fornecedores, ct: Ct)).First().Id;
        var pedido = compras.Novo(fornecedor);
        pedido.CondicaoPagamentoId = condicao.Id;
        pedido.FormaPagamentoId = boleto; // forma geral: boleto
        pedido.Itens = [new PedidoCompraItemInput { ProdutoId = (await lookups.ListarAsync(TipoLookup.Produtos, ct: Ct)).First().Id, Quantidade = 10, CustoUnitario = 50m }];
        var id = await compras.SalvarAsync(pedido, Ct);
        var item = Assert.Single(await banco.Obter<IConsultasCompras>().ItensAsync(id, Ct));
        var recebimento = await compras.ReceberAsync(new RecebimentoInput { PedidoId = id, Data = Hoje, Itens = [new(item.Id, 10)] }, Ct);

        var numero = $"FP{Guid.NewGuid():N}"[..12];
        await compras.LancarNotaAsync(new NotaEntradaInput
        {
            FornecedorId = fornecedor, Numero = numero, DataEmissao = Hoje, ValorTotal = 500m, Recebimentos = [recebimento],
            FormaPagamentoId = boleto, Vencimentos = vencimentos.ToList()
        }, Ct);
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var contas = await db.ContasPagar.Where(c => c.Documento == numero).OrderBy(c => c.Vencimento).ToListAsync(Ct);
        Assert.Equal([(100m, pix), (400m, boleto)], contas.Select(c => (c.Valor, c.FormaPagamentoId!.Value)));
    }

    [Fact]
    public async Task Compra_parcelada_em_contas_a_pagar_segue_as_parcelas_da_condicao()
    {
        var condicao = await SalvarAsync("20/80", (10, 20), (40, 80));
        var financeiro = banco.Obter<FinanceiroService>();

        var plano = await financeiro.SugerirPlanoAsync(condicao.Id, 500m, null, Ct);
        var documento = $"P{Guid.NewGuid():N}"[..10];
        await financeiro.ParcelarAsync(new ParcelamentoInput
        {
            Descricao = "Compra parcelada de teste", ValorTotal = 500m, Documento = documento, Plano = plano
        }, Ct);

        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var contas = await db.ContasPagar.Where(c => c.Documento == documento).OrderBy(c => c.Vencimento).ToListAsync(Ct);
        Assert.Equal([100m, 400m], contas.Select(c => c.Valor));
        Assert.Equal([Hoje.AddDays(10), Hoje.AddDays(40)], contas.Select(c => c.Vencimento));
    }
}
