using System.ComponentModel;

namespace BuffetSharp.WinForms.Infra;

/// <summary>Opção de uma coluna de lista na <see cref="GradeEditavel{T}"/>.</summary>
internal sealed record OpcaoGrade<TValor>(TValor Valor, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Lista editável direto na grade (estilo planilha), com botões para incluir e remover linhas. Usada nas listas que
/// fazem parte de um cadastro: contatos adicionais, embalagens do produto...
/// </summary>
internal sealed class GradeEditavel<T> where T : class
{
    private readonly Func<T> _novo;

    public GradeEditavel(Func<T> novo, string textoIncluir, string textoRemover, int altura = 130)
    {
        _novo = novo;
        // Altura fixa: dentro de painéis com ajuste automático, a grade não encolhe nem cresce com as linhas.
        Controle = new DataGridView { Height = altura, MinimumSize = new Size(0, altura), MaximumSize = new Size(0, altura) };
        Tema.EstilizarGrade(Controle);
        Controle.Dock = DockStyle.Fill;
        Controle.ReadOnly = false;
        Controle.AutoGenerateColumns = false;
        Controle.EditMode = DataGridViewEditMode.EditOnEnter;
        Controle.SelectionMode = DataGridViewSelectionMode.CellSelect;
        Controle.DataSource = Itens;
        Controle.DataError += (_, e) =>
        {
            e.ThrowException = false;
            Mensagens.Aviso(Controle.FindForm(), "Valor inválido nesta coluna.");
        };
        // Combos e marcadores da grade aplicam a escolha na hora (sem precisar sair da célula).
        Controle.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (Controle.CurrentCell is DataGridViewComboBoxCell or DataGridViewCheckBoxCell && Controle.IsCurrentCellDirty)
                Controle.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        Controle.CellValueChanged += (_, _) => Alterado?.Invoke();
        Itens.ListChanged += (_, _) => Alterado?.Invoke();

        Incluir = Tema.BotaoSecundario(textoIncluir, Icones.Adicionar);
        Incluir.Click += (_, _) => IncluirLinha();
        Remover = Tema.BotaoSecundario(textoRemover, Icones.Excluir);
        Remover.Click += (_, _) =>
        {
            Controle.EndEdit();
            if (Controle.CurrentRow?.DataBoundItem is T item)
                Itens.Remove(item);
        };

        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
        barra.Controls.AddRange([Incluir, Remover]);
        var painel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        painel.Controls.Add(barra);
        painel.Controls.Add(Controle);
        Painel = painel;
    }

    public BindingList<T> Itens { get; } = [];

    public DataGridView Controle { get; }

    /// <summary>Botões e grade, para inserir no formulário.</summary>
    public Control Painel { get; }

    public Button Incluir { get; }

    public Button Remover { get; }

    /// <summary>Disparado quando uma linha é incluída, removida ou alterada.</summary>
    public event Action? Alterado;

    public GradeEditavel<T> Texto(string titulo, string propriedade, float peso = 100, int maximo = 0, bool somenteLeitura = false)
    {
        Controle.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = titulo,
            DataPropertyName = propriedade,
            FillWeight = peso,
            MaxInputLength = maximo > 0 ? maximo : 32767,
            ReadOnly = somenteLeitura
        });
        return this;
    }

    public GradeEditavel<T> Numero(string titulo, string propriedade, string formato = "N3", float peso = 40, bool somenteLeitura = false)
    {
        Controle.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = titulo,
            DataPropertyName = propriedade,
            FillWeight = peso,
            ReadOnly = somenteLeitura,
            DefaultCellStyle = { Format = formato, FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        return this;
    }

    public GradeEditavel<T> Marcador(string titulo, string propriedade, float peso = 20)
    {
        Controle.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = titulo, DataPropertyName = propriedade, FillWeight = peso });
        return this;
    }

    public GradeEditavel<T> Opcoes<TValor>(string titulo, string propriedade, IEnumerable<OpcaoGrade<TValor>> opcoes, float peso = 50)
    {
        Controle.Columns.Add(new ColunaLista
        {
            HeaderText = titulo,
            DataPropertyName = propriedade,
            FillWeight = peso,
            DataSource = opcoes.ToList(),
            ValueMember = nameof(OpcaoGrade<TValor>.Valor),
            DisplayMember = nameof(OpcaoGrade<TValor>.Texto),
            ValueType = typeof(TValor),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            FlatStyle = FlatStyle.Flat
        });
        return this;
    }

    public void Carregar(IEnumerable<T> itens)
    {
        Itens.RaiseListChangedEvents = false;
        Itens.Clear();
        foreach (var item in itens)
            Itens.Add(item);
        Itens.RaiseListChangedEvents = true;
        Itens.ResetBindings();
    }

    /// <summary>Conclui a edição da célula atual (chamar antes de ler os itens).</summary>
    public void ConcluirEdicao()
    {
        if (Controle.IsCurrentCellInEditMode)
            Controle.EndEdit();
    }

    public void DefinirSomenteLeitura(bool somenteLeitura)
    {
        Controle.ReadOnly = somenteLeitura;
        Incluir.Enabled = !somenteLeitura;
        Remover.Enabled = !somenteLeitura;
    }

    private void IncluirLinha()
    {
        ConcluirEdicao();
        Itens.Add(_novo());
        var linha = Controle.Rows.Count - 1;
        var coluna = Controle.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => !c.ReadOnly && c is DataGridViewTextBoxColumn)
                     ?? Controle.Columns.Cast<DataGridViewColumn>().FirstOrDefault();
        if (linha < 0 || coluna is null)
            return;
        Controle.Focus();
        Controle.CurrentCell = Controle.Rows[linha].Cells[coluna.Index];
        Controle.BeginEdit(true);
    }
}
