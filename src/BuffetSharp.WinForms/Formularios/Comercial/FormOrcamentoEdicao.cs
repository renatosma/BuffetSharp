using System.ComponentModel;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Parâmetros de abertura do editor (evita ambiguidade entre dois inteiros na injeção).</summary>
/// <param name="TipoEventoId">Sugestões para um orçamento novo (ex.: o que o lead convertido informou).</param>
internal sealed record AberturaOrcamento(int OrcamentoId = 0, int ClienteId = 0, int? TipoEventoId = null, DateOnly? Data = null,
    int? Convidados = null, TimeOnly? HoraInicio = null, TimeOnly? HoraFim = null, int? EspacoId = null);

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
    private readonly ICadastroService<CondicaoPagamento> _condicoesPagamento;
    private readonly AberturaOrcamento _abertura;
    private readonly TabelaPrecoService _precos;
    private readonly DescontoService _descontos;
    private readonly MargemService _margens;
    private readonly Navegador _navegador;

    /// <summary>Custos dos cadastros para a margem prevista (null até carregar).</summary>
    private ParametrosMargem? _parametrosMargem;

    /// <summary>Desconto máximo (%) que o usuário logado pode dar sem aprovação.</summary>
    private decimal _limiteDesconto = 100;

    /// <summary>Ajuste das regras de preço para a data, o pacote e o espaço, e os valores que ele sugeriu.</summary>
    private AjustePreco _ajuste = AjustePreco.Nenhum;
    private decimal? _pessoaSugerida, _locacaoSugerida;

    private OrcamentoInput _input = new();
    private EditorCampos _editor = new(3);
    private readonly BindingList<ServicoItemInput> _servicos = [];
    private IReadOnlyList<ItemLookup> _pacotes = [];
    private Dictionary<int, PacoteResumo> _regrasPacotes = [];
    private IReadOnlyList<ItemLookup> _espacos = [];
    private IReadOnlyList<ItemLookup> _catalogoServicos = [];
    private List<CondicaoPagamento> _condicoes = [];

    private readonly Panel _corpo = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 8, 8, 8), BackColor = Tema.Superficie };
    private readonly Label _titulo = Tema.Titulo("Orçamento");
    private readonly Label _status = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(12, 8, 0, 0) };
    private readonly Label _disponivel = new() { AutoSize = true, Font = Tema.FonteNegrito };
    private readonly Button _entrarEspera = Tema.BotaoSecundario("Entrar na lista de espera", Icones.Relogio);
    private ComboBox _cliente = null!, _tipoEvento = null!;
    private readonly Label _simulacaoPagamento = new() { AutoSize = true, ForeColor = Tema.TextoSuave };
    private readonly Label _faixas = new() { AutoSize = true, ForeColor = Tema.TextoSuave };
    private readonly Label _precoData = new() { AutoSize = true, Font = Tema.FonteNegrito };
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
    private readonly Label _alcada = new() { AutoSize = true, MaximumSize = new Size(260, 0), Margin = new Padding(0, 6, 0, 0) };
    private readonly Label _margem = new() { AutoSize = true, MaximumSize = new Size(260, 0), Margin = new Padding(0, 6, 0, 0) };
    private readonly Button _custos = Tema.BotaoSecundario("Custos (interno)", Icones.Calculadora);
    private ComboBox _vendedor = null!;
    private readonly ListBox _composicao = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };

    private NumericUpDown _convidados = null!, _meia = null!, _isentos = null!, _percentualMeia = null!;
    private NumericUpDown _valorPessoa = null!, _valorLocacao = null!, _percentual = null!;
    private ComboBox _pacote = null!, _espaco = null!, _condicao = null!, _forma = null!;
    private IReadOnlyList<ItemLookup> _formas = [], _formasInativas = [];
    private DateTimePicker _data = null!, _inicio = null!, _fim = null!;

    private readonly Button _salvar = Tema.BotaoPrimario("Salvar", Icones.Salvar);
    private readonly Button _salvarEnviar = Tema.BotaoSecundario("Salvar e enviar", Icones.Enviar);
    private readonly Button _imprimir = Tema.BotaoSecundario("Imprimir proposta", Icones.Imprimir);

    public FormOrcamentoEdicao(OrcamentoService servico, DisponibilidadeService disponibilidade, IConsultasLookup lookups,
        IConsultasEstoqueCardapio cardapio, BuffetOptions opcoes, ICadastroService<CondicaoPagamento> condicoesPagamento,
        AberturaOrcamento abertura, TabelaPrecoService precos, DescontoService descontos, MargemService margens, Navegador navegador)
    {
        _precos = precos;
        _descontos = descontos;
        _margens = margens;
        _navegador = navegador;
        _entrarEspera.Click += async (_, _) => await ExecutarAsync(EntrarNaEsperaAsync, _entrarEspera, cursorEspera: false);
        _servico = servico;
        _disponibilidade = disponibilidade;
        _lookups = lookups;
        _cardapio = cardapio;
        _opcoes = opcoes;
        _condicoesPagamento = condicoesPagamento;
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
        _custos.Click += (_, _) => ImprimirCustos();
        _custos.Visible = margens.PodeVerCustos;
        acoes.Controls.AddRange([fechar, _salvar, _salvarEnviar, _imprimir, _custos]);
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
        total.Controls.Add(_alcada);
        total.Controls.Add(_margem);

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
            _regrasPacotes = (await _cardapio.ListarPacotesAsync()).ToDictionary(p => p.Id);
            _espacos = await _lookups.ListarAsync(TipoLookup.Espacos);
            _catalogoServicos = await _lookups.ListarAsync(TipoLookup.ServicosAdicionais);
            _condicoes = await _condicoesPagamento.ListarAsync(q => q.Include(c => c.Parcelas).ThenInclude(p => p.FormaPagamento).OrderBy(c => c.Nome));
            _formas = await _lookups.ListarAsync(TipoLookup.FormasPagamento);
            _formasInativas = await _lookups.ListarAsync(TipoLookup.FormasPagamento, somenteAtivos: false);
            _limiteDesconto = await _descontos.MeuLimiteAsync();
            _parametrosMargem = await _margens.ParametrosAsync();

            _input = _abertura.OrcamentoId > 0 ? await _servico.ObterAsync(_abertura.OrcamentoId) : _servico.Novo();
            if (_abertura.OrcamentoId == 0)
            {
                if (_abertura.ClienteId > 0)
                    _input.ClienteId = _abertura.ClienteId;
                if (_abertura.TipoEventoId is { } tipoEventoId)
                    _input.TipoEventoId = tipoEventoId;
                if (_abertura.Data is { } data && data > DateOnly.FromDateTime(DateTime.Today))
                    _input.Data = data;
                if (_abertura.Convidados is > 0 and var convidados)
                    _input.Convidados = convidados;
                if (_abertura.HoraInicio is { } horaInicio)
                    _input.HoraInicio = horaInicio;
                if (_abertura.HoraFim is { } horaFim)
                    _input.HoraFim = horaFim;
                if (_abertura.EspacoId is { } espacoId)
                    _input.EspacoId = espacoId;
            }

            MontarCampos(clientes, tipos, vendedores);
            _editor.Carregar();
            foreach (var s in _input.Servicos)
                _servicos.Add(s);
            AtualizarCabecalho();
            ConectarEventos();

            // Orçamento novo: o valor por pessoa e a locação ainda não foram negociados.
            if (_input.Id == 0)
                (_pessoaSugerida, _locacaoSugerida) = (_valorPessoa.Value, _valorLocacao.Value);
            await AtualizarPrecoDataAsync();
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
        _cliente = e.Lista("Cliente", clientes, () => i.ClienteId, v => i.ClienteId = v ?? 0, nameof(i.ClienteId), span: 2);
        _tipoEvento = e.Lista("Tipo de evento", tipos, () => i.TipoEventoId, v => i.TipoEventoId = v ?? 0, nameof(i.TipoEventoId));
        _data = e.Data("Data do evento", () => i.Data, v => i.Data = v ?? i.Data, nameof(i.Data));
        _inicio = e.Hora("Início", () => i.HoraInicio, v => i.HoraInicio = v);
        _fim = e.Hora("Término (pode passar da meia-noite)", () => i.HoraFim, v => i.HoraFim = v);
        e.Data("Proposta válida até", () => i.ValidoAte, v => i.ValidoAte = v ?? i.ValidoAte, nameof(i.ValidoAte));
        _vendedor = e.Lista("Vendedor (comissão)", vendedores, () => i.VendedorId, v => i.VendedorId = v, nameof(i.VendedorId), permitirVazio: true, span: 2);

        e.Secao("Convidados");
        _convidados = e.Inteiro("Convidados", () => i.Convidados, v => i.Convidados = v, nameof(i.Convidados), 1, 10_000);
        _meia = e.Inteiro("Crianças com meia", () => i.ConvidadosMeia, v => i.ConvidadosMeia = v, nameof(i.ConvidadosMeia), 0, 10_000);
        _isentos = e.Inteiro("Crianças isentas", () => i.ConvidadosIsentos, v => i.ConvidadosIsentos = v, nameof(i.ConvidadosIsentos), 0, 10_000);
        _percentualMeia = e.Decimal("Meia (% do valor por pessoa)", () => i.PercentualMeia ?? i.RegraFaixas.PercentualMeia,
            v => i.PercentualMeia = v, nameof(i.PercentualMeia), 2, 0, 100);
        e.Livre(_faixas);

        e.Secao("Cardápio e espaço");
        _pacote = e.Lista("Pacote", _pacotes, () => i.PacoteId, v => i.PacoteId = v, nameof(i.PacoteId), permitirVazio: true, span: 2);
        _valorPessoa = e.Decimal("Valor por pessoa (R$)", () => i.ValorPorPessoa ?? 0, v => i.ValorPorPessoa = v);
        _espaco = e.Lista("Espaço", _espacos, () => i.EspacoId, v => i.EspacoId = v, nameof(i.EspacoId), permitirVazio: true, span: 2);
        _valorLocacao = e.Decimal("Valor da locação (R$)", () => i.ValorEspaco ?? 0, v => i.ValorEspaco = v);
        e.Livre(_precoData);
        e.Livre(_disponivel);
        _entrarEspera.Visible = false;
        e.Livre(_entrarEspera);

        e.Secao("Serviços adicionais");
        e.Livre(CriarPainelServicos());

        e.Secao("Condições comerciais");
        _percentual = e.Decimal($"Desconto (%) - máx. {CalculadoraValores.DescontoMaximoPercentual}%", () => i.PercentualDesconto,
            v => i.PercentualDesconto = v, nameof(i.PercentualDesconto), 2, 0, CalculadoraValores.DescontoMaximoPercentual);
        // Condições inativas continuam aparecendo no orçamento que já as usa.
        var condicoes = _condicoes.Where(c => c.Ativo || c.Id == i.CondicaoPagamentoId)
            .Select(c => new ItemLookup { Id = c.Id, Nome = c.Nome }).ToList();
        _condicao = e.Lista("Condição de pagamento", condicoes, () => i.CondicaoPagamentoId, v => i.CondicaoPagamentoId = v,
            nameof(i.CondicaoPagamentoId), permitirVazio: true, span: 2);
        // Formas inativas continuam aparecendo no orçamento que já as usa.
        _forma = e.Lista("Forma de pagamento", _formas.Concat(_formasInativas.Where(f => f.Id == i.FormaPagamentoId && _formas.All(a => a.Id != f.Id))).ToList(),
            () => i.FormaPagamentoId, v => i.FormaPagamentoId = v, nameof(i.FormaPagamentoId), permitirVazio: true);
        e.Livre(_simulacaoPagamento);
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
            Quantidade = servico.Marcador ? Faixas().Pagantes : 1,
            ValorUnitario = servico.Valor
        });
    }

    private void ConectarEventos()
    {
        _pacote.SelectedIndexChanged += async (_, _) =>
        {
            if (_pacote.SelectedItem is ItemLookup { Id: > 0 } pacote)
            {
                _valorPessoa.Value = pacote.Valor;
                _pessoaSugerida = pacote.Valor;
                if (_regrasPacotes.GetValueOrDefault(pacote.Id) is { } regra)
                {
                    _input.IdadeMaximaIsento = regra.IdadeMaximaIsento;
                    _input.IdadeMaximaMeia = regra.IdadeMaximaMeia;
                    _percentualMeia.Value = regra.PercentualMeia;
                }
            }
            else
                _valorPessoa.Value = 0;
            await AtualizarPrecoDataAsync();
            await AtualizarComposicaoAsync();
        };
        _espaco.SelectedIndexChanged += async (_, _) =>
        {
            _valorLocacao.Value = _espaco.SelectedItem is ItemLookup { Id: > 0 } espaco ? espaco.Valor : 0;
            _locacaoSugerida = _valorLocacao.Value;
            AgendarVerificacao();
            await AtualizarPrecoDataAsync();
        };
        foreach (var quantidade in new[] { _convidados, _isentos })
            quantidade.ValueChanged += (_, _) =>
            {
                // Serviços cobrados por pessoa acompanham os convidados pagantes (os isentos não pagam).
                foreach (var s in _servicos.Where(s => _catalogoServicos.Any(c => c.Id == s.ServicoAdicionalId && c.Marcador)))
                    s.Quantidade = Math.Max(Faixas().Pagantes, 0);
                _servicos.ResetBindings();
                Recalcular();
            };
        foreach (var numero in new[] { _meia, _percentualMeia, _valorPessoa, _valorLocacao, _percentual })
            numero.ValueChanged += (_, _) => Recalcular();
        _condicao.SelectedIndexChanged += (_, _) => Recalcular();
        foreach (var combo in new[] { _forma, _vendedor, _tipoEvento })
            combo.SelectedIndexChanged += (_, _) => Recalcular();
        // A condição traz a forma sugerida (ex.: "30% + 5x" no cartão); só preenche se a forma ainda estiver vazia.
        _condicao.SelectionChangeCommitted += (_, _) =>
        {
            if (_forma.SelectedItem is ItemLookup { Id: > 0 } ||
                _condicao.SelectedItem is not ItemLookup { Id: > 0 } item ||
                _condicoes.FirstOrDefault(c => c.Id == item.Id)?.FormaPagamentoId is not { } formaId)
                return;
            var indice = _forma.Items.Cast<ItemLookup>().ToList().FindIndex(f => f.Id == formaId);
            if (indice >= 0)
                _forma.SelectedIndex = indice;
        };
        _data.ValueChanged += async (_, _) => await AtualizarPrecoDataAsync();
        foreach (var data in new[] { _data, _inicio, _fim })
        {
            data.ValueChanged += (_, _) => AgendarVerificacao();
            data.ValueChanged += (_, _) => Recalcular(); // a duração muda as bebidas e a margem
        }
    }

    /// <summary>
    /// Recalcula o ajuste da data e, nos valores que ainda são os sugeridos (não negociados), aplica o ajuste sobre a tabela.
    /// </summary>
    private async Task AtualizarPrecoDataAsync()
    {
        var pacote = _pacote.SelectedItem is ItemLookup { Id: > 0 } p ? p : null;
        var espaco = _espaco.SelectedItem is ItemLookup { Id: > 0 } s ? s : null;
        try
        {
            _ajuste = await _precos.AjusteAsync(DateOnly.FromDateTime(_data.Value), pacote?.Id, espaco?.Id);
        }
        catch (Exception ex)
        {
            Mensagens.Erro(this, ex);
            return;
        }

        if (_input.PodeEditar)
        {
            if (pacote is not null && _pessoaSugerida == _valorPessoa.Value)
            {
                _pessoaSugerida = _ajuste.Pessoa(pacote.Valor);
                _valorPessoa.Value = _pessoaSugerida.Value;
            }
            if (espaco is not null && _locacaoSugerida == _valorLocacao.Value)
            {
                _locacaoSugerida = _ajuste.Locacao(espaco.Valor);
                _valorLocacao.Value = _locacaoSugerida.Value;
            }
        }
        _input.AjustePessoaPercentual = _ajuste.PercentualPessoa;
        _input.AjusteLocacaoPercentual = _ajuste.PercentualLocacao;
        _input.AjusteDescricao = _ajuste.Regras.Count == 0 ? null : _ajuste.Descricao;
        _precoData.Text = _ajuste.Regras.Count == 0
            ? "Preço da data: tabela, sem ajuste."
            : $"Preço da data: {_ajuste.Descricao}" + (_ajuste.MinimoConvidados is { } minimo ? $"  •  mínimo de {minimo} pagantes" : "");
        _precoData.ForeColor = _ajuste.PercentualPessoa > 0 || _ajuste.PercentualLocacao > 0 ? Tema.Alerta
            : _ajuste.PercentualPessoa < 0 || _ajuste.PercentualLocacao < 0 ? Tema.Sucesso : Tema.TextoSuave;
        Recalcular();
    }

    /// <summary>A data está ocupada: o cliente entra na lista de espera com os dados deste orçamento.</summary>
    private async Task EntrarNaEsperaAsync()
    {
        if (_cliente.SelectedItem is not ItemLookup { Id: > 0 } cliente)
        {
            Mensagens.Aviso(this, "Selecione o cliente antes de colocá-lo na lista de espera.");
            return;
        }
        var input = new EsperaInput
        {
            ClienteId = cliente.Id,
            Data = DateOnly.FromDateTime(_data.Value),
            HoraInicio = TimeOnly.FromDateTime(_inicio.Value),
            HoraFim = TimeOnly.FromDateTime(_fim.Value),
            EspacoId = _espaco.SelectedItem is ItemLookup { Id: > 0 } espaco ? espaco.Id : null,
            TipoEventoId = _tipoEvento.SelectedItem is ItemLookup { Id: > 0 } tipo ? tipo.Id : null,
            Convidados = (int)_convidados.Value
        };
        if (await DialogosEspera.EditarAsync(this, _navegador, input))
            Mensagens.Info(this, $"{cliente.Nome} entrou na lista de espera de {input.Data:dd/MM/yyyy}. Quando a data liberar, aparece nos lembretes.");
    }

    /// <summary>Id do orçamento gravado (0 enquanto não foi salvo).</summary>
    public int OrcamentoSalvo() => _input.Id;

    private void AgendarVerificacao()
    {
        _atrasoDisponibilidade.Stop();
        _atrasoDisponibilidade.Start();
    }

    private QuantidadesConvidados Faixas() => new((int)_convidados.Value, (int)_meia.Value, (int)_isentos.Value);

    private RegraFaixasEtarias RegraFaixas() =>
        new(_input.IdadeMaximaIsento, _input.IdadeMaximaMeia, _percentualMeia.Value);

    private void Recalcular()
    {
        if (_convidados is null)
            return;
        var faixas = Faixas();
        var regra = RegraFaixas();
        var faixasValidas = faixas.Meia + faixas.Isentos <= faixas.Total;
        var convidados = faixas.Total;
        var resumo = CalculadoraValores.Calcular(_valorPessoa.Value, faixasValidas ? faixas : faixas.Total, _valorLocacao.Value,
            _servicos.Sum(s => s.Total), _percentual.Value, regra);

        _faixas.Text = !faixasValidas
            ? "As crianças (meia e isentas) passam do total de convidados."
            : $"Inteira: {faixas.Inteira}  •  meia ({regra.FaixaMeia}): {faixas.Meia} × {Textos.Moeda(regra.ValorMeia(_valorPessoa.Value))}" +
              $"  •  isentas ({regra.FaixaIsentos}): {faixas.Isentos}  •  pagantes: {faixas.Pagantes}";
        _faixas.ForeColor = faixasValidas ? Tema.TextoSuave : Tema.Perigo;

        _alimentacao.Text = Textos.Moeda(resumo.Alimentacao);
        _valorEspaco.Text = Textos.Moeda(resumo.Espaco);
        _valorServicos.Text = Textos.Moeda(resumo.Servicos);
        _subtotal.Text = Textos.Moeda(resumo.Subtotal);
        _desconto.Text = "- " + Textos.Moeda(resumo.Desconto);
        _total.Text = Textos.Moeda(resumo.Total);
        _porPessoa.Text = convidados > 0 ? $"{Textos.Moeda(resumo.Total / convidados)} por convidado" : "";

        var alertas = new List<string>();
        if (_pacote.SelectedItem is ItemLookup { Id: > 0 } pacote && faixas.Pagantes < pacote.Numero)
            alertas.Add($"⚠ O pacote exige no mínimo {pacote.Numero} convidados pagantes.");
        if (_espaco.SelectedItem is ItemLookup { Id: > 0 } espaco && convidados > espaco.Numero)
            alertas.Add($"⚠ O espaço comporta no máximo {espaco.Numero} pessoas.");
        if (_ajuste.MinimoConvidados is { } minimo && faixas.Pagantes < minimo)
            alertas.Add($"⚠ Nesta data o mínimo é de {minimo} convidados pagantes.");
        _alertas.Text = string.Join(Environment.NewLine, alertas);
        AtualizarAlcada(resumo.Total, faixasValidas ? faixas : faixas.Total);
        AtualizarMargem(resumo, convidados);

        var condicao = _condicao.SelectedItem is ItemLookup { Id: > 0 } item ? _condicoes.FirstOrDefault(c => c.Id == item.Id) : null;
        _simulacaoPagamento.Text = condicao is null
            ? "Sem condição de pagamento definida (o plano é combinado depois, no evento)."
            : $"{condicao.Texto}: {TextosPagamento.Simulacao(condicao, resumo.Total, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(_data.Value))}";
    }

    /// <summary>
    /// Desconto efetivo sobre a tabela da data (pacote e espaço com o ajuste, serviços pelo catálogo, meia do pacote), como o
    /// serviço calcula ao salvar, e se ele passa da alçada do usuário.
    /// </summary>
    private void AtualizarAlcada(decimal total, QuantidadesConvidados faixas)
    {
        var pacote = _pacote.SelectedItem is ItemLookup { Id: > 0 } p ? p : null;
        var espaco = _espaco.SelectedItem is ItemLookup { Id: > 0 } s ? s : null;
        var regraTabela = pacote is not null && _regrasPacotes.TryGetValue(pacote.Id, out var regras)
            ? new RegraFaixasEtarias(regras.IdadeMaximaIsento, regras.IdadeMaximaMeia, regras.PercentualMeia)
            : RegraFaixas();
        var servicos = _servicos.Sum(item => Math.Round(item.Quantidade *
            (_catalogoServicos.FirstOrDefault(c => c.Id == item.ServicoAdicionalId)?.Valor ?? item.ValorUnitario), 2));
        var tabela = CalculadoraValores.Calcular(pacote is null ? 0 : _ajuste.Pessoa(pacote.Valor), faixas,
            espaco is null ? 0 : _ajuste.Locacao(espaco.Valor), servicos, 0, regraTabela).Total;
        var efetivo = tabela <= 0 || total >= tabela ? 0 : Math.Round((tabela - total) / tabela * 100, 2);
        if (efetivo == 0)
        {
            _alcada.Text = "";
            return;
        }

        var autorizado = _input.SituacaoDesconto is SituacaoDesconto.Aprovado && efetivo <= _input.PercentualDescontoAutorizado;
        _alcada.Text = $"Desconto efetivo sobre a tabela: {efetivo:0.##}% ({Textos.Moeda(tabela - total)} abaixo de {Textos.Moeda(tabela)})" +
                       (autorizado ? $"{Environment.NewLine}✔ Aprovado por {_input.DescontoDecididoPor}."
                           : efetivo > _limiteDesconto ? $"{Environment.NewLine}⚠ Passa da sua alçada ({_limiteDesconto:0.##}%): vai precisar de aprovação." : "");
        _alcada.ForeColor = !autorizado && efetivo > _limiteDesconto ? Tema.Alerta : Tema.TextoSuave;
    }

    private static int? Id(ComboBox combo) => combo.SelectedItem is ItemLookup { Id: > 0 } item ? item.Id : null;

    /// <summary>Custos previstos com o que está na tela (pelos mesmos parâmetros que o serviço usa ao salvar).</summary>
    private (CustosOrcamento Custos, PercentuaisReceita Percentuais)? CustosNaTela(decimal total, int convidados)
    {
        if (_parametrosMargem is not { } parametros || _vendedor is null)
            return null;
        var servicos = _servicos.Select(s => (s.ServicoAdicionalId, s.Quantidade)).ToList();
        var (inicio, fim) = PeriodoEvento.Calcular(DateOnly.FromDateTime(_data.Value), TimeOnly.FromDateTime(_inicio.Value), TimeOnly.FromDateTime(_fim.Value));
        var consumo = Domain.Cardapio.ContextoConsumo.De(convidados, Math.Min(convidados, Faixas().Inteira), inicio, fim);
        return (parametros.Calcular(total, Id(_pacote), consumo, Id(_tipoEvento) ?? 0, Id(_espaco), servicos, Id(_vendedor), Id(_forma)),
            parametros.Percentuais(Id(_vendedor), Id(_forma)));
    }

    /// <summary>
    /// Margem prevista: administrador e financeiro veem os custos e a margem; o comercial vê o semáforo. Todos veem o desconto
    /// máximo que ainda mantém a margem mínima.
    /// </summary>
    private void AtualizarMargem(ResumoValores resumo, int convidados)
    {
        if (_parametrosMargem is not { } parametros || CustosNaTela(resumo.Total, convidados) is not { } valores || valores.Custos.Total <= 0)
        {
            _margem.Text = "";
            return;
        }
        var (custos, percentuais) = valores;
        var margem = CalculadoraMargem.Percentual(resumo.Total, custos);
        var faixa = parametros.Faixa(margem);
        var maximo = CalculadoraMargem.DescontoMaximo(resumo.Subtotal, custos.Diretos, percentuais, parametros.MargemMinima);
        _margem.Text = (parametros.PodeVerCustos
                           ? $"Custos previstos: {Textos.Moeda(custos.Total)}  •  margem {Textos.Moeda(resumo.Total - custos.Total)} ({TextosMargem.Percentual(margem)})"
                           : $"Margem: {TextosMargem.Semaforo(faixa)}")
                       + (faixa == FaixaMargem.AbaixoDaMinima ? $"{Environment.NewLine}⚠ Abaixo da mínima: vai precisar da aprovação do administrador." : "")
                       + $"{Environment.NewLine}Desconto máximo para a margem mínima: {maximo:0.##}%";
        _margem.ForeColor = TextosMargem.Cor(faixa);
    }

    private void ImprimirCustos()
    {
        if (_parametrosMargem is not { } parametros)
            return;
        var faixas = Faixas();
        var resumo = CalculadoraValores.Calcular(_valorPessoa.Value, faixas, _valorLocacao.Value, _servicos.Sum(s => s.Total), _percentual.Value, RegraFaixas());
        if (CustosNaTela(resumo.Total, faixas.Total) is not { } valores)
            return;
        var (custos, percentuais) = valores;
        TextosMargem.Interna(_input, resumo.Total, custos, percentuais, parametros, resumo.Subtotal, _opcoes.NomeEmpresa).Visualizar(this);
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
            _entrarEspera.Visible = conflito is not null && _input.PodeEditar;
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
                           : "")
                       + _input.SituacaoDesconto switch
                       {
                           SituacaoDesconto.Aguardando => $"  •  DESCONTO DE {_input.PercentualDescontoAutorizado:0.##}% AGUARDANDO APROVAÇÃO",
                           SituacaoDesconto.Recusado => $"  •  DESCONTO RECUSADO POR {_input.DescontoDecididoPor?.ToUpperInvariant()} ({_input.RespostaDesconto})",
                           _ => ""
                       };
        _status.ForeColor = _input.SituacaoDesconto is SituacaoDesconto.Aguardando or SituacaoDesconto.Recusado
            ? Textos.Cor(_input.SituacaoDesconto)
            : Textos.Cor(_input.Status);

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
                if (_input.PacoteId is null)
                    _input.PercentualMeia = null;
                _input.Id = await DialogosAlcada.GravarAsync(this, permitePedido: true, p => _input.PedidoDesconto = p,
                    () => _servico.SalvarAsync(_input));
                if (_input.SituacaoDesconto == SituacaoDesconto.Aguardando)
                    Mensagens.Info(this, $"Orçamento salvo. O desconto de {_input.PercentualDescontoAutorizado:0.##}% está aguardando a aprovação de quem " +
                                         "tem alçada (nos lembretes e em Comercial › Aprovações de desconto)." +
                                         (enviar ? " A proposta só pode ser enviada depois da aprovação." : ""), "Desconto aguardando aprovação");
                else if (enviar)
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
        await ImpressaoProposta.VisualizarAsync(this, _input, _lookups, _cardapio, _opcoes,
            _condicoes.FirstOrDefault(c => c.Id == _input.CondicaoPagamentoId));
    }
}
