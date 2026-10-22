using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Notas fiscais de entrada: faturam os recebimentos dos pedidos de compra e geram as contas a pagar com os vencimentos dos
/// boletos. Nota lançada errada é cancelada (as contas são canceladas e os recebimentos voltam a aguardar nota).
/// </summary>
internal sealed class FormNotasEntrada : FormListagem<NotaEntradaResumo>
{
    private readonly IConsultasCompras _consultas;
    private readonly CompraService _servico;
    private readonly ImportacaoNfeService _importacao;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormNotasEntrada(IConsultasCompras consultas, CompraService servico, ImportacaoNfeService importacao)
        : base("Notas fiscais de entrada", "Cada nota fatura um ou mais recebimentos do mesmo fornecedor e vira uma conta a pagar (ou parcelas, conforme os boletos). Pelo XML da NF-e, a nota vem preenchida; numa compra sem pedido, os itens também entram no estoque.")
    {
        _consultas = consultas;
        _servico = servico;
        _importacao = importacao;

        _de = FiltroData("Emissão de", DateTime.Today.AddMonths(-2));
        _ate = FiltroData("até", null);
        _status.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<StatusNotaEntrada>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Importar XML da NF-e", Icones.Exportar, ImportarAsync, "Lê o XML que o fornecedor mandou: com pedido ou sem pedido", destaque: true);
        Botao("Lançar nota", Icones.Adicionar, LancarAsync, "Escolha o fornecedor e os recebimentos que vieram na nota");
        Botao("Cancelar nota", Icones.Cancelar, CancelarAsync, "Lançada errada: cancela as contas e os recebimentos voltam a aguardar nota");

        Grade.Texto("Número", n => n.Descricao, 16)
            .Data("Emissão", n => n.DataEmissao, peso: 15)
            .Texto("Fornecedor", n => n.Fornecedor, 36)
            .Numero("Recebimentos", n => n.Recebimentos, peso: 14)
            .Moeda("Mercadoria", n => n.ValorProdutos, 18)
            .Moeda("Valor da nota", n => n.ValorTotal, 18)
            .Status("Situação", n => n.Status, 14)
            .Texto("Pagamento", n => n.Pagamento, 18, n => n.Pagamento == "Pago" ? Tema.Sucesso : Tema.Info)
            .Texto("Observações", n => n.MotivoCancelamento ?? n.Observacoes, 40)
            .Busca(n => $"{n.Numero} {n.Fornecedor} {n.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarNotasAsync(new FiltroNotasEntrada
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusNotaEntrada>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<NotaEntradaResumo> visiveis)
    {
        var lancadas = visiveis.Where(n => n.Status == StatusNotaEntrada.Lancada).ToList();
        return $"{visiveis.Count} nota(s)  •  lançadas: {lancadas.Count} ({Textos.Moeda(lancadas.Sum(n => n.ValorTotal))})";
    }

    private async Task LancarAsync()
    {
        if (await DialogosNotaEntrada.LancarAsync(this, _servico, _consultas, _importacao) is { } id)
        {
            await CarregarAsync();
            Grade.Selecionar(n => n.Id == id);
        }
    }

    private async Task ImportarAsync()
    {
        if (await DialogosNotaEntrada.ImportarAsync(this, _importacao, _servico, _consultas) is { } id)
        {
            await CarregarAsync();
            Grade.Selecionar(n => n.Id == id);
        }
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } nota)
            return;
        if (nota.Status != StatusNotaEntrada.Lancada)
        {
            Mensagens.Aviso(this, "Esta nota já está cancelada.");
            return;
        }
        var motivo = Dialogos.PedirTexto(this, "Cancelar nota fiscal de entrada", "Motivo",
            subtitulo: $"NF {nota.Descricao} - {nota.Fornecedor}: as contas a pagar são canceladas e os {nota.Recebimentos} recebimento(s) voltam " +
                       "a aguardar nota (a mercadoria continua no estoque).");
        if (motivo is null)
            return;
        await _servico.CancelarNotaAsync(nota.Id, motivo);
        await CarregarAsync();
    }
}
