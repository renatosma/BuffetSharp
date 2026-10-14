using System.Text;

namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// DataGridView tipado: colunas definidas por funções sobre o item (inclusive propriedades aninhadas),
/// ordenação nativa, filtro local, cores por status e exportação CSV.
/// </summary>
internal sealed class Grade<T> where T : class
{
    private sealed record Coluna(DataGridViewColumn Controle, Func<T, object?> Valor, Func<T, Color?>? Cor);

    private readonly List<Coluna> _colunas = [];
    private List<T> _todos = [];
    private Func<T, Color?>? _corLinha;
    private Func<T, string>? _textoBusca;

    public Grade(DataGridView? controle = null)
    {
        Controle = controle ?? new DataGridView();
        Tema.EstilizarGrade(Controle);
        Controle.CellFormatting += AoFormatarCelula;
        Controle.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && Selecionado is { } item)
                DuploClique?.Invoke(item);
        };
        Controle.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && Selecionado is { } item)
            {
                e.Handled = true;
                DuploClique?.Invoke(item);
            }
        };
    }

    public DataGridView Controle { get; }

    public event Action<T>? DuploClique;

    public T? Selecionado => Controle.CurrentRow?.Tag as T;

    public IReadOnlyList<T> Itens => _todos;

    public IEnumerable<T> Visiveis => Controle.Rows.Cast<DataGridViewRow>().Select(r => (T)r.Tag!);

    // ------------------------- Definição de colunas -------------------------

    public Grade<T> Texto(string titulo, Func<T, object?> valor, float peso = 100, Func<T, Color?>? cor = null) =>
        Adicionar(titulo, valor, peso, null, DataGridViewContentAlignment.MiddleLeft, typeof(string), cor);

    public Grade<T> Moeda(string titulo, Func<T, decimal?> valor, float peso = 70, Func<T, Color?>? cor = null) =>
        Adicionar(titulo, t => valor(t), peso, "C2", DataGridViewContentAlignment.MiddleRight, typeof(decimal), cor);

    public Grade<T> Numero(string titulo, Func<T, decimal?> valor, string formato = "N0", float peso = 50, Func<T, Color?>? cor = null) =>
        Adicionar(titulo, t => valor(t), peso, formato, DataGridViewContentAlignment.MiddleRight, typeof(decimal), cor);

    public Grade<T> Data(string titulo, Func<T, DateTime?> valor, string formato = "dd/MM/yyyy", float peso = 60, Func<T, Color?>? cor = null) =>
        Adicionar(titulo, t => valor(t), peso, formato, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), cor);

    public Grade<T> SimNao(string titulo, Func<T, bool> valor, float peso = 40) =>
        Adicionar(titulo, t => valor(t) ? "Sim" : "Não", peso, null, DataGridViewContentAlignment.MiddleCenter, typeof(string),
            t => valor(t) ? Tema.Sucesso : Tema.Neutro);

    /// <summary>Coluna de status com texto em negrito e cor.</summary>
    public Grade<T> Status(string titulo, Func<T, Enum> valor, float peso = 60) =>
        Adicionar(titulo, t => Infra.Textos.Exibir(valor(t)), peso, null, DataGridViewContentAlignment.MiddleCenter, typeof(string),
            t => Infra.Textos.Cor(valor(t)), negrito: true);

    public Grade<T> CorDaLinha(Func<T, Color?> cor)
    {
        _corLinha = cor;
        return this;
    }

    public Grade<T> Busca(Func<T, string> texto)
    {
        _textoBusca = texto;
        return this;
    }

    private Grade<T> Adicionar(string titulo, Func<T, object?> valor, float peso, string? formato,
        DataGridViewContentAlignment alinhamento, Type tipo, Func<T, Color?>? cor, bool negrito = false)
    {
        var coluna = new DataGridViewTextBoxColumn
        {
            HeaderText = titulo,
            Name = $"col{_colunas.Count}",
            FillWeight = peso,
            MinimumWidth = 50,
            ValueType = tipo,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = { Alignment = alinhamento, Format = formato ?? string.Empty, FormatProvider = Infra.Textos.PtBr }
        };
        if (negrito)
            coluna.DefaultCellStyle.Font = Tema.FonteNegrito;
        coluna.HeaderCell.Style.Alignment = alinhamento;
        Controle.Columns.Add(coluna);
        _colunas.Add(new Coluna(coluna, valor, cor));
        return this;
    }

    // ------------------------- Dados -------------------------

    public void Carregar(IEnumerable<T> itens, string? filtro = null)
    {
        _todos = itens.ToList();
        Filtrar(filtro);
    }

    /// <summary>Filtra localmente (sem ir ao banco) pelo texto informado.</summary>
    public void Filtrar(string? filtro)
    {
        var selecionado = Selecionado;
        var termos = (filtro ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var visiveis = termos.Length == 0 || _textoBusca is null
            ? _todos
            : _todos.Where(i => termos.All(t => _textoBusca(i).Contains(t, StringComparison.CurrentCultureIgnoreCase))).ToList();

        var ordenacao = Controle.SortedColumn;
        var direcao = Controle.SortOrder;

        Controle.SuspendLayout();
        Controle.Rows.Clear();
        var linhas = new DataGridViewRow[visiveis.Count];
        for (var i = 0; i < visiveis.Count; i++)
        {
            var item = visiveis[i];
            var linha = new DataGridViewRow { Tag = item };
            linha.CreateCells(Controle, _colunas.Select(c => c.Valor(item)).ToArray()!);
            if (_corLinha?.Invoke(item) is { } cor)
                linha.DefaultCellStyle.ForeColor = cor;
            linhas[i] = linha;
        }
        Controle.Rows.AddRange(linhas);
        Controle.ResumeLayout();

        if (ordenacao is not null && direcao != SortOrder.None)
            Controle.Sort(ordenacao, direcao == SortOrder.Ascending ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending);

        if (selecionado is not null)
            Selecionar(selecionado);
    }

    public void Selecionar(T item) => Selecionar(i => ReferenceEquals(i, item) || i.Equals(item));

    public void Selecionar(Func<T, bool> criterio)
    {
        foreach (DataGridViewRow linha in Controle.Rows)
        {
            if (criterio((T)linha.Tag!))
            {
                Controle.CurrentCell = linha.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                return;
            }
        }
    }

    private void AoFormatarCelula(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex >= _colunas.Count)
            return;
        var coluna = _colunas[e.ColumnIndex];
        if (coluna.Cor is not null && Controle.Rows[e.RowIndex].Tag is T item && coluna.Cor(item) is { } cor)
        {
            e.CellStyle!.ForeColor = cor;
            e.CellStyle.SelectionForeColor = cor;
        }
    }

    // ------------------------- Exportação -------------------------

    public (string[] Cabecalho, List<string[]> Linhas) ObterTabela()
    {
        var colunas = Controle.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        var linhas = Controle.Rows.Cast<DataGridViewRow>()
            .Select(r => colunas.Select(c => r.Cells[c.Index].FormattedValue?.ToString() ?? string.Empty).ToArray())
            .ToList();
        return (colunas.Select(c => c.HeaderText).ToArray(), linhas);
    }

    public string ParaCsv()
    {
        var (cabecalho, linhas) = ObterTabela();
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', cabecalho.Select(Escapar)));
        foreach (var linha in linhas)
            csv.AppendLine(string.Join(';', linha.Select(Escapar)));
        return csv.ToString();

        static string Escapar(string valor) =>
            valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }
}
