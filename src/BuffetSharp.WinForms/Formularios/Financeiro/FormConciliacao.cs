using System.ComponentModel;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Conciliação bancária com o extrato OFX: cada lançamento do banco aparece com o movimento do sistema correspondente
/// (sugerido pelo valor e pela data). Confirme as sugestões, lance no sistema o que só o banco tem (tarifas, rendimentos) e
/// feche a conta com o saldo do extrato.
/// </summary>
internal sealed class FormConciliacao : FormBase
{
    private sealed class Linha(ItemConciliacao item)
    {
        public ItemConciliacao Item { get; set; } = item;
        public bool Marcar { get; set; } = item.Situacao == SituacaoConciliacao.Sugerido;
        public string Data => Item.Extrato.Data.ToString("dd/MM/yyyy");
        public string Descricao => Item.Extrato.Descricao;
        public decimal Valor => Item.Extrato.Valor;
        public string Situacao => Item.Situacao switch
        {
            SituacaoConciliacao.Conciliado => "Conciliado",
            SituacaoConciliacao.Sugerido => Item.DiferencaDias == 0 ? "Sugerido" : $"Sugerido ({Item.DiferencaDias} dia(s) de diferença)",
            _ => "Só no banco"
        };
        public string Movimento => Item.Movimento is null ? "" : $"{Item.DataMovimento:dd/MM} - {Item.Movimento}";
    }

    private readonly ConciliacaoService _servico;
    private readonly ContaFinanceiraService _contas;
    private readonly int _contaId;
    private readonly DataGridView _grade = new() { Dock = DockStyle.Fill };
    private readonly BindingList<Linha> _linhas = [];
    private readonly Label _resumo = new() { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(4, 6, 4, 0) };
    private readonly Label _cabecalho = Tema.Subtitulo("Abra o extrato OFX exportado do internet banking.");
    private ExtratoOfx? _extrato;
    private AnaliseConciliacao? _analise;

    public bool Alterou { get; private set; }

    public FormConciliacao(ConciliacaoService servico, ContaFinanceiraService contas, int contaId, string nomeConta)
    {
        _servico = servico;
        _contas = contas;
        _contaId = contaId;
        Text = $"Conciliação - {nomeConta}";
        ClientSize = new Size(Px(1100), Px(640));
        MinimizeBox = false;
        ShowInTaskbar = false;

        Tema.EstilizarGrade(_grade);
        _grade.AutoGenerateColumns = false;
        _grade.ReadOnly = false;
        _grade.Columns.AddRange(
            new DataGridViewCheckBoxColumn { HeaderText = "Conciliar", DataPropertyName = nameof(Linha.Marcar), FillWeight = 10 },
            new DataGridViewTextBoxColumn { HeaderText = "Data", DataPropertyName = nameof(Linha.Data), FillWeight = 12, ReadOnly = true },
            new DataGridViewTextBoxColumn { HeaderText = "Extrato do banco", DataPropertyName = nameof(Linha.Descricao), FillWeight = 45, ReadOnly = true },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Valor", DataPropertyName = nameof(Linha.Valor), FillWeight = 14, ReadOnly = true,
                DefaultCellStyle = { Format = "C2", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            },
            new DataGridViewTextBoxColumn { HeaderText = "Situação", DataPropertyName = nameof(Linha.Situacao), FillWeight = 24, ReadOnly = true },
            new DataGridViewTextBoxColumn { HeaderText = "Movimento no sistema", DataPropertyName = nameof(Linha.Movimento), FillWeight = 45, ReadOnly = true });
        _grade.DataSource = _linhas;
        _grade.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _linhas.Count)
                return;
            e.CellStyle!.ForeColor = _linhas[e.RowIndex].Item.Situacao switch
            {
                SituacaoConciliacao.Conciliado => Tema.Sucesso,
                SituacaoConciliacao.SemCorrespondencia => Tema.Alerta,
                _ => Tema.Texto
            };
        };
        _grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grade.IsCurrentCellDirty)
                _grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 4, 12, 6) };
        barra.Controls.AddRange([
            Acao("Abrir extrato (OFX)", Icones.Exportar, AbrirAsync, primario: true),
            Acao("Conciliar marcados", Icones.Confirmar, ConciliarAsync),
            Acao("Lançar no sistema", Icones.Adicionar, LancarAsync),
            Acao("Movimentos sem extrato", Icones.Lista, () =>
            {
                MostrarSemExtrato();
                return Task.CompletedTask;
            }),
            Acao("Fechar com o saldo do banco", Icones.Cadeado, FecharAsync)
        ]);

        var cabecalho = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16, 12, 16, 0) };
        cabecalho.Controls.Add(Tema.Titulo("Conciliação bancária"));
        cabecalho.Controls.Add(_cabecalho);

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 8) };
        corpo.Controls.Add(_grade);
        corpo.Controls.Add(_resumo);

        Controls.Add(corpo);
        Controls.Add(barra);
        Controls.Add(cabecalho);
    }

    private Button Acao(string texto, char icone, Func<Task> acao, bool primario = false)
    {
        var botao = primario ? Tema.BotaoPrimario(texto, icone) : Tema.BotaoSecundario(texto, icone);
        botao.Click += async (_, _) => await ExecutarAsync(acao, botao, cursorEspera: false);
        return botao;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await ExecutarAsync(AbrirAsync, cursorEspera: false);
    }

    private async Task AbrirAsync()
    {
        using var dialogo = new OpenFileDialog { Filter = "Extrato OFX (*.ofx)|*.ofx|Todos os arquivos (*.*)|*.*", Title = "Extrato do banco (OFX)" };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        _extrato = LeitorOfx.Ler(await File.ReadAllBytesAsync(dialogo.FileName));
        await AnalisarAsync();
    }

    private async Task AnalisarAsync()
    {
        if (_extrato is null)
            return;
        _analise = await _servico.AnalisarAsync(_contaId, _extrato);
        _linhas.RaiseListChangedEvents = false;
        _linhas.Clear();
        foreach (var item in _analise.Itens)
            _linhas.Add(new Linha(item));
        _linhas.RaiseListChangedEvents = true;
        _linhas.ResetBindings();

        _cabecalho.Text = $"Extrato {(_extrato.Banco is { } banco ? $"do banco {banco} " : "")}{(_extrato.Conta is { } conta ? $"conta {conta} " : "")}" +
                          $"de {_extrato.Inicio ?? _extrato.Lancamentos.Min(l => l.Data):dd/MM/yyyy} a {_extrato.Fim ?? _extrato.Lancamentos.Max(l => l.Data):dd/MM/yyyy}";
        var itens = _analise.Itens;
        _resumo.Text = $"{itens.Count} lançamento(s) no extrato  •  conciliados: {itens.Count(i => i.Situacao == SituacaoConciliacao.Conciliado)}  •  " +
                       $"sugeridos: {itens.Count(i => i.Situacao == SituacaoConciliacao.Sugerido)}  •  " +
                       $"só no banco: {itens.Count(i => i.Situacao == SituacaoConciliacao.SemCorrespondencia)}  •  " +
                       $"no sistema sem extrato: {_analise.SemExtrato.Count}" + Environment.NewLine +
                       (_analise.DiferencaSaldo is { } diferenca
                           ? $"Saldo do banco em {_extrato.DataSaldo:dd/MM/yyyy}: {Textos.Moeda(_extrato.Saldo ?? 0)}  •  sistema: {Textos.Moeda(_analise.SaldoSistemaNaDataDoExtrato)}" +
                             $"  •  diferença: {Textos.Moeda(diferenca)}"
                           : "O arquivo não trouxe o saldo final da conta.");
        _resumo.ForeColor = _analise.DiferencaSaldo is { } d && d != 0 ? Tema.Alerta : Tema.Texto;
    }

    private async Task ConciliarAsync()
    {
        _grade.EndEdit();
        var marcados = _linhas.Where(l => l.Marcar && l.Item.Situacao == SituacaoConciliacao.Sugerido).Select(l => l.Item).ToList();
        if (marcados.Count == 0)
        {
            Mensagens.Aviso(this, "Marque as sugestões que conferem com o extrato.");
            return;
        }
        var conciliados = await _servico.ConciliarAsync(_contaId, marcados);
        Alterou = true;
        await AnalisarAsync();
        Mensagens.Info(this, $"{conciliados} movimento(s) conciliado(s).");
    }

    private async Task LancarAsync()
    {
        if (_grade.CurrentRow?.DataBoundItem is not Linha { Item.Situacao: SituacaoConciliacao.SemCorrespondencia } linha)
        {
            Mensagens.Aviso(this, "Selecione um lançamento que está só no banco (sem movimento no sistema).");
            return;
        }
        var lancamento = linha.Item.Extrato;
        var descricao = Dialogos.PedirTexto(this, "Lançar no sistema", "Descrição do movimento", $"Extrato: {lancamento.Descricao}", linhas: 1,
            subtitulo: $"{(lancamento.Valor > 0 ? "Entrada" : "Saída")} de {Textos.Moeda(Math.Abs(lancamento.Valor))} em {lancamento.Data:dd/MM/yyyy}, " +
                       "já conciliada. Use para tarifas, rendimentos e depósitos que não passaram pelo sistema.");
        if (descricao is null)
            return;
        await _servico.LancarAsync(_contaId, lancamento, descricao);
        Alterou = true;
        await AnalisarAsync();
    }

    private void MostrarSemExtrato()
    {
        if (_analise is null)
            return;
        var grade = new Grade<MovimentoSemExtrato>();
        grade.Data("Data", m => m.Data.ToDateTime(TimeOnly.MinValue), peso: 16).Texto("Descrição", m => m.Descricao, 70)
            .Moeda("Valor", m => m.Valor, 20, m => m.Valor < 0 ? Tema.Perigo : Tema.Sucesso);
        grade.Carregar(_analise.SemExtrato);
        grade.Controle.Height = Px(320);
        var e = new EditorCampos(1);
        e.Livre(grade.Controle);
        using var dialogo = new FormEdicao("Movimentos do sistema sem extrato",
            "Lançados no sistema no período, mas que o banco não mostrou: ainda não compensados, em outra conta ou lançados errado.", e,
            () => Task.CompletedTask, 760, somenteLeitura: true);
        dialogo.ShowDialog(this);
    }

    private async Task FecharAsync()
    {
        if (_extrato?.Saldo is not { } saldo || _analise is null)
        {
            Mensagens.Aviso(this, "Abra um extrato com o saldo final da conta.");
            return;
        }
        var data = _extrato.DataSaldo ?? _extrato.Fim ?? _extrato.Lancamentos.Max(l => l.Data);
        var diferenca = _analise.DiferencaSaldo ?? 0;
        if (!Mensagens.Confirmar(this, $"Fechar (conferir) a conta até {data:dd/MM/yyyy} com o saldo do banco, {Textos.Moeda(saldo)}?" +
                                       (diferenca == 0 ? "" : $" A diferença de {Textos.Moeda(diferenca)} vira um movimento de ajuste.") +
                                       " Depois disso, a conta não aceita movimentos até essa data.", "Fechar conta"))
            return;
        await _contas.FecharAsync(new FechamentoInput
        {
            ContaFinanceiraId = _contaId,
            Ate = data,
            SaldoInformado = saldo,
            Observacao = diferenca == 0 ? "Conciliação com o extrato OFX." : "Diferença apurada na conciliação com o extrato OFX."
        });
        Alterou = true;
        Mensagens.Info(this, $"Conta conferida até {data:dd/MM/yyyy}.");
    }
}
