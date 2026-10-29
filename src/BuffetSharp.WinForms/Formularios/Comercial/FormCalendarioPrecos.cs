using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Calendário de preços: o ajuste de cada dia do mês (para um pacote e espaço), para o vendedor consultar ao negociar.</summary>
internal sealed class FormCalendarioPrecos : FormBase
{
    private readonly TabelaPrecoService _precos;
    private readonly IConsultasLookup _lookups;
    private readonly Navegador _navegador;
    private readonly CalendarioPrecos _calendario = new() { Dock = DockStyle.Fill };
    private readonly Label _mes = new() { AutoSize = true, Font = Tema.FonteSubtitulo, Margin = new Padding(8, 8, 8, 0) };
    private readonly ComboBox _pacote = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 12, 0) };
    private readonly ComboBox _espaco = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 12, 0) };
    private readonly Label _legenda = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 6, 0, 0) };
    private DateOnly _mesAtual = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public FormCalendarioPrecos(TabelaPrecoService precos, IConsultasLookup lookups, Navegador navegador)
    {
        _precos = precos;
        _lookups = lookups;
        _navegador = navegador;
        Text = "Calendário de preços";

        var titulo = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20, 14, 20, 0), FlowDirection = FlowDirection.TopDown };
        titulo.Controls.Add(Tema.Titulo("Calendário de preços"));
        titulo.Controls.Add(Tema.Subtitulo("Ajuste de cada dia sobre o preço de tabela (as regras do dia se somam). Passe o mouse no dia para ver as regras."));

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 6, 16, 4), WrapContents = true };
        var anterior = Tema.BotaoSecundario("◀");
        var proximo = Tema.BotaoSecundario("▶");
        var hoje = Tema.BotaoSecundario("Hoje");
        var regras = Tema.BotaoSecundario("Regras de preço", Icones.Calculadora);
        anterior.Click += async (_, _) => await MudarMesAsync(-1);
        proximo.Click += async (_, _) => await MudarMesAsync(1);
        hoje.Click += async (_, _) =>
        {
            _mesAtual = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            await ExecutarAsync(CarregarAsync);
        };
        regras.Click += (_, _) => _navegador.Abrir($"cadastro:{nameof(RegraPreco)}",
            () => _navegador.Criar<Cadastros.FormCadastro<RegraPreco>>(Cadastros.DefinicoesCadastros.RegrasPreco));
        barra.Controls.AddRange([anterior, _mes, proximo, hoje,
            new Label { Text = "Pacote", AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(16, 9, 4, 0) }, _pacote,
            new Label { Text = "Espaço", AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 9, 4, 0) }, _espaco, regras]);

        var rodape = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(20, 4, 20, 10) };
        rodape.Controls.Add(_legenda);

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 4) };
        corpo.Controls.Add(_calendario);
        Controls.Add(corpo);
        Controls.Add(rodape);
        Controls.Add(barra);
        Controls.Add(titulo);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(async () =>
        {
            var pacotes = (await _lookups.ListarAsync(TipoLookup.Pacotes)).ToList();
            pacotes.Insert(0, new ItemLookup { Id = 0, Nome = "(qualquer pacote)" });
            var espacos = (await _lookups.ListarAsync(TipoLookup.Espacos)).ToList();
            espacos.Insert(0, new ItemLookup { Id = 0, Nome = "(qualquer espaço)" });
            _pacote.DataSource = pacotes;
            _espaco.DataSource = espacos;
            _pacote.SelectedIndexChanged += async (_, _) => await ExecutarAsync(CarregarAsync);
            _espaco.SelectedIndexChanged += async (_, _) => await ExecutarAsync(CarregarAsync);
            await CarregarAsync();
        });
    }

    private Task MudarMesAsync(int meses)
    {
        _mesAtual = _mesAtual.AddMonths(meses);
        return ExecutarAsync(CarregarAsync);
    }

    private async Task CarregarAsync()
    {
        var pacoteId = _pacote.SelectedItem is ItemLookup { Id: > 0 } pacote ? pacote.Id : (int?)null;
        var espacoId = _espaco.SelectedItem is ItemLookup { Id: > 0 } espaco ? espaco.Id : (int?)null;
        var dias = await _precos.CalendarioAsync(_mesAtual, pacoteId, espacoId);
        _mes.Text = Textos.PtBr.TextInfo.ToTitleCase(_mesAtual.ToString("MMMM 'de' yyyy", Textos.PtBr));
        _calendario.Definir(_mesAtual, dias);

        var comAjuste = dias.Where(d => d.Ajuste.Regras.Count > 0).ToList();
        _legenda.Text = comAjuste.Count == 0
            ? "Nenhuma regra de preço vale neste mês: todos os dias pelo preço de tabela."
            : $"{comAjuste.Count(d => d.Ajuste.PercentualPessoa > 0 || d.Ajuste.PercentualLocacao > 0)} dia(s) com acréscimo  •  " +
              $"{comAjuste.Count(d => d.Ajuste.PercentualPessoa < 0 || d.Ajuste.PercentualLocacao < 0)} com desconto  •  " +
              $"{dias.Count(d => d.Ajuste.MinimoConvidados is not null)} com mínimo de convidados  •  " +
              "o número grande é o ajuste do valor por pessoa (ou da locação, quando só ela muda)";
    }
}
