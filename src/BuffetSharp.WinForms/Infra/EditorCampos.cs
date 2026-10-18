using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Monta formulários de edição em código: cada campo tem rótulo, controle, função de leitura
/// (Carregar) e de gravação (Aplicar). Erros de validação do domínio são exibidos no campo correspondente.
/// </summary>
internal sealed class EditorCampos
{
    private readonly List<Action> _carregar = [];
    private readonly List<Action> _aplicar = [];
    private readonly Dictionary<string, Control> _porPropriedade = new(StringComparer.OrdinalIgnoreCase);
    private readonly ErrorProvider _erros = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly int _colunas;
    private int _coluna;
    private int _linha = -1;

    public EditorCampos(int colunas = 2)
    {
        _colunas = colunas;
        Painel = new TableLayoutPanel
        {
            ColumnCount = colunas,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        for (var i = 0; i < colunas; i++)
            Painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colunas));
    }

    public TableLayoutPanel Painel { get; }

    public bool SomenteLeitura { get; private set; }

    // ============================== Campos ==============================

    public TextBox Texto(string rotulo, Func<string?> obter, Action<string?> definir, string? propriedade = null,
        int maximo = 0, int span = 1, int linhas = 1, string? dica = null)
    {
        var caixa = new TextBox { MaxLength = maximo, Multiline = linhas > 1, PlaceholderText = dica ?? string.Empty };
        if (linhas > 1)
        {
            caixa.Height = caixa.Font.Height * linhas + 10;
            caixa.ScrollBars = ScrollBars.Vertical;
            caixa.AcceptsReturn = true;
        }
        Adicionar(rotulo, caixa, propriedade, span);
        _carregar.Add(() => caixa.Text = linhas > 1 ? obter()?.ReplaceLineEndings() ?? string.Empty : obter() ?? string.Empty);
        _aplicar.Add(() => definir(string.IsNullOrWhiteSpace(caixa.Text) ? null : caixa.Text.Trim()));
        return caixa;
    }

    public TextBox Senha(string rotulo, Action<string?> definir, string? propriedade = null, int span = 1, string? dica = null)
    {
        var caixa = new TextBox { UseSystemPasswordChar = true, PlaceholderText = dica ?? string.Empty };
        Adicionar(rotulo, caixa, propriedade, span);
        _aplicar.Add(() => definir(string.IsNullOrEmpty(caixa.Text) ? null : caixa.Text));
        return caixa;
    }

    /// <summary>Campo com máscara (telefone, CEP). O valor é gravado com a formatação.</summary>
    public MaskedTextBox Mascara(string rotulo, string mascara, Func<string?> obter, Action<string?> definir,
        string? propriedade = null, int span = 1)
    {
        var caixa = new MaskedTextBox(mascara) { TextMaskFormat = MaskFormat.IncludeLiterals, PromptChar = '_' };
        Adicionar(rotulo, caixa, propriedade, span);
        _carregar.Add(() => caixa.Text = obter() ?? string.Empty);
        _aplicar.Add(() =>
        {
            caixa.TextMaskFormat = MaskFormat.ExcludePromptAndLiterals;
            var vazio = string.IsNullOrWhiteSpace(caixa.Text);
            caixa.TextMaskFormat = MaskFormat.IncludeLiterals;
            definir(vazio ? null : caixa.Text);
        });
        return caixa;
    }

    /// <summary>CPF/CNPJ: aceita com ou sem pontuação, formata ao sair do campo e grava somente os dígitos.</summary>
    public TextBox Documento(string rotulo, Func<string?> obter, Action<string?> definir, string? propriedade = null, int span = 1)
    {
        var caixa = new TextBox { MaxLength = 18, PlaceholderText = "CPF ou CNPJ" };
        caixa.Leave += (_, _) => caixa.Text = Domain.Comum.Documento.Formatar(caixa.Text);
        Adicionar(rotulo, caixa, propriedade, span);
        _carregar.Add(() => caixa.Text = Domain.Comum.Documento.Formatar(obter()));
        _aplicar.Add(() =>
        {
            var digitos = Domain.Comum.Documento.SomenteDigitos(caixa.Text);
            definir(digitos.Length == 0 ? null : digitos);
        });
        return caixa;
    }

    public NumericUpDown Decimal(string rotulo, Func<decimal> obter, Action<decimal> definir, string? propriedade = null,
        int casas = 2, decimal minimo = 0, decimal maximo = 99_999_999, int span = 1)
    {
        var numero = new NumericUpDown
        {
            DecimalPlaces = casas,
            Minimum = minimo,
            Maximum = maximo,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right,
            Increment = casas == 0 ? 1 : casas >= 3 ? 0.001m : 1m
        };
        numero.Enter += (_, _) => numero.Select(0, numero.Text.Length);
        Adicionar(rotulo, numero, propriedade, span);
        _carregar.Add(() => numero.Value = Math.Clamp(obter(), minimo, maximo));
        _aplicar.Add(() => definir(numero.Value));
        return numero;
    }

    public NumericUpDown Inteiro(string rotulo, Func<int> obter, Action<int> definir, string? propriedade = null,
        int minimo = 0, int maximo = 100_000, int span = 1) =>
        Decimal(rotulo, () => obter(), v => definir((int)v), propriedade, 0, minimo, maximo, span);

    public DateTimePicker Data(string rotulo, Func<DateOnly?> obter, Action<DateOnly?> definir, string? propriedade = null,
        bool opcional = false, int span = 1)
    {
        var data = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = opcional };
        Adicionar(rotulo, data, propriedade, span);
        _carregar.Add(() =>
        {
            var valor = obter();
            data.Value = (valor ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
            if (opcional)
                data.Checked = valor is not null;
        });
        _aplicar.Add(() => definir(opcional && !data.Checked ? null : DateOnly.FromDateTime(data.Value)));
        return data;
    }

    public DateTimePicker Hora(string rotulo, Func<TimeOnly> obter, Action<TimeOnly> definir, string? propriedade = null, int span = 1)
    {
        var hora = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };
        Adicionar(rotulo, hora, propriedade, span);
        _carregar.Add(() => hora.Value = DateTime.Today.Add(obter().ToTimeSpan()));
        _aplicar.Add(() => definir(TimeOnly.FromDateTime(hora.Value)));
        return hora;
    }

    public CheckBox Marcador(string texto, Func<bool> obter, Action<bool> definir, string? propriedade = null, int span = 1)
    {
        var marcador = new CheckBox { Text = texto, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        Adicionar(" ", marcador, propriedade, span);
        _carregar.Add(() => marcador.Checked = obter());
        _aplicar.Add(() => definir(marcador.Checked));
        return marcador;
    }

    /// <summary>Combo de cadastro relacionado (Id). Permite digitar para localizar.</summary>
    public ComboBox Lista(string rotulo, IEnumerable<ItemLookup> itens, Func<int?> obter, Action<int?> definir,
        string? propriedade = null, bool permitirVazio = false, int span = 1)
    {
        var opcoes = itens.ToList();
        if (permitirVazio)
            opcoes.Insert(0, new ItemLookup { Id = 0, Nome = "(nenhum)" });

        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            DataSource = opcoes,
            DisplayMember = nameof(ItemLookup.Nome)
        };
        combo.Validating += (_, _) =>
        {
            if (combo.SelectedIndex < 0 && combo.Text.Length > 0)
                combo.SelectedIndex = combo.FindStringExact(combo.Text);
        };
        // O combo editável do Win32 seleciona todo o texto ao ser redimensionado; desfaz o realce.
        combo.Resize += (_, _) =>
        {
            if (combo.IsHandleCreated && !combo.Focused)
                combo.BeginInvoke(() => combo.SelectionLength = 0);
        };
        Adicionar(rotulo, combo, propriedade, span);
        _carregar.Add(() =>
        {
            var id = obter() ?? 0;
            var indice = opcoes.FindIndex(o => o.Id == id);
            combo.SelectedIndex = indice;
            if (indice < 0)
                combo.Text = string.Empty;
            combo.SelectionLength = 0;
        });
        _aplicar.Add(() =>
        {
            if (combo.SelectedIndex < 0 && combo.Text.Length > 0)
                combo.SelectedIndex = combo.FindStringExact(combo.Text);
            var id = (combo.SelectedItem as ItemLookup)?.Id ?? 0;
            definir(id == 0 ? null : id);
        });
        return combo;
    }

    public ComboBox Opcoes<TEnum>(string rotulo, Func<TEnum> obter, Action<TEnum> definir, string? propriedade = null, int span = 1)
        where TEnum : struct, Enum
    {
        var opcoes = Infra.Textos.Opcoes<TEnum>();
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = opcoes };
        Adicionar(rotulo, combo, propriedade, span);
        _carregar.Add(() => combo.SelectedIndex = opcoes.FindIndex(o => o.Valor.Equals(obter())));
        _aplicar.Add(() =>
        {
            if (combo.SelectedItem is OpcaoEnum<TEnum> opcao)
                definir(opcao.Valor);
        });
        return combo;
    }

    /// <summary>Valor somente leitura (ex.: saldo de estoque controlado pelo sistema).</summary>
    public Label Informacao(string rotulo, Func<string> obter, int span = 1)
    {
        var valor = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 4, 0, 4) };
        Adicionar(rotulo, valor, null, span);
        _carregar.Add(() => valor.Text = obter());
        return valor;
    }

    /// <summary>Título de seção ocupando a linha inteira.</summary>
    public void Secao(string titulo)
    {
        NovaLinha();
        var rotulo = new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = new Font(Tema.FonteNegrito.FontFamily, 8.5f, FontStyle.Bold),
            ForeColor = Tema.Primaria,
            AutoSize = true,
            Margin = new Padding(4, _linha == 0 ? 0 : 14, 4, 2)
        };
        Painel.Controls.Add(rotulo, 0, _linha);
        Painel.SetColumnSpan(rotulo, _colunas);
        _coluna = _colunas;
    }

    /// <summary>Insere um controle livre (grade, painel...) ocupando a linha inteira.</summary>
    public void Livre(Control controle, string? rotulo = null)
    {
        NovaLinha();
        controle.Dock = DockStyle.Fill;
        if (rotulo is null)
        {
            controle.Margin = new Padding(4);
            Painel.Controls.Add(controle, 0, _linha);
            Painel.SetColumnSpan(controle, _colunas);
        }
        else
        {
            Adicionar(rotulo, controle, null, _colunas);
        }
        _coluna = _colunas;
    }

    /// <summary>Ação extra executada ao carregar os campos (ex.: preencher uma lista editável).</summary>
    public void AoCarregar(Action acao) => _carregar.Add(acao);

    /// <summary>Ação extra executada antes de salvar (ex.: devolver ao cadastro os itens de uma lista editável).</summary>
    public void AoAplicar(Action acao) => _aplicar.Add(acao);

    /// <summary>Associa um controle livre a uma propriedade, para destacar os erros de validação dela.</summary>
    public void Associar(string propriedade, Control controle) => _porPropriedade[propriedade] = controle;

    public void NovaLinha()
    {
        _linha++;
        _coluna = 0;
        Painel.RowCount = _linha + 1;
        Painel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private void Adicionar(string rotulo, Control controle, string? propriedade, int span)
    {
        span = Math.Clamp(span, 1, _colunas);
        if (_linha < 0 || _coluna + span > _colunas)
            NovaLinha();

        var bloco = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 3, 12, 3)
        };
        bloco.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bloco.Controls.Add(new Label
        {
            Text = rotulo,
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Margin = new Padding(0, 0, 0, 2)
        }, 0, 0);

        controle.Margin = new Padding(0);
        if (controle is not Label and not CheckBox)
            controle.Dock = DockStyle.Fill;
        bloco.Controls.Add(controle, 0, 1);

        Painel.Controls.Add(bloco, _coluna, _linha);
        Painel.SetColumnSpan(bloco, span);
        _coluna += span;

        if (propriedade is not null)
            _porPropriedade[propriedade] = controle;
    }

    // ============================== Ciclo ==============================

    public void Carregar()
    {
        foreach (var acao in _carregar)
            acao();
        LimparErros();
    }

    public void Aplicar()
    {
        foreach (var acao in _aplicar)
            acao();
    }

    public void DefinirSomenteLeitura(bool somenteLeitura)
    {
        SomenteLeitura = somenteLeitura;
        foreach (var controle in _porPropriedade.Values.Concat(Painel.Controls.OfType<TableLayoutPanel>()
                     .SelectMany(b => b.Controls.OfType<Control>().Skip(1))).Distinct())
        {
            switch (controle)
            {
                case TextBoxBase caixa: caixa.ReadOnly = somenteLeitura; break;
                case Label: break;
                default: controle.Enabled = !somenteLeitura; break;
            }
        }
    }

    public Control? Controle(string propriedade) => _porPropriedade.GetValueOrDefault(propriedade);

    /// <summary>Exibe os erros ao lado dos campos. Retorna verdadeiro se algum erro foi associado a um campo.</summary>
    public bool MostrarErros(ValidacaoException erro)
    {
        LimparErros();
        var associou = false;
        foreach (var resultado in erro.Erros)
        {
            foreach (var membro in resultado.MemberNames)
            {
                if (_porPropriedade.TryGetValue(membro, out var controle))
                {
                    _erros.SetError(controle, resultado.ErrorMessage);
                    _erros.SetIconPadding(controle, -20);
                    associou = true;
                }
            }
        }
        if (associou)
            _porPropriedade.Values.FirstOrDefault(c => _erros.GetError(c).Length > 0)?.Focus();
        return associou;
    }

    public void LimparErros()
    {
        foreach (var controle in _porPropriedade.Values)
            _erros.SetError(controle, string.Empty);
    }
}
