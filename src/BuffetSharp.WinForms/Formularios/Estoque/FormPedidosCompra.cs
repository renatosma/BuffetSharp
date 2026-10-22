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
/// Pedidos de compra aos fornecedores (feitos à mão, pela lista de compras ou pela cotação). A mercadoria chega em um ou
/// mais recebimentos, que dão entrada no estoque; a nota fiscal de entrada fatura os recebimentos e gera as contas a pagar.
/// </summary>
internal sealed class FormPedidosCompra : FormListagem<PedidoCompraResumo>
{
    private readonly IConsultasCompras _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly CompraService _servico;
    private readonly BuffetOptions _opcoes;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormPedidosCompra(IConsultasCompras consultas, IConsultasLookup lookups, CompraService servico, BuffetOptions opcoes)
        : base("Pedidos de compra", "O que foi pedido a cada fornecedor. Ao chegar a mercadoria (inteira ou em partes), registre o recebimento: entra no estoque na hora. A nota fiscal vem depois, em Notas fiscais de entrada.")
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
        Botao("Receber mercadoria", Icones.Confirmar, ReceberAsync, "Registra o que chegou (tudo ou parte): entra no estoque agora");
        Botao("Encerrar saldo", Icones.Desfazer, EncerrarSaldoAsync, "O que falta não vem mais: encerra o pedido com o que foi recebido");
        Botao("Cancelar pedido", Icones.Cancelar, CancelarAsync);
        Separador();
        Botao("Imprimir", Icones.Imprimir, ImprimirAsync);
        Botao("WhatsApp", Icones.Mensagem, WhatsAppAsync, "Envia o pedido ao fornecedor");

        Grade.Texto("Código", p => p.Codigo, 16)
            .Data("Data", p => p.Data, peso: 16)
            .Texto("Fornecedor", p => p.Fornecedor, 36)
            .Numero("Itens", p => p.Itens, peso: 10)
            .Moeda("Total", p => p.Total, 18)
            .Status("Situação", p => p.Status, 22)
            .Numero("Recebido %", p => p.PercentualRecebido, "N0", 14,
                p => p.PercentualRecebido >= 100 ? Tema.Sucesso : p.PercentualRecebido > 0 ? Tema.Alerta : null)
            .Texto("Sem nota", p => p.AFaturar > 0 ? $"{p.AFaturar} receb." : "", 14, _ => Tema.Alerta)
            .Data("Previsão", p => p.PrevisaoEntrega, peso: 16)
            .Texto("Cotação", p => p.Cotacao, 14)
            .Texto("Observações", p => p.MotivoCancelamento ?? p.Observacoes, 40)
            .Busca(p => $"{p.Codigo} {p.Fornecedor} {p.Cotacao} {p.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarPedidosAsync(new FiltroPedidosCompra
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusPedidoCompra>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<PedidoCompraResumo> visiveis)
    {
        var pendentes = visiveis.Where(p => p.Status is StatusPedidoCompra.Aberto or StatusPedidoCompra.ParcialmenteRecebido).ToList();
        return $"{visiveis.Count} pedido(s)  •  a receber: {pendentes.Count} ({Textos.Moeda(pendentes.Sum(p => p.Total - p.Recebido))})  •  " +
               $"recebido: {Textos.Moeda(visiveis.Where(p => p.Status != StatusPedidoCompra.Cancelado).Sum(p => p.Recebido))}";
    }

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
        if (pedido.Status is not (StatusPedidoCompra.Aberto or StatusPedidoCompra.ParcialmenteRecebido))
        {
            Mensagens.Aviso(this, "Selecione um pedido em aberto ou recebido em parte.");
            return;
        }
        if (await DialogosPedidoCompra.ReceberAsync(this, _servico, _consultas, pedido) is not { } recebimento)
            return;
        await CarregarAsync();
        Grade.Selecionar(p => p.Id == pedido.Id);
        if (Mensagens.Confirmar(this, "Mercadoria no estoque. A nota fiscal já chegou? Você pode lançá-la agora ou depois, em " +
                                      "Compras › Notas fiscais de entrada (uma nota pode juntar vários recebimentos).", "Lançar a nota fiscal agora?") &&
            await DialogosNotaEntrada.LancarAsync(this, _servico, _consultas, pedido.FornecedorId, [recebimento]) is not null)
            await CarregarAsync();
    }

    private async Task EncerrarSaldoAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        if (pedido.Status != StatusPedidoCompra.ParcialmenteRecebido)
        {
            Mensagens.Aviso(this, "Selecione um pedido recebido em parte. Um pedido sem recebimento é cancelado.");
            return;
        }
        var motivo = Dialogos.PedirTexto(this, "Encerrar saldo", "Motivo",
            subtitulo: $"{pedido.Codigo} - {pedido.Fornecedor}: o que falta ({Textos.Moeda(pedido.Total - pedido.Recebido)}) não será mais entregue.");
        if (motivo is null)
            return;
        await _servico.EncerrarSaldoAsync(pedido.Id, motivo);
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
            ("Cotação", pedido.Cotacao ?? "-"),
            ("Telefone", pedido.TelefoneFornecedor ?? "-"));
        doc.Tabela(["Produto", "Quantidade", "Un.", "Recebido", "Custo unitário", "Total"],
            itens.Select(i => new[]
            {
                i.Produto, i.Quantidade.ToString("N3"), i.Unidade, i.QuantidadeRecebida.ToString("N3"), i.CustoUnitario.ToString("C4"),
                Textos.Moeda(i.Total)
            }).ToList(),
            [4, 1.3f, 0.8f, 1.3f, 1.5f, 1.5f], [false, true, false, true, true, true]);
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
