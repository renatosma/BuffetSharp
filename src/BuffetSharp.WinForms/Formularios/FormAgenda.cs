using BuffetSharp.Application.Consultas;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios;

/// <summary>Agenda mensal de ocupação dos espaços (eventos e pré-reservas).</summary>
internal sealed class FormAgenda : FormBase
{
    private readonly IConsultasAgenda _agenda;
    private readonly IConsultasLookup _lookups;
    private readonly Navegador _navegador;

    private readonly CalendarioMensal _calendario = new() { Dock = DockStyle.Fill };
    private readonly Label _mes = new() { AutoSize = true, Font = Tema.FonteSubtitulo, Margin = new Padding(10, 6, 10, 0) };
    private readonly ComboBox _espaco = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 3, 12, 0) };
    private readonly CheckBox _preReservas = new() { Text = "Mostrar pré-reservas", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
    private readonly FlowLayoutPanel _legenda = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
    private readonly Label _tituloDia = new() { Dock = DockStyle.Top, Height = 30, Font = Tema.FonteNegrito };
    private readonly Grade<ItemAgenda> _doDia = new();
    private IReadOnlyList<ItemAgenda> _itensMes = [];
    private IReadOnlyDictionary<DateOnly, int> _esperasMes = new Dictionary<DateOnly, int>();

    /// <summary>Eventos confirmados do mês com menos equipe que o dimensionado (Id → quantos faltam).</summary>
    private IReadOnlyDictionary<int, int> _faltasEquipe = new Dictionary<int, int>();

    public FormAgenda(IConsultasAgenda agenda, IConsultasLookup lookups, Navegador navegador)
    {
        _agenda = agenda;
        _lookups = lookups;
        _navegador = navegador;
        Text = "Agenda";

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(18, 4, 18, 8), WrapContents = true };
        var anterior = Tema.BotaoSecundario("◀");
        anterior.Click += async (_, _) => await MudarMesAsync(-1);
        var hoje = Tema.BotaoSecundario("Hoje");
        hoje.Click += async (_, _) =>
        {
            _calendario.Mes = DateOnly.FromDateTime(DateTime.Today);
            await CarregarAsync();
        };
        var proximo = Tema.BotaoSecundario("▶");
        proximo.Click += async (_, _) => await MudarMesAsync(1);
        var exportar = Tema.BotaoSecundario("Exportar agenda (.ics)", Icones.Exportar);
        exportar.Click += async (_, _) => await ExecutarAsync(ExportarAsync, exportar, cursorEspera: false);
        barra.Controls.AddRange([anterior, hoje, proximo, _mes,
            new Label { Text = "Espaço:", AutoSize = true, Margin = new Padding(12, 7, 0, 0), ForeColor = Tema.TextoSuave }, _espaco, _preReservas, exportar, _legenda]);
        _espaco.SelectionChangeCommitted += async (_, _) => await CarregarAsync();
        _preReservas.CheckedChanged += async (_, _) => await CarregarAsync();

        _calendario.ItemAberto += AbrirItem;
        _calendario.DiaSelecionado += MostrarDia;

        _doDia.Data("Início", i => i.Inicio, "HH:mm", 18)
            .Texto("Evento", i => i.Titulo, 60)
            .Texto("Espaço", i => i.Espaco, 30)
            .Numero("Conv.", i => i.Convidados, peso: 14)
            .Texto("Equipe", i => i.Origem == "Evento" && _faltasEquipe.TryGetValue(i.Id, out var faltam) ? $"faltam {faltam}" : "", 18, _ => Tema.Alerta);
        _doDia.DuploClique += AbrirItem;

        var lateral = Tema.Cartao(new Padding(10));
        lateral.Dock = DockStyle.Right;
        lateral.Width = LogicalToDeviceUnits(380);
        lateral.Controls.Add(_doDia.Controle);
        lateral.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            ForeColor = Tema.TextoSuave,
            Text = "Duplo clique abre o evento (ou o orçamento, nas pré-reservas tracejadas)."
        });
        lateral.Controls.Add(_tituloDia);

        var cartaoCalendario = Tema.Cartao(new Padding(1));
        cartaoCalendario.Dock = DockStyle.Fill;
        cartaoCalendario.Controls.Add(_calendario);

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 14) };
        corpo.Controls.Add(cartaoCalendario);
        corpo.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
        corpo.Controls.Add(lateral);

        var cabecalho = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 14, 20, 4) };
        cabecalho.Controls.Add(Tema.Titulo("Agenda"));
        cabecalho.Controls.Add(Tema.Subtitulo($"Ocupação dos espaços. O sistema exige intervalo mínimo entre eventos no mesmo espaço."));

        Controls.Add(corpo);
        Controls.Add(barra);
        Controls.Add(cabecalho);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(async () =>
        {
            var espacos = (await _lookups.ListarAsync(TipoLookup.Espacos)).ToList();
            foreach (var espaco in espacos)
            {
                _legenda.Controls.Add(new Label
                {
                    Text = "■ " + espaco.Nome,
                    ForeColor = CalendarioMensal.CorDoEspaco(espaco.Id),
                    AutoSize = true,
                    Margin = new Padding(0, 7, 10, 0)
                });
            }
            espacos.Insert(0, new ItemLookup { Id = 0, Nome = "(todos)" });
            _espaco.DataSource = espacos;
        });
        await CarregarAsync();
        MostrarDia(_calendario.DiaAtual);
    }

    /// <summary>
    /// Eventos e degustações dos próximos 12 meses (a partir do mês exibido) num arquivo para importar no Google Agenda ou Outlook.
    /// Importar de novo atualiza os mesmos compromissos.
    /// </summary>
    private async Task ExportarAsync()
    {
        var de = _calendario.Mes;
        using var salvar = new SaveFileDialog
        {
            FileName = $"agenda-buffet-{de:yyyy-MM}.ics", Filter = "Agenda (*.ics)|*.ics", Title = "Exportar a agenda do buffet"
        };
        if (salvar.ShowDialog(this) != DialogResult.OK)
            return;
        var ics = await _navegador.Servico<Application.Servicos.ConviteService>().AgendaAsync(de, de.AddMonths(12).AddDays(-1));
        await File.WriteAllTextAsync(salvar.FileName, ics, new System.Text.UTF8Encoding(false));
        Mensagens.Info(this, $"Agenda de {de:MM/yyyy} a {de.AddMonths(11):MM/yyyy} exportada. Importe o arquivo no Google Agenda (Configurações › " +
                             "Importar) ou abra no Outlook; importar de novo atualiza os mesmos compromissos.", "Exportar agenda");
    }

    private Task MudarMesAsync(int meses)
    {
        _calendario.Mes = _calendario.Mes.AddMonths(meses);
        return CarregarAsync();
    }

    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        _mes.Text = _calendario.Mes.ToString("MMMM 'de' yyyy", Textos.PtBr);
        var inicio = _calendario.InicioGrade.ToDateTime(TimeOnly.MinValue);
        var espacoId = (_espaco.SelectedItem as ItemLookup)?.Id;
        _itensMes = await _agenda.ListarAsync(inicio, inicio.AddDays(42), espacoId is 0 ? null : espacoId, _preReservas.Checked);
        _calendario.DefinirItens(_itensMes);
        _esperasMes = await _navegador.Servico<Application.Servicos.EsperaService>().PorDiaAsync(_calendario.InicioGrade, _calendario.InicioGrade.AddDays(41));
        _calendario.DefinirEsperas(_esperasMes);
        _faltasEquipe = await _navegador.Servico<Application.Servicos.DimensionamentoService>().FaltasAsync(inicio, inicio.AddDays(42));
        MostrarDia(_calendario.DiaAtual);
    });

    private void MostrarDia(DateOnly dia)
    {
        _tituloDia.Text = dia.ToString("dddd, dd/MM/yyyy", Textos.PtBr) +
                          (_esperasMes.TryGetValue(dia, out var esperas) ? $"  •  {esperas} na lista de espera" : "") +
                          (_itensMes.Count(i => DateOnly.FromDateTime(i.Inicio) == dia && i.Origem == "Evento" && _faltasEquipe.ContainsKey(i.Id)) is > 0 and var incompletos
                              ? $"  •  {incompletos} com equipe incompleta" : "");
        _doDia.Carregar(_itensMes.Where(i => DateOnly.FromDateTime(i.Inicio) == dia).OrderBy(i => i.Inicio));
    }

    private void AbrirItem(ItemAgenda item)
    {
        if (item.Provisorio)
        {
            using var dialogo = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(item.Id));
            dialogo.ShowDialog(this);
            _ = CarregarAsync();
        }
        else
        {
            _navegador.Abrir($"evento:{item.Id}", () => _navegador.Criar<FormEventoDetalhe>(item.Id));
        }
    }
}
