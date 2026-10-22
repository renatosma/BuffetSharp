using System.ComponentModel;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Lançamento da nota fiscal de entrada: os recebimentos que ela fatura e os vencimentos dos boletos.</summary>
internal static class DialogosNotaEntrada
{
    private sealed class LinhaRecebimento
    {
        public bool Marcado { get; set; }
        public int Id { get; init; }
        public string Codigo { get; init; } = "";
        public string Pedido { get; init; } = "";
        public DateTime Data { get; init; }
        public int Itens { get; init; }
        public decimal Total { get; init; }
    }

    private sealed class LinhaVencimento
    {
        public DateTime Vencimento { get; set; }
        public decimal Valor { get; set; }
    }

    /// <summary>
    /// Lança a nota de um fornecedor com recebimentos a faturar. <paramref name="marcados"/> vêm marcados (senão, todos os
    /// recebimentos do fornecedor). Retorna o Id da nota, ou nulo se cancelar.
    /// </summary>
    public static async Task<int?> LancarAsync(IWin32Window dono, CompraService servico, IConsultasCompras consultas, int? fornecedorId = null,
        IReadOnlyCollection<int>? marcados = null)
    {
        var pendentes = await consultas.ListarRecebimentosAsync(new FiltroRecebimentos { Status = StatusRecebimento.AFaturar });
        if (pendentes.Count == 0)
        {
            Mensagens.Aviso(dono, "Não há recebimentos aguardando nota fiscal. Receba um pedido de compra primeiro.");
            return null;
        }

        var fornecedores = pendentes.GroupBy(r => r.FornecedorId)
            .Select(g => new ItemLookup { Id = g.Key, Nome = $"{g.First().Fornecedor} ({g.Count()} recebimento(s) a faturar)" })
            .OrderBy(f => f.Nome)
            .ToList();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var input = new NotaEntradaInput
        {
            FornecedorId = fornecedorId is { } f && fornecedores.Any(x => x.Id == f) ? f : fornecedores[0].Id,
            DataEmissao = hoje
        };
        var linhas = new BindingList<LinhaRecebimento>();
        var vencimentos = new BindingList<LinhaVencimento>();
        var parcelas = 1;
        var intervalo = 0;
        var primeiro = hoje.AddDays(28);
        var totalEditado = false;

        var e = new EditorCampos();
        var fornecedor = e.Lista("Fornecedor", fornecedores, () => input.FornecedorId, v => input.FornecedorId = v ?? 0,
            nameof(input.FornecedorId), span: 2);
        e.Texto("Número da nota", () => input.Numero, v => input.Numero = v ?? "", nameof(input.Numero), NotaFiscalEntrada.TamanhoNumero);
        e.Texto("Série", () => input.Serie, v => input.Serie = v, nameof(input.Serie), NotaFiscalEntrada.TamanhoSerie, dica: "Opcional");
        e.Data("Data de emissão", () => input.DataEmissao, v => input.DataEmissao = v ?? hoje, nameof(input.DataEmissao));
        e.Opcoes("Categoria da despesa", () => input.Categoria, v => input.Categoria = v, nameof(input.Categoria));

        e.Secao("Recebimentos que a nota fatura");
        var grade = new DataGridView { AutoGenerateColumns = false, Height = 150, Dock = DockStyle.Top };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.AllowUserToAddRows = false;
        grade.Columns.AddRange(
            new DataGridViewCheckBoxColumn { HeaderText = "Na nota", DataPropertyName = nameof(LinhaRecebimento.Marcado), FillWeight = 10 },
            new DataGridViewTextBoxColumn { HeaderText = "Recebimento", DataPropertyName = nameof(LinhaRecebimento.Codigo), FillWeight = 18, ReadOnly = true },
            new DataGridViewTextBoxColumn { HeaderText = "Pedido", DataPropertyName = nameof(LinhaRecebimento.Pedido), FillWeight = 16, ReadOnly = true },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Data", DataPropertyName = nameof(LinhaRecebimento.Data), FillWeight = 16, ReadOnly = true,
                DefaultCellStyle = { Format = "dd/MM/yyyy" }
            },
            new DataGridViewTextBoxColumn { HeaderText = "Itens", DataPropertyName = nameof(LinhaRecebimento.Itens), FillWeight = 10, ReadOnly = true },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Valor", DataPropertyName = nameof(LinhaRecebimento.Total), FillWeight = 18, ReadOnly = true,
                DefaultCellStyle = { Format = "C2", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        grade.DataSource = linhas;
        grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grade.IsCurrentCellDirty && grade.CurrentCell is DataGridViewCheckBoxCell)
                grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        e.Livre(grade);
        e.Associar(nameof(input.Recebimentos), grade);

        e.Secao("Valor e vencimentos");
        var valorTotal = e.Decimal("Valor total da nota", () => input.ValorTotal, v => input.ValorTotal = v, nameof(input.ValorTotal));
        var mercadoria = e.Informacao("Mercadoria recebida", () => "");
        var campoParcelas = e.Inteiro("Parcelas", () => parcelas, v => parcelas = v, minimo: 1, maximo: PlanoParcelas.MaximoParcelas);
        var campoPrimeiro = e.Data("1º vencimento", () => primeiro, v => primeiro = v ?? primeiro);
        var campoIntervalo = e.Inteiro("Dias entre parcelas (0 = mensal)", () => intervalo, v => intervalo = v, maximo: 365, span: 2);

        var gradeVencimentos = new DataGridView { AutoGenerateColumns = false, Height = 150, Dock = DockStyle.Top };
        Tema.EstilizarGrade(gradeVencimentos);
        gradeVencimentos.ReadOnly = false;
        gradeVencimentos.AllowUserToAddRows = false;
        gradeVencimentos.Columns.AddRange(
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
        gradeVencimentos.DataSource = vencimentos;
        gradeVencimentos.DataError += (_, ev) => ev.ThrowException = false;
        var conferencia = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 6, 0, 0), Dock = DockStyle.Bottom };
        var painel = new Panel { Height = 190, Dock = DockStyle.Top };
        painel.Controls.Add(gradeVencimentos);
        painel.Controls.Add(conferencia);
        e.Livre(painel);

        void Conferir()
        {
            var soma = vencimentos.Sum(v => v.Valor);
            var total = valorTotal.Value;
            conferencia.Text = soma == total
                ? $"Soma dos vencimentos: {Textos.Moeda(soma)} (confere com a nota)"
                : $"Soma dos vencimentos: {Textos.Moeda(soma)}  •  diferença: {Textos.Moeda(total - soma)}";
            conferencia.ForeColor = soma == total ? Tema.Sucesso : Tema.Perigo;
        }
        void Gerar()
        {
            vencimentos.Clear();
            if (valorTotal.Value > 0)
            {
                var plano = PlanoParcelas.Calcular(valorTotal.Value, (int)campoParcelas.Value, DateOnly.FromDateTime(campoPrimeiro.Value),
                    intervaloDias: campoIntervalo.Value > 0 ? (int)campoIntervalo.Value : null);
                foreach (var p in plano)
                    vencimentos.Add(new LinhaVencimento { Vencimento = p.Vencimento.ToDateTime(TimeOnly.MinValue), Valor = p.Valor });
            }
            Conferir();
        }
        var atualizando = false;
        void Somar()
        {
            var soma = linhas.Where(l => l.Marcado).Sum(l => l.Total);
            mercadoria.Text = $"{Textos.Moeda(soma)} em {linhas.Count(l => l.Marcado)} recebimento(s)";
            if (totalEditado)
                return;
            atualizando = true;
            valorTotal.Value = Math.Min(soma, valorTotal.Maximum);
            atualizando = false;
            Gerar();
        }
        void CarregarRecebimentos(int fornecedorEscolhido, IReadOnlyCollection<int>? marcar)
        {
            linhas.Clear();
            foreach (var r in pendentes.Where(r => r.FornecedorId == fornecedorEscolhido).OrderBy(r => r.Data).ThenBy(r => r.Id))
            {
                linhas.Add(new LinhaRecebimento
                {
                    Marcado = marcar is null || marcar.Count == 0 || marcar.Contains(r.Id),
                    Id = r.Id, Codigo = r.Codigo, Pedido = r.Pedido, Data = r.Data, Itens = r.Itens, Total = r.Total
                });
            }
            totalEditado = false;
            Somar();
        }

        grade.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex >= 0)
                Somar();
        };
        valorTotal.ValueChanged += (_, _) =>
        {
            if (atualizando)
                return;
            totalEditado = true; // frete, desconto ou impostos: o valor da nota vale
            Gerar();
        };
        campoParcelas.ValueChanged += (_, _) => Gerar();
        campoPrimeiro.ValueChanged += (_, _) => Gerar();
        campoIntervalo.ValueChanged += (_, _) => Gerar();
        gradeVencimentos.CellEndEdit += (_, _) => Conferir();
        fornecedor.SelectionChangeCommitted += (_, _) =>
        {
            if (fornecedor.SelectedItem is ItemLookup { Id: > 0 } escolhido)
                CarregarRecebimentos(escolhido.Id, null);
        };
        e.AoCarregar(() => CarregarRecebimentos(input.FornecedorId, marcados));
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            gradeVencimentos.EndEdit();
            input.Recebimentos = linhas.Where(l => l.Marcado).Select(l => l.Id).ToList();
            input.Vencimentos = vencimentos.Select(v => new VencimentoInput { Vencimento = DateOnly.FromDateTime(v.Vencimento), Valor = v.Valor })
                .ToList();
        });

        int? lancada = null;
        using var dialogo = new FormEdicao("Lançar nota fiscal de entrada",
            "Marque os recebimentos que vieram nesta nota. O valor total já vem com a mercadoria recebida; ajuste se a nota tiver " +
            "frete, desconto ou impostos. Os vencimentos viram contas a pagar (ou parcelas).", e,
            async () => lancada = await servico.LancarNotaAsync(input), 760);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? lancada : null;
    }
}
