using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>Diálogos do evento: dados gerais, plano de pagamento, importação de convidados e excedentes.</summary>
internal static class DialogosEvento
{
    /// <summary>Cria (venda direta) ou altera um evento. Retorna o Id salvo ou nulo se cancelado.</summary>
    /// <param name="vendedorPadrao">Vendedor sugerido na venda direta (normalmente o usuário logado).</param>
    public static async Task<int?> EditarAsync(IWin32Window dono, EventoService servico, IConsultasLookup lookups, int eventoId,
        int? vendedorPadrao = null)
    {
        var input = eventoId == 0
            ? new EventoInput { Data = DateOnly.FromDateTime(DateTime.Today).AddMonths(1) }
            : await servico.ObterParaEdicaoAsync(eventoId);
        var vendedores = await lookups.ListarAsync(TipoLookup.Vendedores);
        if (eventoId == 0 && vendedores.Any(v => v.Id == vendedorPadrao))
            input.VendedorId = vendedorPadrao;

        var clientes = await lookups.ListarAsync(TipoLookup.Clientes);
        var tipos = await lookups.ListarAsync(TipoLookup.TiposEvento);
        var espacos = await lookups.ListarAsync(TipoLookup.Espacos);
        var condicoes = await lookups.ListarAsync(TipoLookup.CondicoesPagamento, somenteAtivos: false);
        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento, somenteAtivos: false);

        var e = new EditorCampos(3);
        e.Texto("Título do evento", () => input.Titulo, v => input.Titulo = v ?? "", nameof(input.Titulo), 150, span: 3,
            dica: "Ex.: Casamento Ana & João");
        var cliente = e.Lista("Cliente", clientes, () => input.ClienteId, v => input.ClienteId = v ?? 0, nameof(input.ClienteId), span: 2);
        var tipo = e.Lista("Tipo de evento", tipos, () => input.TipoEventoId, v => input.TipoEventoId = v ?? 0, nameof(input.TipoEventoId));
        if (eventoId > 0)
        {
            // Cliente e tipo vêm do contrato/orçamento e não mudam após a criação.
            cliente.Enabled = false;
            tipo.Enabled = false;
        }
        e.Lista("Espaço", espacos, () => input.EspacoId, v => input.EspacoId = v ?? 0, nameof(input.EspacoId), span: 2);
        e.Inteiro("Convidados", () => input.Convidados, v => input.Convidados = v, nameof(input.Convidados), 1, 10_000);
        e.Inteiro("Crianças com meia", () => input.ConvidadosMeia, v => input.ConvidadosMeia = v, nameof(input.ConvidadosMeia), 0, 10_000);
        e.Inteiro("Crianças isentas", () => input.ConvidadosIsentos, v => input.ConvidadosIsentos = v, nameof(input.ConvidadosIsentos), 0, 10_000);
        e.Data("Data", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        e.Hora("Início", () => input.HoraInicio, v => input.HoraInicio = v);
        e.Hora("Término", () => input.HoraFim, v => input.HoraFim = v);
        e.Decimal("Valor total do contrato (R$)", () => input.ValorTotal, v => input.ValorTotal = v, nameof(input.ValorTotal));
        e.Lista("Condição de pagamento", condicoes, () => input.CondicaoPagamentoId, v => input.CondicaoPagamentoId = v,
            nameof(input.CondicaoPagamentoId), permitirVazio: true);
        e.Lista("Forma de pagamento", formas, () => input.FormaPagamentoId, v => input.FormaPagamentoId = v,
            nameof(input.FormaPagamentoId), permitirVazio: true);
        e.Lista("Vendedor (comissão)", vendedores, () => input.VendedorId, v => input.VendedorId = v, nameof(input.VendedorId),
            permitirVazio: true, span: 2);
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 2000, span: 3, linhas: 3);

        int? salvo = null;
        using var dialogo = new FormEdicao(eventoId == 0 ? "Novo evento (venda direta)" : "Editar evento",
            eventoId == 0 ? "Para fechar a partir de uma proposta, aprove o orçamento correspondente." : $"EVT-{eventoId:D5}",
            e, async () => salvo = await servico.SalvarAsync(input), 820);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? salvo : null;
    }

    /// <param name="condicaoPadrao">Condição de pagamento do evento: já abre com o plano calculado por ela.</param>
    /// <param name="formaPadrao">Forma de pagamento combinada no evento (sugerida nas parcelas).</param>
    public static async Task<bool> PlanoPagamentoAsync(IWin32Window dono, EventoService servico, IConsultasLookup lookups,
        int eventoId, decimal saldoAParcelar, DateOnly dataEvento, int? condicaoPadrao = null, int? formaPadrao = null)
    {
        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento);
        var condicoes = await lookups.ListarAsync(TipoLookup.CondicoesPagamento);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var plano = condicaoPadrao is { } condicaoId && condicoes.Any(c => c.Id == condicaoId)
            ? await servico.SugerirPlanoAsync(eventoId, condicaoId)
            : new PlanoPagamentoInput
            {
                Quantidade = Math.Clamp((dataEvento.DayNumber - hoje.DayNumber) / 30, 1, 10),
                PrimeiroVencimento = hoje.AddMonths(1),
                ValorEntrada = Math.Round(saldoAParcelar * 0.3m, 2),
                VencimentoEntrada = hoje.AddDays(2),
                FormaPagamentoId = formaPadrao is { } f && formas.Any(x => x.Id == f) ? f : formas.FirstOrDefault()?.Id
            };
        plano.FormaPagamentoId ??= formas.FirstOrDefault()?.Id;

        var e = new EditorCampos();
        e.Informacao("Saldo a parcelar", () => Textos.Moeda(saldoAParcelar));
        e.Informacao("Data do evento", () => dataEvento.ToString("dd/MM/yyyy"));
        var condicao = e.Lista("Condição de pagamento (preenche o plano)", condicoes, () => plano.CondicaoPagamentoId,
            v => plano.CondicaoPagamentoId = v, permitirVazio: true, span: 2);
        var entrada = e.Decimal("Entrada (R$)", () => plano.ValorEntrada, v => plano.ValorEntrada = v);
        var vencimentoEntrada = e.Data("Vencimento da entrada", () => plano.VencimentoEntrada, v => plano.VencimentoEntrada = v);
        var quantidade = e.Inteiro("Quantidade de parcelas (após a entrada)", () => plano.Quantidade, v => plano.Quantidade = v,
            minimo: 1, maximo: 48);
        var primeiro = e.Data("Primeiro vencimento", () => plano.PrimeiroVencimento, v => plano.PrimeiroVencimento = v ?? hoje);
        var intervalo = e.Inteiro("Intervalo entre parcelas (dias; 0 = mensal)", () => plano.IntervaloDias ?? 0,
            v => plano.IntervaloDias = v, minimo: 0, maximo: 365);
        var forma = e.Lista("Forma de pagamento", formas, () => plano.FormaPagamentoId, v => plano.FormaPagamentoId = v, permitirVazio: true);
        var definidas = new Label { AutoSize = true, Margin = new Padding(4, 8, 4, 4) };
        e.Livre(definidas);

        // Condição com as parcelas definidas (prazo e % de cada uma): o plano vem pronto e os campos ficam travados.
        void MostrarDefinidas()
        {
            var prontas = plano.Vencimentos is { Count: > 0 };
            foreach (var campo in new Control[] { entrada, vencimentoEntrada, quantidade, primeiro, intervalo })
                campo.Enabled = !prontas;
            definidas.Visible = prontas;
            if (prontas)
                definidas.Text = "Parcelas definidas na condição:" + Environment.NewLine + string.Join(Environment.NewLine,
                    plano.Vencimentos!.Select((v, i) => $"   {i + 1}/{plano.Vencimentos!.Count}   {v.Vencimento:dd/MM/yyyy}   {Textos.Moeda(v.Valor)}" +
                                                         (formas.FirstOrDefault(f => f.Id == v.FormaPagamentoId)?.Nome is { } nome ? $"   {nome}" : "")));
        }
        e.AoCarregar(MostrarDefinidas);

        // Escolher a condição recalcula entrada, parcelas e vencimentos (os campos continuam editáveis, salvo parcelas definidas).
        condicao.SelectionChangeCommitted += async (_, _) =>
        {
            if (condicao.SelectedItem is not ItemLookup { Id: > 0 } item)
            {
                plano.Vencimentos = null;
                MostrarDefinidas();
                return;
            }
            try
            {
                var sugerido = await servico.SugerirPlanoAsync(eventoId, item.Id);
                plano.Vencimentos = sugerido.Vencimentos;
                MostrarDefinidas();
                entrada.Value = Math.Clamp(sugerido.ValorEntrada, entrada.Minimum, entrada.Maximum);
                vencimentoEntrada.Value = (sugerido.VencimentoEntrada ?? sugerido.PrimeiroVencimento).ToDateTime(TimeOnly.MinValue);
                quantidade.Value = Math.Clamp(sugerido.Quantidade, 1, 48);
                primeiro.Value = sugerido.PrimeiroVencimento.ToDateTime(TimeOnly.MinValue);
                intervalo.Value = sugerido.IntervaloDias ?? 0;
                if (sugerido.FormaPagamentoId is { } formaId && formas.Any(f => f.Id == formaId))
                    forma.SelectedIndex = formas.Select((f, i) => (f, i)).First(x => x.f.Id == formaId).i + 1;
            }
            catch (Exception ex)
            {
                Mensagens.Erro(dono, ex);
            }
        };

        using var dialogo = new FormEdicao("Plano de pagamento",
            "Parcelas em aberto serão substituídas; parcelas já pagas são mantidas. Escolha uma condição para preencher o plano ou ajuste à mão.",
            e, () => servico.GerarParcelasAsync(eventoId, plano), 640);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>
    /// Fecha a contagem de quem veio (o check-in já vem preenchido) e cobra os excedentes. Retorna o Id da parcela gerada,
    /// 0 quando ninguém passou do contratado, ou nulo se cancelado.
    /// </summary>
    public static async Task<int?> ApurarExcedentesAsync(IWin32Window dono, EventoService servico, IConsultasLookup lookups, Evento evento)
    {
        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento);
        var pagantes = evento.QuantidadeConvidados - evento.ConvidadosIsentos;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var input = new ApuracaoExcedentesInput
        {
            Presentes = Math.Max(evento.PessoasPresentes, evento.QuantidadeConvidados),
            PresentesMeia = evento.ConvidadosMeia,
            PresentesIsentos = evento.ConvidadosIsentos,
            ValorPorPessoa = evento.ValorPorPessoa > 0 || pagantes <= 0 ? evento.ValorPorPessoa : Math.Round(evento.ValorTotal / pagantes, 2),
            Vencimento = DateOnly.FromDayNumber(Math.Min(hoje.AddDays(5).DayNumber, evento.DataEvento.AddDays(60).DayNumber)),
            FormaPagamentoId = evento.FormaPagamentoId is { } f && formas.Any(x => x.Id == f) ? f : formas.FirstOrDefault()?.Id
        };

        var e = new EditorCampos(3);
        e.Informacao("Contratado", () => TextosFaixas.Resumo(evento.QuantidadesPorFaixa), span: 2);
        e.Informacao("Check-in", () => $"{evento.PessoasPresentes} pessoas");
        var presentes = e.Inteiro("Pessoas presentes", () => input.Presentes, v => input.Presentes = v, minimo: 1, maximo: 10_000);
        var meia = e.Inteiro("Crianças com meia", () => input.PresentesMeia, v => input.PresentesMeia = v, minimo: 0, maximo: 10_000);
        var isentos = e.Inteiro("Crianças isentas", () => input.PresentesIsentos, v => input.PresentesIsentos = v, minimo: 0, maximo: 10_000);
        var valor = e.Decimal("Valor por pessoa (R$)", () => input.ValorPorPessoa, v => input.ValorPorPessoa = v);
        e.Data("Vencimento da cobrança", () => input.Vencimento, v => input.Vencimento = v ?? hoje);
        e.Lista("Forma de pagamento", formas, () => input.FormaPagamentoId, v => input.FormaPagamentoId = v, permitirVazio: true);
        var resumo = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(4, 8, 4, 4) };
        e.Livre(resumo);

        void Simular()
        {
            var contagem = new QuantidadesConvidados((int)presentes.Value, (int)meia.Value, (int)isentos.Value);
            if (contagem.Meia + contagem.Isentos > contagem.Total)
            {
                resumo.Text = "As crianças não podem passar do total de pessoas presentes.";
                resumo.ForeColor = Tema.Perigo;
                return;
            }
            var x = evento.CalcularExcedentes(contagem, valor.Value);
            var regra = evento.RegraFaixas;
            var partes = new List<string>();
            if (x.Inteira > 0)
                partes.Add($"{x.Inteira} inteira × {Textos.Moeda(valor.Value)}");
            if (x.Meia > 0)
                partes.Add($"{x.Meia} meia × {Textos.Moeda(regra.ValorMeia(valor.Value))}");
            if (x.Isentos > 0)
                partes.Add($"{x.Isentos} isento(s), sem custo");
            resumo.Text = x.Total == 0
                ? "Ninguém passou do contratado: a contagem fica registrada, sem cobrança."
                : $"Excedentes: {string.Join(" + ", partes)}" + Environment.NewLine + $"A cobrar: {Textos.Moeda(x.Valor)}" +
                  (evento.Espaco is { } espaco && contagem.Total > espaco.CapacidadeMaxima
                      ? $"   ⚠ acima da capacidade do espaço ({espaco.CapacidadeMaxima})"
                      : "");
            resumo.ForeColor = x.Valor > 0 ? Tema.Alerta : Tema.Texto;
        }
        foreach (var campo in new[] { presentes, meia, isentos, valor })
            campo.ValueChanged += (_, _) => Simular();
        e.AoCarregar(Simular);

        int? parcelaId = null;
        using var dialogo = new FormEdicao("Convidados excedentes",
            "Informe quantas pessoas vieram (o check-in mais quem entrou pela portaria). Quem passou do contratado é cobrado numa " +
            "parcela própria, fora do plano de pagamento. A vaga de um adulto que faltou cobre uma criança a mais, nunca o contrário." +
            (evento.EstoqueBaixado ? " O estoque já foi baixado: o consumo dos excedentes não muda (ajuste pelo inventário)." : ""),
            e, async () => parcelaId = (await servico.ApurarExcedentesAsync(evento.Id, input)).ParcelaId ?? 0, 760);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? parcelaId : null;
    }

    public static async Task<int?> ImportarConvidadosAsync(IWin32Window dono, EventoService servico, int eventoId)
    {
        string? texto = null;
        int? importados = null;
        var e = new EditorCampos(1);
        e.Texto("Cole a lista (uma pessoa por linha)", () => texto, v => texto = v, "Texto", 100_000, linhas: 14,
            dica: "Nome;Telefone;Acompanhantes;Mesa");
        using var dialogo = new FormEdicao("Importar convidados",
            "Formato por linha: Nome;Telefone;Acompanhantes;Mesa — somente o nome é obrigatório. Também aceita colunas copiadas do Excel.",
            e, async () => importados = await servico.ImportarConvidadosAsync(eventoId, texto ?? ""), 640);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? importados : null;
    }
}
