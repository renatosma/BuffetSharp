using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>Filtros comuns de títulos financeiros (vencimento, situação, somente vencidos).</summary>
internal abstract class FormTitulos<T> : FormListagem<T> where T : class
{
    protected readonly DateTimePicker De;
    protected readonly DateTimePicker Ate;
    protected readonly ComboBox Situacao = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    protected readonly CheckBox SomenteVencidos = new() { Text = "Somente vencidos", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    protected FormTitulos(string titulo, string subtitulo) : base(titulo, subtitulo)
    {
        De = FiltroData("Vencimento de", DateTime.Today.AddDays(-60));
        Ate = FiltroData("até", DateTime.Today.AddDays(90));
        Situacao.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<StatusTitulo>()).ToList();
        Filtro("Situação", Situacao);
        Filtros.Controls.Add(SomenteVencidos);
        De.ValueChanged += async (_, _) => await RecarregarAsync();
        Ate.ValueChanged += async (_, _) => await RecarregarAsync();
        Situacao.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();
        SomenteVencidos.CheckedChanged += async (_, _) => await RecarregarAsync();
    }

    protected FiltroTitulos FiltroAtual() => new()
    {
        VencimentoDe = ValorData(De),
        VencimentoAte = ValorData(Ate),
        Status = (Situacao.SelectedItem as OpcaoEnum<StatusTitulo>)?.Valor,
        SomenteVencidos = SomenteVencidos.Checked
    };

    protected static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);
}

internal sealed class FormContasReceber : FormTitulos<ParcelaResumo>
{
    private readonly IConsultasFinanceiro _consultas;
    private readonly IConsultasContas _contas;
    private readonly IConsultasLookup _lookups;
    private readonly BaixaService _baixas;
    private readonly Navegador _navegador;

    public FormContasReceber(IConsultasFinanceiro consultas, IConsultasContas contas, IConsultasLookup lookups, BaixaService baixas,
        Navegador navegador)
        : base("Contas a receber", "Parcelas dos eventos. Os planos de pagamento são gerados na tela do evento.")
    {
        _consultas = consultas;
        _contas = contas;
        _lookups = lookups;
        _baixas = baixas;
        _navegador = navegador;

        Botao("Registrar recebimento", Icones.Cartao, ReceberAsync, destaque: true);
        Botao("Estornar", Icones.Desfazer, async () =>
        {
            if (Selecionado() is not { } p)
                return;
            if (p.Status != StatusTitulo.Pago)
            {
                Mensagens.Aviso(this, "Selecione uma parcela recebida.");
                return;
            }
            if (await DialogoBaixa.EstornarAsync(this, _baixas, TipoBaixa.Recebimento, p.Id, $"{p.Descricao} ({p.Cliente})"))
                await CarregarAsync();
        });
        Botao("Abrir evento", Icones.Bandeira, () => Selecionado() is { } p ? AoAbrirAsync(p) : Task.CompletedTask);

        Grade.Data("Vencimento", p => p.Vencimento, peso: 20, cor: p => p.DiasAtraso > 0 ? Tema.Perigo : null)
            .Texto("Cliente", p => p.Cliente, 40)
            .Texto("Evento", p => $"{p.EventoCodigo} - {p.EventoTitulo}", 55)
            .Texto("Parcela", p => p.Descricao, 25)
            .Moeda("Valor", p => p.Valor, 20)
            .Status("Situação", p => p.Status, 16)
            .Numero("Atraso (dias)", p => p.DiasAtraso, peso: 15, cor: p => p.DiasAtraso > 0 ? Tema.Perigo : null)
            .Data("Pago em", p => p.DataPagamento, peso: 18)
            .Moeda("Valor pago", p => p.ValorPago, 20)
            .Texto("Forma", p => p.FormaPagamento, 22)
            .Busca(p => $"{p.Cliente} {p.EventoCodigo} {p.EventoTitulo} {p.Descricao} {p.FormaPagamento}");
    }

    protected override async Task CarregarAsync() => Exibir(await _consultas.ListarParcelasAsync(FiltroAtual(), Hoje));

    protected override string TextoRodape(IReadOnlyList<ParcelaResumo> v) =>
        $"{v.Count} parcela(s)  •  em aberto: {Textos.Moeda(v.Where(p => p.Status == StatusTitulo.Aberto).Sum(p => p.Valor))}" +
        $"  •  vencido: {Textos.Moeda(v.Where(p => p.DiasAtraso > 0).Sum(p => p.Valor))}" +
        $"  •  recebido: {Textos.Moeda(v.Where(p => p.Status == StatusTitulo.Pago).Sum(p => p.ValorPago ?? 0))}";

    protected override Task AoAbrirAsync(ParcelaResumo item)
    {
        _navegador.Abrir($"evento:{item.EventoId}", () => _navegador.Criar<FormEventoDetalhe>(item.EventoId));
        return Task.CompletedTask;
    }

    private async Task ReceberAsync()
    {
        if (Selecionado() is not { } parcela)
            return;
        if (parcela.Status != StatusTitulo.Aberto)
        {
            Mensagens.Aviso(this, "Selecione uma parcela em aberto.");
            return;
        }
        if (!await DialogoBaixa.ExibirAsync(this, _contas, _lookups, _baixas, TipoBaixa.Recebimento, parcela.ClienteId, parcela.Cliente, [parcela.Id]))
            return;
        await CarregarAsync();
        Grade.Selecionar(p => p.Id == parcela.Id);
    }
}

internal sealed class FormContasPagar : FormTitulos<ContaPagarResumo>
{
    private readonly IConsultasFinanceiro _consultas;
    private readonly IConsultasContas _contas;
    private readonly IConsultasLookup _lookups;
    private readonly FinanceiroService _financeiro;
    private readonly BaixaService _baixas;

    public FormContasPagar(IConsultasFinanceiro consultas, IConsultasContas contas, IConsultasLookup lookups, FinanceiroService financeiro,
        BaixaService baixas)
        : base("Contas a pagar", "Despesas do buffet. Vincule ao evento os custos extras para apurar o resultado de cada festa.")
    {
        _consultas = consultas;
        _contas = contas;
        _lookups = lookups;
        _financeiro = financeiro;
        _baixas = baixas;

        Botao("Nova conta", Icones.Adicionar, () => EditarAsync(0), destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } c ? EditarAsync(c.Id) : Task.CompletedTask);
        Botao("Pagar", Icones.Banco, PagarAsync);
        Botao("Estornar", Icones.Desfazer, async () =>
        {
            if (Selecionado() is not { } c)
                return;
            if (c.Status != StatusTitulo.Pago)
            {
                Mensagens.Aviso(this, "Selecione uma conta paga.");
                return;
            }
            if (await DialogoBaixa.EstornarAsync(this, _baixas, TipoBaixa.Pagamento, c.Id, c.Descricao))
                await CarregarAsync();
        });
        Botao("Cancelar", Icones.Cancelar, () => AcaoAsync(c => _financeiro.CancelarContaAsync(c.Id), "Cancelar a conta"));
        Botao("Excluir", Icones.Excluir, () => AcaoAsync(c => _financeiro.ExcluirContaAsync(c.Id), "Excluir a conta"));

        Grade.Data("Vencimento", c => c.Vencimento, peso: 20, cor: c => c.DiasAtraso > 0 ? Tema.Perigo : null)
            .Texto("Descrição", c => c.Descricao, 60)
            .Texto("Categoria", c => Textos.Exibir(c.Categoria), 22)
            .Texto("Fornecedor", c => c.Fornecedor, 35)
            .Texto("Evento", c => c.Evento, 40)
            .Moeda("Valor", c => c.Valor, 20)
            .Status("Situação", c => c.Status, 16)
            .Data("Pago em", c => c.DataPagamento, peso: 18)
            .Moeda("Valor pago", c => c.ValorPago, 20)
            .Busca(c => $"{c.Descricao} {c.Categoria} {c.Fornecedor} {c.Evento}");
    }

    protected override async Task CarregarAsync() => Exibir(await _consultas.ListarContasPagarAsync(FiltroAtual(), Hoje));

    protected override string TextoRodape(IReadOnlyList<ContaPagarResumo> v) =>
        $"{v.Count} conta(s)  •  em aberto: {Textos.Moeda(v.Where(c => c.Status == StatusTitulo.Aberto).Sum(c => c.Valor))}" +
        $"  •  vencido: {Textos.Moeda(v.Where(c => c.DiasAtraso > 0).Sum(c => c.Valor))}" +
        $"  •  pago: {Textos.Moeda(v.Where(c => c.Status == StatusTitulo.Pago).Sum(c => c.ValorPago ?? 0))}";

    protected override Task AoAbrirAsync(ContaPagarResumo item) => EditarAsync(item.Id);

    private async Task EditarAsync(int id)
    {
        var input = id == 0 ? new ContaPagarInput { Vencimento = Hoje.AddDays(7) } : await _financeiro.ObterContaAsync(id);
        var fornecedores = await _lookups.ListarAsync(TipoLookup.Fornecedores);
        var eventos = await _lookups.ListarAsync(TipoLookup.EventosConfirmados);

        var e = new EditorCampos();
        e.Texto("Descrição", () => input.Descricao, v => input.Descricao = v ?? "", nameof(input.Descricao), 150, span: 2);
        e.Opcoes("Categoria", () => input.Categoria, v => input.Categoria = v);
        e.Decimal("Valor (R$)", () => input.Valor, v => input.Valor = v, nameof(input.Valor));
        e.Data("Vencimento", () => input.Vencimento, v => input.Vencimento = v ?? Hoje, nameof(input.Vencimento));
        if (id == 0)
            e.Inteiro("Repetir por (meses)", () => input.Repeticoes, v => input.Repeticoes = v, minimo: 1, maximo: 24);
        e.Lista("Fornecedor", fornecedores, () => input.FornecedorId, v => input.FornecedorId = v, permitirVazio: true, span: 2);
        e.Lista("Vincular ao evento (custo extra do evento)", eventos, () => input.EventoId, v => input.EventoId = v, permitirVazio: true, span: 2);
        e.Texto("Observação", () => input.Observacao, v => input.Observacao = v, maximo: 500, span: 2, linhas: 2);

        using var dialogo = new FormEdicao(id == 0 ? "Nova conta a pagar" : "Editar conta a pagar",
            id == 0 ? "Use \"Repetir por\" para despesas recorrentes (aluguel, energia...)." : null,
            e, () => _financeiro.SalvarContaAsync(input), 640, somenteLeitura: input.Status != StatusTitulo.Aberto);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private async Task PagarAsync()
    {
        if (Selecionado() is not { } conta)
            return;
        if (conta.Status != StatusTitulo.Aberto)
        {
            Mensagens.Aviso(this, "Selecione uma conta em aberto.");
            return;
        }
        if (!await DialogoBaixa.ExibirAsync(this, _contas, _lookups, _baixas, TipoBaixa.Pagamento, conta.FornecedorId,
                conta.Fornecedor ?? "Contas sem fornecedor", [conta.Id]))
            return;
        await CarregarAsync();
        Grade.Selecionar(c => c.Id == conta.Id);
    }

    private async Task AcaoAsync(Func<ContaPagarResumo, Task> acao, string verbo)
    {
        if (Selecionado() is not { } conta || !Mensagens.Confirmar(this, $"{verbo} \"{conta.Descricao}\"?", perigoso: true))
            return;
        await acao(conta);
        await CarregarAsync();
    }
}
