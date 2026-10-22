using System.ComponentModel;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Edição do pedido de compra (cabeçalho e itens) e recebimento da nota fiscal com os vencimentos.</summary>
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
            input.Status == StatusPedidoCompra.Recebido ? "Pedido já recebido. Para corrigir, estorne o recebimento." : "Pedido cancelado.");
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
            "Os itens entram no estoque só no recebimento, quando você informa a nota fiscal e os vencimentos.", e,
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

    private sealed class LinhaVencimento
    {
        public DateTime Vencimento { get; set; }
        public decimal Valor { get; set; }
    }

    /// <summary>Recebe o pedido: nota fiscal, data da compra e vencimentos (gerados pelas parcelas e editáveis).</summary>
    public static bool Receber(IWin32Window dono, CompraService servico, PedidoCompraResumo pedido)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var input = new RecebimentoPedidoInput { PedidoId = pedido.Id, DataCompra = hoje };
        var parcelas = 1;
        var intervalo = 0;
        var primeiro = hoje.AddDays(28);
        var vencimentos = new BindingList<LinhaVencimento>();

        var e = new EditorCampos();
        e.Informacao("Pedido", () => $"{pedido.Codigo} - {pedido.Fornecedor}", span: 2);
        e.Informacao("Total da nota", () => Textos.Moeda(pedido.Total));
        e.Informacao("Itens", () => $"{pedido.Itens} produto(s) entram no estoque");
        e.Texto("Nota fiscal", () => input.NotaFiscal, v => input.NotaFiscal = v ?? "", nameof(input.NotaFiscal), PedidoCompra.TamanhoNotaFiscal,
            dica: "Número da NF");
        e.Data("Data da compra", () => input.DataCompra, v => input.DataCompra = v ?? hoje, nameof(input.DataCompra));

        e.Secao("Vencimentos");
        var campoParcelas = e.Inteiro("Parcelas", () => parcelas, v => parcelas = v, minimo: 1, maximo: PlanoParcelas.MaximoParcelas);
        var campoPrimeiro = e.Data("1º vencimento", () => primeiro, v => primeiro = v ?? primeiro);
        var campoIntervalo = e.Inteiro("Dias entre parcelas (0 = mensal)", () => intervalo, v => intervalo = v, maximo: 365, span: 2);

        var grade = new DataGridView { AutoGenerateColumns = false, Height = 170, Dock = DockStyle.Top };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.AllowUserToAddRows = false;
        grade.Columns.AddRange(
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Vencimento", DataPropertyName = nameof(LinhaVencimento.Vencimento),
                DefaultCellStyle = { Format = "dd/MM/yyyy", FormatProvider = Textos.PtBr }
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Valor", DataPropertyName = nameof(LinhaVencimento.Valor),
                DefaultCellStyle = { Format = "N2", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        grade.DataSource = vencimentos;
        grade.DataError += (_, ev) => ev.ThrowException = false;
        var conferencia = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 6, 0, 0) };
        void Conferir()
        {
            var soma = vencimentos.Sum(v => v.Valor);
            conferencia.Text = soma == pedido.Total
                ? $"Soma dos vencimentos: {Textos.Moeda(soma)} (confere com a nota)"
                : $"Soma dos vencimentos: {Textos.Moeda(soma)}  •  diferença: {Textos.Moeda(pedido.Total - soma)}";
            conferencia.ForeColor = soma == pedido.Total ? Tema.Sucesso : Tema.Perigo;
        }
        void Gerar()
        {
            if (pedido.Total <= 0)
                return;
            var plano = PlanoParcelas.Calcular(pedido.Total, (int)campoParcelas.Value, DateOnly.FromDateTime(campoPrimeiro.Value),
                intervaloDias: campoIntervalo.Value > 0 ? (int)campoIntervalo.Value : null);
            vencimentos.Clear();
            foreach (var p in plano)
                vencimentos.Add(new LinhaVencimento { Vencimento = p.Vencimento.ToDateTime(TimeOnly.MinValue), Valor = p.Valor });
            Conferir();
        }
        campoParcelas.ValueChanged += (_, _) => Gerar();
        campoPrimeiro.ValueChanged += (_, _) => Gerar();
        campoIntervalo.ValueChanged += (_, _) => Gerar();
        grade.CellEndEdit += (_, _) => Conferir();
        e.AoCarregar(Gerar);

        var painel = new Panel { Height = 210, Dock = DockStyle.Top };
        painel.Controls.Add(grade);
        painel.Controls.Add(conferencia);
        conferencia.Dock = DockStyle.Bottom;
        e.Livre(painel);
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            input.Vencimentos = vencimentos.Select(v => new VencimentoInput { Vencimento = DateOnly.FromDateTime(v.Vencimento), Valor = v.Valor })
                .ToList();
        });

        using var dialogo = new FormEdicao($"Receber pedido {pedido.Codigo}",
            "Todos os itens entram no estoque e a nota vira uma conta a pagar (ou parcelas). Ajuste as datas e os valores dos " +
            "vencimentos conforme os boletos.", e, () => servico.ReceberAsync(input), 620);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }
}
