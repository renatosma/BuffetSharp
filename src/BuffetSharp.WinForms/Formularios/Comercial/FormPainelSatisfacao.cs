using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Painel de satisfação: NPS do período, promotores/neutros/detratores, média por área, por tipo de evento e por vendedor.</summary>
internal sealed class FormPainelSatisfacao : FormBase
{
    private readonly PesquisaService _pesquisas;
    private readonly DateTimePicker _de = new() { Format = DateTimePickerFormat.Short, Width = 130, Margin = new Padding(0, 4, 12, 0) };
    private readonly DateTimePicker _ate = new() { Format = DateTimePickerFormat.Short, Width = 130, Margin = new Padding(0, 4, 12, 0) };
    private readonly CartaoIndicador _nps = new("NPS", "-", Tema.Primaria);
    private readonly CartaoIndicador _respostas = new("Respostas", "-", Tema.Info);
    private readonly CartaoIndicador _promotores = new("Promotores (9-10)", "-", Tema.Sucesso);
    private readonly CartaoIndicador _neutros = new("Neutros (7-8)", "-", Tema.Alerta);
    private readonly CartaoIndicador _detratores = new("Detratores (0-6)", "-", Tema.Perigo);
    private readonly CartaoIndicador _areas = new("Média por área (1 a 5)", "-", Tema.Info);
    private readonly Grade<GrupoSatisfacao> _porTipo = new();
    private readonly Grade<GrupoSatisfacao> _porVendedor = new();
    private readonly Grade<PesquisaResumo> _comentarios = new();

    public FormPainelSatisfacao(PesquisaService pesquisas)
    {
        _pesquisas = pesquisas;
        Text = "Painel de satisfação";
        _de.Value = DateTime.Today.AddMonths(-6);
        _ate.Value = DateTime.Today;

        var cabecalho = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20, 14, 20, 0) };
        cabecalho.Controls.Add(Tema.Titulo("Painel de satisfação"));
        cabecalho.Controls.Add(Tema.Subtitulo("NPS = % de promotores - % de detratores, das pesquisas respondidas dos eventos realizados no período."));

        var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(18, 6, 18, 0) };
        filtros.Controls.AddRange([Rotulo("Eventos de"), _de, Rotulo("até"), _ate]);
        _de.ValueChanged += async (_, _) => await ExecutarAsync(CarregarAsync);
        _ate.ValueChanged += async (_, _) => await ExecutarAsync(CarregarAsync);

        var indicadores = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 4, 14, 4) };
        indicadores.Controls.AddRange([_nps, _respostas, _promotores, _neutros, _detratores, _areas]);

        Colunas(_porTipo, "Tipo de evento");
        Colunas(_porVendedor, "Vendedor");
        _comentarios.Data("Data", p => p.Data, "dd/MM/yyyy", 14)
            .Texto("Cliente", p => p.Cliente, 24)
            .Numero("NPS", p => p.Nps, peso: 8, cor: p => p.Nps >= 9 ? Tema.Sucesso : p.Nps <= 6 ? Tema.Perigo : Tema.Alerta)
            .Texto("Comentário", p => p.Comentario, 80);

        var grupos = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(14, 4, 14, 14) };
        for (var i = 0; i < 3; i++)
            grupos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i == 2 ? 50 : 25));
        grupos.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grupos.Controls.Add(Cartao("Por tipo de evento", _porTipo.Controle), 0, 0);
        grupos.Controls.Add(Cartao("Por vendedor", _porVendedor.Controle), 1, 0);
        grupos.Controls.Add(Cartao("Comentários", _comentarios.Controle), 2, 0);

        Controls.Add(grupos);
        Controls.Add(indicadores);
        Controls.Add(filtros);
        Controls.Add(cabecalho);
    }

    private static Label Rotulo(string texto) => new() { Text = texto, AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 8, 4, 0) };

    private static void Colunas(Grade<GrupoSatisfacao> grade, string titulo) =>
        grade.Texto(titulo, g => g.Nome, 40)
            .Numero("Resp.", g => g.Respostas, peso: 14)
            .Numero("NPS", g => g.Nps, "N1", 14, g => g.Nps >= 50 ? Tema.Sucesso : g.Nps < 0 ? Tema.Perigo : null)
            .Numero("Média", g => g.MediaAreas, "N2", 14);

    private static Control Cartao(string titulo, Control conteudo)
    {
        var cartao = Tema.Cartao(new Padding(10));
        cartao.Dock = DockStyle.Fill;
        cartao.Margin = new Padding(4);
        conteudo.Dock = DockStyle.Fill;
        cartao.Controls.Add(conteudo);
        cartao.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Top, Font = Tema.FonteNegrito, Height = 26 });
        return cartao;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(CarregarAsync);
    }

    private async Task CarregarAsync()
    {
        var painel = await _pesquisas.PainelAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value));
        _nps.Atualizar(painel.Nps is { } nps ? nps.ToString("0.#") : "-", painel.Nps is null ? "sem respostas no período"
            : painel.Nps >= 75 ? "excelente" : painel.Nps >= 50 ? "muito bom" : painel.Nps >= 0 ? "razoável" : "crítico");
        _respostas.Atualizar(painel.Respondidas.ToString(), $"{painel.Enviadas} enviada(s) de {painel.Eventos} evento(s)");
        _promotores.Atualizar(painel.Promotores.ToString(), Percentual(painel.Promotores, painel.Respondidas));
        _neutros.Atualizar(painel.Neutros.ToString(), Percentual(painel.Neutros, painel.Respondidas));
        _detratores.Atualizar(painel.Detratores.ToString(), Percentual(painel.Detratores, painel.Respondidas));
        _areas.Atualizar(painel.Comida is null ? "-" : $"{new[] { painel.Comida, painel.Atendimento, painel.Espaco, painel.Organizacao }.Where(m => m is not null).Average():0.0}",
            $"comida {Nota(painel.Comida)} • atend. {Nota(painel.Atendimento)} • espaço {Nota(painel.Espaco)} • organiz. {Nota(painel.Organizacao)}");
        _porTipo.Carregar(painel.PorTipo);
        _porVendedor.Carregar(painel.PorVendedor);
        _comentarios.Carregar(painel.Comentarios);
    }

    private static string Percentual(int parte, int total) => total == 0 ? "" : $"{parte * 100m / total:0}% das respostas";

    private static string Nota(decimal? media) => media is { } m ? m.ToString("0.0") : "-";
}
