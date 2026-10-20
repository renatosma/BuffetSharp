using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Pagamento da equipe: as diárias dos eventos realizados (escalas com presença registrada) viram uma conta a pagar por
/// funcionário, na categoria Pessoal.
/// </summary>
internal sealed class FormPagamentoEquipe : FormListagem<DiariaFuncionario>
{
    private readonly IConsultasEquipe _consultas;
    private readonly EquipeService _servico;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly DateTimePicker _vencimento;
    private readonly Grade<FechamentoEquipeResumo> _fechamentos = new();

    public FormPagamentoEquipe(IConsultasEquipe consultas, EquipeService servico)
        : base("Pagamento da equipe", "Diárias dos eventos realizados no período (só as escalas com presença). Gere as contas a pagar para quitar a equipe.")
    {
        _consultas = consultas;
        _servico = servico;

        var hoje = DateTime.Today;
        var inicioSemana = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7) - 7);
        _de = FiltroData("Eventos de", inicioSemana, opcional: false);
        _ate = FiltroData("até", inicioSemana.AddDays(6), opcional: false);
        _vencimento = FiltroData("Vencimento das contas", hoje.AddDays(2), opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Gerar conta", Icones.Confirmar, FecharAsync, "Lança as diárias em aberto do funcionário selecionado em contas a pagar", destaque: true);
        Botao("Gerar todas", Icones.Dinheiro, FecharTodosAsync, "Lança as diárias em aberto de todos os funcionários do período");
        Botao("Detalhar", Icones.Lista, DetalharAsync, "Eventos e diárias do funcionário");
        Botao("Desfazer fechamento", Icones.Desfazer, DesfazerAsync, "Selecione o fechamento na lista de baixo (somente se a conta não foi paga)");

        Grade.Texto("Funcionário", d => d.Funcionario, 40)
            .Texto("Cargo", d => d.Cargo, 25)
            .Numero("Diárias", d => d.Diarias, peso: 14)
            .Numero("Faltas", d => d.Ausencias, peso: 12, cor: d => d.Ausencias > 0 ? Tema.Alerta : null)
            .Moeda("Total", d => d.Valor, 22)
            .Moeda("Já lançado", d => d.ValorFechado, 22, _ => Tema.Sucesso)
            .Moeda("Em aberto", d => d.ValorEmAberto, 22, d => d.ValorEmAberto > 0 ? Tema.Primaria : null)
            .Busca(d => $"{d.Funcionario} {d.Cargo}");

        _fechamentos.Texto("Funcionário", f => f.Funcionario, 35)
            .Texto("Período", f => $"{f.De:dd/MM/yyyy} a {f.Ate:dd/MM/yyyy}", 30)
            .Numero("Diárias", f => f.Diarias, peso: 12)
            .Moeda("Valor", f => f.Valor, 20)
            .Data("Vencimento", f => f.Vencimento, peso: 18)
            .Data("Lançado em", f => f.CriadoEm, peso: 18)
            .Texto("Conta a pagar", f => Textos.Exibir<StatusTitulo>(f.SituacaoConta), 18,
                f => f.SituacaoConta == nameof(StatusTitulo.Pago) ? Tema.Sucesso : Tema.Info);
        var cartao = Tema.Cartao(new Padding(10));
        cartao.Dock = DockStyle.Fill;
        cartao.Controls.Add(_fechamentos.Controle);
        cartao.Controls.Add(new Label { Text = "Contas geradas (fechamentos)", Dock = DockStyle.Top, Height = 26, Font = Tema.FonteNegrito });
        var historico = new Panel { Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(20, 4, 20, 4) };
        historico.Controls.Add(cartao);
        Controls.Add(historico);
        Controls.SetChildIndex(historico, 1);
    }

    private DateOnly De => DateOnly.FromDateTime(_de.Value);
    private DateOnly Ate => DateOnly.FromDateTime(_ate.Value);
    private DateOnly Vencimento => DateOnly.FromDateTime(_vencimento.Value);

    protected override async Task CarregarAsync()
    {
        Exibir(await _consultas.ApurarAsync(De, Ate));
        _fechamentos.Carregar(await _consultas.ListarFechamentosAsync());
    }

    protected override string TextoRodape(IReadOnlyList<DiariaFuncionario> visiveis) =>
        $"{visiveis.Count} funcionário(s)  •  {visiveis.Sum(d => d.Diarias)} diária(s)  •  total {Textos.Moeda(visiveis.Sum(d => d.Valor))}  •  " +
        $"em aberto: {Textos.Moeda(visiveis.Sum(d => d.ValorEmAberto))}";

    private async Task FecharAsync()
    {
        if (Selecionado() is not { } funcionario)
            return;
        if (funcionario.ValorEmAberto <= 0)
        {
            Mensagens.Aviso(this, $"{funcionario.Funcionario} não tem diárias em aberto no período.");
            return;
        }
        if (!Mensagens.Confirmar(this, $"Lançar {funcionario.DiariasEmAberto} diária(s) de {funcionario.Funcionario} " +
                                       $"({Textos.Moeda(funcionario.ValorEmAberto)}) em contas a pagar, com vencimento em {Vencimento:dd/MM/yyyy}?",
                "Pagamento da equipe"))
            return;
        await _servico.FecharAsync(funcionario.FuncionarioId, De, Ate, Vencimento);
        await CarregarAsync();
    }

    private async Task FecharTodosAsync()
    {
        var emAberto = Grade.Itens.Where(d => d.ValorEmAberto > 0).ToList();
        if (emAberto.Count == 0)
        {
            Mensagens.Aviso(this, "Não há diárias em aberto no período.");
            return;
        }
        if (!Mensagens.Confirmar(this, $"Lançar as diárias em aberto de {emAberto.Count} funcionário(s) " +
                                       $"({Textos.Moeda(emAberto.Sum(d => d.ValorEmAberto))}) em contas a pagar, uma por funcionário?",
                "Pagamento da equipe"))
            return;
        var geradas = await _servico.FecharTodosAsync(De, Ate, Vencimento);
        await CarregarAsync();
        Mensagens.Info(this, $"{geradas} conta(s) a pagar gerada(s).");
    }

    private async Task DetalharAsync()
    {
        if (Selecionado() is not { } funcionario)
            return;
        var escalas = await _consultas.DetalharAsync(funcionario.FuncionarioId, De, Ate);
        var grade = new Grade<EscalaApurada>();
        grade.Data("Data", e => e.Inicio, "dd/MM/yyyy", 16).Texto("Evento", e => $"{e.EventoCodigo} - {e.Evento}", 55)
            .Texto("Função", e => e.Funcao, 22).Moeda("Diária", e => e.ValorDiaria, 18)
            .Texto("Presença", e => e.Presente ? "Presente" : "Faltou", 14, e => e.Presente ? null : Tema.Alerta)
            .Texto("Situação", e => !e.Presente ? "" : e.FechamentoEquipeId is null ? "Em aberto" : "Lançada", 14,
                e => e.FechamentoEquipeId is null ? Tema.Primaria : Tema.Sucesso);
        grade.Carregar(escalas);
        grade.Controle.Height = Px(320);
        var e = new EditorCampos(1);
        e.Livre(grade.Controle);
        using var dialogo = new FormEdicao($"Diárias de {funcionario.Funcionario}", $"Eventos realizados de {De:dd/MM/yyyy} a {Ate:dd/MM/yyyy}",
            e, () => Task.CompletedTask, 860, somenteLeitura: true);
        dialogo.ShowDialog(this);
    }

    private async Task DesfazerAsync()
    {
        if (_fechamentos.Selecionado is not { } fechamento)
        {
            Mensagens.Aviso(this, "Selecione o fechamento na lista \"Contas geradas\".");
            return;
        }
        if (!Mensagens.Confirmar(this, $"Desfazer o fechamento de {fechamento.Funcionario} ({fechamento.De:dd/MM} a {fechamento.Ate:dd/MM})? " +
                                       "A conta a pagar gerada será excluída e as diárias voltam a ficar em aberto.", "Desfazer", perigoso: true))
            return;
        await _servico.DesfazerAsync(fechamento.Id);
        await CarregarAsync();
    }
}
