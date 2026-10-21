using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Kardex: histórico de entradas, saídas, ajustes e consumo dos eventos.</summary>
internal sealed class FormMovimentacoes : FormListagem<MovimentacaoResumo>
{
    private readonly IConsultasEstoqueCardapio _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly int _produtoInicial;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _produto = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tipo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _carregouFiltros;

    public FormMovimentacoes(IConsultasEstoqueCardapio consultas, IConsultasLookup lookups, int produtoId = 0)
        : base("Movimentações de estoque", "Kardex com todas as entradas, saídas, ajustes de inventário e consumo dos eventos.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _produtoInicial = produtoId;

        _de = FiltroData("De", DateTime.Today.AddMonths(-3), opcional: false);
        _ate = FiltroData("Até", DateTime.Today, opcional: false);
        Filtro("Produto", _produto);
        Filtro("Tipo", _tipo);
        var aplicar = Tema.BotaoSecundario("Aplicar", Icones.Filtro);
        aplicar.Click += async (_, _) => await RecarregarAsync();
        Filtros.Controls.Add(aplicar);

        Grade.Data("Data", m => m.Data, "dd/MM/yyyy HH:mm", 30)
            .Texto("Produto", m => m.Produto, 50)
            .Status("Tipo", m => m.Tipo, 18)
            .Numero("Quantidade", m => m.Quantidade, "N3", 22, m => m.Quantidade < 0 || m.Tipo == TipoMovimentacao.Saida ? Tema.Perigo : Tema.Sucesso)
            .Texto("Un.", m => m.Unidade, 10)
            .Texto("Compra", m => m.Compra, 16)
            .Texto("Fornecedor", m => m.Fornecedor, 30)
            .Texto("Conta a pagar", m => m.ContaPagar, 26,
                m => m.SituacaoConta == Domain.Financeiro.StatusTitulo.Pago || (m.ParcelasConta > 0 && m.ParcelasPagas == m.ParcelasConta) ? Tema.Sucesso : Tema.Info)
            .Numero("Custo unit.", m => m.CustoUnitario, "C4", 22)
            .Moeda("Valor", m => m.ValorTotal, 22)
            .Numero("Saldo após", m => m.SaldoApos, "N3", 22)
            .Texto("Evento", m => m.EventoId is { } id ? $"EVT-{id:D5}" : "", 18)
            .Texto("Observação", m => m.Observacao, 60)
            .Busca(m => $"{m.Produto} {m.Observacao} {m.EventoId} {m.Fornecedor} {m.ContaPagar}");
    }

    protected override async Task CarregarAsync()
    {
        if (!_carregouFiltros)
        {
            var produtos = (await _lookups.ListarAsync(TipoLookup.Produtos, somenteAtivos: false)).ToList();
            produtos.Insert(0, new ItemLookup { Id = 0, Nome = "(todos)" });
            _produto.DataSource = produtos;
            _produto.SelectedIndex = Math.Max(0, produtos.FindIndex(p => p.Id == _produtoInicial));
            _tipo.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<TipoMovimentacao>()).ToList();
            _carregouFiltros = true;
            if (_produtoInicial > 0)
                Text = $"Movimentações - {(_produto.SelectedItem as ItemLookup)?.Nome}";
        }

        var produtoId = (_produto.SelectedItem as ItemLookup)?.Id;
        var tipo = (_tipo.SelectedItem as OpcaoEnum<TipoMovimentacao>)?.Valor;
        Exibir(await _consultas.ListarMovimentacoesAsync(_de.Value.Date, _ate.Value.Date, produtoId is 0 ? null : produtoId, tipo));
    }

    protected override string TextoRodape(IReadOnlyList<MovimentacaoResumo> visiveis) =>
        $"{visiveis.Count} movimentação(ões)  •  entradas: {Textos.Moeda(visiveis.Where(m => m.Tipo == TipoMovimentacao.Entrada).Sum(m => m.ValorTotal))}" +
        $"  •  saídas: {Textos.Moeda(visiveis.Where(m => m.Tipo == TipoMovimentacao.Saida).Sum(m => m.ValorTotal))}";
}
