using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>Diálogos do evento: dados gerais, plano de pagamento e importação de convidados.</summary>
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
        e.Data("Data", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        e.Hora("Início", () => input.HoraInicio, v => input.HoraInicio = v);
        e.Hora("Término", () => input.HoraFim, v => input.HoraFim = v);
        e.Decimal("Valor total do contrato (R$)", () => input.ValorTotal, v => input.ValorTotal = v, nameof(input.ValorTotal));
        e.Lista("Condição de pagamento", condicoes, () => input.CondicaoPagamentoId, v => input.CondicaoPagamentoId = v,
            nameof(input.CondicaoPagamentoId), permitirVazio: true);
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
    public static async Task<bool> PlanoPagamentoAsync(IWin32Window dono, EventoService servico, IConsultasLookup lookups,
        int eventoId, decimal saldoAParcelar, DateOnly dataEvento, int? condicaoPadrao = null)
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
                FormaPagamentoId = formas.FirstOrDefault()?.Id
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

        // Escolher a condição recalcula entrada, parcelas e vencimentos (os campos continuam editáveis).
        condicao.SelectionChangeCommitted += async (_, _) =>
        {
            if (condicao.SelectedItem is not ItemLookup { Id: > 0 } item)
                return;
            try
            {
                var sugerido = await servico.SugerirPlanoAsync(eventoId, item.Id);
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
