using System.ComponentModel;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Lançamento da nota fiscal de entrada: os recebimentos que ela fatura e os vencimentos dos boletos. O XML da NF-e
/// preenche a nota; sem pedido, a importação dá entrada no estoque e lança a nota de uma vez.
/// </summary>
internal static class DialogosNotaEntrada
{
    /// <summary>Escolha do arquivo XML (substituída nos testes de tela).</summary>
    internal static Func<IWin32Window, string?> EscolherXml = dono =>
    {
        using var dialogo = new OpenFileDialog
        {
            Title = "Importar XML da NF-e", Filter = "XML da NF-e (*.xml)|*.xml|Todos os arquivos (*.*)|*.*", CheckFileExists = true
        };
        return dialogo.ShowDialog(dono) == DialogResult.OK ? dialogo.FileName : null;
    };

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

        /// <summary>Forma desta parcela (0 = a forma da nota).</summary>
        public int FormaPagamentoId { get; set; }

        public VencimentoInput ParaInput() => new()
        {
            Vencimento = DateOnly.FromDateTime(Vencimento), Valor = Valor, FormaPagamentoId = FormaPagamentoId > 0 ? FormaPagamentoId : null
        };
    }

    // =========================== Nota dos recebimentos (com pedido) ===========================

    /// <summary>
    /// Lança a nota de um fornecedor com recebimentos a faturar. <paramref name="marcados"/> vêm marcados (senão, todos os
    /// recebimentos do fornecedor); com <paramref name="analise"/>, os dados vêm do XML da NF-e. Retorna o Id da nota, ou
    /// nulo se cancelar.
    /// </summary>
    public static async Task<int?> LancarAsync(IWin32Window dono, CompraService servico, IConsultasCompras consultas,
        ImportacaoNfeService importacao, int? fornecedorId = null, IReadOnlyCollection<int>? marcados = null, AnaliseNfe? analise = null)
    {
        var pendentes = await consultas.ListarRecebimentosAsync(new FiltroRecebimentos { Status = StatusRecebimento.AFaturar });
        if (pendentes.Count == 0)
        {
            Mensagens.Aviso(dono, "Não há recebimentos aguardando nota fiscal. Receba um pedido de compra primeiro (ou, numa compra " +
                                  "sem pedido, use Importar XML da NF-e em Notas fiscais de entrada).");
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
        var totalEditado = false;
        var doXml = false;
        var (condicoes, formas) = await servico.OpcoesPagamentoAsync();
        var itensCondicao = condicoes.Select(c => new ItemLookup { Id = c.Id, Nome = c.Nome }).ToList();
        var itensForma = formas.Select(f => new ItemLookup { Id = f.Id, Nome = f.Nome }).ToList();
        int? condicaoId = null;
        var pagamentoEscolhido = false; // o usuário mexeu na condição ou na forma: não volta ao padrão do pedido

        var e = new EditorCampos();
        var fornecedor = e.Lista("Fornecedor", fornecedores, () => input.FornecedorId, v => input.FornecedorId = v ?? 0,
            nameof(input.FornecedorId), span: 2);
        var numero = e.Texto("Número da nota", () => input.Numero, v => input.Numero = v ?? "", nameof(input.Numero), NotaFiscalEntrada.TamanhoNumero);
        var serie = e.Texto("Série", () => input.Serie, v => input.Serie = v, nameof(input.Serie), NotaFiscalEntrada.TamanhoSerie, dica: "Opcional");
        var emissao = e.Data("Data de emissão", () => input.DataEmissao, v => input.DataEmissao = v ?? hoje, nameof(input.DataEmissao));
        e.Opcoes("Categoria da despesa", () => input.Categoria, v => input.Categoria = v, nameof(input.Categoria));
        var condicao = e.Lista("Condição de pagamento (preenche os vencimentos)", itensCondicao, () => condicaoId, v => condicaoId = v,
            permitirVazio: true);
        var forma = e.Lista("Forma de pagamento", itensForma, () => input.FormaPagamentoId, v => input.FormaPagamentoId = v,
            nameof(input.FormaPagamentoId), permitirVazio: true);

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
        var conferenciaXml = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Dock = DockStyle.Bottom, Visible = false };
        var painelRecebimentos = new Panel { Height = 150, Dock = DockStyle.Top };
        painelRecebimentos.Controls.Add(grade);
        painelRecebimentos.Controls.Add(conferenciaXml);
        e.Livre(painelRecebimentos);
        e.Associar(nameof(input.Recebimentos), grade);

        e.Secao("Valor e vencimentos");
        var valorTotal = e.Decimal("Valor total da nota", () => input.ValorTotal, v => input.ValorTotal = v, nameof(input.ValorTotal));
        var mercadoria = e.Informacao("Mercadoria recebida", () => "");
        var (painelVencimentos, gradeVencimentos, conferir) = PainelVencimentos(vencimentos, () => valorTotal.Value, itensForma);
        e.Livre(painelVencimentos);

        CondicaoPagamento? CondicaoEscolhida() =>
            condicao.SelectedItem is ItemLookup { Id: > 0 } item ? condicoes.FirstOrDefault(c => c.Id == item.Id) : null;
        void Gerar()
        {
            if (doXml)
                return; // os vencimentos são os boletos da NF-e
            vencimentos.Clear();
            if (valorTotal.Value > 0 && CondicaoEscolhida() is { } escolhida)
            {
                // Prazos da condição contados a partir da emissão da nota.
                foreach (var v in CompraService.Vencimentos(escolhida, valorTotal.Value, DateOnly.FromDateTime(emissao.Value)))
                    vencimentos.Add(new LinhaVencimento
                    {
                        Vencimento = v.Vencimento.ToDateTime(TimeOnly.MinValue), Valor = v.Valor, FormaPagamentoId = v.FormaPagamentoId ?? 0
                    });
            }
            else if (valorTotal.Value > 0)
            {
                // Sem condição: um vencimento em 28 dias (ajuste na grade ou escolha a condição).
                vencimentos.Add(new LinhaVencimento { Vencimento = emissao.Value.Date.AddDays(28), Valor = valorTotal.Value });
            }
            conferir();
        }
        void Selecionar(ComboBox combo, IReadOnlyList<ItemLookup> itens, int? id)
        {
            var indice = id is { } valor ? itens.ToList().FindIndex(i => i.Id == valor) : -1;
            combo.SelectedIndex = indice + 1; // a primeira opção é "(nenhum)"
        }
        void ConferirXml()
        {
            if (analise is null || analise.FornecedorId != input.FornecedorId)
            {
                conferenciaXml.Visible = false;
                return;
            }
            var divergencias = analise.Divergencias(linhas.Where(l => l.Marcado).Select(l => l.Id).ToList());
            conferenciaXml.Visible = true;
            conferenciaXml.ForeColor = divergencias.Count == 0 ? Tema.Sucesso : Tema.Alerta;
            conferenciaXml.Text = divergencias.Count == 0
                ? $"✓ Os {analise.Itens.Count} item(ns) da NF-e conferem com os recebimentos marcados."
                : "⚠ Conferência com a NF-e: " + string.Join("; ", divergencias.Take(4)) + (divergencias.Count > 4 ? $" (e mais {divergencias.Count - 4})" : "");
        }
        var atualizando = false;
        void Somar()
        {
            var soma = linhas.Where(l => l.Marcado).Sum(l => l.Total);
            mercadoria.Text = $"{Textos.Moeda(soma)} em {linhas.Count(l => l.Marcado)} recebimento(s)";
            ConferirXml();
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
            // Condição e forma do pedido do primeiro recebimento marcado.
            if (!pagamentoEscolhido && pendentes.FirstOrDefault(r => linhas.Any(l => l.Marcado && l.Id == r.Id)) is { } origem)
            {
                Selecionar(condicao, itensCondicao, origem.CondicaoPagamentoId);
                Selecionar(forma, itensForma, origem.FormaPagamentoId);
            }
            totalEditado = doXml;
            Somar();
        }

        // Dados da NF-e: fornecedor, número, emissão, valor e boletos; os recebimentos do fornecedor vêm marcados.
        void AplicarXml(AnaliseNfe dados)
        {
            if (dados.FornecedorId is not { } id || fornecedores.All(x => x.Id != id))
            {
                Mensagens.Aviso(dono, $"A NF-e é de {dados.Fornecedor}, que não tem recebimentos aguardando nota. Para uma compra sem pedido, " +
                                      "use Importar XML da NF-e em Compras › Notas fiscais de entrada.");
                return;
            }
            analise = dados;
            var nota = dados.Nota;
            doXml = true;
            input.FornecedorId = id;
            input.ChaveAcesso = nota.ChaveAcesso;
            fornecedor.SelectedIndex = fornecedores.FindIndex(x => x.Id == id);
            numero.Text = nota.Numero;
            serie.Text = nota.Serie ?? "";
            emissao.Value = nota.DataEmissao.ToDateTime(TimeOnly.MinValue);
            atualizando = true;
            valorTotal.Value = Math.Min(nota.ValorTotal, valorTotal.Maximum);
            atualizando = false;
            vencimentos.Clear();
            var boletos = nota.Duplicatas.Count > 0 ? nota.Duplicatas.Select(d => (d.Vencimento, d.Valor)).ToList() : [(nota.DataEmissao, nota.ValorTotal)];
            foreach (var (vencimento, valor) in boletos)
                vencimentos.Add(new LinhaVencimento { Vencimento = vencimento.ToDateTime(TimeOnly.MinValue), Valor = valor });
            CarregarRecebimentos(id, null);
            if (forma.SelectedItem is not ItemLookup { Id: > 0 } && dados.FormaPagamentoId is { } formaDaNota)
                Selecionar(forma, itensForma, formaDaNota);
            conferir();
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
            if (doXml)
                conferir();
            else
                Gerar();
        };
        condicao.SelectionChangeCommitted += (_, _) =>
        {
            pagamentoEscolhido = true;
            doXml = false;
            Gerar();
        };
        forma.SelectionChangeCommitted += (_, _) => pagamentoEscolhido = true;
        emissao.ValueChanged += (_, _) =>
        {
            if (CondicaoEscolhida() is not null)
                Gerar();
        };
        fornecedor.SelectionChangeCommitted += (_, _) =>
        {
            if (fornecedor.SelectedItem is ItemLookup { Id: > 0 } escolhido)
            {
                input.FornecedorId = escolhido.Id;
                CarregarRecebimentos(escolhido.Id, null);
            }
        };
        e.AoCarregar(() =>
        {
            CarregarRecebimentos(input.FornecedorId, marcados);
            if (analise is not null)
                AplicarXml(analise);
        });
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            gradeVencimentos.EndEdit();
            input.Recebimentos = linhas.Where(l => l.Marcado).Select(l => l.Id).ToList();
            input.Vencimentos = vencimentos.Select(v => v.ParaInput()).ToList();
            if (analise is not null && (analise.FornecedorId != input.FornecedorId || analise.Nota.Numero != input.Numero.Trim()))
                input.ChaveAcesso = null; // trocou o fornecedor ou o número: não é mais a NF-e importada
        });

        int? lancada = null;
        using var dialogo = new FormEdicao("Lançar nota fiscal de entrada",
            "Marque os recebimentos que vieram nesta nota. O valor total já vem com a mercadoria recebida; ajuste se a nota tiver " +
            "frete, desconto ou impostos. A condição de pagamento dá os vencimentos (ajustáveis na grade), que viram contas a pagar. " +
            "Com o XML da NF-e, tudo vem preenchido.", e,
            async () => lancada = await servico.LancarNotaAsync(input), 760);
        var importar = dialogo.AdicionarAcao(Tema.BotaoSecundario("Importar XML da NF-e", Icones.Exportar));
        importar.Click += async (_, _) =>
        {
            if (await LerAsync(dialogo, importacao) is { } dados)
                AplicarXml(dados);
        };
        return dialogo.ShowDialog(dono) == DialogResult.OK ? lancada : null;
    }

    // =========================== Importação do XML ===========================

    /// <summary>
    /// Importa o XML da NF-e: se o fornecedor tem recebimentos aguardando nota e a nota é deles, abre o lançamento da nota
    /// já preenchido; senão, a compra sem pedido (entrada no estoque e nota de uma vez). Retorna o Id da nota lançada.
    /// </summary>
    public static async Task<int?> ImportarAsync(IWin32Window dono, ImportacaoNfeService importacao, CompraService compras, IConsultasCompras consultas)
    {
        if (await LerAsync(dono, importacao) is not { } analise)
            return null;
        if (analise.RecebimentosAFaturar.Count > 0 &&
            Mensagens.Confirmar(dono, $"{analise.Fornecedor} tem {analise.RecebimentosAFaturar.Count} recebimento(s) de pedido aguardando nota. " +
                                      $"A {analise.Nota.Descricao} é desses pedidos?\n\nSim: a nota fatura os recebimentos (a mercadoria já está no estoque).\n" +
                                      "Não: é uma compra sem pedido; os itens da nota entram no estoque agora.", "NF-e de pedido?"))
            return await LancarAsync(dono, compras, consultas, importacao, analise.FornecedorId, null, analise);
        return await SemPedidoAsync(dono, importacao, compras, analise);
    }

    private static async Task<AnaliseNfe?> LerAsync(IWin32Window dono, ImportacaoNfeService importacao)
    {
        if (EscolherXml(dono) is not { } arquivo)
            return null;
        try
        {
            await using var conteudo = File.OpenRead(arquivo);
            return await importacao.AnalisarAsync(LeitorNfe.Ler(conteudo));
        }
        catch (Exception ex) when (ex is RegraNegocioException or IOException or UnauthorizedAccessException)
        {
            Mensagens.Erro(dono, ex);
            return null;
        }
    }

    // =========================== Compra sem pedido ===========================

    /// <summary>Item da NF-e e o produto do sistema em que ele entra (0 = escolher, -1 = não entra no estoque).</summary>
    private sealed class LinhaItemNfe : INotifyPropertyChanged
    {
        public const int NaoEstocar = -1;

        public required ItemNfe Item { get; init; }
        public string Descricao => Item.Descricao;
        public string Codigo => Item.Codigo;
        public decimal Quantidade => Item.Quantidade;
        public string Unidade => Item.Unidade;
        public decimal Custo => Item.CustoUnitario;
        public decimal Total => Item.ValorTotal - Item.Desconto;
        public int ProdutoId { get; set; }
        public int EmbalagemId { get; set; }
        public OrigemVinculo Origem { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void Atualizar() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private static async Task<int?> SemPedidoAsync(IWin32Window dono, ImportacaoNfeService importacao, CompraService compras, AnaliseNfe analise)
    {
        var nota = analise.Nota;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var produtos = new List<ProdutoCompra>
        {
            new(0, "(escolha o produto)", "", 0, null, [new UnidadeCompra(0, "", 1)]),
            new(LinhaItemNfe.NaoEstocar, "(não entra no estoque)", "", 0, null, [new UnidadeCompra(0, "-", 1)])
        };
        produtos.AddRange(await compras.ProdutosAsync(analise.FornecedorId ?? 0));
        var linhas = new BindingList<LinhaItemNfe>(analise.Itens.Select(i => new LinhaItemNfe
        {
            Item = i.Item, ProdutoId = i.ProdutoId ?? 0, EmbalagemId = i.ProdutoEmbalagemId, Origem = i.Origem
        }).ToList());
        var input = new NotaSemPedidoInput
        {
            Nota = nota, FornecedorId = analise.FornecedorId, DataRecebimento = hoje, FormaPagamentoId = analise.FormaPagamentoId
        };
        var (condicoes, formasPagamento) = await compras.OpcoesPagamentoAsync();
        var formas = formasPagamento.Select(f => new ItemLookup { Id = f.Id, Nome = f.Nome }).ToList();
        var itensCondicao = condicoes.Select(c => new ItemLookup { Id = c.Id, Nome = c.Nome }).ToList();
        int? condicaoId = null;
        var vencimentos = new BindingList<LinhaVencimento>(
            (nota.Duplicatas.Count > 0 ? nota.Duplicatas.Select(d => (d.Vencimento, d.Valor)).ToList() : [(nota.DataEmissao, nota.ValorTotal)])
            .Select(v => new LinhaVencimento { Vencimento = v.Item1.ToDateTime(TimeOnly.MinValue), Valor = v.Item2 }).ToList());

        var e = new EditorCampos();
        e.Informacao("NF-e", () => $"{nota.Descricao}  •  emitida em {nota.DataEmissao:dd/MM/yyyy}", span: 2);
        e.Informacao("Fornecedor", () => $"{analise.Fornecedor} ({Documento.Formatar(nota.Emitente.Documento)})" +
                                         (analise.FornecedorId is null ? "  •  será cadastrado com os dados da nota" : ""), span: 2);
        e.Informacao("Valor da nota", () => $"{Textos.Moeda(nota.ValorTotal)}  (produtos {Textos.Moeda(nota.ValorProdutos)}" +
                                            (nota.ValorFrete > 0 ? $", frete {Textos.Moeda(nota.ValorFrete)}" : "") +
                                            (nota.ValorDesconto > 0 ? $", desconto {Textos.Moeda(nota.ValorDesconto)}" : "") +
                                            (nota.ValorOutros > 0 ? $", outros {Textos.Moeda(nota.ValorOutros)}" : "") + ")", span: 2);
        e.Data("Data do recebimento", () => input.DataRecebimento, v => input.DataRecebimento = v ?? hoje, nameof(input.DataRecebimento));
        e.Opcoes("Categoria da despesa", () => input.Categoria, v => input.Categoria = v, nameof(input.Categoria));
        e.Lista("Forma de pagamento", formas, () => input.FormaPagamentoId, v => input.FormaPagamentoId = v, nameof(input.FormaPagamentoId),
            permitirVazio: true);

        e.Secao("Itens da nota e produtos do sistema");
        var grade = new DataGridView { AutoGenerateColumns = false };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.AllowUserToAddRows = false;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        DataGridViewTextBoxColumn Coluna(string titulo, string propriedade, float peso, string? formato = null) => new()
        {
            HeaderText = titulo, DataPropertyName = propriedade, FillWeight = peso, ReadOnly = true,
            DefaultCellStyle = formato is null
                ? new DataGridViewCellStyle()
                : new DataGridViewCellStyle { Format = formato, FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
        };
        var colunaProduto = new ColunaLista
        {
            HeaderText = "Produto do sistema", DataPropertyName = nameof(LinhaItemNfe.ProdutoId), FillWeight = 30, DataSource = produtos,
            ValueMember = nameof(ProdutoCompra.Id), DisplayMember = nameof(ProdutoCompra.Nome),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
        };
        var colunaUnidade = new ColunaLista
        {
            HeaderText = "Vem em", DataPropertyName = nameof(LinhaItemNfe.EmbalagemId), FillWeight = 17,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
        };
        var colunaEstoque = new DataGridViewTextBoxColumn { HeaderText = "No estoque", FillWeight = 12, ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } };
        var colunaSituacao = new DataGridViewTextBoxColumn { HeaderText = "Situação", FillWeight = 22, ReadOnly = true };
        grade.Columns.AddRange(
            Coluna("Item da nota", nameof(LinhaItemNfe.Descricao), 27),
            Coluna("Cód.", nameof(LinhaItemNfe.Codigo), 9),
            Coluna("Qtd.", nameof(LinhaItemNfe.Quantidade), 9, "N3"),
            Coluna("Un.", nameof(LinhaItemNfe.Unidade), 6),
            Coluna("Custo unit.", nameof(LinhaItemNfe.Custo), 11, "N4"),
            Coluna("Total", nameof(LinhaItemNfe.Total), 11, "N2"),
            colunaProduto, colunaUnidade, colunaEstoque, colunaSituacao);
        grade.DataSource = linhas;
        grade.DataError += (_, ev) => ev.ThrowException = false;

        ProdutoCompra? Produto(LinhaItemNfe linha) => produtos.FirstOrDefault(p => p.Id == linha.ProdutoId);
        void AjustarUnidade(int indice)
        {
            if (indice >= grade.Rows.Count || Produto(linhas[indice]) is not { } produto)
                return;
            var celula = (DataGridViewComboBoxCell)grade.Rows[indice].Cells[colunaUnidade.Index];
            celula.DataSource = produto.Unidades.ToList();
            celula.ValueMember = nameof(UnidadeCompra.Id);
            celula.DisplayMember = nameof(UnidadeCompra.Nome);
        }
        grade.CellFormatting += (_, ev) =>
        {
            if (ev.RowIndex < 0 || ev.RowIndex >= linhas.Count)
                return;
            var linha = linhas[ev.RowIndex];
            var produto = Produto(linha);
            var unidade = produto?.Unidades.FirstOrDefault(u => u.Id == linha.EmbalagemId);
            if (ev.ColumnIndex == colunaEstoque.Index)
                ev.Value = linha.ProdutoId > 0 && unidade is not null ? $"{linha.Quantidade * unidade.Fator:0.###} {produto!.Unidade}" : "";
            else if (ev.ColumnIndex == colunaSituacao.Index)
            {
                var siglaEscolhida = unidade?.Nome.Split(' ')[0];
                (ev.Value, var cor) = linha.ProdutoId switch
                {
                    0 => ("Escolha o produto", Tema.Perigo),
                    LinhaItemNfe.NaoEstocar => ("Fora do estoque", Tema.Neutro),
                    _ when !string.Equals(siglaEscolhida, linha.Unidade, StringComparison.OrdinalIgnoreCase) =>
                        ($"⚠ Nota em {linha.Unidade}: confira a unidade", Tema.Alerta),
                    _ => linha.Origem switch
                    {
                        OrigemVinculo.Vinculo => ("Vínculo salvo", Tema.Sucesso),
                        OrigemVinculo.CodigoFornecedor => ("Código do fornecedor", Tema.Sucesso),
                        OrigemVinculo.Nome => ("Pelo nome: confira", Tema.Alerta),
                        _ => ("Escolhido", Tema.Sucesso)
                    }
                };
                ev.CellStyle.ForeColor = cor;
            }
        };
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
            if (ev.ColumnIndex == colunaProduto.Index)
            {
                // Produto escolhido: a unidade com a sigla da nota, se houver; senão, a do estoque.
                linha.EmbalagemId = Produto(linha)?.Unidades
                    .FirstOrDefault(u => u.Id > 0 && string.Equals(u.Nome.Split(' ')[0], linha.Unidade, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
                linha.Origem = OrigemVinculo.Nenhuma;
                AjustarUnidade(ev.RowIndex);
            }
            linha.Atualizar();
            grade.InvalidateRow(ev.RowIndex);
        };
        var painel = new Panel { Height = 300, Dock = DockStyle.Top };
        grade.Dock = DockStyle.Fill;
        painel.Controls.Add(grade);
        painel.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(1050, 0), Dock = DockStyle.Bottom, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 4, 0, 0),
            Text = "O vínculo de cada código fica gravado: na próxima nota deste fornecedor os itens já vêm reconhecidos. " +
                   "Se a unidade da nota for uma embalagem que não está no produto, cadastre-a em Estoque › Produtos."
        });
        e.Livre(painel);
        e.Associar(nameof(input.Itens), grade);

        e.Secao("Vencimentos (boletos da nota)");
        var condicao = e.Lista("Condição de pagamento (troca os vencimentos)", itensCondicao, () => condicaoId, v => condicaoId = v,
            permitirVazio: true, span: 2);
        var (painelVencimentos, gradeVencimentos, conferir) = PainelVencimentos(vencimentos, () => nota.ValorTotal, formas);
        e.Livre(painelVencimentos);
        condicao.SelectionChangeCommitted += (_, _) =>
        {
            vencimentos.Clear();
            var escolhida = condicao.SelectedItem is ItemLookup { Id: > 0 } item ? condicoes.FirstOrDefault(c => c.Id == item.Id) : null;
            var novos = escolhida is null
                ? (nota.Duplicatas.Count > 0 ? nota.Duplicatas.Select(d => (d.Vencimento, d.Valor, (int?)null)).ToList() : [(nota.DataEmissao, nota.ValorTotal, null)])
                : CompraService.Vencimentos(escolhida, nota.ValorTotal, nota.DataEmissao).Select(v => (v.Vencimento, v.Valor, v.FormaPagamentoId)).ToList();
            foreach (var (vencimento, valor, formaId) in novos)
                vencimentos.Add(new LinhaVencimento { Vencimento = vencimento.ToDateTime(TimeOnly.MinValue), Valor = valor, FormaPagamentoId = formaId ?? 0 });
            conferir();
        };
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 500, span: 2);
        e.AoCarregar(conferir);
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            gradeVencimentos.EndEdit();
            var semProduto = linhas.Where(l => l.ProdutoId == 0).Select(l => $"{l.Item.Numero} ({l.Descricao})").ToList();
            Validador.Garantir(semProduto.Count == 0,
                $"Escolha o produto (ou \"não entra no estoque\") dos itens {string.Join(", ", semProduto.Take(5))}{(semProduto.Count > 5 ? "..." : "")}.");
            input.Itens = linhas.Select(l => new ItemSemPedidoInput(l.Item.Numero, l.ProdutoId > 0 ? l.ProdutoId : null, l.EmbalagemId)).ToList();
            input.Vencimentos = vencimentos.Select(v => v.ParaInput()).ToList();
        });

        int? lancada = null;
        using var dialogo = new FormEdicao($"Importar {nota.Descricao} (compra sem pedido)",
            "Confira o produto do sistema de cada item: a quantidade entra no estoque agora, convertida pela unidade escolhida, e a " +
            "nota vira contas a pagar com os boletos dela.", e, async () => lancada = await importacao.LancarSemPedidoAsync(input), 1150);
        dialogo.Shown += (_, _) =>
        {
            for (var i = 0; i < linhas.Count; i++)
                AjustarUnidade(i);
            grade.Invalidate();
        };
        return dialogo.ShowDialog(dono) == DialogResult.OK ? lancada : null;
    }

    // =========================== Vencimentos ===========================

    /// <summary>
    /// Grade editável dos vencimentos (data, valor e forma de cada um) com a conferência da soma contra o total da nota.
    /// </summary>
    private static (Panel Painel, DataGridView Grade, Action Conferir) PainelVencimentos(BindingList<LinhaVencimento> vencimentos, Func<decimal> total,
        IReadOnlyList<ItemLookup> formas)
    {
        var grade = new DataGridView { AutoGenerateColumns = false, Dock = DockStyle.Fill };
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
            },
            new ColunaLista
            {
                HeaderText = "Forma", DataPropertyName = nameof(LinhaVencimento.FormaPagamentoId), ValueMember = nameof(ItemLookup.Id),
                DisplayMember = nameof(ItemLookup.Nome), ValueType = typeof(int),
                DataSource = new[] { new ItemLookup { Id = 0, Nome = "(forma da nota)" } }.Concat(formas).ToList(),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
            });
        grade.DataSource = vencimentos;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        grade.DataError += (_, ev) => ev.ThrowException = false;
        grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grade.IsCurrentCellDirty && grade.CurrentCell is DataGridViewComboBoxCell)
                grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        var conferencia = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 6, 0, 0), Dock = DockStyle.Bottom };
        var painel = new Panel { Height = 190, Dock = DockStyle.Top };
        painel.Controls.Add(grade);
        painel.Controls.Add(conferencia);

        void Conferir()
        {
            var soma = vencimentos.Sum(v => v.Valor);
            var valor = total();
            conferencia.Text = soma == valor
                ? $"Soma dos vencimentos: {Textos.Moeda(soma)} (confere com a nota)"
                : $"Soma dos vencimentos: {Textos.Moeda(soma)}  •  diferença: {Textos.Moeda(valor - soma)}";
            conferencia.ForeColor = soma == valor ? Tema.Sucesso : Tema.Perigo;
        }
        grade.CellEndEdit += (_, _) => Conferir();

        var incluir = Tema.BotaoSecundario("Incluir vencimento", Icones.Adicionar);
        incluir.Click += (_, _) =>
        {
            grade.EndEdit();
            var ultimo = vencimentos.LastOrDefault();
            var falta = total() - vencimentos.Sum(v => v.Valor);
            vencimentos.Add(new LinhaVencimento
            {
                Vencimento = (ultimo?.Vencimento ?? DateTime.Today).AddDays(ultimo is null ? 28 : 30), Valor = Math.Max(falta, 0)
            });
            Conferir();
        };
        var remover = Tema.BotaoSecundario("Remover vencimento", Icones.Excluir);
        remover.Click += (_, _) =>
        {
            grade.EndEdit();
            if (grade.CurrentRow?.DataBoundItem is LinhaVencimento linha)
                vencimentos.Remove(linha);
            Conferir();
        };
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        barra.Controls.AddRange([incluir, remover]);
        painel.Height = 230;
        painel.Controls.Add(barra);
        return (painel, grade, Conferir);
    }
}
