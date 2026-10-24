using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>
/// Campos da condição de pagamento. O jeito principal é a grade de parcelas (prazo e percentual de cada uma, com o
/// exemplo calculado ao lado e um preenchimento rápido); a regra antiga (entrada + parcelas iguais) fica como alternativa.
/// </summary>
internal static class EditorCondicaoPagamento
{
    private const decimal ValorExemplo = 1_000m;

    public static void Montar(EditorCampos e, CondicaoPagamento x, IReadOnlyList<ItemLookup> formas)
    {
        e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 80, span: 2, dica: "Ex.: 28/35/42 dias ou 30% de entrada + 5x");
        e.Texto("Texto na proposta e no contrato (opcional)", () => x.Descricao, v => x.Descricao = v, nameof(x.Descricao), 300,
            span: 2, linhas: 2, dica: "Vazio: usa o resumo calculado pelas parcelas");

        e.Secao("Parcelas");
        var porParcela = new RadioButton { Text = "Parcela a parcela (prazo e percentual de cada uma)", AutoSize = true, Margin = new Padding(0, 4, 24, 4) };
        var porRegra = new RadioButton { Text = "Pela regra (entrada + parcelas iguais)", AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        var modo = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        modo.Controls.AddRange([porParcela, porRegra]);
        e.Livre(modo);

        // ---------------- Parcela a parcela ----------------
        GradeEditavel<LinhaParcelaCondicao> grade = null!;
        grade = new GradeEditavel<LinhaParcelaCondicao>(() => Nova(grade.Itens), "Nova parcela", "Remover parcela", 190)
            .Numero("Nº", nameof(LinhaParcelaCondicao.Numero), "N0", 8, somenteLeitura: true)
            .Numero("Prazo (dias)", nameof(LinhaParcelaCondicao.Dias), "N0", 20)
            .Numero("Percentual (%)", nameof(LinhaParcelaCondicao.Percentual), "N2", 18)
            .Opcoes("Forma", nameof(LinhaParcelaCondicao.FormaPagamentoId),
                new[] { new OpcaoGrade<int>(0, "(forma geral)") }.Concat(formas.Select(f => new OpcaoGrade<int>(f.Id, f.Nome))), 26)
            .Texto("Vence em *", nameof(LinhaParcelaCondicao.VenceEm), 20, somenteLeitura: true)
            .Numero($"Valor em {Textos.Moeda(ValorExemplo)} *", nameof(LinhaParcelaCondicao.ValorExemplo), "C2", 22, somenteLeitura: true);
        foreach (var coluna in grade.Controle.Columns.Cast<DataGridViewColumn>().Where(c => c.ReadOnly))
            coluna.DefaultCellStyle.ForeColor = Tema.TextoSuave;

        NumericUpDown Campo(decimal valor, decimal minimo, decimal maximo, int casas = 0) => new()
        {
            Minimum = minimo, Maximum = maximo, Value = valor, DecimalPlaces = casas, Width = casas > 0 ? 64 : 52,
            TextAlign = HorizontalAlignment.Right, Margin = new Padding(0, 4, 4, 4)
        };
        Label Rotulo(string texto) => new() { Text = texto, AutoSize = true, Margin = new Padding(0, 8, 4, 0) };
        var quantidade = Campo(3, 1, 48);
        var primeira = Campo(30, 0, 730);
        var intervalo = Campo(30, 1, 365);
        var entrada = Campo(0, 0, 99, 2);
        var preencher = Tema.BotaoSecundario("Preencher a grade", Icones.Calculadora);
        var rapido = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        rapido.Controls.AddRange([Rotulo("Preencher:"), quantidade, Rotulo("parcela(s), a 1ª em"), primeira, Rotulo("dias e as demais a cada"),
            intervalo, Rotulo("dias; entrada à vista de"), entrada, Rotulo("%"), preencher]);

        var total = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 4, 0, 0) };
        var exemplo = new Label
        {
            AutoSize = true, MaximumSize = new Size(580, 0), ForeColor = Tema.TextoSuave, Margin = new Padding(0, 2, 0, 0),
            Text = $"* Exemplo para {Textos.Moeda(ValorExemplo)} com a data base de hoje (os prazos contam do fechamento do orçamento ou " +
                   "da emissão da nota). Os percentuais somam 100%; os centavos ficam na última parcela. Forma: a da parcela " +
                   "(ex.: entrada no PIX, demais no boleto); \"forma geral\" usa a do pedido ou do evento."
        };
        var painelParcelas = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        painelParcelas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        painelParcelas.Controls.AddRange([rapido, grade.Painel, total, exemplo]);
        e.Livre(painelParcelas);
        e.Associar(nameof(x.Parcelas), grade.Controle);

        // ---------------- Pela regra ----------------
        var regra = new EditorCampos();
        e.Associar(nameof(x.PercentualEntrada),
            regra.Decimal("Entrada (% do valor)", () => x.PercentualEntrada, v => x.PercentualEntrada = v, nameof(x.PercentualEntrada), 2, 0, 100));
        regra.Inteiro("Vencimento da entrada (dias após o fechamento)", () => x.DiasEntrada, v => x.DiasEntrada = v, nameof(x.DiasEntrada), 0, 365);
        e.Associar(nameof(x.QuantidadeParcelas),
            regra.Inteiro("Quantidade de parcelas (0 = à vista)", () => x.QuantidadeParcelas, v => x.QuantidadeParcelas = v,
                nameof(x.QuantidadeParcelas), 0, 48));
        regra.Inteiro("Primeira parcela (dias após o fechamento)", () => x.DiasPrimeiraParcela, v => x.DiasPrimeiraParcela = v,
            nameof(x.DiasPrimeiraParcela), 0, 730);
        regra.Marcador("Parcelas mensais (mesmo dia de cada mês)", () => x.Mensal, v => x.Mensal = v, nameof(x.Mensal));
        regra.Inteiro("Intervalo entre parcelas (dias, se não forem mensais)", () => x.IntervaloDias, v => x.IntervaloDias = v,
            nameof(x.IntervaloDias), 1, 365);
        e.Livre(regra.Painel);
        e.AoCarregar(regra.Carregar);
        e.AoAplicar(regra.Aplicar);

        e.Secao("Outros");
        e.Inteiro("Quitar até quantos dias antes do evento (0 = sem limite)", () => x.QuitarDiasAntesEvento ?? 0,
            v => x.QuitarDiasAntesEvento = v == 0 ? null : v, nameof(x.QuitarDiasAntesEvento), 0, 365, span: 2);
        e.Lista("Forma de pagamento sugerida", formas, () => x.FormaPagamentoId, v => x.FormaPagamentoId = v, nameof(x.FormaPagamentoId),
            permitirVazio: true);
        e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);

        void Atualizar()
        {
            var hoje = DateTime.Today;
            var ordenadas = grade.Itens.OrderBy(l => l.Dias).ToList();
            var acumulado = 0m;
            for (var i = 0; i < ordenadas.Count; i++)
            {
                var linha = ordenadas[i];
                linha.Numero = i + 1;
                linha.VenceEm = linha.Dias == 0 ? "à vista (hoje)" : hoje.AddDays(linha.Dias).ToString("dd/MM/yyyy");
                linha.ValorExemplo = i == ordenadas.Count - 1
                    ? ValorExemplo - acumulado
                    : Math.Round(ValorExemplo * linha.Percentual / 100, 2, MidpointRounding.ToZero);
                acumulado += linha.ValorExemplo;
            }
            var soma = grade.Itens.Sum(l => l.Percentual);
            total.Text = grade.Itens.Count == 0
                ? "Inclua as parcelas ou use \"Preencher a grade\"."
                : soma == 100
                    ? $"Total: 100% em {grade.Itens.Count} parcela(s)  ✓"
                    : $"Total: {soma:N2}%  •  {(soma < 100 ? $"faltam {100 - soma:N2}%" : $"passou {soma - 100:N2}%")}";
            total.ForeColor = soma == 100 ? Tema.Sucesso : Tema.Alerta;
            grade.Controle.Invalidate();
        }
        grade.Alterado += Atualizar;

        preencher.Click += (_, _) =>
        {
            grade.ConcluirEdicao();
            var linhas = new List<LinhaParcelaCondicao>();
            var aVista = entrada.Value;
            if (aVista > 0)
                linhas.Add(new LinhaParcelaCondicao { Dias = 0, Percentual = aVista });
            var n = (int)quantidade.Value;
            var cada = Math.Round((100 - aVista) / n, 2, MidpointRounding.ToZero);
            for (var i = 0; i < n; i++)
            {
                linhas.Add(new LinhaParcelaCondicao
                {
                    Dias = (int)primeira.Value + i * (int)intervalo.Value,
                    Percentual = i == n - 1 ? 100 - aVista - cada * (n - 1) : cada
                });
            }
            Validador.Garantir(linhas.Select(l => l.Dias).Distinct().Count() == linhas.Count,
                "Com entrada à vista, a primeira parcela precisa vencer depois de 0 dias.");
            grade.Carregar(linhas);
            Atualizar();
        };

        void AplicarModo()
        {
            painelParcelas.Visible = porParcela.Checked;
            regra.Painel.Visible = porRegra.Checked;
        }
        porParcela.CheckedChanged += (_, _) => AplicarModo();
        porParcela.Checked = x.ParcelasDefinidas || x.EhNovo;
        porRegra.Checked = !porParcela.Checked;
        AplicarModo();

        e.AoCarregar(() =>
        {
            grade.Carregar(x.Parcelas.OrderBy(p => p.Dias).Select(LinhaParcelaCondicao.De));
            Atualizar();
        });
        e.AoAplicar(() =>
        {
            grade.ConcluirEdicao();
            if (porRegra.Checked)
            {
                x.Parcelas = [];
                return;
            }
            Validador.Garantir(grade.Itens.Count > 0, "Inclua ao menos uma parcela (ou escolha \"Pela regra\").");
            x.Parcelas = grade.Itens.Select(l => l.ParaParcela()).ToList();
        });
    }

    /// <summary>Nova linha: 30 dias depois da última e o percentual que falta para 100%.</summary>
    private static LinhaParcelaCondicao Nova(IReadOnlyCollection<LinhaParcelaCondicao> atuais) => new()
    {
        Dias = atuais.Count == 0 ? 30 : atuais.Max(l => l.Dias) + 30,
        Percentual = Math.Max(0, 100 - atuais.Sum(l => l.Percentual))
    };
}
