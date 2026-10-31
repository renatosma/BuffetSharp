using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cardapio;

/// <summary>Pacote que o simulador abre já escolhido (nulo: começa vazio).</summary>
internal sealed record AberturaSimulador(int? PacoteId);

/// <summary>
/// Simulador de preço do pacote: monta a composição (de um pacote ou item a item), escolhe o cenário (tipo de evento,
/// convidados, duração, adultos, comissão, taxa e margem alvo) e vê o custo por pessoa, o preço mínimo e o sugerido por
/// número de convidados e a margem do preço atual. Aplica o preço ao pacote ou cria um pacote com a composição.
/// </summary>
internal sealed class FormSimuladorPreco : FormBase
{
    private readonly SimuladorPrecoService _servico;
    private readonly IConsultasLookup _lookups;
    private readonly AberturaSimulador _abertura;
    private DadosSimulador? _dados;
    private bool _carregando = true;

    private readonly ComboBox _pacote = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tipoEvento = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _convidados = new() { Width = 70, Minimum = 1, Maximum = 5_000, Value = 100 };
    private readonly NumericUpDown _horas = new() { Width = 60, Minimum = 1, Maximum = 24, DecimalPlaces = 1, Increment = 0.5m, Value = 5 };
    private readonly NumericUpDown _adultos = new() { Width = 60, Maximum = 100, Value = 100 };
    private readonly NumericUpDown _comissao = new() { Width = 60, Maximum = 20, DecimalPlaces = 2 };
    private readonly NumericUpDown _taxa = new() { Width = 60, Maximum = 20, DecimalPlaces = 2 };
    private readonly NumericUpDown _margemAlvo = new() { Width = 60, Maximum = 80, DecimalPlaces = 1 };
    private readonly CheckedListBox _itens = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly Label _resultado = new() { Dock = DockStyle.Top, AutoSize = true, Font = Tema.FonteSubtitulo, Padding = new Padding(0, 4, 0, 8), MaximumSize = new Size(900, 0) };
    private readonly Grade<ResultadoSimulacao> _tabela = new();
    private readonly Button _aplicar = Tema.BotaoPrimario("Aplicar preço sugerido ao pacote", Icones.Aceitar);
    private readonly Button _criar = Tema.BotaoSecundario("Criar pacote com esta composição", Icones.Adicionar);

    public FormSimuladorPreco(SimuladorPrecoService servico, IConsultasLookup lookups, AberturaSimulador abertura)
    {
        _servico = servico;
        _lookups = lookups;
        _abertura = abertura;
        Text = "Simulador de preço";

        var cenario = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(18, 8, 18, 6), WrapContents = true };
        void Campo(string rotulo, Control controle)
        {
            cenario.Controls.Add(new Label { Text = rotulo, AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(8, 8, 4, 0) });
            controle.Margin = new Padding(0, 4, 8, 0);
            cenario.Controls.Add(controle);
        }
        Campo("Pacote", _pacote);
        Campo("Tipo de evento (equipe)", _tipoEvento);
        Campo("Convidados", _convidados);
        Campo("Duração (h)", _horas);
        Campo("Adultos (%)", _adultos);
        Campo("Comissão (%)", _comissao);
        Campo("Taxa de pagamento (%)", _taxa);
        Campo("Margem alvo (%)", _margemAlvo);

        var composicao = Tema.Cartao(new Padding(10));
        composicao.Dock = DockStyle.Left;
        composicao.Width = LogicalToDeviceUnits(340);
        composicao.Controls.Add(_itens);
        composicao.Controls.Add(new Label { Text = "Composição (marque os itens)", Dock = DockStyle.Top, Height = 26, Font = Tema.FonteNegrito });

        _tabela.Numero("Convidados", r => r.Convidados, peso: 12)
            .Moeda("Insumos / pessoa", r => r.InsumosPorPessoa, 16)
            .Moeda("Equipe / pessoa", r => r.EquipePorPessoa, 16)
            .Moeda("Custo direto / pessoa", r => r.CustoDiretoPorPessoa, 18)
            .Moeda("Preço mínimo", r => r.PrecoMinimo, 16, _ => Tema.Alerta)
            .Moeda("Preço sugerido", r => r.PrecoSugerido, 16, _ => Tema.Sucesso)
            .Texto("Margem do preço atual", r => r.MargemAtual is { } m ? $"{m:0.#}%" : "", 18,
                r => r.MargemAtual is { } m && _dados is { } d ? m < d.Parametros.MargemMinima ? Tema.Perigo : m < d.Parametros.MargemMeta ? Tema.Alerta : Tema.Sucesso : null);

        var resultado = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 4, 4, 4) };
        var acoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        acoes.Controls.AddRange([_aplicar, _criar]);
        resultado.Controls.Add(_tabela.Controle);
        resultado.Controls.Add(_resultado);
        resultado.Controls.Add(acoes);

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 4, 18, 14) };
        corpo.Controls.Add(resultado);
        corpo.Controls.Add(composicao);
        Controls.Add(corpo);
        Controls.Add(cenario);

        _pacote.SelectedIndexChanged += (_, _) => EscolherPacote();
        foreach (var combo in new[] { _tipoEvento })
            combo.SelectedIndexChanged += (_, _) => Recalcular();
        foreach (var numero in new[] { _convidados, _horas, _adultos, _comissao, _taxa, _margemAlvo })
            numero.ValueChanged += (_, _) => Recalcular();
        _itens.ItemCheck += (_, _) => BeginInvoke(Recalcular); // o item só fica marcado depois do evento
        _aplicar.Click += async (_, _) => await ExecutarAsync(AplicarAsync, _aplicar, cursorEspera: false);
        _criar.Click += async (_, _) => await ExecutarAsync(CriarAsync, _criar, cursorEspera: false);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var ok = await ExecutarAsync(async () =>
        {
            _dados = await _servico.DadosAsync();
            _tipoEvento.DataSource = (await _lookups.ListarAsync(TipoLookup.TiposEvento)).ToList();
            _itens.Items.AddRange(_dados.Itens.Cast<object>().ToArray());
            _pacote.Items.Add("(composição livre)");
            _pacote.Items.AddRange(_dados.Pacotes.Cast<object>().ToArray());
            _comissao.Value = Math.Min(_comissao.Maximum, _dados.ComissaoMedia);
            _margemAlvo.Value = Math.Min(_margemAlvo.Maximum, _dados.Parametros.MargemMeta);
            _carregando = false;
            _pacote.SelectedItem = _dados.Pacotes.FirstOrDefault(p => p.Id == _abertura.PacoteId) ?? (object)"(composição livre)";
            Recalcular();
        });
        if (!ok)
            Close();
    }

    private PacoteSimulado? Pacote => _pacote.SelectedItem as PacoteSimulado;

    /// <summary>Escolher um pacote marca a composição dele e passa a mostrar a margem do preço dele.</summary>
    private void EscolherPacote()
    {
        if (_dados is null)
            return;
        _carregando = true;
        for (var i = 0; i < _itens.Items.Count; i++)
            _itens.SetItemChecked(i, Pacote?.Itens.Contains(((ItemSimulado)_itens.Items[i]).Id) == true);
        _carregando = false;
        Recalcular();
    }

    private SimulacaoInput Cenario() => new()
    {
        Itens = _itens.CheckedItems.Cast<ItemSimulado>().Select(i => i.Id).ToList(),
        TipoEventoId = (_tipoEvento.SelectedItem as ItemLookup)?.Id ?? 0,
        Convidados = (int)_convidados.Value,
        Horas = _horas.Value,
        PercentualAdultos = _adultos.Value,
        Comissao = _comissao.Value,
        Taxa = _taxa.Value,
        MargemAlvo = _margemAlvo.Value,
        PrecoAtual = Pacote?.Preco
    };

    private void Recalcular()
    {
        if (_dados is not { } dados || _carregando)
            return;
        var cenario = Cenario();
        var atual = SimuladorPrecoService.Simular(dados, cenario);
        _tabela.Carregar(SimuladorPrecoService.Tabela(dados, cenario));
        _tabela.Selecionar(r => r.Convidados == cenario.Convidados);
        _resultado.Text = cenario.Itens.Count == 0
            ? "Marque os itens da composição (ou escolha um pacote)."
            : $"Para {atual.Convidados} convidados: custo direto de {Textos.Moeda(atual.CustoDiretoPorPessoa)} por pessoa " +
              $"(insumos {Textos.Moeda(atual.InsumosPorPessoa)}, equipe {Textos.Moeda(atual.EquipePorPessoa)}).{Environment.NewLine}" +
              $"Preço mínimo (margem de {dados.Parametros.MargemMinima:0.#}%): {Preco(atual.PrecoMinimo)}  •  " +
              $"sugerido (margem de {cenario.MargemAlvo:0.#}%): {Preco(atual.PrecoSugerido)}" +
              (atual.PrecoAtual is { } preco ? $"{Environment.NewLine}Preço atual do pacote: {Textos.Moeda(preco)}, margem de {atual.MargemAtual:0.#}%." : "") +
              $"{Environment.NewLine}Impostos de {dados.Parametros.AliquotaImpostos:0.#}% sobre a receita; considera que todos pagam inteira.";
        _resultado.ForeColor = atual.MargemAtual is { } m && m < dados.Parametros.MargemMinima ? Tema.Perigo : Tema.Texto;
        _aplicar.Enabled = Pacote is not null && atual.PrecoSugerido is not null && cenario.Itens.Count > 0;
        _criar.Enabled = cenario.Itens.Count > 0 && atual.PrecoSugerido is not null;
    }

    private static string Preco(decimal? preco) => preco is { } p ? Textos.Moeda(p) : "inviável (os percentuais já passam da margem)";

    private async Task AplicarAsync()
    {
        if (Pacote is not { } pacote || _dados is null || SimuladorPrecoService.Simular(_dados, Cenario()).PrecoSugerido is not { } preco)
            return;
        if (!Mensagens.Confirmar(this, $"Mudar o preço do pacote {pacote.Nome} de {Textos.Moeda(pacote.Preco)} para {Textos.Moeda(preco)} por pessoa? " +
                                       "Os orçamentos já feitos não mudam.", "Aplicar preço"))
            return;
        await _servico.AplicarPrecoAsync(pacote.Id, preco);
        await RecarregarAsync(pacote.Id);
        Mensagens.Info(this, $"O pacote {pacote.Nome} passou a custar {Textos.Moeda(preco)} por pessoa.", "Preço aplicado");
    }

    private async Task CriarAsync()
    {
        if (_dados is null || SimuladorPrecoService.Simular(_dados, Cenario()) is not { PrecoSugerido: { } preco })
            return;
        var nome = Dialogos.PedirTexto(this, "Criar pacote", "Nome do pacote", linhas: 1,
            subtitulo: $"{_itens.CheckedItems.Count} itens, a {Textos.Moeda(preco)} por pessoa (o preço sugerido). Ajuste o resto no cadastro de pacotes.");
        if (nome is null)
            return;
        var id = await _servico.CriarPacoteAsync(nome, Cenario().Itens, preco);
        await RecarregarAsync(id);
        Mensagens.Info(this, $"Pacote {nome} criado a {Textos.Moeda(preco)} por pessoa.", "Pacote criado");
    }

    private async Task RecarregarAsync(int pacoteId)
    {
        _carregando = true;
        _dados = await _servico.DadosAsync();
        _pacote.Items.Clear();
        _pacote.Items.Add("(composição livre)");
        _pacote.Items.AddRange(_dados.Pacotes.Cast<object>().ToArray());
        _carregando = false;
        _pacote.SelectedItem = _dados.Pacotes.FirstOrDefault(p => p.Id == pacoteId) ?? (object)"(composição livre)";
    }
}
