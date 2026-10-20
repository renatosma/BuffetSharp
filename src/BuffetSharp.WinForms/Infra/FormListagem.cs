using BuffetSharp.WinForms.Impressao;

namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Base das telas de consulta: cabeçalho, barra de ações, filtros, grade e rodapé com totais.
/// Inclui pesquisa local, atualização (F5), impressão e exportação CSV da grade.
/// </summary>
internal abstract class FormListagem : FormBase
{
    protected readonly ToolStrip Ferramentas;
    protected readonly FlowLayoutPanel Filtros;
    protected readonly TextBox Busca;
    protected readonly DataGridView GradeControle = new();
    protected readonly Label Rodape;
    private readonly string _titulo;
    private readonly System.Windows.Forms.Timer _atrasoBusca = new() { Interval = 250 };
    private readonly SemaphoreSlim _carga = new(1, 1);

    protected FormListagem(string titulo, string? subtitulo = null)
    {
        _titulo = titulo;
        Text = titulo;

        var cabecalho = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(20, 14, 20, 6)
        };
        cabecalho.Controls.Add(Tema.Titulo(titulo));
        if (subtitulo is not null)
            cabecalho.Controls.Add(Tema.Subtitulo(subtitulo));

        Ferramentas = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Tema.Fundo,
            Padding = new Padding(16, 2, 16, 2),
            RenderMode = ToolStripRenderMode.System,
            ImageScalingSize = new Size(16, 16).Escalar(this)
        };
        Ferramentas.Renderer = new RenderizadorBarra();

        Busca = new TextBox { Width = 260, PlaceholderText = "Pesquisar (Ctrl+F)...", Margin = new Padding(0, 4, 12, 4) };
        Busca.TextChanged += (_, _) =>
        {
            _atrasoBusca.Stop();
            _atrasoBusca.Start();
        };
        _atrasoBusca.Tick += (_, _) =>
        {
            _atrasoBusca.Stop();
            AoPesquisar(Busca.Text);
        };

        Filtros = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(20, 4, 20, 6),
            WrapContents = true
        };
        Filtros.Controls.Add(Busca);

        var cartao = Tema.Cartao(new Padding(1));
        cartao.Dock = DockStyle.Fill;
        cartao.Margin = new Padding(0);
        cartao.Controls.Add(GradeControle);
        var area = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 4) };
        area.Controls.Add(cartao);

        Rodape = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(22, 0, 20, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Tema.TextoSuave
        };

        Controls.Add(area);
        Controls.Add(Filtros);
        Controls.Add(Ferramentas);
        Controls.Add(cabecalho);
        Controls.Add(Rodape);
    }

    /// <summary>Carrega (ou recarrega) os dados da tela.</summary>
    protected abstract Task CarregarAsync();

    /// <summary>Pesquisa local por padrão; telas com filtro no banco podem sobrescrever.</summary>
    protected virtual void AoPesquisar(string texto)
    {
    }

    /// <summary>
    /// Recarrega em fila: mudar dois filtros seguidos dispara duas cargas, e a de filtros antigos não pode terminar depois
    /// da nova e deixar a grade desatualizada. Cada carga lê os filtros quando começa, então a última mostra o estado atual.
    /// </summary>
    protected Task RecarregarAsync() => ExecutarAsync(async () =>
    {
        await _carga.WaitAsync();
        try
        {
            await CarregarAsync();
        }
        finally
        {
            _carga.Release();
        }
    });

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        AdicionarAcoesPadrao();
        await RecarregarAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F5)
        {
            e.Handled = true;
            _ = RecarregarAsync();
        }
        else if (e.Control && e.KeyCode == Keys.F)
        {
            e.Handled = true;
            Busca.Focus();
            Busca.SelectAll();
        }
    }

    protected ToolStripButton Botao(string texto, char icone, Func<Task> acao, string? dica = null, bool destaque = false)
    {
        var botao = new ToolStripButton(texto, Icones.Imagem(icone, destaque ? Tema.Primaria : Tema.Texto))
        {
            ToolTipText = dica ?? texto,
            Margin = new Padding(0, 1, 4, 2),
            Padding = new Padding(4, 2, 4, 2),
            Font = destaque ? Tema.FonteNegrito : Tema.Fonte,
            ForeColor = destaque ? Tema.Primaria : Tema.Texto
        };
        botao.Click += async (_, _) =>
        {
            botao.Enabled = false;
            try
            {
                await ExecutarAsync(acao, cursorEspera: false);
            }
            finally
            {
                botao.Enabled = true;
            }
        };
        Ferramentas.Items.Add(botao);
        return botao;
    }

    protected void Separador() => Ferramentas.Items.Add(new ToolStripSeparator());

    /// <summary>Controle de filtro com rótulo, adicionado à barra de filtros.</summary>
    protected T Filtro<T>(string rotulo, T controle) where T : Control
    {
        var painel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 12, 0) };
        painel.Controls.Add(new Label { Text = rotulo, AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 8, 4, 0) });
        controle.Margin = new Padding(0, 4, 0, 4);
        painel.Controls.Add(controle);
        Filtros.Controls.Add(painel);
        return controle;
    }

    protected DateTimePicker FiltroData(string rotulo, DateTime? valor, bool opcional = true)
    {
        var data = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 150, ShowCheckBox = opcional };
        data.Value = valor ?? DateTime.Today;
        if (opcional)
            data.Checked = valor is not null;
        return Filtro(rotulo, data);
    }

    protected static DateTime? ValorData(DateTimePicker data) => !data.ShowCheckBox || data.Checked ? data.Value.Date : null;

    private void AdicionarAcoesPadrao()
    {
        var direita = new ToolStripDropDownButton("Mais", Icones.Imagem(Icones.Lista, Tema.Texto))
        {
            Alignment = ToolStripItemAlignment.Right
        };
        direita.DropDownItems.Add("Atualizar (F5)", Icones.Imagem(Icones.Atualizar, Tema.Texto), async (_, _) => await RecarregarAsync());
        direita.DropDownItems.Add("Imprimir listagem", Icones.Imagem(Icones.Imprimir, Tema.Texto), (_, _) => ImprimirListagem());
        direita.DropDownItems.Add("Exportar para Excel (CSV)", Icones.Imagem(Icones.Exportar, Tema.Texto), (_, _) => ExportarCsv());
        Ferramentas.Items.Add(direita);
    }

    /// <summary>Tabela exibida (formatada) para impressão/exportação.</summary>
    protected abstract (string[] Cabecalho, List<string[]> Linhas) TabelaAtual();

    protected abstract string CsvAtual();

    private void ImprimirListagem()
    {
        var (cabecalho, linhas) = TabelaAtual();
        var documento = new DocumentoImpresso(_titulo);
        documento.Subtitulo($"{linhas.Count} registro(s) - emitido em {DateTime.Now:dd/MM/yyyy HH:mm}");
        documento.Tabela(cabecalho, linhas);
        documento.Visualizar(this, paisagem: cabecalho.Length > 6);
    }

    private void ExportarCsv()
    {
        using var dialogo = new SaveFileDialog
        {
            Filter = "Planilha CSV (*.csv)|*.csv",
            FileName = $"{_titulo.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        // UTF-8 com BOM para o Excel reconhecer acentos.
        File.WriteAllText(dialogo.FileName, CsvAtual(), new System.Text.UTF8Encoding(true));
        Mensagens.Info(this, $"Arquivo gerado:{Environment.NewLine}{dialogo.FileName}", "Exportação concluída");
    }

    private sealed class RenderizadorBarra : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }
    }
}

/// <summary>Listagem tipada sobre uma <see cref="Grade{T}"/>.</summary>
internal abstract class FormListagem<T> : FormListagem where T : class
{
    protected readonly Grade<T> Grade;

    protected FormListagem(string titulo, string? subtitulo = null) : base(titulo, subtitulo)
    {
        Grade = new Grade<T>(GradeControle);
        Grade.DuploClique += item => _ = ExecutarAsync(() => AoAbrirAsync(item));
    }

    protected virtual Task AoAbrirAsync(T item) => Task.CompletedTask;

    protected override void AoPesquisar(string texto)
    {
        Grade.Filtrar(texto);
        AtualizarRodape();
    }

    protected void Exibir(IEnumerable<T> itens)
    {
        Grade.Carregar(itens, Busca.Text);
        AtualizarRodape();
    }

    /// <summary>Texto do rodapé (contagem e totais). Pode ser sobrescrito para mostrar somatórios.</summary>
    protected virtual string TextoRodape(IReadOnlyList<T> visiveis) => $"{visiveis.Count} registro(s)";

    protected void AtualizarRodape() => Rodape.Text = TextoRodape(Grade.Visiveis.ToList());

    /// <summary>Item selecionado; avisa o usuário quando não há seleção.</summary>
    protected T? Selecionado()
    {
        var item = Grade.Selecionado;
        if (item is null)
            Mensagens.Aviso(this, "Selecione um registro na lista.");
        return item;
    }

    protected override (string[] Cabecalho, List<string[]> Linhas) TabelaAtual() => Grade.ObterTabela();

    protected override string CsvAtual() => Grade.ParaCsv();
}

internal static class ExtensoesTamanho
{
    public static Size Escalar(this Size tamanho, Control referencia) =>
        new(referencia.LogicalToDeviceUnits(tamanho.Width), referencia.LogicalToDeviceUnits(tamanho.Height));
}
