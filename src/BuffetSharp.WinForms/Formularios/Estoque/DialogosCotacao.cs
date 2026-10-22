using System.ComponentModel;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Edição da cotação (produtos e participantes) e o mapa de preços, onde se escolhe o vencedor de cada produto.</summary>
internal static class DialogosCotacao
{
    private sealed class LinhaItem
    {
        public int ProdutoId { get; set; }
        public decimal Quantidade { get; set; }
        public string? Especificacao { get; set; }
    }

    private sealed class LinhaParticipante
    {
        public int Id { get; init; }
        public int FornecedorId { get; set; }
        public string? Nome { get; set; }
        public string? Contato { get; set; }
        public string? Telefone { get; set; }
        public string? Email { get; set; }
        public DateTime? PrevisaoEntrega { get; set; }
        public string? CondicaoPagamento { get; set; }
    }

    /// <summary>Abre a cotação (0 = nova). Retorna o Id gravado, ou nulo se cancelar.</summary>
    public static async Task<int?> EditarAsync(IWin32Window dono, CotacaoService servico, IConsultasLookup lookups, int cotacaoId)
    {
        var input = cotacaoId == 0 ? servico.Nova() : await servico.ObterAsync(cotacaoId);
        Validador.Garantir(input.Status == StatusCotacao.Aberta,
            input.Status == StatusCotacao.Concluida ? "A cotação já foi concluída (os pedidos foram gerados)." : "A cotação foi cancelada.");
        var produtos = await lookups.ListarAsync(TipoLookup.Produtos);
        var eventos = await lookups.ListarAsync(TipoLookup.EventosConfirmados);
        var fornecedores = new List<ItemLookup> { new() { Id = 0, Nome = "(não cadastrado)" } };
        fornecedores.AddRange(await lookups.ListarAsync(TipoLookup.Fornecedores));

        var itens = new BindingList<LinhaItem>(input.Itens.Select(i => new LinhaItem
        {
            ProdutoId = i.ProdutoId, Quantidade = i.Quantidade, Especificacao = i.Especificacao
        }).ToList());
        var participantes = new BindingList<LinhaParticipante>(input.Participantes.Select(p => new LinhaParticipante
        {
            Id = p.Id, FornecedorId = p.FornecedorId ?? 0, Nome = p.Nome, Contato = p.Contato, Telefone = p.Telefone, Email = p.Email,
            PrevisaoEntrega = p.PrevisaoEntrega?.ToDateTime(TimeOnly.MinValue), CondicaoPagamento = p.CondicaoPagamento
        }).ToList());

        var e = new EditorCampos();
        e.Texto("Descrição", () => input.Descricao, v => input.Descricao = v ?? "", nameof(input.Descricao), CotacaoCompra.TamanhoDescricao,
            span: 2, dica: "Ex.: compras do casamento Silva (300 convidados)");
        e.Data("Data", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        e.Data("Prazo para os preços", () => input.PrazoResposta, v => input.PrazoResposta = v, nameof(input.PrazoResposta), opcional: true);
        e.Lista("Evento (opcional)", eventos, () => input.EventoId, v => input.EventoId = v, nameof(input.EventoId), permitirVazio: true, span: 2);
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 1000, span: 2,
            dica: "Preferências do cliente, marcas, condições...");

        // ---------------- Produtos ----------------
        e.Secao("Produtos (na unidade do estoque)");
        var gradeItens = new DataGridView { AutoGenerateColumns = false };
        Tema.EstilizarGrade(gradeItens);
        gradeItens.ReadOnly = false;
        gradeItens.AllowUserToAddRows = false;
        gradeItens.EditMode = DataGridViewEditMode.EditOnEnter;
        var colunaProduto = new DataGridViewComboBoxColumn
        {
            HeaderText = "Produto", DataPropertyName = nameof(LinhaItem.ProdutoId), FillWeight = 40, DataSource = produtos.ToList(),
            ValueMember = nameof(ItemLookup.Id), DisplayMember = nameof(ItemLookup.Nome),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
        };
        var colunaUnidade = new DataGridViewTextBoxColumn { HeaderText = "Un.", FillWeight = 8, ReadOnly = true };
        gradeItens.Columns.AddRange(
            colunaProduto,
            colunaUnidade,
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Quantidade", DataPropertyName = nameof(LinhaItem.Quantidade), FillWeight = 14,
                DefaultCellStyle = { Format = "N3", FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Especificação (marca, qualidade...)", DataPropertyName = nameof(LinhaItem.Especificacao), FillWeight = 38,
                MaxInputLength = CotacaoItem.TamanhoEspecificacao
            });
        gradeItens.DataSource = itens;
        gradeItens.DataError += (_, ev) => ev.ThrowException = false;
        gradeItens.CellFormatting += (_, ev) =>
        {
            if (ev.ColumnIndex == colunaUnidade.Index && ev.RowIndex >= 0 && ev.RowIndex < itens.Count)
                ev.Value = produtos.FirstOrDefault(p => p.Id == itens[ev.RowIndex].ProdutoId)?.Info;
        };
        gradeItens.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (gradeItens.IsCurrentCellDirty && gradeItens.CurrentCell is DataGridViewComboBoxCell)
                gradeItens.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        gradeItens.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex >= 0)
                gradeItens.InvalidateRow(ev.RowIndex);
        };
        e.Livre(PainelGrade(gradeItens, 220, "Incluir produto", () =>
        {
            itens.Add(new LinhaItem());
            return 0;
        }, "Remover produto", () =>
        {
            if (gradeItens.CurrentRow?.DataBoundItem is LinhaItem linha)
                itens.Remove(linha);
        }));
        e.Associar(nameof(input.Itens), gradeItens);

        // ---------------- Participantes ----------------
        e.Secao("Participantes (fornecedores consultados)");
        var gradeParticipantes = new DataGridView { AutoGenerateColumns = false };
        Tema.EstilizarGrade(gradeParticipantes);
        gradeParticipantes.ReadOnly = false;
        gradeParticipantes.AllowUserToAddRows = false;
        gradeParticipantes.EditMode = DataGridViewEditMode.EditOnEnter;
        var colunaFornecedor = new DataGridViewComboBoxColumn
        {
            HeaderText = "Fornecedor cadastrado", DataPropertyName = nameof(LinhaParticipante.FornecedorId), FillWeight = 24,
            DataSource = fornecedores, ValueMember = nameof(ItemLookup.Id), DisplayMember = nameof(ItemLookup.Nome),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat
        };
        DataGridViewTextBoxColumn Texto(string titulo, string propriedade, float peso, int maximo) => new()
        {
            HeaderText = titulo, DataPropertyName = propriedade, FillWeight = peso, MaxInputLength = maximo
        };
        gradeParticipantes.Columns.AddRange(
            colunaFornecedor,
            Texto("Nome", nameof(LinhaParticipante.Nome), 24, CotacaoParticipante.TamanhoNome),
            Texto("Contato", nameof(LinhaParticipante.Contato), 14, 100),
            Texto("Telefone", nameof(LinhaParticipante.Telefone), 13, 20),
            Texto("E-mail", nameof(LinhaParticipante.Email), 18, 150),
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Entrega", DataPropertyName = nameof(LinhaParticipante.PrevisaoEntrega), FillWeight = 11,
                DefaultCellStyle = { Format = "dd/MM/yyyy", FormatProvider = Textos.PtBr, NullValue = "", DataSourceNullValue = null }
            },
            Texto("Pagamento", nameof(LinhaParticipante.CondicaoPagamento), 14, 100));
        gradeParticipantes.DataSource = participantes;
        gradeParticipantes.DataError += (_, ev) => ev.ThrowException = false;
        gradeParticipantes.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (gradeParticipantes.IsCurrentCellDirty && gradeParticipantes.CurrentCell is DataGridViewComboBoxCell)
                gradeParticipantes.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        gradeParticipantes.CellValueChanged += (_, ev) =>
        {
            // Fornecedor escolhido: nome e telefone vêm do cadastro (se ainda não foram digitados).
            if (ev.RowIndex < 0 || ev.ColumnIndex != colunaFornecedor.Index || ev.RowIndex >= participantes.Count)
                return;
            var linha = participantes[ev.RowIndex];
            if (fornecedores.FirstOrDefault(f => f.Id == linha.FornecedorId) is not { Id: > 0 } fornecedor)
                return;
            if (string.IsNullOrWhiteSpace(linha.Nome) || fornecedores.Any(f => f.Nome == linha.Nome))
                linha.Nome = fornecedor.Nome;
            if (string.IsNullOrWhiteSpace(linha.Telefone))
                linha.Telefone = fornecedor.Info;
            gradeParticipantes.InvalidateRow(ev.RowIndex);
        };
        e.Livre(PainelGrade(gradeParticipantes, 200, "Incluir participante", () =>
        {
            participantes.Add(new LinhaParticipante());
            return 1;
        }, "Remover participante", () =>
        {
            if (gradeParticipantes.CurrentRow?.DataBoundItem is LinhaParticipante linha)
                participantes.Remove(linha);
        }));
        e.Associar(nameof(input.Participantes), gradeParticipantes);

        e.AoAplicar(() =>
        {
            gradeItens.EndEdit();
            gradeParticipantes.EndEdit();
            input.Itens = itens.Where(i => i.ProdutoId > 0).Select(i => new CotacaoItemInput
            {
                ProdutoId = i.ProdutoId, Quantidade = i.Quantidade, Especificacao = i.Especificacao
            }).ToList();
            var anteriores = input.Participantes.ToDictionary(p => p.Id);
            input.Participantes = participantes.Where(p => !string.IsNullOrWhiteSpace(p.Nome) || p.FornecedorId > 0).Select(p =>
            {
                var anterior = anteriores.GetValueOrDefault(p.Id);
                return new CotacaoParticipanteInput
                {
                    Id = p.Id,
                    Nome = p.Nome ?? "",
                    FornecedorId = p.FornecedorId > 0 ? p.FornecedorId : null,
                    Contato = p.Contato,
                    Telefone = p.Telefone,
                    Email = p.Email,
                    PrevisaoEntrega = p.PrevisaoEntrega is { } d ? DateOnly.FromDateTime(d) : null,
                    CondicaoPagamento = p.CondicaoPagamento,
                    Observacoes = anterior?.Observacoes
                };
            }).ToList();
        });

        int? salvo = null;
        using var dialogo = new FormEdicao(cotacaoId == 0 ? "Nova cotação de compra" : $"Editar cotação {input.Codigo}",
            "Os participantes não precisam ser fornecedores cadastrados: o cadastro só é exigido para virar pedido. " +
            "Depois de gravar, informe os preços no mapa da cotação.", e, async () => salvo = await servico.SalvarAsync(input), 1000);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? salvo : null;
    }

    private static Panel PainelGrade(DataGridView grade, int altura, string textoIncluir, Func<int> incluir, string textoRemover, Action remover)
    {
        var botaoIncluir = Tema.BotaoSecundario(textoIncluir, Icones.Adicionar);
        botaoIncluir.Click += (_, _) =>
        {
            grade.EndEdit();
            var coluna = incluir();
            grade.CurrentCell = grade.Rows[^1].Cells[coluna];
            grade.BeginEdit(true);
        };
        var botaoRemover = Tema.BotaoSecundario(textoRemover, Icones.Excluir);
        botaoRemover.Click += (_, _) =>
        {
            grade.EndEdit();
            remover();
        };
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        barra.Controls.AddRange([botaoIncluir, botaoRemover]);
        var painel = new Panel { Height = altura, Dock = DockStyle.Top };
        grade.Dock = DockStyle.Fill;
        painel.Controls.Add(grade);
        painel.Controls.Add(barra);
        return painel;
    }

    // =========================== Mapa da cotação ===========================

    private sealed record OpcaoVencedor(int Id, string Nome);

    /// <summary>
    /// Mapa de preços: um produto por linha e, por participante, o preço unitário e a marca/observação. O menor preço fica
    /// em verde e vence por padrão; "Escolha" troca o vencedor (com o motivo, ex.: marca pedida pelo cliente).
    /// </summary>
    public static async Task<bool> MapaAsync(IWin32Window dono, CotacaoService servico, int cotacaoId)
    {
        var mapa = await servico.ObterMapaAsync(cotacaoId);
        if (mapa.Participantes.Count == 0 || mapa.Itens.Count == 0)
        {
            Mensagens.Aviso(dono, "Inclua os produtos e os participantes da cotação (Editar) antes de informar os preços.");
            return false;
        }
        var somenteLeitura = mapa.Status != StatusCotacao.Aberta;

        var grade = new DataGridView();
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = somenteLeitura;
        grade.AllowUserToAddRows = false;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grade.ScrollBars = ScrollBars.Both;
        grade.DataError += (_, ev) => ev.ThrowException = false;

        DataGridViewTextBoxColumn Coluna(string titulo, int largura, bool somenteLeituraColuna = true, bool direita = false) => new()
        {
            HeaderText = titulo, Width = largura, ReadOnly = somenteLeituraColuna, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Alignment = direita ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft }
        };
        var produto = Coluna("Produto", 170);
        produto.Frozen = true;
        grade.Columns.Add(produto);
        grade.Columns.Add(Coluna("Qtd.", 95, direita: true));
        grade.Columns.Add(Coluna("Especificação", 110));

        // Duas colunas por participante: preço unitário e marca/observação.
        var colunasPreco = new Dictionary<int, int>();
        var colunasObs = new Dictionary<int, int>();
        foreach (var participante in mapa.Participantes)
        {
            var preco = Coluna($"{participante.Nome}{(participante.FornecedorId is null ? " *" : "")}\nPreço unit.", 95, somenteLeitura, direita: true);
            preco.ValueType = typeof(decimal);
            preco.DefaultCellStyle.Format = "#,##0.00##";
            preco.DefaultCellStyle.FormatProvider = Textos.PtBr;
            preco.DefaultCellStyle.NullValue = "";
            colunasPreco[participante.Id] = grade.Columns.Add(preco);
            var obs = Coluna("Marca / obs.", 95, somenteLeitura);
            obs.MaxInputLength = CotacaoPreco.TamanhoObservacao;
            obs.DefaultCellStyle.ForeColor = Tema.TextoSuave;
            colunasObs[participante.Id] = grade.Columns.Add(obs);
        }

        var opcoes = new List<OpcaoVencedor> { new(0, "Menor preço") };
        opcoes.AddRange(mapa.Participantes.Select(p => new OpcaoVencedor(p.Id, p.Nome)));
        var colunaEscolha = new DataGridViewComboBoxColumn
        {
            HeaderText = "Escolha", Width = 130, DataSource = opcoes, ValueMember = nameof(OpcaoVencedor.Id),
            DisplayMember = nameof(OpcaoVencedor.Nome), ValueType = typeof(int), ReadOnly = somenteLeitura,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        grade.Columns.Add(colunaEscolha);
        var colunaMotivo = Coluna("Motivo da escolha", 150, somenteLeitura);
        colunaMotivo.MaxInputLength = CotacaoItem.TamanhoMotivo;
        grade.Columns.Add(colunaMotivo);
        var colunaVencedor = Coluna("Vencedor", 130);
        colunaVencedor.DefaultCellStyle.Font = Tema.FonteNegrito;
        grade.Columns.Add(colunaVencedor);
        var colunaTotal = Coluna("Total", 90, direita: true);
        grade.Columns.Add(colunaTotal);
        grade.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grade.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

        foreach (var item in mapa.Itens)
        {
            var linha = grade.Rows[grade.Rows.Add()];
            linha.Tag = item;
            linha.Cells[0].Value = item.Produto;
            linha.Cells[1].Value = $"{item.Quantidade:N3} {item.Unidade}";
            linha.Cells[2].Value = item.Especificacao;
            foreach (var participante in mapa.Participantes)
            {
                var preco = mapa.Preco(item.ItemId, participante.Id);
                linha.Cells[colunasPreco[participante.Id]].Value = preco?.PrecoUnitario;
                linha.Cells[colunasObs[participante.Id]].Value = preco?.Observacao;
            }
            linha.Cells[colunaEscolha.Index].Value = item.Escolhido ?? 0;
            linha.Cells[colunaMotivo.Index].Value = item.Motivo;
        }

        var resumo = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 8, 0, 0), Dock = DockStyle.Bottom };
        var legenda = new Label
        {
            AutoSize = true, ForeColor = Tema.TextoSuave, Dock = DockStyle.Bottom, Margin = new Padding(0, 4, 0, 0),
            Text = "Verde = menor preço do produto (vence por padrão).  * = participante ainda não cadastrado como fornecedor (será cadastrado ao gerar o pedido)."
        };

        // Lê a grade para o mapa e recalcula vencedores, cores e totais.
        void Ler()
        {
            mapa.Precos.Clear();
            foreach (DataGridViewRow linha in grade.Rows)
            {
                var item = (MapaCotacaoItem)linha.Tag!;
                foreach (var participante in mapa.Participantes)
                {
                    var valor = linha.Cells[colunasPreco[participante.Id]].Value as decimal? ?? 0;
                    var obs = linha.Cells[colunasObs[participante.Id]].Value as string;
                    if (valor > 0 || !string.IsNullOrWhiteSpace(obs))
                        mapa.Precos.Add(new MapaCotacaoPreco { ItemId = item.ItemId, ParticipanteId = participante.Id, PrecoUnitario = valor, Observacao = obs });
                }
                var escolhido = linha.Cells[colunaEscolha.Index].Value as int? ?? 0;
                item.Escolhido = escolhido > 0 ? escolhido : null;
                item.Motivo = linha.Cells[colunaMotivo.Index].Value as string;
            }
        }
        var recalculando = false;
        void Recalcular()
        {
            if (recalculando)
                return;
            recalculando = true;
            try
            {
                Ler();
                Pintar();
            }
            finally
            {
                recalculando = false;
            }
        }
        void Pintar()
        {
            foreach (DataGridViewRow linha in grade.Rows)
            {
                var item = (MapaCotacaoItem)linha.Tag!;
                var menor = mapa.MenorPreco(item.ItemId);
                var vencedor = mapa.Vencedor(item);
                foreach (var participante in mapa.Participantes)
                {
                    var celula = linha.Cells[colunasPreco[participante.Id]];
                    celula.Style.BackColor = menor?.ParticipanteId == participante.Id ? Color.FromArgb(220, 240, 222) : Color.Empty;
                    celula.Style.Font = vencedor == participante.Id ? Tema.FonteNegrito : null;
                }
                var semPreco = item.Escolhido is { } escolhido && mapa.Preco(item.ItemId, escolhido) is not { PrecoUnitario: > 0 };
                var nome = mapa.Participantes.FirstOrDefault(p => p.Id == vencedor)?.Nome;
                linha.Cells[colunaVencedor.Index].Value = semPreco ? "⚠ escolhido sem preço" : nome ?? "(sem preço)";
                linha.Cells[colunaVencedor.Index].Style.ForeColor = semPreco ? Tema.Perigo : item.Escolhido is null ? Tema.Sucesso : Tema.Alerta;
                var total = vencedor is { } v ? Math.Round(item.Quantidade * (mapa.Preco(item.ItemId, v)?.PrecoUnitario ?? 0), 2) : 0;
                linha.Cells[colunaTotal.Index].Value = total > 0 ? Textos.Moeda(total) : "";
            }

            var pelosVencedores = mapa.Itens.Sum(i => mapa.Vencedor(i) is { } v ? Math.Round(i.Quantidade * (mapa.Preco(i.ItemId, v)?.PrecoUnitario ?? 0), 2) : 0);
            var peloMenor = mapa.Itens.Sum(i => Math.Round(i.Quantidade * (mapa.MenorPreco(i.ItemId)?.PrecoUnitario ?? 0), 2));
            var porParticipante = mapa.Participantes.Select(p =>
            {
                var cotados = mapa.Itens.Where(i => mapa.Preco(i.ItemId, p.Id) is { PrecoUnitario: > 0 }).ToList();
                var ganhos = mapa.Itens.Count(i => mapa.Vencedor(i) == p.Id);
                return $"{p.Nome}: {cotados.Count}/{mapa.Itens.Count} cotados, vence {ganhos}";
            });
            var diferenca = pelosVencedores - peloMenor;
            resumo.Text = $"Total pelos vencedores: {Textos.Moeda(pelosVencedores)}" +
                          (diferenca > 0 ? $"  (+{Textos.Moeda(diferenca)} sobre o menor preço em tudo, pelas escolhas)" : "") +
                          $"\n{string.Join("  •  ", porParticipante)}";
        }

        grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grade.IsCurrentCellDirty && grade.CurrentCell is DataGridViewComboBoxCell)
                grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grade.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex >= 0 && grade.Rows[ev.RowIndex].Tag is not null)
                Recalcular();
        };

        var e = new EditorCampos(1);
        var menorEmTudo = Tema.BotaoSecundario("Menor preço em tudo", Icones.Calculadora);
        menorEmTudo.Enabled = !somenteLeitura;
        menorEmTudo.Click += (_, _) =>
        {
            grade.EndEdit();
            foreach (DataGridViewRow linha in grade.Rows)
            {
                linha.Cells[colunaEscolha.Index].Value = 0;
                linha.Cells[colunaMotivo.Index].Value = null;
            }
            Recalcular();
        };
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        barra.Controls.Add(menorEmTudo);
        var painel = new Panel { Height = 460, Dock = DockStyle.Top };
        grade.Dock = DockStyle.Fill;
        painel.Controls.Add(grade);
        painel.Controls.Add(barra);
        painel.Controls.Add(legenda);
        painel.Controls.Add(resumo);
        e.Livre(painel);
        e.AoCarregar(Recalcular);
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            Ler();
        });

        using var dialogo = new FormEdicao($"Mapa da cotação {mapa.Codigo}", $"{mapa.Descricao}. Preços por unidade do estoque; em branco = não cotou.",
            e, () => servico.SalvarMapaAsync(mapa), 1300, somenteLeitura);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }
}
