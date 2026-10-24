using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Condição e forma de pagamento no orçamento (passam ao evento, à proposta e ao contrato) e no pedido de compra.</summary>
[Collection(ColecaoBanco.Nome)]
public class FormaCondicaoPagamentoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

    [Fact]
    public async Task Forma_do_orcamento_vai_para_a_proposta_o_evento_o_plano_e_o_contrato()
    {
        var pix = await Lookup(TipoLookup.FormasPagamento, f => f.Nome == "PIX");
        var condicao = await Lookup(TipoLookup.CondicoesPagamento, c => c.Nome == "30% de entrada + 5x");
        var orcamentos = banco.Obter<OrcamentoService>();
        var input = orcamentos.Novo();
        input.ClienteId = (await Lookup(TipoLookup.Clientes)).Id;
        input.TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id;
        input.PacoteId = (await Lookup(TipoLookup.Pacotes)).Id;
        input.EspacoId = (await Lookup(TipoLookup.Espacos, e => e.Numero >= 100)).Id;
        input.Data = Hoje.AddYears(6).AddDays(33);
        input.HoraInicio = new TimeOnly(8, 0);
        input.HoraFim = new TimeOnly(9, 30);
        input.Convidados = 100;
        input.CondicaoPagamentoId = condicao.Id;
        input.FormaPagamentoId = pix.Id;
        var id = await orcamentos.SalvarAsync(input, Ct);
        Assert.Equal((condicao.Id, pix.Id), ((await orcamentos.ObterAsync(id, Ct)).CondicaoPagamentoId, (await orcamentos.ObterAsync(id, Ct)).FormaPagamentoId));

        var whatsapp = await banco.Obter<PropostaService>().PrepararWhatsAppAsync(id, Ct);
        Assert.Contains("Forma de pagamento: PIX", Uri.UnescapeDataString(whatsapp.Link));

        // Aprovado, o evento herda a forma; o plano pela condição sugere a forma do evento.
        var eventoId = await orcamentos.AprovarAsync(id, "Evento com PIX", Ct);
        var eventos = banco.Obter<EventoService>();
        Assert.Equal(pix.Id, (await eventos.ObterParaEdicaoAsync(eventoId, Ct)).FormaPagamentoId);
        Assert.Equal(pix.Id, (await eventos.SugerirPlanoAsync(eventoId, condicao.Id, Ct)).FormaPagamentoId);
        Assert.Equal("PIX", await banco.Obter<ContratoService>().PreVisualizarAsync(eventoId, "{{FORMA_PAGAMENTO}}", Ct));

        // Trocada no evento, vale a nova; forma inexistente é recusada.
        var editar = await eventos.ObterParaEdicaoAsync(eventoId, Ct);
        var boleto = await Lookup(TipoLookup.FormasPagamento, f => f.Nome == "Boleto Bancário");
        editar.FormaPagamentoId = boleto.Id;
        await eventos.SalvarAsync(editar, Ct);
        Assert.Equal("Boleto Bancário", await banco.Obter<ContratoService>().PreVisualizarAsync(eventoId, "{{FORMA_PAGAMENTO}}", Ct));
        var invalida = await eventos.ObterParaEdicaoAsync(eventoId, Ct);
        invalida.FormaPagamentoId = 999_999;
        await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.SalvarAsync(invalida, Ct));
    }

    [Fact]
    public async Task Condicao_do_pedido_sugere_os_vencimentos_e_a_forma_vai_para_as_contas_da_nota()
    {
        var condicao = await banco.Obter<ICadastroService<CondicaoPagamento>>().SalvarAsync(new CondicaoPagamento
        {
            Nome = $"28/42 dias {Guid.NewGuid():N}"[..24], PercentualEntrada = 0, QuantidadeParcelas = 2, DiasPrimeiraParcela = 28,
            Mensal = false, IntervaloDias = 14
        }, Ct);
        var boleto = await Lookup(TipoLookup.FormasPagamento, f => f.Nome == "Boleto Bancário");
        var compras = banco.Obter<CompraService>();
        var produto = (await Lookup(TipoLookup.Produtos)).Id;
        var fornecedor = (await Lookup(TipoLookup.Fornecedores)).Id;

        var pedido = compras.Novo(fornecedor);
        pedido.CondicaoPagamentoId = condicao.Id;
        pedido.FormaPagamentoId = boleto.Id;
        pedido.Itens = [new PedidoCompraItemInput { ProdutoId = produto, Quantidade = 10, CustoUnitario = 22.9m }];
        var id = await compras.SalvarAsync(pedido, Ct);
        Assert.Equal((condicao.Id, boleto.Id), ((await compras.ObterAsync(id, Ct)).CondicaoPagamentoId, (await compras.ObterAsync(id, Ct)).FormaPagamentoId));
        var resumo = (await banco.Obter<IConsultasCompras>().ListarPedidosAsync(new FiltroPedidosCompra(), Ct)).Single(p => p.Id == id);
        Assert.Equal($"{condicao.Nome} • Boleto Bancário", resumo.Pagamento);

        var item = Assert.Single(await banco.Obter<IConsultasCompras>().ItensAsync(id, Ct));
        var recebimento = await compras.ReceberAsync(new RecebimentoInput { PedidoId = id, Data = Hoje, Itens = [new(item.Id, 10)] }, Ct);
        var pendente = (await banco.Obter<IConsultasCompras>().ListarRecebimentosAsync(new FiltroRecebimentos(), Ct)).Single(r => r.Id == recebimento);
        Assert.Equal((condicao.Id, boleto.Id), (pendente.CondicaoPagamentoId, pendente.FormaPagamentoId));

        // A condição dá os vencimentos a partir da emissão; os centavos da divisão ficam na última parcela.
        var vencimentos = await compras.VencimentosAsync(condicao.Id, 229.01m, Hoje, Ct);
        Assert.Equal([Hoje.AddDays(28), Hoje.AddDays(42)], vencimentos.Select(v => v.Vencimento));
        Assert.Equal([114.50m, 114.51m], vencimentos.Select(v => v.Valor));

        var numero = $"F{Guid.NewGuid():N}"[..12];
        await compras.LancarNotaAsync(new NotaEntradaInput
        {
            FornecedorId = fornecedor, Numero = numero, DataEmissao = Hoje, ValorTotal = 229m, Recebimentos = [recebimento],
            FormaPagamentoId = boleto.Id, Vencimentos = (await compras.VencimentosAsync(condicao.Id, 229m, Hoje, Ct)).ToList()
        }, Ct);
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var contas = await db.ContasPagar.Where(c => c.Documento == numero).ToListAsync(Ct);
        Assert.Equal(2, contas.Count);
        Assert.All(contas, c => Assert.Equal((boleto.Id, StatusTitulo.Aberto), (c.FormaPagamentoId, c.Status)));
    }
}
