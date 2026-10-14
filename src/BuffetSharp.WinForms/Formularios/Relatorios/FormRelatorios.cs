using BuffetSharp.Application.Consultas;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Relatorios;

/// <summary>Relatórios gerenciais (Dapper): faturamento, pacotes, lucratividade, fluxo de caixa e conversão.</summary>
internal sealed class FormRelatorios : FormBase
{
    private readonly IConsultasRelatorios _relatorios;
    private readonly DateTimePicker _de = new() { Format = DateTimePickerFormat.Short, Width = 130, Value = new DateTime(DateTime.Today.Year, 1, 1) };
    private readonly DateTimePicker _ate = new() { Format = DateTimePickerFormat.Short, Width = 130, Value = new DateTime(DateTime.Today.Year, 12, 31) };
    private readonly TabControl _abas = new() { Dock = DockStyle.Fill, Padding = new Point(14, 6) };

    private readonly Grade<FaturamentoTipoEvento> _porTipo = new();
    private readonly GraficoBarras _graficoTipo = new() { Dock = DockStyle.Fill, Titulo = "Faturamento por tipo de evento" };
    private readonly Grade<RankingPacote> _pacotes = new();
    private readonly Grade<LucratividadeEvento> _lucro = new();
    private readonly Label _totalLucro = new() { Dock = DockStyle.Bottom, Height = 30, Font = Tema.FonteNegrito, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Grade<FluxoCaixaMes> _fluxo = new();
    private readonly GraficoBarras _graficoFluxo = new() { Dock = DockStyle.Fill, Titulo = "Fluxo de caixa realizado: recebido x pago" };
    private readonly Grade<ConversaoOrcamentos> _conversao = new();

    public FormRelatorios(IConsultasRelatorios relatorios)
    {
        _relatorios = relatorios;
        Text = "Relatórios";

        _porTipo.Texto("Tipo de evento", t => t.TipoEvento, 40).Numero("Eventos", t => t.QuantidadeEventos, peso: 15)
            .Numero("Convidados", t => t.Convidados, peso: 18).Moeda("Faturamento", t => t.ValorTotal, 25)
            .Moeda("Ticket médio", t => t.TicketMedio, 25).Moeda("Valor / convidado", t => t.ValorPorConvidado, 25);
        _pacotes.Texto("Pacote", p => p.Pacote, 40).Numero("Eventos", p => p.QuantidadeEventos, peso: 15)
            .Numero("Convidados", p => p.Convidados, peso: 18).Moeda("Faturamento", p => p.ValorTotal, 25);
        _lucro.Texto("Código", l => l.Codigo, 16).Data("Data", l => l.Inicio, peso: 16).Texto("Evento", l => l.Titulo, 50)
            .Texto("Situação", l => l.Status, 16).Numero("Conv.", l => l.Convidados, peso: 12)
            .Moeda("Receita", l => l.Receita, 22).Moeda("Insumos", l => l.CustoInsumos, 22).Moeda("Equipe", l => l.CustoEquipe, 20)
            .Moeda("Despesas", l => l.Despesas, 20).Moeda("Lucro", l => l.Lucro, 22, l => l.Lucro < 0 ? Tema.Perigo : Tema.Sucesso)
            .Numero("Margem %", l => l.MargemPercentual, "N1", 15, l => l.MargemPercentual < 30 ? Tema.Alerta : null)
            .Busca(l => $"{l.Codigo} {l.Titulo} {l.Cliente}");
        _fluxo.Texto("Mês", f => f.Rotulo, 20).Moeda("A receber (previsto)", f => f.ReceberPrevisto, 25).Moeda("Recebido", f => f.Recebido, 25)
            .Moeda("A pagar (previsto)", f => f.PagarPrevisto, 25).Moeda("Pago", f => f.Pago, 25)
            .Moeda("Saldo previsto", f => f.SaldoPrevisto, 25, f => f.SaldoPrevisto < 0 ? Tema.Perigo : Tema.Sucesso)
            .Moeda("Saldo realizado", f => f.SaldoRealizado, 25, f => f.SaldoRealizado < 0 ? Tema.Perigo : Tema.Sucesso);
        _conversao.Texto("Situação do orçamento", c => c.Status, 40).Numero("Quantidade", c => c.Quantidade, peso: 20)
            .Moeda("Valor", c => c.Valor, 25);

        _abas.TabPages.Add(Aba("Faturamento por tipo", Dividir(_porTipo.Controle, _graficoTipo)));
        _abas.TabPages.Add(Aba("Ranking de pacotes", _pacotes.Controle));
        var lucro = new Panel { Dock = DockStyle.Fill };
        lucro.Controls.Add(_lucro.Controle);
        lucro.Controls.Add(_totalLucro);
        _abas.TabPages.Add(Aba("Lucratividade por evento", lucro));
        _abas.TabPages.Add(Aba("Fluxo de caixa", Dividir(_fluxo.Controle, _graficoFluxo)));
        _abas.TabPages.Add(Aba("Conversão de orçamentos", _conversao.Controle));

        var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20, 4, 20, 8) };
        var gerar = Tema.BotaoPrimario("Gerar", Icones.Atualizar);
        gerar.Click += async (_, _) => await CarregarAsync();
        var imprimir = Tema.BotaoSecundario("Imprimir aba", Icones.Imprimir);
        imprimir.Click += (_, _) => ImprimirAbaAtual();
        var exportar = Tema.BotaoSecundario("Exportar CSV", Icones.Exportar);
        exportar.Click += (_, _) => ExportarAbaAtual();
        filtros.Controls.AddRange([new Label { Text = "Período de", AutoSize = true, Margin = new Padding(0, 8, 4, 0) }, _de,
            new Label { Text = "até", AutoSize = true, Margin = new Padding(8, 8, 4, 0) }, _ate, gerar, imprimir, exportar]);

        var cabecalho = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 14, 20, 4) };
        cabecalho.Controls.Add(Tema.Titulo("Relatórios gerenciais"));
        cabecalho.Controls.Add(Tema.Subtitulo("Eventos cancelados não entram no faturamento. Lucratividade usa o custo real do estoque quando o evento já foi realizado."));

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 14) };
        corpo.Controls.Add(_abas);
        Controls.Add(corpo);
        Controls.Add(filtros);
        Controls.Add(cabecalho);
    }

    private static TabPage Aba(string titulo, Control conteudo)
    {
        conteudo.Dock = DockStyle.Fill;
        return new TabPage(titulo) { Controls = { conteudo }, Padding = new Padding(8) };
    }

    private static Control Dividir(Control superior, Control inferior)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        superior.Dock = DockStyle.Fill;
        inferior.Dock = DockStyle.Fill;
        layout.Controls.Add(superior, 0, 0);
        layout.Controls.Add(inferior, 0, 1);
        return layout;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await CarregarAsync();
    }

    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var de = _de.Value.Date;
        var ate = _ate.Value.Date;

        var porTipo = await _relatorios.FaturamentoPorTipoAsync(de, ate);
        _porTipo.Carregar(porTipo);
        _graficoTipo.Definir(porTipo.Select(t => t.TipoEvento).ToList(),
            new GraficoBarras.Serie("Faturamento", Tema.SerieA, porTipo.Select(t => t.ValorTotal).ToList()));

        _pacotes.Carregar(await _relatorios.RankingPacotesAsync(de, ate));

        var lucro = await _relatorios.LucratividadeAsync(de, ate);
        _lucro.Carregar(lucro);
        var receita = lucro.Sum(l => l.Receita);
        var resultado = lucro.Sum(l => l.Lucro);
        _totalLucro.Text = $"  {lucro.Count} evento(s)  •  receita {Textos.Moeda(receita)}  •  lucro {Textos.Moeda(resultado)}" +
                           $"  •  margem média {(receita == 0 ? 0 : resultado / receita * 100):0.#}%";

        var fluxo = await _relatorios.FluxoCaixaAsync(de, ate);
        _fluxo.Carregar(fluxo);
        _graficoFluxo.Definir(fluxo.Select(f => f.Rotulo).ToList(),
            new GraficoBarras.Serie("Recebido", Tema.Sucesso, fluxo.Select(f => f.Recebido).ToList()),
            new GraficoBarras.Serie("Pago", Tema.Perigo, fluxo.Select(f => f.Pago).ToList()));

        _conversao.Carregar(await _relatorios.ConversaoOrcamentosAsync(de, ate));
    });

    private (string Titulo, string[] Cabecalho, List<string[]> Linhas, string Csv) AbaAtual() => _abas.SelectedIndex switch
    {
        0 => Dados(_porTipo),
        1 => Dados(_pacotes),
        2 => Dados(_lucro),
        3 => Dados(_fluxo),
        _ => Dados(_conversao)
    };

    private (string, string[], List<string[]>, string) Dados<T>(Grade<T> grade) where T : class
    {
        var (cabecalho, linhas) = grade.ObterTabela();
        return (_abas.SelectedTab!.Text, cabecalho, linhas, grade.ParaCsv());
    }

    private void ImprimirAbaAtual()
    {
        var (titulo, cabecalho, linhas, _) = AbaAtual();
        var doc = new DocumentoImpresso(titulo);
        doc.Subtitulo($"Período: {_de.Value:dd/MM/yyyy} a {_ate.Value:dd/MM/yyyy}");
        doc.Tabela(cabecalho, linhas);
        if (_abas.SelectedIndex == 2)
            doc.Paragrafo(_totalLucro.Text.Trim(), negrito: true);
        doc.Visualizar(this, paisagem: cabecalho.Length > 6);
    }

    private void ExportarAbaAtual()
    {
        var (titulo, _, _, csv) = AbaAtual();
        using var dialogo = new SaveFileDialog { Filter = "Planilha CSV (*.csv)|*.csv", FileName = $"{titulo.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd}.csv" };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        File.WriteAllText(dialogo.FileName, csv, new System.Text.UTF8Encoding(true));
        Mensagens.Info(this, $"Arquivo gerado:{Environment.NewLine}{dialogo.FileName}", "Exportação concluída");
    }
}
