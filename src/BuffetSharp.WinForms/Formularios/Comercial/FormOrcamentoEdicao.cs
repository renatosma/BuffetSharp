using System.ComponentModel;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Parâmetros de abertura do editor (evita ambiguidade entre dois inteiros na injeção).</summary>
internal sealed record AberturaOrcamento(int OrcamentoId = 0, int ClienteId = 0);

/// <summary>
/// Editor de orçamento: dados do evento, pacote e espaço com valores negociáveis, serviços adicionais,
/// desconto, resumo financeiro em tempo real e verificação de disponibilidade do espaço.
/// </summary>
internal sealed class FormOrcamentoEdicao : FormBase
{
    private readonly OrcamentoService _servico;
    private readonly DisponibilidadeService _disponibilidade;
    private readonly IConsultasLookup _lookups;
    private readonly IConsultasEstoqueCardapio _cardapio;
    private readonly BuffetOptions _opcoes;
    private readonly AberturaOrcamento _abertura;

    private OrcamentoInput _input = new();
    private EditorCampos _editor = new(3);
    private readonly BindingList<ServicoItemInput> _servicos = [];
    private IReadOnlyList<ItemLookup> _pacotes = [];
    private IReadOnlyList<ItemLookup> _espacos = [];
    private IReadOnlyList<ItemLookup> _catalogoServicos = [];

    private readonly Panel _corpo = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 8, 8, 8), BackColor = Tema.Superficie };
    private readonly Label _titulo = Tema.Titulo("Orçamento");
    private readonly Label _status = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(12, 8, 0, 0) };
    private readonly Label _disponivel = new() { AutoSize = true, Font = Tema.FonteNegrito };
    private readonly DataGridView _gradeServicos = new() { Height = 150 };
    private readonly ComboBox _novoServico = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.Timer _atrasoDisponibilidade = new() { Interval = 450 };

    // Resumo
    private readonly Label _alimentacao = ValorResumo();
    private readonly Label _valorEspaco = ValorResumo();
    private readonly Label _valorServicos = ValorResumo();
    private readonly Label _subtotal = ValorResumo();
    private readonly Label _desconto = ValorResumo();
    private readonly Label _total = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 20f), ForeColor = Tema.Primaria };
    private readonly Label _porPessoa = new() { AutoSize = true, ForeColor = Tema.TextoSuave };
    private readonly Label _alertas = new() { AutoSize = true, ForeColor = Tema.Alerta, MaximumSize = new Size(260, 0) };
    private readonly ListBox _composicao = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };

    private NumericUpDown _convidados = null!, _valorPessoa = null!, _valorLocacao = null!, _percentual = null!;
    private ComboBox _pacote = null!, _espaco = null!;
    private DateTimePicker _data = null!, _inicio = null!, _fim = null!;

    private readonly Button _salvar = Tema.BotaoPrimario("Salvar", Icones.Salvar);
    private readonly Button _salvarEnviar = Tema.BotaoSecundario("Salvar e enviar", Icones.Enviar);
    private readonly Button _imprimir = Tema.BotaoSecundario("Imprimir proposta", Icones.Imprimir);

    public FormOrcamentoEdicao(OrcamentoService servico, DisponibilidadeService disponibilidade, IConsultasLookup lookups,
        IConsultasEstoqueCardapio cardapio, BuffetOptions opcoes, AberturaOrcamento abertura)
    {
        _servico = servico;
        _disponibilidade = disponibilidade;
        _lookups = lookups;
        _cardapio = cardapio;
        _opcoes = opcoes;
        _abertura = abertura;

        Text = "Orçamento";
        ClientSize = new Size(1180, 780);
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var cabecalho = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20, 14, 20, 8), BackColor = Tema.Superficie };
        cabecalho.Controls.Add(_titulo);
        cabecalho.Controls.Add(_status);

        var acoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 10, 16, 10) };
        var fechar = Tema.BotaoSecundario("Fechar");
        fechar.DialogResult = DialogResult.Cancel;
        _salvar.Click += async (_, _) => await SalvarAsync(enviar: false);
        _salvarEnviar.Click += async (_, _) => await SalvarAsync(enviar: true);
        _imprimir.Click += async (_, _) => await ExecutarAsync(ImprimirAsync, _imprimir, cursorEspera: false);
        acoes.Controls.AddRange([fechar, _salvar, _salvarEnviar, _imprimir]);
        CancelButton = fechar;

        Controls.Add(_corpo);
        Controls.Add(CriarResumo());
        Controls.Add(cabecalho);
        Controls.Add(acoes);

        _atrasoDisponibilidade.Tick += async (_, _) =>
        {
            _atrasoDisponibilidade.Stop();
            await VerificarDisponibilidadeAsync();
        };
    }

    private static Label ValorResumo() => new() { AutoSize = true, Font = Tema.FonteNegrito, Anchor = AnchorStyles.Right };

    private Control CriarResumo()
    {
        var painel = new Panel { Dock = DockStyle.Right, Width = LogicalToDeviceUnits(320), Padding = new Padding(10, 8, 16, 8) };
        var cartao = Tema.Cartao(new Padding(16));
        cartao.Dock = DockStyle.Fill;

        var tabela = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        void Linha(string rotulo, Label valor)
        {
            tabela.Controls.Add(new Label { Text = rotulo, AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 4, 0, 4) });
            tabela.Controls.Add(valor);
        }
        tabela.Controls.Add(new Label { Text = "RESUMO DO INVESTIMENTO", AutoSize = true, Font = Tema.FonteNegrito, ForeColor = Tema.Primaria, Margin = new Padding(0, 0, 0, 8) });
        tabela.SetColumnSpan(tabela.Controls[^1], 2);
        Linha("Alimentação", _alimentacao);
        Linha("Espaço", _valorEspaco);
        Linha("Serviços", _valorServicos);
        Linha("Subtotal", _subtotal);
        Linha("Desconto", _desconto);

        var total = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 10, 0, 6) };
        total.Controls.Add(new Label { Text = "Total", AutoSize = true, ForeColor = Tema.TextoSuave });
        total.Controls.Add(_total);
        total.Controls.Add(_porPessoa);
        total.Controls.Add(_alertas);

        var composicao = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        composicao.Controls.Add(_composicao);
        composicao.Controls.Add(new Label { Text = "Cardápio do pacote", Dock = DockStyle.Top, Font = Tema.FonteNegrito, Height = 26 });

        cartao.Controls.Add(composicao);
        cartao.Controls.Add(total);
        cartao.Controls.Add(tabela);
        painel.Controls.Add(cartao);
        return painel;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var ok = await ExecutarAsync(async () =>
        {
            var clientes = await _lookups.ListarAsync(TipoLookup.Clientes);
            var tipos = await _lookups.ListarAsync(TipoLookup.TiposEvento);
            var vendedores = await _lookups.ListarAsync(TipoLookup.Vendedores);
            _pacotes = await _lookups.ListarAsync(TipoLookup.Pacotes);
            _espacos = await _lookups.ListarAsync(TipoLookup.Espacos);
            _catalogoServicos = await _lookups.ListarAsync(TipoLookup.ServicosAdicionais);

            _input = _abertura.OrcamentoId > 0 ? await _servico.ObterAsync(_abertura.OrcamentoId) : _servico.Novo();
            if (_abertura.OrcamentoId == 0 && _abertura.ClienteId > 0)
                _input.ClienteId = _abertura.ClienteId;

            MontarCampos(clientes, tipos, vendedores);
            _editor.Carregar();
            foreach (var s in _input.Servicos)
                _servicos.Add(s);
            AtualizarCabecalho();
            ConectarEventos();
            Recalcular();
            await AtualizarComposicaoAsync();
            await VerificarDisponibilidadeAsync();
        });
        if (!ok)
            Close();
    }

    private void MontarCampos(IReadOnlyList<ItemLookup> clientes, IReadOnlyList<ItemLookup> tipos, IReadOnlyList<ItemLookup> vendedores)
    {
        var i = _input;
        _editor = new EditorCampos(3);
        var e = _editor;

        e.Secao("Cliente e evento");
        e.Lista("Cliente", clientes, () => i.ClienteId, v => i.ClienteId = v ?? 0, nameof(i.ClienteId), span: 2);
        e.Lista("Tipo de evento", tipos, () => i.TipoEventoId, v => i.TipoEventoId = v ?? 0, nameof(i.TipoEventoId));
        _data = e.Data("Data do evento", () => i.Data, v => i.Data = v ?? i.Data, nameof(i.Data));
        _inicio = e.Hora("Início", () => i.HoraInicio, v => i.HoraInicio = v);
        _fim = e.Hora("Término (pode passar da meia-noite)", () => i.HoraFim, v => i.HoraFim = v);
        _convidados = e.Inteiro("Convidados", () => i.Convidados, v => i.Convidados = v, nameof(i.Convidados), 1, 10_000);
        e.Data("Proposta válida até", () => i.ValidoAte, v => i.ValidoAte = v ?? i.ValidoAte, nameof(i.ValidoAte));
        e.Lista("Vendedor (comissão)", vendedores, () => i.VendedorId, v => i.VendedorId = v, nameof(i.VendedorId), permitirVazio: true);

        e.Secao("Cardápio e espaço");
        _pacote = e.Lista("Pacote", _pacotes, () => i.PacoteId, v => i.PacoteId = v, nameof(i.PacoteId), permitirVazio: true, span: 2);
        _valorPessoa = e.Decimal("Valor por pessoa (R$)", () => i.ValorPorPessoa ?? 0, v => i.ValorPorPessoa = v);
        _espaco = e.Lista("Espaço", _espacos, () => i.EspacoId, v => i.EspacoId = v, nameof(i.EspacoId), permitirVazio: true, span: 2);
        _valorLocacao = e.Decimal("Valor da locação (R$)", () => i.ValorEspaco ?? 0, v => i.ValorEspaco = v);
        e.Livre(_disponivel);

        e.Secao("Serviços adicionais");
        e.Livre(CriarPainelServicos());

        e.Secao("Condições comerciais");
        _percentual = e.Decimal($"Desconto (%) - máx. {CalculadoraValores.DescontoMaximoPercentual}%", () => i.PercentualDesconto,
            v => i.PercentualDesconto = v, nameof(i.PercentualDesconto), 2, 0, CalculadoraValores.DescontoMaximoPercentual);
        e.Texto("Observações (impressas na proposta)", () => i.Observacoes, v => i.Observacoes = v, nameof(i.Observacoes), 2000, span: 3, linhas: 3);

        _corpo.Controls.Clear();
        _corpo.Controls.Add(e.Painel);
    }

    private Control CriarPainelServicos()
    {
        var painel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
        _novoServico.DataSource = _catalogoServicos.ToList();
        var adicionar = Tema.BotaoSecundario("Adicionar", Icones.Adicionar);
        adicionar.Click += (_, _) => AdicionarServico();
        var remover = Tema.BotaoSecundario("Remover", Icones.Excluir);
        remover.Click += (_, _) =>
        {
            if (_gradeServicos.CurrentRow?.DataBoundItem is ServicoItemInput item)
                _servicos.Remove(item);
        };
        barra.Controls.AddRange([_novoServico, adicionar, remover]);

        Tema.EstilizarGrade(_gradeServicos);
        _gradeServicos.Dock = DockStyle.Fill;
        _gradeServicos.ReadOnly = false;
        _gradeServicos.AutoGenerateColumns = false;
        _gradeServicos.EditMode = DataGridViewEditMode.EditOnEnter;
        _gradeServicos.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _gradeServicos.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Serviço", DataPropertyName = nameof(ServicoItemInput.Descricao), ReadOnly = true, FillWeight = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Quantidade", DataPropertyName = nameof(ServicoItemInput.Quantidade), FillWeight = 15, DefaultCellStyle = { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Valor unitário", DataPropertyName = nameof(ServicoItemInput.ValorUnitario), FillWeight = 18, DefaultCellStyle = { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Total", DataPropertyName = nameof(ServicoItemInput.Total), ReadOnly = true, FillWeight = 18, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _gradeServicos.DataSource = _servicos;
        _gradeServicos.CellEndEdit += (_, _) =>
        {
            _gradeServicos.InvalidateRow(_gradeServicos.CurrentCell?.RowIndex ?? 0);
            Recalcular();
        };
        _gradeServicos.DataError += (_, ev) =>
        {
            ev.ThrowException = false;
            Mensagens.Aviso(this, "Valor inválido. Informe um número.");
        };
        _servicos.ListChanged += (_, _) => Recalcular();

        painel.Controls.Add(barra);
        painel.Controls.Add(_gradeServicos);
        return painel;
    }

    private void AdicionarServico()
    {
        if (_novoServico.SelectedItem is not ItemLookup servico)
            return;
        if (_servicos.Any(s => s.ServicoAdicionalId == servico.Id))
        {
            Mensagens.Aviso(this, "Este serviço já foi incluído. Ajuste a quantidade na lista.");
            return;
        }
        _servicos.Add(new ServicoItemInput
        {
            ServicoAdicionalId = servico.Id,
            Descricao = servico.Nome + (servico.Marcador ? " (por pessoa)" : ""),
            Quantidade = servico.Marcador ? _convidados.Value : 1,
            ValorUnitario = servico.Valor
        });
    }

    private void ConectarEventos()
    {
        _pacote.SelectedIndexChanged += async (_, _) =>
        {
            if (_pacote.SelectedItem is ItemLookup { Id: > 0 } pacote)
                _valorPessoa.Value = pacote.Valor;
            else
                _valorPessoa.Value = 0;
            Recalcular();
            await AtualizarComposicaoAsync();
        };
        _espaco.SelectedIndexChanged += (_, _) =>
        {
            _valorLocacao.Value = _espaco.SelectedItem is ItemLookup { Id: > 0 } espaco ? espaco.Valor : 0;
            Recalcular();
            AgendarVerificacao();
        };
        _convidados.ValueChanged += (_, _) =>
        {
            // Serviços cobrados por pessoa acompanham a quantidade de convidados.
            foreach (var s in _servicos.Where(s => _catalogoServicos.Any(c => c.Id == s.ServicoAdicionalId && c.Marcador)))
                s.Quantidade = _convidados.Value;
            _servicos.ResetBindings();
            Recalcular();
        };
        foreach (var numero in new[] { _valorPessoa, _valorLocacao, _percentual })
            numero.ValueChanged += (_, _) => Recalcular();
        foreach (var data in new[] { _data, _inicio, _fim })
            data.ValueChanged += (_, _) => AgendarVerificacao();
    }

    private void AgendarVerificacao()
    {
        _atrasoDisponibilidade.Stop();
        _atrasoDisponibilidade.Start();
    }

    private void Recalcular()
    {
        if (_convidados is null)
            return;
        var convidados = (int)_convidados.Value;
        var resumo = CalculadoraValores.Calcular(_valorPessoa.Value, convidados, _valorLocacao.Value,
            _servicos.Sum(s => s.Total), _percentual.Value);

        _alimentacao.Text = Textos.Moeda(resumo.Alimentacao);
        _valorEspaco.Text = Textos.Moeda(resumo.Espaco);
        _valorServicos.Text = Textos.Moeda(resumo.Servicos);
        _subtotal.Text = Textos.Moeda(resumo.Subtotal);
        _desconto.Text = "- " + Textos.Moeda(resumo.Desconto);
        _total.Text = Textos.Moeda(resumo.Total);
        _porPessoa.Text = convidados > 0 ? $"{Textos.Moeda(resumo.Total / convidados)} por convidado" : "";

        var alertas = new List<string>();
        if (_pacote.SelectedItem is ItemLookup { Id: > 0 } pacote && convidados < pacote.Numero)
            alertas.Add($"⚠ O pacote exige no mínimo {pacote.Numero} convidados.");
        if (_espaco.SelectedItem is ItemLookup { Id: > 0 } espaco && convidados > espaco.Numero)
            alertas.Add($"⚠ O espaço comporta no máximo {espaco.Numero} pessoas.");
        _alertas.Text = string.Join(Environment.NewLine, alertas);
    }

    private async Task AtualizarComposicaoAsync()
    {
        _composicao.Items.Clear();
        if (_pacote.SelectedItem is not ItemLookup { Id: > 0 } pacote)
        {
            _composicao.Items.Add("(nenhum pacote selecionado)");
            return;
        }
        foreach (var grupo in (await _cardapio.ComposicaoPacoteAsync(pacote.Id)).GroupBy(c => c.Categoria))
        {
            _composicao.Items.Add(grupo.Key.ToUpperInvariant());
            foreach (var item in grupo)
                _composicao.Items.Add("   • " + item.Item);
        }
    }

    private async Task VerificarDisponibilidadeAsync()
    {
        if (_espaco.SelectedItem is not ItemLookup { Id: > 0 } espaco)
        {
            _disponivel.Text = "Selecione um espaço para verificar a disponibilidade da data.";
            _disponivel.ForeColor = Tema.TextoSuave;
            return;
        }
        try
        {
            var (inicio, fim) = PeriodoEvento.Calcular(DateOnly.FromDateTime(_data.Value), TimeOnly.FromDateTime(_inicio.Value),
                TimeOnly.FromDateTime(_fim.Value));
            var conflito = await _disponibilidade.BuscarConflitoAsync(espaco.Id, inicio, fim, _input.EventoId);
            _disponivel.Text = conflito is null
                ? $"✔ {espaco.Nome} disponível em {inicio:dd/MM/yyyy} das {inicio:HH:mm} às {fim:HH:mm}."
                : $"✖ Conflito: {conflito.Codigo} - {conflito.Titulo} ({conflito.Inicio:dd/MM HH:mm} às {conflito.Fim:HH:mm}).";
            _disponivel.ForeColor = conflito is null ? Tema.Sucesso : Tema.Perigo;
        }
        catch (RegraNegocioException ex)
        {
            _disponivel.Text = "✖ " + ex.Message;
            _disponivel.ForeColor = Tema.Perigo;
        }
    }

    private void AtualizarCabecalho()
    {
        _titulo.Text = _input.Id == 0 ? "Novo orçamento" : $"Orçamento {_input.Codigo}";
        Text = _titulo.Text;
        _status.Text = Textos.Exibir(_input.Status).ToUpperInvariant() + (_input.EventoId is { } ev ? $"  →  EVT-{ev:D5}" : "")
                       + (_input.MotivoRecusa is { } motivo ? $"  ({motivo})" : "")
                       + (_input.Envios.FirstOrDefault() is { } envio
                           ? $"  •  último envio: {Textos.Exibir(envio.Canal)} em {envio.EnviadoEm:dd/MM/yyyy HH:mm} ({envio.Destinatario})"
                           : "");
        _status.ForeColor = Textos.Cor(_input.Status);

        var editavel = _input.PodeEditar;
        _editor.DefinirSomenteLeitura(!editavel);
        _gradeServicos.ReadOnly = !editavel;
        _novoServico.Enabled = editavel;
        _salvar.Visible = editavel;
        _salvarEnviar.Visible = editavel && _input.Status == StatusOrcamento.Rascunho;
    }

    private async Task SalvarAsync(bool enviar)
    {
        _editor.LimparErros();
        _gradeServicos.EndEdit();
        await ExecutarAsync(async () =>
        {
            try
            {
                _editor.Aplicar();
                _input.Servicos = _servicos.ToList();
                if (_input.ValorPorPessoa == 0)
                    _input.ValorPorPessoa = null; // usa o preço de tabela do pacote
                _input.Id = await _servico.SalvarAsync(_input);
                if (enviar)
                    await _servico.EnviarAsync(_input.Id);
            }
            catch (ValidacaoException ex) when (_editor.MostrarErros(ex))
            {
                return;
            }
            DialogResult = DialogResult.OK;
        }, _salvar);
    }

    private async Task ImprimirAsync()
    {
        if (_input.PodeEditar)
        {
            _editor.Aplicar();
            _input.Servicos = _servicos.ToList();
        }
        await ImpressaoProposta.VisualizarAsync(this, _input, _lookups, _cardapio, _opcoes);
    }
}
