using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Recebimento ou pagamento: marca as contas em aberto da pessoa (uma ou várias, no todo ou em parte, com juros ou
/// desconto) e informa como foi pago (uma ou várias formas, cada uma com a sua conta ou cartão).
/// </summary>
internal static class DialogoBaixa
{
    public sealed class LinhaTitulo
    {
        public int Id { get; init; }
        public bool Incluir { get; set; }
        public DateTime Vencimento { get; init; }
        public string Descricao { get; init; } = string.Empty;
        public string? Referencia { get; init; }
        public decimal Valor { get; init; }
        public decimal ABaixar { get; set; }
        public decimal Juros { get; set; }
        public decimal Desconto { get; set; }
        public decimal Total => Incluir ? ABaixar + Juros - Desconto : 0;
    }

    public sealed class LinhaForma
    {
        public int FormaId { get; set; }
        public decimal Valor { get; set; }
        public int ContaId { get; set; }
        public int CartaoId { get; set; }
        public int Parcelas { get; set; } = 1;
    }

    /// <param name="pessoaId">Cliente (recebimento) ou fornecedor (pagamento; nulo = contas sem fornecedor).</param>
    /// <param name="marcados">Contas que já abrem marcadas.</param>
    /// <returns>Verdadeiro se a baixa foi registrada.</returns>
    public static async Task<bool> ExibirAsync(IWin32Window dono, IConsultasContas consultas, IConsultasLookup lookups,
        BaixaService baixas, TipoBaixa tipo, int? pessoaId, string pessoa, IReadOnlyCollection<int> marcados)
    {
        var recebimento = tipo == TipoBaixa.Recebimento;
        var titulos = await consultas.TitulosEmAbertoAsync(tipo, pessoaId);
        if (titulos.Count == 0)
        {
            Mensagens.Info(dono, $"{pessoa} não tem contas em aberto.");
            return false;
        }

        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento);
        var contas = await lookups.ListarAsync(TipoLookup.ContasFinanceiras);
        var cartoes = await lookups.ListarAsync(TipoLookup.CartoesCredito);
        TipoFormaPagamento TipoDa(int formaId) =>
            Enum.TryParse<TipoFormaPagamento>(formas.FirstOrDefault(f => f.Id == formaId)?.Info, out var t) ? t : TipoFormaPagamento.Outros;
        bool UsaCartao(int formaId) => !recebimento && TipoDa(formaId) == TipoFormaPagamento.CartaoCredito;

        var data = DateOnly.FromDateTime(DateTime.Today);
        string? observacao = null;

        // ---------------- Contas em aberto ----------------
        var gradeTitulos = new GradeEditavel<LinhaTitulo>(() => new LinhaTitulo(), "", "", altura: 190)
            .Marcador("Pagar", nameof(LinhaTitulo.Incluir), 14)
            .Texto("Vencimento", nameof(LinhaTitulo.Vencimento), 22, somenteLeitura: true)
            .Texto("Descrição", nameof(LinhaTitulo.Descricao), 55, somenteLeitura: true)
            .Texto(recebimento ? "Evento" : "Evento vinculado", nameof(LinhaTitulo.Referencia), 45, somenteLeitura: true)
            .Numero("Valor", nameof(LinhaTitulo.Valor), "N2", 22, somenteLeitura: true)
            .Numero(recebimento ? "A receber" : "A pagar", nameof(LinhaTitulo.ABaixar), "N2", 22)
            .Numero("Juros/multa", nameof(LinhaTitulo.Juros), "N2", 20)
            .Numero("Desconto", nameof(LinhaTitulo.Desconto), "N2", 20)
            .Numero("Total", nameof(LinhaTitulo.Total), "N2", 22, somenteLeitura: true);
        gradeTitulos.Incluir.Visible = false;
        gradeTitulos.Remover.Visible = false;
        gradeTitulos.Controle.Columns[1].DefaultCellStyle.Format = "dd/MM/yyyy";
        gradeTitulos.Carregar(titulos.Select(t => new LinhaTitulo
        {
            Id = t.Id,
            Incluir = marcados.Contains(t.Id),
            Vencimento = t.Vencimento,
            Descricao = t.Descricao,
            Referencia = t.Referencia,
            Valor = t.Valor,
            ABaixar = t.Valor
        }));

        // ---------------- Formas de pagamento ----------------
        decimal TotalTitulos() => gradeTitulos.Itens.Sum(t => t.Total);
        decimal Restante(LinhaForma? ignorar = null) => TotalTitulos() - gradeFormasItens().Where(f => f != ignorar).Sum(f => f.Valor);
        GradeEditavel<LinhaForma>? gradeFormas = null;
        IEnumerable<LinhaForma> gradeFormasItens() => gradeFormas?.Itens ?? Enumerable.Empty<LinhaForma>();

        var opcoesContas = new List<OpcaoGrade<int>> { new(0, "(nenhuma)") };
        opcoesContas.AddRange(contas.Select(c => new OpcaoGrade<int>(c.Id, c.Nome)));
        var opcoesCartoes = new List<OpcaoGrade<int>> { new(0, "(nenhum)") };
        opcoesCartoes.AddRange(cartoes.Select(c => new OpcaoGrade<int>(c.Id, c.Nome)));

        gradeFormas = new GradeEditavel<LinhaForma>(() => new LinhaForma { Valor = Math.Max(0, Restante()) },
                "Incluir forma", "Remover forma", altura: 130)
            .Opcoes("Forma de pagamento", nameof(LinhaForma.FormaId),
                new[] { new OpcaoGrade<int>(0, "(escolha)") }.Concat(formas.Select(f => new OpcaoGrade<int>(f.Id, f.Nome))), 40)
            .Numero("Valor", nameof(LinhaForma.Valor), "N2", 22)
            .Opcoes(recebimento ? "Entra na conta" : "Sai da conta", nameof(LinhaForma.ContaId), opcoesContas, 40);
        if (!recebimento)
        {
            gradeFormas.Opcoes("Cartão de crédito", nameof(LinhaForma.CartaoId), opcoesCartoes, 36)
                .Numero("Parcelas", nameof(LinhaForma.Parcelas), "N0", 14);
        }

        // Ao escolher a forma, sugere a conta (caixa para dinheiro, banco para as demais) ou o cartão.
        gradeFormas.Controle.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || gradeFormas.Controle.Columns[e.ColumnIndex].DataPropertyName != nameof(LinhaForma.FormaId))
                return;
            var linha = gradeFormas.Itens[e.RowIndex];
            var tipoForma = TipoDa(linha.FormaId);
            if (UsaCartao(linha.FormaId))
            {
                linha.ContaId = 0;
                if (linha.CartaoId == 0)
                    linha.CartaoId = cartoes.FirstOrDefault()?.Id ?? 0;
            }
            else
            {
                linha.CartaoId = 0;
                linha.Parcelas = 1;
                var caixa = tipoForma == TipoFormaPagamento.Dinheiro;
                if (tipoForma == TipoFormaPagamento.Outros)
                    linha.ContaId = 0;
                else if (contas.FirstOrDefault(c => c.Id == linha.ContaId) is not { } atual || atual.Marcador != caixa)
                    linha.ContaId = contas.FirstOrDefault(c => c.Marcador == caixa)?.Id ?? 0;
            }
            gradeFormas.Controle.BeginInvoke(() => gradeFormas.Controle.Refresh());
        };

        var resumo = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(4, 8, 4, 4) };
        void AtualizarResumo()
        {
            var total = TotalTitulos();
            var pago = gradeFormasItens().Sum(f => f.Valor);
            var diferenca = total - pago;
            resumo.Text = $"Total {(recebimento ? "a receber" : "a pagar")}: {Textos.Moeda(total)}     •     Formas: {Textos.Moeda(pago)}" +
                          (diferenca == 0 ? "     ✓" : $"     •     Falta distribuir: {Textos.Moeda(diferenca)}");
            resumo.ForeColor = diferenca == 0 && total > 0 ? Tema.Sucesso : Tema.Alerta;
        }

        gradeTitulos.Controle.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0)
                return;
            var linha = gradeTitulos.Itens[e.RowIndex];
            var coluna = gradeTitulos.Controle.Columns[e.ColumnIndex].DataPropertyName;
            if (coluna == nameof(LinhaTitulo.Incluir) && linha.Incluir && linha.ABaixar <= 0)
                linha.ABaixar = linha.Valor;
            if (coluna != nameof(LinhaTitulo.Incluir) && !linha.Incluir)
                linha.Incluir = true;

            // Com uma só forma, ela acompanha o total (o caso mais comum).
            if (gradeFormas.Itens.Count == 1)
                gradeFormas.Itens[0].Valor = TotalTitulos();
            gradeTitulos.Controle.BeginInvoke(() =>
            {
                gradeTitulos.Controle.Refresh();
                gradeFormas.Controle.Refresh();
            });
        };
        gradeTitulos.Alterado += AtualizarResumo;
        gradeFormas.Alterado += AtualizarResumo;

        // Já abre com uma forma: a sugerida no título (quando houver) ou a primeira.
        var primeira = new LinhaForma { FormaId = formas.FirstOrDefault()?.Id ?? 0, Valor = TotalTitulos() };
        gradeFormas.Carregar([primeira]);
        if (primeira.FormaId != 0)
        {
            var tipoForma = TipoDa(primeira.FormaId);
            if (UsaCartao(primeira.FormaId))
                primeira.CartaoId = cartoes.FirstOrDefault()?.Id ?? 0;
            else if (tipoForma != TipoFormaPagamento.Outros)
                primeira.ContaId = contas.FirstOrDefault(c => c.Marcador == (tipoForma == TipoFormaPagamento.Dinheiro))?.Id ?? 0;
        }

        // ---------------- Montagem ----------------
        var e = new EditorCampos(3);
        e.Informacao(recebimento ? "Cliente" : "Fornecedor", () => pessoa, span: 2);
        e.Data(recebimento ? "Data do recebimento" : "Data do pagamento", () => data, v => data = v ?? data);
        e.Secao(recebimento ? "Parcelas em aberto (marque as que estão sendo recebidas)" : "Contas em aberto (marque as que estão sendo pagas)");
        e.Livre(gradeTitulos.Painel);
        e.Secao("Como foi pago");
        e.Livre(gradeFormas.Painel);
        e.Livre(resumo);
        e.Texto("Observação", () => observacao, v => observacao = v, maximo: 500, span: 3);
        e.AoCarregar(AtualizarResumo);

        using var dialogo = new FormEdicao(recebimento ? "Registrar recebimento" : "Registrar pagamento",
            recebimento
                ? "Receba várias parcelas de uma vez, só uma parte de uma parcela ou com várias formas (ex.: PIX + dinheiro)."
                : "Pague várias contas do mesmo fornecedor de uma vez, só uma parte ou com várias formas. No cartão de crédito da empresa a fatura vira conta a pagar.",
            e, async () =>
            {
                gradeTitulos.ConcluirEdicao();
                gradeFormas.ConcluirEdicao();
                var marcadas = gradeTitulos.Itens.Where(t => t.Incluir).ToList();
                Validador.Garantir(marcadas.Count > 0, recebimento ? "Marque ao menos uma parcela." : "Marque ao menos uma conta.");
                Validador.Garantir(gradeFormas.Itens.All(f => f.FormaId != 0), "Escolha a forma de pagamento de cada linha.");
                var input = new BaixaInput
                {
                    Tipo = tipo,
                    Data = data,
                    Observacao = observacao,
                    Titulos = marcadas.Select(t => new BaixaTituloInput { TituloId = t.Id, Valor = t.ABaixar, Juros = t.Juros, Desconto = t.Desconto }).ToList(),
                    Formas = gradeFormas.Itens.Where(f => f.Valor != 0 || gradeFormas.Itens.Count == 1).Select(f => new BaixaFormaInput
                    {
                        FormaPagamentoId = f.FormaId,
                        Valor = f.Valor,
                        ContaFinanceiraId = f.ContaId == 0 ? null : f.ContaId,
                        CartaoCreditoId = UsaCartao(f.FormaId) && f.CartaoId != 0 ? f.CartaoId : null,
                        Parcelas = UsaCartao(f.FormaId) ? Math.Max(1, f.Parcelas) : 1
                    }).ToList()
                };
                // Desconto acima da alçada de quem recebe: só com a senha de quem pode aprovar.
                await DialogosAlcada.GravarAsync(dono, permitePedido: false, p => input.PedidoDesconto = p, () => baixas.RegistrarAsync(input));
            }, 1080);
        dialogo.AdicionarAcao(Tema.BotaoSecundario("Marcar todas", Icones.Confirmar)).Click += (_, _) =>
        {
            foreach (var linha in gradeTitulos.Itens.Where(l => !l.Incluir))
            {
                linha.Incluir = true;
                linha.ABaixar = linha.Valor;
            }
            if (gradeFormas.Itens.Count == 1)
                gradeFormas.Itens[0].Valor = TotalTitulos();
            gradeTitulos.Controle.Refresh();
            gradeFormas.Controle.Refresh();
            AtualizarResumo();
        };
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>
    /// Estorno pela conta selecionada: mostra o que o pagamento dela quitou (pode incluir outras contas) e desfaz tudo.
    /// </summary>
    public static async Task<bool> EstornarAsync(IWin32Window dono, BaixaService baixas, TipoBaixa tipo, int tituloId, string descricao)
    {
        var baixa = await baixas.ObterDoTituloAsync(tipo, tituloId);
        var texto = baixa is null
            ? $"Estornar o pagamento de \"{descricao}\"? A conta volta a ficar em aberto."
            : $"Estornar o {(tipo == TipoBaixa.Recebimento ? "recebimento" : "pagamento")} de {baixa.Data:dd/MM/yyyy} ({Textos.Moeda(baixa.Total)})?" +
              $"{Environment.NewLine}{Environment.NewLine}Contas que voltam a ficar em aberto:{Environment.NewLine}  • " +
              string.Join($"{Environment.NewLine}  • ", baixa.Titulos) +
              $"{Environment.NewLine}{Environment.NewLine}Formas desfeitas:{Environment.NewLine}  • " +
              string.Join($"{Environment.NewLine}  • ", baixa.Formas) +
              (baixa.FaturasGeradas > 0 ? $"{Environment.NewLine}{Environment.NewLine}As {baixa.FaturasGeradas} parcela(s) da fatura do cartão também serão excluídas." : "");
        if (!Mensagens.Confirmar(dono, texto, "Estornar", perigoso: true))
            return false;

        if (baixa is null)
            await baixas.EstornarTituloAsync(tipo, tituloId);
        else
            await baixas.EstornarAsync(baixa.Id);
        return true;
    }
}
