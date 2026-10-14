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
        e.Lista("Vendedor (comissão)", vendedores, () => input.VendedorId, v => input.VendedorId = v, nameof(input.VendedorId),
            permitirVazio: true, span: 2);
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 2000, span: 3, linhas: 3);

        int? salvo = null;
        using var dialogo = new FormEdicao(eventoId == 0 ? "Novo evento (venda direta)" : "Editar evento",
            eventoId == 0 ? "Para fechar a partir de uma proposta, aprove o orçamento correspondente." : $"EVT-{eventoId:D5}",
            e, async () => salvo = await servico.SalvarAsync(input), 820);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? salvo : null;
    }

    public static async Task<bool> PlanoPagamentoAsync(IWin32Window dono, EventoService servico, IConsultasLookup lookups,
        int eventoId, decimal saldoAParcelar, DateOnly dataEvento)
    {
        var formas = await lookups.ListarAsync(TipoLookup.FormasPagamento);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var plano = new PlanoPagamentoInput
        {
            Quantidade = Math.Clamp((dataEvento.DayNumber - hoje.DayNumber) / 30, 1, 10),
            PrimeiroVencimento = hoje.AddMonths(1),
            ValorEntrada = Math.Round(saldoAParcelar * 0.3m, 2),
            VencimentoEntrada = hoje.AddDays(2),
            FormaPagamentoId = formas.FirstOrDefault()?.Id
        };

        var e = new EditorCampos();
        e.Informacao("Saldo a parcelar", () => Textos.Moeda(saldoAParcelar));
        e.Informacao("Data do evento", () => dataEvento.ToString("dd/MM/yyyy"));
        e.Decimal("Entrada (R$)", () => plano.ValorEntrada, v => plano.ValorEntrada = v);
        e.Data("Vencimento da entrada", () => plano.VencimentoEntrada, v => plano.VencimentoEntrada = v);
        e.Inteiro("Quantidade de parcelas (após a entrada)", () => plano.Quantidade, v => plano.Quantidade = v, minimo: 1, maximo: 48);
        e.Data("Primeiro vencimento", () => plano.PrimeiroVencimento, v => plano.PrimeiroVencimento = v ?? hoje);
        e.Lista("Forma de pagamento", formas, () => plano.FormaPagamentoId, v => plano.FormaPagamentoId = v, permitirVazio: true, span: 2);

        using var dialogo = new FormEdicao("Plano de pagamento",
            "Parcelas em aberto serão substituídas; parcelas já pagas são mantidas. Vencimentos mensais a partir do primeiro.",
            e, () => servico.GerarParcelasAsync(eventoId, plano), 620);
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
