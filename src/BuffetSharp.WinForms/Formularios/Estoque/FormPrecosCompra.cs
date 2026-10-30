using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Cor da variação de preço: acima do limite de alta em vermelho, subindo em laranja, caindo em verde.</summary>
internal static class CoresPreco
{
    public static Color? Variacao(decimal? variacao, decimal limite) => variacao switch
    {
        null => null,
        var v when limite > 0 && v > limite => Tema.Perigo,
        > 0 => Tema.Alerta,
        < 0 => Tema.Sucesso,
        _ => null
    };

    public static string Texto(decimal? variacao) => variacao is { } v ? v.ToString("+0.0'%';-0.0'%';0'%'", Textos.PtBr) : "";
}

/// <summary>
/// Preços de compra por produto no período (tirados das entradas no estoque): primeiro, último, menor, maior, médio, a variação
/// e o fornecedor mais barato. Daqui se abre o histórico com o gráfico e o comparativo de fornecedores.
/// </summary>
internal sealed class FormPrecosCompra : FormListagem<PrecoProdutoResumo>
{
    private readonly PrecoCompraService _precos;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormPrecosCompra(PrecoCompraService precos, Navegador navegador)
        : base("Preços de compra", "Quanto se pagou por produto no período (entradas no estoque, sem as estornadas), do que mais subiu para o que mais caiu.")
    {
        _precos = precos;
        _navegador = navegador;
        var (de, ate) = precos.UltimoAno;
        _de = FiltroData("De", de.ToDateTime(TimeOnly.MinValue), opcional: false);
        _ate = FiltroData("até", ate.ToDateTime(TimeOnly.MinValue), opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Histórico do produto", Icones.Historico, () => Selecionado() is { } p ? AoAbrirAsync(p) : Task.CompletedTask,
            "Cada compra do produto, com o gráfico do preço por fornecedor", destaque: true);
        Botao("Comparar fornecedores", Icones.Lista, () =>
        {
            _navegador.Abrir<FormComparativoPrecos>();
            return Task.CompletedTask;
        }, "Último preço de cada fornecedor por produto, com o mais barato");
        Botao("Maiores altas", Icones.Aviso, () =>
        {
            _navegador.Abrir<FormMaioresAltas>();
            return Task.CompletedTask;
        });

        var limite = precos.LimiteAlta;
        Grade.Texto("Produto", p => p.Produto, 30)
            .Texto("Un.", p => p.Unidade, 7)
            .Numero("Compras", p => p.Compras, "N0", 9)
            .Numero("Primeiro", p => p.Primeiro, "N4", 12)
            .Numero("Último", p => p.Ultimo, "N4", 12)
            .Texto("Variação", p => CoresPreco.Texto(p.Variacao), 10, p => CoresPreco.Variacao(p.Variacao, limite))
            .Numero("Menor", p => p.Menor, "N4", 12)
            .Numero("Maior", p => p.Maior, "N4", 12)
            .Numero("Médio", p => p.Medio, "N4", 12)
            .Data("Última compra", p => p.UltimaCompra, "dd/MM/yyyy", 12)
            .Texto("Último fornecedor", p => p.UltimoFornecedor, 20)
            .Texto("Mais barato", p => p.MelhorFornecedor is null ? "" : $"{p.MelhorFornecedor} ({p.MelhorPreco:N4})", 24)
            .Busca(p => $"{p.Produto} {p.UltimoFornecedor} {p.MelhorFornecedor}");
    }

    protected override Task AoAbrirAsync(PrecoProdutoResumo item)
    {
        _navegador.Abrir<FormHistoricoPrecos>($"historico-precos:{item.ProdutoId}", new HistoricoPrecoInicial(item.ProdutoId));
        return Task.CompletedTask;
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _precos.ResumoAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<PrecoProdutoResumo> visiveis) =>
        $"{visiveis.Count} produto(s) comprado(s)  •  {visiveis.Count(p => p.Variacao > 0)} subiram, {visiveis.Count(p => p.Variacao < 0)} caíram" +
        (_precos.LimiteAlta > 0 ? $"  •  {visiveis.Count(p => p.Variacao > _precos.LimiteAlta)} acima de {_precos.LimiteAlta:0.#}%" : "");
}

/// <summary>Produto já escolhido ao abrir o histórico (pela tela de preços de compra).</summary>
internal sealed record HistoricoPrecoInicial(int ProdutoId);

/// <summary>Histórico de preços de um produto: o gráfico do preço no tempo (uma linha por fornecedor) e cada compra.</summary>
internal sealed class FormHistoricoPrecos : FormListagem<PrecoCompra>
{
    private readonly PrecoCompraService _precos;
    private readonly IConsultasLookup _lookups;
    private readonly HistoricoPrecoInicial? _inicial;
    private readonly ComboBox _produto = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly GraficoLinhas _grafico = new() { Dock = DockStyle.Fill, Titulo = "Preço por unidade do estoque" };

    public FormHistoricoPrecos(PrecoCompraService precos, IConsultasLookup lookups)
        : this(precos, lookups, null)
    {
    }

    public FormHistoricoPrecos(PrecoCompraService precos, IConsultasLookup lookups, HistoricoPrecoInicial? inicial)
        : base("Histórico de preços", "Cada compra do produto: o preço na unidade do estoque, o que foi comprado e a variação sobre a compra anterior.")
    {
        _precos = precos;
        _lookups = lookups;
        _inicial = inicial;
        Filtro("Produto", _produto);
        var (de, ate) = precos.UltimoAno;
        _de = FiltroData("De", de.AddMonths(-12).ToDateTime(TimeOnly.MinValue), opcional: false);
        _ate = FiltroData("até", ate.ToDateTime(TimeOnly.MinValue), opcional: false);
        _produto.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        // Gráfico acima da grade.
        var cartao = Tema.Cartao(new Padding(1));
        cartao.Dock = DockStyle.Fill;
        cartao.Controls.Add(_grafico);
        var painel = new Panel { Dock = DockStyle.Top, Height = LogicalToDeviceUnits(260), Padding = new Padding(20, 4, 20, 4) };
        painel.Controls.Add(cartao);
        Controls.Add(painel);
        Controls.SetChildIndex(painel, 1);

        var limite = precos.LimiteAlta;
        Grade.Data("Data", c => c.Data, "dd/MM/yyyy", 12)
            .Texto("Fornecedor", c => c.Fornecedor, 24)
            .Texto("Comprado", c => c.Compra, 24)
            .Numero("Quantidade", c => c.Quantidade, "N3", 12)
            .Texto("Un.", c => c.Unidade, 7)
            .Numero("Preço por un.", c => c.CustoUnitario, "N4", 13)
            .Texto("Vs. anterior", c => CoresPreco.Texto(c.Variacao), 11, c => CoresPreco.Variacao(c.Variacao, limite))
            .Moeda("Total", c => c.Total, 13)
            .Busca(c => $"{c.Fornecedor} {c.Compra}");
    }

    private int? ProdutoId => (_produto.SelectedItem as ItemLookup)?.Id;

    protected override async Task CarregarAsync()
    {
        if (_produto.DataSource is null)
        {
            var produtos = (await _lookups.ListarAsync(TipoLookup.Produtos)).ToList();
            _produto.DataSource = produtos;
            _produto.DisplayMember = nameof(ItemLookup.Nome);
            var indice = _inicial is null ? 0 : produtos.FindIndex(p => p.Id == _inicial.ProdutoId);
            _produto.SelectedIndex = produtos.Count == 0 ? -1 : Math.Max(0, indice);
        }
        if (ProdutoId is not { } produtoId)
        {
            Exibir([]);
            _grafico.Definir([]);
            return;
        }

        var compras = await _precos.HistoricoAsync(produtoId, DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value));
        Text = $"Preços - {(_produto.SelectedItem as ItemLookup)?.Nome}";
        Exibir(compras.OrderByDescending(c => c.Data).ToList());
        _grafico.Definir(compras.GroupBy(c => c.Fornecedor).OrderBy(g => g.Key)
            .Select((g, i) => new GraficoLinhas.Serie(g.Key, GraficoLinhas.Paleta[i % GraficoLinhas.Paleta.Length],
                g.Select(c => new GraficoLinhas.Ponto(c.Data, c.CustoUnitario, c.Compra)).ToList()))
            .ToList());
    }

    protected override string TextoRodape(IReadOnlyList<PrecoCompra> visiveis)
    {
        if (visiveis.Count == 0)
            return "Nenhuma compra do produto no período.";
        var ordenadas = visiveis.OrderBy(c => c.Data).ToList();
        var quantidade = ordenadas.Sum(c => c.Quantidade);
        var medio = quantidade == 0 ? 0 : ordenadas.Sum(c => c.Quantidade * c.CustoUnitario) / quantidade;
        var variacao = PrecoCompraService.Variacao(ordenadas[0].CustoUnitario, ordenadas[^1].CustoUnitario);
        return $"{visiveis.Count} compra(s)  •  último {ordenadas[^1].CustoUnitario:C4}  •  menor {ordenadas.Min(c => c.CustoUnitario):C4}  •  " +
               $"maior {ordenadas.Max(c => c.CustoUnitario):C4}  •  médio {medio:C4}  •  no período {CoresPreco.Texto(variacao)}";
    }
}

/// <summary>Comparativo de fornecedores: o último preço de cada um por produto, com o mais barato em destaque.</summary>
internal sealed class FormComparativoPrecos : FormListagem<PrecoFornecedor>
{
    private readonly PrecoCompraService _precos;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly CheckBox _varios = new() { Text = "Só produtos com mais de um fornecedor", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    public FormComparativoPrecos(PrecoCompraService precos)
        : base("Comparativo de fornecedores", "Último preço pago a cada fornecedor por produto, na unidade do estoque. O mais barato aparece em verde.")
    {
        _precos = precos;
        var (de, ate) = precos.UltimoAno;
        _de = FiltroData("De", de.ToDateTime(TimeOnly.MinValue), opcional: false);
        _ate = FiltroData("até", ate.ToDateTime(TimeOnly.MinValue), opcional: false);
        Filtros.Controls.Add(_varios);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _varios.CheckedChanged += async (_, _) => await RecarregarAsync();

        Grade.Texto("Produto", p => p.Produto, 30)
            .Texto("Fornecedor", p => p.Fornecedor, 26, Cor)
            .Numero("Último preço", p => p.Ultimo, "N4", 13, Cor)
            .Texto("Un.", p => p.Unidade, 7)
            .Data("Em", p => p.Data, "dd/MM/yyyy", 12)
            .Numero("Compras", p => p.Compras, "N0", 9)
            .Texto("Comparação", p => p.FornecedoresDoProduto == 1 ? "Único fornecedor" : p.MaisBarato ? "Mais barato" : $"{CoresPreco.Texto(p.AcimaDoMelhor)} acima",
                16, Cor)
            .Busca(p => $"{p.Produto} {p.Fornecedor}");
    }

    private static Color? Cor(PrecoFornecedor p) => p.FornecedoresDoProduto == 1 ? null : p.MaisBarato ? Tema.Sucesso : Tema.Alerta;

    protected override async Task CarregarAsync()
    {
        var linhas = await _precos.ComparativoAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value));
        Exibir(_varios.Checked ? linhas.Where(l => l.FornecedoresDoProduto > 1).ToList() : linhas);
    }

    protected override string TextoRodape(IReadOnlyList<PrecoFornecedor> visiveis) =>
        $"{visiveis.Select(p => p.ProdutoId).Distinct().Count()} produto(s)  •  " +
        $"{visiveis.Where(p => p.FornecedoresDoProduto > 1).Select(p => p.ProdutoId).Distinct().Count()} com mais de um fornecedor";
}

/// <summary>Maiores altas: a variação da primeira para a última compra do período e quanto ela pesa por mês.</summary>
internal sealed class FormMaioresAltas : FormListagem<AltaPreco>
{
    private readonly PrecoCompraService _precos;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormMaioresAltas(PrecoCompraService precos, Navegador navegador)
        : base("Maiores altas de preço", "Produtos com duas compras ou mais no período, do que mais subiu para o que mais caiu. O impacto é a alta vezes o que se compra por mês.")
    {
        _precos = precos;
        _navegador = navegador;
        var ate = precos.UltimoAno.Ate;
        _de = FiltroData("De", ate.AddMonths(-6).AddDays(1).ToDateTime(TimeOnly.MinValue), opcional: false);
        _ate = FiltroData("até", ate.ToDateTime(TimeOnly.MinValue), opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        Botao("Histórico do produto", Icones.Historico, () => Selecionado() is { } a ? AoAbrirAsync(a) : Task.CompletedTask, destaque: true);

        var limite = precos.LimiteAlta;
        Grade.Texto("Produto", a => a.Produto, 30)
            .Texto("Un.", a => a.Unidade, 7)
            .Numero("Compras", a => a.Compras, "N0", 9)
            .Numero("Primeiro preço", a => a.Primeiro, "N4", 13)
            .Data("Em", a => a.PrimeiraCompra, "dd/MM/yyyy", 11)
            .Numero("Último preço", a => a.Ultimo, "N4", 13)
            .Data("Em", a => a.UltimaCompra, "dd/MM/yyyy", 11)
            .Texto("Variação", a => CoresPreco.Texto(a.Variacao), 10, a => CoresPreco.Variacao(a.Variacao, limite))
            .Numero("Compra por mês", a => a.QuantidadeMes, "N3", 13)
            .Moeda("Impacto por mês", a => a.ImpactoMes, 14, a => a.ImpactoMes > 0 ? Tema.Perigo : a.ImpactoMes < 0 ? Tema.Sucesso : null)
            .Busca(a => a.Produto);
    }

    protected override Task AoAbrirAsync(AltaPreco item)
    {
        _navegador.Abrir<FormHistoricoPrecos>($"historico-precos:{item.ProdutoId}", new HistoricoPrecoInicial(item.ProdutoId));
        return Task.CompletedTask;
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _precos.MaioresAltasAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<AltaPreco> visiveis) =>
        $"{visiveis.Count} produto(s)  •  {visiveis.Count(a => a.Variacao > 0)} subiram, {visiveis.Count(a => a.Variacao < 0)} caíram  •  " +
        $"as altas custam {Textos.Moeda(visiveis.Where(a => a.ImpactoMes > 0).Sum(a => a.ImpactoMes))} a mais por mês";
}
