using System.ComponentModel;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cardapio;

/// <summary>Itens do cardápio com custo por pessoa calculado pela ficha técnica.</summary>
internal sealed class FormItensCardapio : FormListagem<ItemCardapioResumo>
{
    private readonly IConsultasEstoqueCardapio _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly CardapioService _servico;

    public FormItensCardapio(IConsultasEstoqueCardapio consultas, IConsultasLookup lookups, CardapioService servico)
        : base("Itens do cardápio", "Pratos e bebidas com ficha técnica: insumos por convidado, que geram o custo e a baixa de estoque dos eventos.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;

        Botao("Novo", Icones.Adicionar, () => EditarAsync(0), "Novo item", destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } i ? EditarAsync(i.Id) : Task.CompletedTask);
        Botao("Excluir", Icones.Excluir, async () =>
        {
            if (Selecionado() is { } i && Mensagens.Confirmar(this, $"Excluir \"{i.Nome}\"?", "Excluir", perigoso: true))
            {
                await _servico.ExcluirItemAsync(i.Id);
                await CarregarAsync();
            }
        });

        Grade.Texto("Seção", i => i.Categoria, 30)
            .Texto("Item", i => i.Nome, 60)
            .Numero("Ingredientes", i => i.QuantidadeIngredientes, peso: 18)
            .Numero("Custo / pessoa", i => i.CustoPorPessoa, "C2", 22)
            .SimNao("Ativo", i => i.Ativo, 12)
            .CorDaLinha(i => i.Ativo ? null : Tema.Neutro)
            .Busca(i => $"{i.Categoria} {i.Nome}");
    }

    protected override async Task CarregarAsync() => Exibir(await _consultas.ListarItensCardapioAsync());

    protected override Task AoAbrirAsync(ItemCardapioResumo item) => EditarAsync(item.Id);

    private async Task EditarAsync(int id)
    {
        var input = id == 0 ? new ItemCardapioInput() : await _servico.ObterItemAsync(id);
        var secoes = await _lookups.ListarAsync(TipoLookup.CategoriasCardapio, somenteAtivos: false);
        var produtos = await _lookups.ListarAsync(TipoLookup.Produtos);

        var ingredientes = new BindingList<IngredienteInput>(input.Ingredientes);
        var custo = new Label { AutoSize = true, Font = Tema.FonteSubtitulo, ForeColor = Tema.Primaria };
        void AtualizarCusto() => custo.Text = $"Custo por pessoa: {ingredientes.Sum(i => i.CustoPorPessoa):C2}";
        ingredientes.ListChanged += (_, _) => AtualizarCusto();
        AtualizarCusto();

        var e = new EditorCampos();
        e.Texto("Nome", () => input.Nome, v => input.Nome = v ?? "", nameof(input.Nome), 120);
        e.Lista("Seção do cardápio", secoes, () => input.CategoriaCardapioId, v => input.CategoriaCardapioId = v ?? 0, "CategoriaCardapioId");
        e.Texto("Descrição (impressa na proposta)", () => input.Descricao, v => input.Descricao = v, nameof(input.Descricao), 500, span: 2, linhas: 2);
        e.Marcador("Ativo", () => input.Ativo, v => input.Ativo = v);
        e.Secao("Ficha técnica - quantidade por convidado");
        e.Livre(CriarFichaTecnica(ingredientes, produtos, custo));

        using var dialogo = new FormEdicao(id == 0 ? "Novo item do cardápio" : "Editar item do cardápio", input.Nome.Length > 0 ? input.Nome : null,
            e, async () =>
            {
                input.Ingredientes = ingredientes.ToList();
                await _servico.SalvarItemAsync(input);
            }, 760);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private static Control CriarFichaTecnica(BindingList<IngredienteInput> ingredientes, IReadOnlyList<ItemLookup> produtos, Label custo)
    {
        var painel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var produto = new ComboBox
        {
            Width = 280,
            DataSource = produtos.ToList(),
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        var quantidade = new NumericUpDown { Width = 100, DecimalPlaces = 4, Maximum = 1000, Increment = 0.01m, Value = 0.1m };
        var unidade = new Label { AutoSize = true, Margin = new Padding(0, 8, 8, 0), ForeColor = Tema.TextoSuave };
        produto.SelectedIndexChanged += (_, _) => unidade.Text = (produto.SelectedItem as ItemLookup)?.Info ?? "";
        var adicionar = Tema.BotaoSecundario("Adicionar", Icones.Adicionar);
        var remover = Tema.BotaoSecundario("Remover", Icones.Excluir);
        barra.Controls.AddRange([produto, quantidade, unidade, adicionar, remover]);

        var grade = new DataGridView { Height = 220 };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.AutoGenerateColumns = false;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        grade.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grade.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Insumo", DataPropertyName = nameof(IngredienteInput.Produto), ReadOnly = true, FillWeight = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Qtd. por pessoa", DataPropertyName = nameof(IngredienteInput.QuantidadePorPessoa), FillWeight = 20, DefaultCellStyle = { Format = "N4", Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Un.", DataPropertyName = nameof(IngredienteInput.Unidade), ReadOnly = true, FillWeight = 10 },
            new DataGridViewTextBoxColumn { HeaderText = "Custo unit.", DataPropertyName = nameof(IngredienteInput.CustoUnitario), ReadOnly = true, FillWeight = 18, DefaultCellStyle = { Format = "C4", Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Custo / pessoa", DataPropertyName = nameof(IngredienteInput.CustoPorPessoa), ReadOnly = true, FillWeight = 18, DefaultCellStyle = { Format = "C4", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grade.DataSource = ingredientes;
        grade.CellEndEdit += (_, _) => ingredientes.ResetBindings();
        grade.DataError += (_, ev) => ev.ThrowException = false;

        adicionar.Click += (_, _) =>
        {
            if (produto.SelectedItem is not ItemLookup p)
                return;
            if (ingredientes.Any(i => i.ProdutoId == p.Id))
            {
                Mensagens.Aviso(adicionar.FindForm(), "Este insumo já está na ficha técnica. Ajuste a quantidade na lista.");
                return;
            }
            ingredientes.Add(new IngredienteInput
            {
                ProdutoId = p.Id,
                Produto = p.Nome,
                Unidade = p.Info ?? "",
                CustoUnitario = p.Valor,
                QuantidadePorPessoa = quantidade.Value
            });
        };
        remover.Click += (_, _) =>
        {
            if (grade.CurrentRow?.DataBoundItem is IngredienteInput item)
                ingredientes.Remove(item);
        };

        painel.Controls.Add(barra);
        painel.Controls.Add(grade);
        painel.Controls.Add(custo);
        return painel;
    }
}

/// <summary>Pacotes comerciais com custo, margem e composição.</summary>
internal sealed class FormPacotes : FormListagem<PacoteResumo>
{
    private readonly IConsultasEstoqueCardapio _consultas;
    private readonly CardapioService _servico;

    public FormPacotes(IConsultasEstoqueCardapio consultas, CardapioService servico)
        : base("Pacotes", "Combinações de itens com preço por pessoa. A margem considera o custo atual dos insumos (custo médio).")
    {
        _consultas = consultas;
        _servico = servico;

        Botao("Novo", Icones.Adicionar, () => EditarAsync(0), "Novo pacote", destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } p ? EditarAsync(p.Id) : Task.CompletedTask);
        Botao("Excluir", Icones.Excluir, async () =>
        {
            if (Selecionado() is { } p && Mensagens.Confirmar(this, $"Excluir o pacote \"{p.Nome}\"?", "Excluir", perigoso: true))
            {
                await _servico.ExcluirPacoteAsync(p.Id);
                await CarregarAsync();
            }
        });

        Grade.Texto("Pacote", p => p.Nome, 50)
            .Moeda("Preço / pessoa", p => p.PrecoPorPessoa, 22)
            .Numero("Custo / pessoa", p => p.CustoPorPessoa, "C2", 22)
            .Numero("Margem (%)", p => p.MargemPercentual, "N1", 18, p => p.MargemPercentual < 40 ? Tema.Alerta : Tema.Sucesso)
            .Numero("Mín. convidados", p => p.MinimoConvidados, peso: 18)
            .Numero("Itens", p => p.QuantidadeItens, peso: 12)
            .SimNao("Ativo", p => p.Ativo, 12)
            .CorDaLinha(p => p.Ativo ? null : Tema.Neutro)
            .Busca(p => p.Nome);
    }

    protected override async Task CarregarAsync() => Exibir(await _consultas.ListarPacotesAsync());

    protected override Task AoAbrirAsync(PacoteResumo item) => EditarAsync(item.Id);

    private async Task EditarAsync(int id)
    {
        var input = id == 0 ? new PacoteInput() : await _servico.ObterPacoteAsync(id);
        var itens = await _consultas.ListarItensCardapioAsync();

        var lista = new CheckedListBox { Height = 280, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        foreach (var item in itens.Where(i => i.Ativo || input.ItensCardapioIds.Contains(i.Id)))
            lista.Items.Add(new ItemMarcavel(item), input.ItensCardapioIds.Contains(item.Id));

        var resumo = new Label { AutoSize = true, Font = Tema.FonteSubtitulo, ForeColor = Tema.Primaria };
        NumericUpDown? preco = null;
        void Atualizar(int indiceAlterado = -1, bool marcado = false)
        {
            var selecionados = lista.Items.Cast<ItemMarcavel>().Where((_, i) => i == indiceAlterado ? marcado : lista.GetItemChecked(i)).ToList();
            var custo = selecionados.Sum(s => s.Item.CustoPorPessoa);
            var valor = preco?.Value ?? input.PrecoPorPessoa;
            var margem = valor == 0 ? 0 : (valor - custo) / valor * 100;
            resumo.Text = $"{selecionados.Count} itens  •  custo/pessoa {custo:C2}  •  margem {margem:0.#}%";
            resumo.ForeColor = margem < 40 ? Tema.Alerta : Tema.Sucesso;
        }
        lista.ItemCheck += (_, ev) => Atualizar(ev.Index, ev.NewValue == CheckState.Checked);

        var e = new EditorCampos(3);
        e.Texto("Nome do pacote", () => input.Nome, v => input.Nome = v ?? "", nameof(input.Nome), 100);
        preco = e.Decimal("Preço por pessoa (R$)", () => input.PrecoPorPessoa, v => input.PrecoPorPessoa = v, nameof(input.PrecoPorPessoa));
        preco.ValueChanged += (_, _) => Atualizar();
        e.Inteiro("Mínimo de convidados", () => input.MinimoConvidados, v => input.MinimoConvidados = v, nameof(input.MinimoConvidados), 1, 10_000);
        e.Texto("Descrição (impressa na proposta)", () => input.Descricao, v => input.Descricao = v, nameof(input.Descricao), 1000, span: 3, linhas: 2);
        e.Marcador("Ativo", () => input.Ativo, v => input.Ativo = v);
        e.Secao("Itens incluídos");
        e.Livre(lista);
        e.Livre(resumo);
        Atualizar();

        using var dialogo = new FormEdicao(id == 0 ? "Novo pacote" : "Editar pacote", input.Nome.Length > 0 ? input.Nome : null, e, async () =>
        {
            input.ItensCardapioIds = lista.CheckedItems.Cast<ItemMarcavel>().Select(i => i.Item.Id).ToList();
            await _servico.SalvarPacoteAsync(input);
        }, 820);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private sealed record ItemMarcavel(ItemCardapioResumo Item)
    {
        public override string ToString() => $"{Item.Categoria}  ›  {Item.Nome}   ({Item.CustoPorPessoa:C2}/pessoa)";
    }
}
