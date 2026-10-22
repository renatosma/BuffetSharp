using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Recebimentos dos pedidos de compra: o que chegou e entrou no estoque. Os que ainda não têm nota fiscal ficam "a faturar"
/// até a nota de entrada ser lançada.
/// </summary>
internal sealed class FormRecebimentos : FormListagem<RecebimentoResumo>
{
    private readonly IConsultasCompras _consultas;
    private readonly CompraService _servico;
    private readonly ImportacaoNfeService _importacao;
    private readonly BuffetOptions _opcoes;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormRecebimentos(IConsultasCompras consultas, CompraService servico, ImportacaoNfeService importacao, BuffetOptions opcoes)
        : base("Recebimentos", "Mercadoria que chegou dos pedidos de compra (já no estoque). Lance a nota fiscal de entrada para gerar as contas a pagar: uma nota pode juntar vários recebimentos do mesmo fornecedor.")
    {
        _consultas = consultas;
        _servico = servico;
        _importacao = importacao;
        _opcoes = opcoes;

        _de = FiltroData("De", DateTime.Today.AddMonths(-2));
        _ate = FiltroData("Até", null);
        _status.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<StatusRecebimento>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Lançar nota fiscal", Icones.Pagina, LancarNotaAsync, "Fatura os recebimentos do fornecedor e gera as contas a pagar", destaque: true);
        Botao("Estornar recebimento", Icones.Desfazer, EstornarAsync, "Lançado errado ou devolvido: tira do estoque e volta ao saldo do pedido");
        Separador();
        Botao("Imprimir", Icones.Imprimir, ImprimirAsync, "Comprovante do recebimento");

        Grade.Texto("Código", r => r.Codigo, 16)
            .Data("Data", r => r.Data, peso: 15)
            .Texto("Pedido", r => r.Pedido, 14)
            .Texto("Fornecedor", r => r.Fornecedor, 36)
            .Numero("Itens", r => r.Itens, peso: 9)
            .Moeda("Valor", r => r.Total, 18)
            .Status("Situação", r => r.Status, 22)
            .Texto("Nota fiscal", r => r.NotaFiscal, 16)
            .Texto("Usuário", r => r.Usuario, 14)
            .Texto("Observações", r => r.MotivoEstorno ?? r.Observacoes, 40)
            .Busca(r => $"{r.Codigo} {r.Pedido} {r.Fornecedor} {r.NotaFiscal} {r.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarRecebimentosAsync(new FiltroRecebimentos
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusRecebimento>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<RecebimentoResumo> visiveis)
    {
        var aFaturar = visiveis.Where(r => r.Status == StatusRecebimento.AFaturar).ToList();
        return $"{visiveis.Count} recebimento(s)  •  aguardando nota: {aFaturar.Count} ({Textos.Moeda(aFaturar.Sum(r => r.Total))})";
    }

    protected override Task AoAbrirAsync(RecebimentoResumo item) => ImprimirAsync(item);

    private async Task LancarNotaAsync()
    {
        var selecionado = Selecionado();
        var marcados = selecionado is { Status: StatusRecebimento.AFaturar } ? new[] { selecionado.Id } : null;
        if (await DialogosNotaEntrada.LancarAsync(this, _servico, _consultas, _importacao, selecionado?.FornecedorId, marcados) is null)
            return;
        await CarregarAsync();
    }

    private async Task EstornarAsync()
    {
        if (Selecionado() is not { } recebimento)
            return;
        if (recebimento.Status != StatusRecebimento.AFaturar)
        {
            Mensagens.Aviso(this, recebimento.Status == StatusRecebimento.Faturado
                ? $"O recebimento está na nota fiscal {recebimento.NotaFiscal}. Cancele a nota antes (Compras › Notas fiscais de entrada)."
                : "Este recebimento já foi estornado.");
            return;
        }
        var motivo = Dialogos.PedirTexto(this, "Estornar recebimento", "Motivo",
            subtitulo: $"{recebimento.Codigo} ({recebimento.Pedido} - {recebimento.Fornecedor}): os itens saem do estoque e voltam ao saldo do pedido.");
        if (motivo is null)
            return;
        await _servico.EstornarRecebimentoAsync(recebimento.Id, motivo);
        await CarregarAsync();
    }

    private Task ImprimirAsync() => Selecionado() is { } recebimento ? ImprimirAsync(recebimento) : Task.CompletedTask;

    private async Task ImprimirAsync(RecebimentoResumo recebimento)
    {
        var itens = await _consultas.ItensRecebimentoAsync(recebimento.Id);
        var doc = new DocumentoImpresso($"Recebimento {recebimento.Codigo}", _opcoes.NomeEmpresa);
        doc.Campos(3,
            ("Fornecedor", recebimento.Fornecedor),
            ("Pedido", recebimento.Pedido),
            ("Data", recebimento.Data.ToString("dd/MM/yyyy")),
            ("Situação", Textos.Exibir(recebimento.Status)),
            ("Nota fiscal", recebimento.NotaFiscal ?? "-"),
            ("Recebido por", recebimento.Usuario ?? "-"));
        doc.Tabela(["Produto", "Quantidade", "Un.", "Custo unitário", "Total"],
            itens.Select(i => new[] { i.Produto, i.Quantidade.ToString("N3"), i.Unidade, i.CustoUnitario.ToString("C4"), Textos.Moeda(i.Total) }).ToList(),
            [4, 1.3f, 0.8f, 1.5f, 1.5f], [false, true, false, true, true]);
        doc.Total("Total recebido", Textos.Moeda(itens.Sum(i => i.Total)));
        if (!string.IsNullOrWhiteSpace(recebimento.Observacoes))
            doc.Paragrafo(recebimento.Observacoes);
        doc.Assinaturas("Conferido por");
        doc.Visualizar(this);
    }
}
