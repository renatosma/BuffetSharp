using System.ComponentModel;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Edição do pedido de compra (cabeçalho e itens) e recebimento da mercadoria (inteira ou em partes).</summary>
internal static class DialogosPedidoCompra
{
    /// <summary>Linha da grade de itens. A unidade de compra depende do produto (cada linha tem a sua lista).</summary>
    private sealed class LinhaItem : INotifyPropertyChanged
    {
        public int ProdutoId { get; set; }
        public int EmbalagemId { get; set; }
        public decimal Quantidade { get; set; }
        public decimal Custo { get; set; }
        public decimal Total => Math.Round(Quantidade * Custo, 2);

        public event PropertyChangedEventHandler? PropertyChanged;
        public void Atualizar() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
    }

    /// <summary>Abre o pedido (0 = novo) para edição. Retorna o Id gravado, ou nulo se cancelar.</summary>
    public static async Task<int?> EditarAsync(IWin32Window dono, CompraService servico, IConsultasLookup lookups, int pedidoId)
    {
        var input = pedidoId == 0 ? servico.Novo() : await servico.ObterAsync(pedidoId);
        Validador.Garantir(input.Status == StatusPedidoCompra.Aberto,
            input.Status == StatusPedidoCompra.Cancelado ? "Pedido cancelado." : "O pedido já teve recebimento e não pode mais ser alterado.");
        var fornecedores = await lookups.ListarAsync(TipoLookup.Fornecedores);
        if (input.FornecedorId == 0 && fornecedores.Count > 0)
            input.FornecedorId = fornecedores[0].Id;
        var produtos = await servico.ProdutosAsync(input.FornecedorId);

        var linhas = new BindingList<LinhaItem>(input.Itens.Select(i => new LinhaItem
        {
            ProdutoId = i.ProdutoId, EmbalagemId = i.ProdutoEmbalagemId, Quantidade = i.Quantidade, Custo = i.CustoUnitario
        }).ToList());

        var e = new EditorCampos();
        var fornecedor = e.Lista("Fornecedor", fornecedores, () => input.FornecedorId, v => input.FornecedorId = v ?? 0,
            nameof(input.FornecedorId), span: 2);
        e.Data("Data do pedido", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        e.Data("Previsão de entrega", () => input.PrevisaoEntrega, v => input.PrevisaoEntrega = v, nameof(input.PrevisaoEntrega), opcional: true);
        e.Opcoes("Categoria da despesa", () => input.Categoria, v => input.Categoria = v, nameof(input.Categoria));
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 1000,
            dica: "Prazo combinado, condições, contato...");

        e.Secao("Itens");
        var grade = CriarGradeItens(linhas, () => produtos);
        var total = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 6, 0, 0) };
        void AtualizarTotal() => total.Text = $"Total do pedido: {Textos.Moeda(linhas.Sum(l => l.Total))}  •  {linhas.Count} item(ns)";
        var incluir = Tema.BotaoSecundario("Incluir produto", Icones.Adicionar);
        incluir.Click += (_, _) =>
        {
            grade.EndEdit();
            linhas.Add(new LinhaItem());
            grade.CurrentCell = grade.Rows[^1].Cells[0];
            grade.BeginEdit(true);
        };
        var remover = Tema.BotaoSecundario("Remover produto", Icones.Excluir);
        remover.Click += (_, _) =>
        {
            if (grade.CurrentRow?.DataBoundItem is LinhaItem linha)
                linhas.Remove(linha);
            AtualizarTotal();
        };
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        barra.Controls.AddRange([incluir, remover, total]);
        var painel = new Panel { Height = 300, Dock = DockStyle.Top };
        grade.Dock = DockStyle.Fill;
        painel.Controls.Add(grade);
        painel.Controls.Add(barra);
        e.Livre(painel);
        e.Associar(nameof(input.Itens), grade);
        linhas.ListChanged += (_, _) => AtualizarTotal();
        AtualizarTotal();

        // Outro fornecedor: as embalagens possíveis mudam; itens com embalagem que não vale voltam para a unidade do estoque.
        fornecedor.SelectionChangeCommitted += async (_, _) =>
        {
            if (fornecedor.SelectedItem is not ItemLookup { Id: > 0 } escolhido)
                return;
            produtos = await servico.ProdutosAsync(escolhido.Id);
            foreach (var linha in linhas.Where(l => produtos.FirstOrDefault(p => p.Id == l.ProdutoId)?.Unidades.All(u => u.Id != l.EmbalagemId) == true))
                linha.EmbalagemId = 0;
            AjustarUnidades(grade, linhas, produtos);
        };

        e.AoAplicar(() =>
        {
            grade.EndEdit();
            input.Itens = linhas.Where(l => l.ProdutoId > 0).Select(l => new PedidoCompraItemInput
            {
                ProdutoId = l.ProdutoId, ProdutoEmbalagemId = l.EmbalagemId, Quantidade = l.Quantidade, CustoUnitario = l.Custo
            }).ToList();
        });

        int? salvo = null;
        using var dialogo = new FormEdicao(pedidoId == 0 ? "Novo pedido de compra" : $"Editar pedido {input.Codigo}",
            "Os itens entram no estoque no recebimento (pode ser em partes); a nota fiscal gera as contas a pagar.", e,
            async () => salvo = await servico.SalvarAsync(input), 920);
        dialogo.Shown += (_, _) => AjustarUnidades(grade, linhas, produtos);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? salvo : null;
    }

    private static DataGridView CriarGradeItens(BindingList<LinhaItem> linhas, Func<IReadOnlyList<ProdutoCompra>> produtos)
    {
        var grade = new DataGridView { AutoGenerateColumns = false };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        var colunaProduto = new DataGridViewComboBoxColumn
        {
            HeaderText = "Produto", DataPropertyName = nameof(LinhaItem.ProdutoId), FillWeight = 45,
            DataSource = produtos().ToList(), ValueMember = nameof(ProdutoCompra.Id), DisplayMember = nameof(ProdutoCompra.Nome),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
        };
        var colunaUnidade = new DataGridViewComboBoxColumn
        {
            HeaderText = "Unidade de compra", DataPropertyName = nameof(LinhaItem.EmbalagemId), FillWeight = 25,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
        };
        grade.Columns.AddRange(
            colunaProduto,
            colunaUnidade,
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Quantidade", DataPropertyName = nameof(LinhaItem.Quantidade), FillWeight = 14,
                DefaultCellStyle = { Format = "N3", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Custo unitário", DataPropertyName = nameof(LinhaItem.Custo), FillWeight = 14,
                DefaultCellStyle = { Format = "N4", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Total", DataPropertyName = nameof(LinhaItem.Total), FillWeight = 14, ReadOnly = true,
                DefaultCellStyle = { Format = "C2", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        grade.DataSource = linhas;

        // A lista de unidades é de cada célula; enquanto ela não é montada, o valor ligado pode não estar nela.
        grade.DataError += (_, ev) => ev.ThrowException = false;
        grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grade.IsCurrentCellDirty && grade.CurrentCell is DataGridViewComboBoxCell)
                grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grade.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex < 0 || ev.RowIndex >= linhas.Count)
                return;
            var linha = linhas[ev.RowIndex];
            var produto = produtos().FirstOrDefault(p => p.Id == linha.ProdutoId);
            if (ev.ColumnIndex == colunaProduto.Index && produto is not null)
            {
                // Produto escolhido: unidade do estoque e o custo médio como sugestão.
                linha.EmbalagemId = 0;
                linha.Custo = produto.CustoMedio;
                AjustarUnidade(grade.Rows[ev.RowIndex], produto);
                grade.InvalidateRow(ev.RowIndex);
            }
            else if (ev.ColumnIndex == colunaUnidade.Index && produto is not null)
            {
                var fator = produto.Unidades.FirstOrDefault(u => u.Id == linha.EmbalagemId)?.Fator ?? 1;
                linha.Custo = Math.Round(produto.CustoMedio * fator, 4);
                grade.InvalidateRow(ev.RowIndex);
            }
            linha.Atualizar();
        };
        return grade;
    }

    private static void AjustarUnidades(DataGridView grade, IList<LinhaItem> linhas, IReadOnlyList<ProdutoCompra> produtos)
    {
        ((DataGridViewComboBoxColumn)grade.Columns[0]).DataSource = produtos.ToList();
        for (var i = 0; i < linhas.Count && i < grade.Rows.Count; i++)
        {
            if (produtos.FirstOrDefault(p => p.Id == linhas[i].ProdutoId) is { } produto)
                AjustarUnidade(grade.Rows[i], produto);
        }
        grade.Invalidate();
    }

    private static void AjustarUnidade(DataGridViewRow linha, ProdutoCompra produto)
    {
        var celula = (DataGridViewComboBoxCell)linha.Cells[1];
        celula.DataSource = produto.Unidades.ToList();
        celula.ValueMember = nameof(UnidadeCompra.Id);
        celula.DisplayMember = nameof(UnidadeCompra.Nome);
    }

    // =========================== Recebimento ===========================

    /// <summary>Item do pedido na grade do recebimento: o que falta chegar e o que chegou agora.</summary>
    private sealed class LinhaRecebimento
    {
        public int ItemId { get; init; }
        public string Produto { get; init; } = "";
        public string Unidade { get; init; } = "";
        public decimal Pedido { get; init; }
        public decimal JaRecebido { get; init; }
        public decimal Saldo => Pedido - JaRecebido;
        public decimal Custo { get; init; }
        public decimal Chegou { get; set; }
    }

    /// <summary>
    /// Recebe o pedido, inteiro ou em partes: as quantidades que chegaram entram no estoque agora. A nota fiscal (e as
    /// contas a pagar) vem depois. Retorna o Id do recebimento, ou nulo se cancelar.
    /// </summary>
    public static async Task<int?> ReceberAsync(IWin32Window dono, CompraService servico, IConsultasCompras consultas, PedidoCompraResumo pedido)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var itens = await consultas.ItensAsync(pedido.Id);
        var linhas = new BindingList<LinhaRecebimento>(itens.Where(i => i.Saldo > 0).Select(i => new LinhaRecebimento
        {
            ItemId = i.Id, Produto = i.Produto, Unidade = i.Unidade, Pedido = i.Quantidade, JaRecebido = i.QuantidadeRecebida,
            Custo = i.CustoUnitario, Chegou = i.Saldo
        }).ToList());
        var input = new RecebimentoInput { PedidoId = pedido.Id, Data = hoje };

        var e = new EditorCampos();
        e.Informacao("Pedido", () => $"{pedido.Codigo} - {pedido.Fornecedor}", span: 2);
        e.Data("Data do recebimento", () => input.Data, v => input.Data = v ?? hoje, nameof(input.Data));
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), RecebimentoCompra.TamanhoObservacoes,
            dica: "Ex.: faltou um fardo, entregam amanhã");

        e.Secao("O que chegou (na unidade do pedido)");
        var grade = new DataGridView { AutoGenerateColumns = false };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.AllowUserToAddRows = false;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        DataGridViewTextBoxColumn Numero(string titulo, string propriedade, float peso, bool somenteLeitura = true) => new()
        {
            HeaderText = titulo, DataPropertyName = propriedade, FillWeight = peso, ReadOnly = somenteLeitura,
            DefaultCellStyle = { Format = "N3", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
        };
        grade.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Produto", DataPropertyName = nameof(LinhaRecebimento.Produto), FillWeight = 40, ReadOnly = true },
            new DataGridViewTextBoxColumn { HeaderText = "Un.", DataPropertyName = nameof(LinhaRecebimento.Unidade), FillWeight = 8, ReadOnly = true },
            Numero("Pedido", nameof(LinhaRecebimento.Pedido), 13),
            Numero("Já recebido", nameof(LinhaRecebimento.JaRecebido), 13),
            Numero("Falta", nameof(LinhaRecebimento.Saldo), 13),
            Numero("Chegou agora", nameof(LinhaRecebimento.Chegou), 15, somenteLeitura: false));
        grade.Columns[^1].DefaultCellStyle.BackColor = Tema.Fundo;
        grade.Columns[^1].DefaultCellStyle.Font = Tema.FonteNegrito;
        grade.DataSource = linhas;
        grade.DataError += (_, ev) => ev.ThrowException = false;

        var resumo = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(8, 8, 0, 0) };
        void Atualizar()
        {
            var parcial = linhas.Any(l => l.Chegou < l.Saldo);
            resumo.Text = $"Valor recebido: {Textos.Moeda(linhas.Sum(l => Math.Round(l.Chegou * l.Custo, 2)))}" +
                          (parcial ? "  •  recebimento parcial: o saldo continua no pedido" : "  •  pedido completo");
            resumo.ForeColor = parcial ? Tema.Alerta : Tema.Sucesso;
        }
        grade.CellEndEdit += (_, _) => Atualizar();
        var tudo = Tema.BotaoSecundario("Chegou tudo", Icones.Confirmar);
        tudo.Click += (_, _) =>
        {
            grade.EndEdit();
            foreach (var linha in linhas)
                linha.Chegou = linha.Saldo;
            grade.Refresh();
            Atualizar();
        };
        var nada = Tema.BotaoSecundario("Zerar", Icones.Desfazer);
        nada.Click += (_, _) =>
        {
            grade.EndEdit();
            foreach (var linha in linhas)
                linha.Chegou = 0;
            grade.Refresh();
            Atualizar();
        };
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        barra.Controls.AddRange([tudo, nada, resumo]);
        var painel = new Panel { Height = 280, Dock = DockStyle.Top };
        grade.Dock = DockStyle.Fill;
        painel.Controls.Add(grade);
        painel.Controls.Add(barra);
        e.Livre(painel);
        e.Associar(nameof(input.Itens), grade);
        e.AoCarregar(Atualizar);
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            input.Itens = linhas.Select(l => new RecebimentoItemInput(l.ItemId, l.Chegou)).ToList();
        });

        int? recebido = null;
        using var dialogo = new FormEdicao($"Receber pedido {pedido.Codigo}",
            "Informe o que chegou: entra no estoque agora. O que faltar continua no pedido para o próximo recebimento. " +
            "A nota fiscal e as contas a pagar são lançadas depois (uma nota pode juntar vários recebimentos).", e,
            async () => recebido = await servico.ReceberAsync(input), 820);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? recebido : null;
    }
}
