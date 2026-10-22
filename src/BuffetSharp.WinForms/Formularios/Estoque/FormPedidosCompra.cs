using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Pedidos de compra aos fornecedores. No recebimento, informa-se só a nota fiscal, a data da compra e os vencimentos: todos
/// os itens entram no estoque e a nota vira uma conta a pagar (ou parcelas).
/// </summary>
internal sealed class FormPedidosCompra : FormListagem<PedidoCompraResumo>
{
    private readonly IConsultasCompras _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly CompraService _servico;
    private readonly BuffetOptions _opcoes;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormPedidosCompra(IConsultasCompras consultas, IConsultasLookup lookups, CompraService servico, BuffetOptions opcoes)
        : base("Pedidos de compra", "Vários produtos de um fornecedor numa nota só. Ao receber, informe a nota fiscal, a data e os vencimentos: tudo entra no estoque e em contas a pagar de uma vez.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;
        _opcoes = opcoes;

        _de = FiltroData("De", DateTime.Today.AddMonths(-2));
        _ate = FiltroData("Até", null);
        _status.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<StatusPedidoCompra>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Novo pedido", Icones.Adicionar, () => EditarAsync(0), destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } p ? EditarAsync(p.Id) : Task.CompletedTask);
        Separador();
        Botao("Receber (nota fiscal)", Icones.Confirmar, ReceberAsync, "Dá entrada de todos os itens e lança a nota em contas a pagar");
        Botao("Estornar recebimento", Icones.Desfazer, EstornarAsync, "Nota lançada errada: tira os itens do estoque e cancela as contas");
        Botao("Cancelar pedido", Icones.Cancelar, CancelarAsync);
        Separador();
        Botao("Imprimir", Icones.Imprimir, ImprimirAsync);
        Botao("WhatsApp", Icones.Mensagem, WhatsAppAsync, "Envia o pedido ao fornecedor");

        Grade.Texto("Código", p => p.Codigo, 18)
            .Data("Data", p => p.Data, peso: 18)
            .Texto("Fornecedor", p => p.Fornecedor, 40)
            .Numero("Itens", p => p.Itens, peso: 13)
            .Moeda("Total", p => p.Total, 20)
            .Status("Situação", p => p.Status, 18)
            .Data("Previsão", p => p.PrevisaoEntrega, peso: 18)
            .Texto("Nota fiscal", p => p.NotaFiscal, 18)
            .Data("Data da NF", p => p.DataCompra, peso: 20)
            .Texto("Pagamento", p => p.Pagamento, 20, p => p.Pagamento == "Pago" ? Tema.Sucesso : Tema.Info)
            .Texto("Observações", p => p.MotivoCancelamento ?? p.Observacoes, 40)
            .Busca(p => $"{p.Codigo} {p.Fornecedor} {p.NotaFiscal} {p.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarPedidosAsync(new FiltroPedidosCompra
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusPedidoCompra>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<PedidoCompraResumo> visiveis) =>
        $"{visiveis.Count} pedido(s)  •  em aberto: {visiveis.Count(p => p.Status == StatusPedidoCompra.Aberto)} " +
        $"({Textos.Moeda(visiveis.Where(p => p.Status == StatusPedidoCompra.Aberto).Sum(p => p.Total))})  •  " +
        $"recebidos: {Textos.Moeda(visiveis.Where(p => p.Status == StatusPedidoCompra.Recebido).Sum(p => p.Total))}";

    protected override Task AoAbrirAsync(PedidoCompraResumo item) =>
        item.Status == StatusPedidoCompra.Aberto ? EditarAsync(item.Id) : ImprimirAsync(item);

    private async Task EditarAsync(int id)
    {
        if (await DialogosPedidoCompra.EditarAsync(this, _servico, _lookups, id) is { } salvo)
        {
            await CarregarAsync();
            Grade.Selecionar(p => p.Id == salvo);
        }
    }

    private async Task ReceberAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        if (pedido.Status != StatusPedidoCompra.Aberto)
        {
            Mensagens.Aviso(this, "Selecione um pedido em aberto.");
            return;
        }
        if (!DialogosPedidoCompra.Receber(this, _servico, pedido))
            return;
        await CarregarAsync();
        Grade.Selecionar(p => p.Id == pedido.Id);
    }

    private async Task EstornarAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        if (pedido.Status != StatusPedidoCompra.Recebido)
        {
            Mensagens.Aviso(this, "Selecione um pedido recebido.");
            return;
        }
        var motivo = Dialogos.PedirTexto(this, "Estornar recebimento", "Motivo",
            subtitulo: $"{pedido.Codigo} - NF {pedido.NotaFiscal}: os itens saem do estoque, as contas da nota são canceladas e o pedido volta a ficar aberto.");
        if (motivo is null)
            return;
        await _servico.EstornarRecebimentoAsync(pedido.Id, motivo);
        await CarregarAsync();
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        var motivo = Dialogos.PedirTexto(this, "Cancelar pedido", "Motivo", subtitulo: $"{pedido.Codigo} - {pedido.Fornecedor}");
        if (motivo is null)
            return;
        await _servico.CancelarAsync(pedido.Id, motivo);
        await CarregarAsync();
    }

    private Task ImprimirAsync() => Selecionado() is { } pedido ? ImprimirAsync(pedido) : Task.CompletedTask;

    private async Task ImprimirAsync(PedidoCompraResumo pedido)
    {
        var itens = await _consultas.ItensAsync(pedido.Id);
        var doc = new DocumentoImpresso($"Pedido de compra {pedido.Codigo}", _opcoes.NomeEmpresa);
        doc.Campos(3,
            ("Fornecedor", pedido.Fornecedor),
            ("Data", pedido.Data.ToString("dd/MM/yyyy")),
            ("Previsão de entrega", pedido.PrevisaoEntrega?.ToString("dd/MM/yyyy") ?? "-"),
            ("Situação", Textos.Exibir(pedido.Status)),
            ("Nota fiscal", pedido.NotaFiscal ?? "-"),
            ("Telefone", pedido.TelefoneFornecedor ?? "-"));
        doc.Tabela(["Produto", "Quantidade", "Un.", "Custo unitário", "Total"],
            itens.Select(i => new[] { i.Produto, i.Quantidade.ToString("N3"), i.Unidade, i.CustoUnitario.ToString("C4"), Textos.Moeda(i.Total) }).ToList(),
            [4, 1.3f, 0.8f, 1.5f, 1.5f], [false, true, false, true, true]);
        doc.Total("Total do pedido", Textos.Moeda(itens.Sum(i => i.Total)));
        if (!string.IsNullOrWhiteSpace(pedido.Observacoes))
            doc.Paragrafo(pedido.Observacoes);
        doc.Visualizar(this);
    }

    private async Task WhatsAppAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        var itens = await _consultas.ItensAsync(pedido.Id);
        var texto = $"Olá! Aqui é do *{_opcoes.NomeEmpresa}*. Segue o pedido de compra *{pedido.Codigo}*:\n\n" +
                    string.Join("\n", itens.Select(i => $"• {i.Produto}: {i.Quantidade:0.###} {i.Unidade}")) +
                    (pedido.PrevisaoEntrega is { } previsao ? $"\n\nEntrega até {previsao:dd/MM/yyyy}." : "") +
                    "\n\nPode me confirmar o valor e o prazo? Obrigado!";
        DialogosEnvio.WhatsApp(this, new MensagemWhatsApp(pedido.TelefoneFornecedor ?? "", texto), $"pedido para {pedido.Fornecedor}");
    }
}
