using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Parcelamento de contas a pagar, como o plano de pagamento do evento: compra parcelada (valor total em entrada +
/// parcelas) ou contas em aberto do mesmo fornecedor juntadas e reparceladas. Mostra as parcelas enquanto se edita.
/// </summary>
internal static class DialogoParcelamento
{
    public sealed class LinhaConta
    {
        public int Id { get; init; }
        public bool Incluir { get; set; }
        public DateTime Vencimento { get; init; }
        public string Descricao { get; init; } = string.Empty;
        public string Categoria { get; init; } = string.Empty;
        public string? Referencia { get; init; }
        public string? Documento { get; init; }
        public decimal Valor { get; init; }
        public int? EventoId { get; init; }
    }

    /// <param name="juntar">Conta a partir da qual juntar as do mesmo fornecedor; nula = compra parcelada.</param>
    /// <returns>Verdadeiro se o parcelamento foi gerado.</returns>
    public static async Task<bool> ExibirAsync(IWin32Window dono, FinanceiroService servico, IConsultasContas consultas,
        IConsultasLookup lookups, ContaPagarResumo? juntar = null)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento);
        var condicoes = await lookups.ListarAsync(TipoLookup.CondicoesPagamento);
        var input = new ParcelamentoInput
        {
            Plano = new PlanoPagamentoInput { Quantidade = 3, PrimeiroVencimento = hoje.AddDays(30), VencimentoEntrada = hoje }
        };

        GradeEditavel<LinhaConta>? grade = null;
        if (juntar is not null)
        {
            // A fatura do cartão não é reparcelada: ela se desfaz pelo estorno do pagamento feito com o cartão.
            var abertas = (await consultas.TitulosEmAbertoAsync(TipoBaixa.Pagamento, juntar.FornecedorId))
                .Where(t => t.Categoria != CategoriaDespesa.FaturaCartao).ToList();
            if (abertas.Count == 0)
            {
                Mensagens.Info(dono, "Não há contas em aberto deste fornecedor para parcelar.");
                return false;
            }
            grade = new GradeEditavel<LinhaConta>(() => new LinhaConta(), "", "", altura: 170)
                .Marcador("Juntar", nameof(LinhaConta.Incluir), 14)
                .Texto("Vencimento", nameof(LinhaConta.Vencimento), 22, somenteLeitura: true)
                .Texto("Descrição", nameof(LinhaConta.Descricao), 55, somenteLeitura: true)
                .Texto("Categoria", nameof(LinhaConta.Categoria), 24, somenteLeitura: true)
                .Texto("Evento vinculado", nameof(LinhaConta.Referencia), 40, somenteLeitura: true)
                .Texto("Documento", nameof(LinhaConta.Documento), 22, somenteLeitura: true)
                .Numero("Valor", nameof(LinhaConta.Valor), "N2", 22, somenteLeitura: true);
            grade.Incluir.Visible = false;
            grade.Remover.Visible = false;
            grade.Controle.Columns[1].DefaultCellStyle.Format = "dd/MM/yyyy";
            grade.Carregar(abertas.Select(t => new LinhaConta
            {
                Id = t.Id,
                Incluir = t.Id == juntar.Id,
                Vencimento = t.Vencimento,
                Descricao = t.Descricao,
                Categoria = t.Categoria is { } c ? Textos.Exibir(c) : "",
                Referencia = t.Referencia,
                Documento = t.Documento,
                Valor = t.Valor,
                EventoId = t.EventoId
            }));
            input.Descricao = $"Parcelamento - {juntar.Fornecedor ?? "contas sem fornecedor"}";
        }

        List<LinhaConta> Marcadas() => grade?.Itens.Where(l => l.Incluir).ToList() ?? [];

        // ---------------- Campos ----------------
        var e = new EditorCampos(3);
        NumericUpDown? valorTotal = null;
        e.Texto("Descrição", () => input.Descricao, v => input.Descricao = v ?? "", nameof(input.Descricao),
            ContaPagar.TamanhoDescricao - 10, span: 2, dica: juntar is null ? "Ex.: Compra de freezer" : null);
        e.Texto("Documento (NF, boleto)", () => input.Documento, v => input.Documento = v, nameof(input.Documento), ContaPagar.TamanhoDocumento);
        if (juntar is null)
        {
            var fornecedores = await lookups.ListarAsync(TipoLookup.Fornecedores);
            var eventos = await lookups.ListarAsync(TipoLookup.EventosConfirmados);
            e.Opcoes("Categoria", () => input.Categoria, v => input.Categoria = v);
            e.Lista("Fornecedor", fornecedores, () => input.FornecedorId, v => input.FornecedorId = v, permitirVazio: true, span: 2);
            e.Lista("Vincular ao evento (custo extra do evento)", eventos, () => input.EventoId, v => input.EventoId = v,
                permitirVazio: true, span: 2);
            valorTotal = e.Decimal("Valor total (R$)", () => input.ValorTotal, v => input.ValorTotal = v, nameof(input.ValorTotal));
        }
        else
        {
            e.Informacao("Fornecedor", () => juntar.Fornecedor ?? "(contas sem fornecedor)", span: 3);
            e.Secao("Contas em aberto (marque as que serão juntadas)");
            e.Livre(grade!.Painel);
        }

        var plano = input.Plano;
        e.Secao("Parcelas");
        var condicao = e.Lista("Condição de pagamento (preenche o plano)", condicoes, () => plano.CondicaoPagamentoId,
            v => plano.CondicaoPagamentoId = v, permitirVazio: true, span: 2);
        var forma = e.Lista("Forma de pagamento", formas, () => plano.FormaPagamentoId, v => plano.FormaPagamentoId = v, permitirVazio: true);
        var entrada = e.Decimal("Entrada (R$)", () => plano.ValorEntrada, v => plano.ValorEntrada = v);
        var vencimentoEntrada = e.Data("Vencimento da entrada", () => plano.VencimentoEntrada, v => plano.VencimentoEntrada = v);
        var quantidade = e.Inteiro("Parcelas (após a entrada)", () => plano.Quantidade, v => plano.Quantidade = v, minimo: 1,
            maximo: PlanoParcelas.MaximoParcelas);
        var primeiro = e.Data("Primeiro vencimento", () => plano.PrimeiroVencimento, v => plano.PrimeiroVencimento = v ?? hoje);
        var intervalo = e.Inteiro("Intervalo (dias; 0 = mensal)", () => plano.IntervaloDias ?? 0, v => plano.IntervaloDias = v,
            minimo: 0, maximo: 365);
        var previa = new Label { AutoSize = true, ForeColor = Tema.Texto, Margin = new Padding(4, 8, 4, 4) };
        e.Livre(previa);
        e.Texto("Observação", () => input.Observacao, v => input.Observacao = v, maximo: ParcelamentoPagar.TamanhoObservacao, span: 3);

        decimal Total() => valorTotal?.Value ?? Marcadas().Sum(l => l.Valor);

        // Condição com as parcelas definidas (prazo e % de cada uma): o plano vem pronto e os campos ficam travados.
        void Travar()
        {
            foreach (var campo in new Control[] { entrada, vencimentoEntrada, quantidade, primeiro, intervalo })
                campo.Enabled = plano.Vencimentos is not { Count: > 0 };
        }

        void AtualizarPrevia()
        {
            var total = Total();
            if (plano.Vencimentos is { Count: > 0 } prontos)
            {
                previa.Text = $"Total: {Textos.Moeda(total)}  •  parcelas definidas na condição" + Environment.NewLine +
                              string.Join(Environment.NewLine, prontos.Select((v, i) => $"   {i + 1}/{prontos.Count,-6} {v.Vencimento:dd/MM/yyyy}   {Textos.Moeda(v.Valor)}" +
                                  (formas.FirstOrDefault(f => f.Id == v.FormaPagamentoId)?.Nome is { } nome ? $"   {nome}" : "")));
                previa.ForeColor = prontos.Sum(v => v.Valor) == total ? Tema.Texto : Tema.Alerta;
                return;
            }
            try
            {
                var parcelas = PlanoParcelas.Calcular(total, (int)quantidade.Value, DateOnly.FromDateTime(primeiro.Value), entrada.Value,
                    DateOnly.FromDateTime(vencimentoEntrada.Value), intervalo.Value > 0 ? (int)intervalo.Value : null);
                previa.Text = $"Total: {Textos.Moeda(total)}" + Environment.NewLine + string.Join(Environment.NewLine, parcelas.Select(p =>
                    $"   {(p.Entrada ? "Entrada" : $"{p.Numero}/{p.Quantidade}"),-8} {p.Vencimento:dd/MM/yyyy}   {Textos.Moeda(p.Valor)}"));
                previa.ForeColor = Tema.Texto;
            }
            catch (RegraNegocioException ex)
            {
                previa.Text = total > 0 ? ex.Message : juntar is null ? "Informe o valor total." : "Marque as contas que serão juntadas.";
                previa.ForeColor = Tema.Alerta;
            }
        }

        foreach (var numero in new[] { entrada, quantidade, intervalo })
            numero.ValueChanged += (_, _) => AtualizarPrevia();
        foreach (var data in new[] { vencimentoEntrada, primeiro })
            data.ValueChanged += (_, _) => AtualizarPrevia();
        // O total mudou: as parcelas definidas da condição são recalculadas para o novo valor.
        async void Recalcular()
        {
            if (plano.Vencimentos is { Count: > 0 } && condicao.SelectedItem is ItemLookup { Id: > 0 } escolhida && Total() > 0)
            {
                try
                {
                    var evento = juntar is null ? input.EventoId : Marcadas().FirstOrDefault()?.EventoId;
                    plano.Vencimentos = (await servico.SugerirPlanoAsync(escolhida.Id, Total(), evento)).Vencimentos;
                }
                catch (Exception ex)
                {
                    Mensagens.Erro(dono, ex);
                }
            }
            AtualizarPrevia();
        }
        if (valorTotal is not null)
            valorTotal.ValueChanged += (_, _) => Recalcular();
        if (grade is not null)
            grade.Alterado += Recalcular;
        e.AoCarregar(AtualizarPrevia);

        // Escolher a condição recalcula entrada, parcelas e vencimentos (os campos continuam editáveis).
        condicao.SelectionChangeCommitted += async (_, _) =>
        {
            if (condicao.SelectedItem is not ItemLookup { Id: > 0 } item)
            {
                plano.Vencimentos = null;
                Travar();
                AtualizarPrevia();
                return;
            }
            try
            {
                var evento = juntar is null ? input.EventoId : Marcadas().FirstOrDefault()?.EventoId;
                var sugerido = await servico.SugerirPlanoAsync(item.Id, Total(), evento);
                plano.Vencimentos = sugerido.Vencimentos;
                Travar();
                entrada.Value = Math.Clamp(sugerido.ValorEntrada, entrada.Minimum, entrada.Maximum);
                vencimentoEntrada.Value = (sugerido.VencimentoEntrada ?? sugerido.PrimeiroVencimento).ToDateTime(TimeOnly.MinValue);
                quantidade.Value = Math.Clamp(sugerido.Quantidade, 1, PlanoParcelas.MaximoParcelas);
                primeiro.Value = sugerido.PrimeiroVencimento.ToDateTime(TimeOnly.MinValue);
                intervalo.Value = sugerido.IntervaloDias ?? 0;
                if (sugerido.FormaPagamentoId is { } formaId && formas.Any(f => f.Id == formaId))
                    forma.SelectedIndex = formas.Select((f, i) => (f, i)).First(x => x.f.Id == formaId).i + 1;
                AtualizarPrevia();
            }
            catch (Exception ex)
            {
                Mensagens.Erro(dono, ex);
            }
        };

        using var dialogo = new FormEdicao(juntar is null ? "Compra parcelada" : "Juntar e parcelar contas",
            juntar is null
                ? "O valor total é dividido em entrada + parcelas (os centavos ficam na última). Escolha uma condição para preencher o plano ou ajuste à mão."
                : "As contas marcadas ficam canceladas e são substituídas pelas parcelas. Enquanto nenhuma parcela for paga, dá para desfazer.",
            e, async () =>
            {
                if (grade is not null)
                {
                    grade.ConcluirEdicao();
                    input.ContasAgrupadasIds = Marcadas().Select(l => l.Id).ToList();
                    Validador.Garantir(input.ContasAgrupadasIds.Count > 0, "Marque ao menos uma conta.");
                }
                await servico.ParcelarAsync(input);
            }, 980);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }
}
