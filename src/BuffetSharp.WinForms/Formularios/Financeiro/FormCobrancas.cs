using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Boletos e PIX registrados (Asaas): pendentes, pagos e cancelados. O pagamento é conferido sozinho de tempos em tempos (e pelo
/// botão "Verificar pagamentos") e baixa a parcela; o que não deu para baixar aparece na mensagem.
/// </summary>
internal sealed class FormCobrancas : FormListagem<CobrancaResumo>
{
    private readonly IConsultasCobrancas _consultas;
    private readonly CobrancaService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormCobrancas(IConsultasCobrancas consultas, CobrancaService servico, Navegador navegador)
        : base("Cobranças registradas (boleto e PIX)", "Emitidas em Contas a receber (\"Boleto / PIX\"). O sistema confere os pagamentos sozinho " +
                                                       "e baixa as parcelas pagas; as pendentes aparecem sempre, qualquer que seja o período.")
    {
        _consultas = consultas;
        _servico = servico;
        _navegador = navegador;

        _de = FiltroData("Emitidas de", DateTime.Today.AddMonths(-3));
        _ate = FiltroData("até", null);
        _status.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<StatusCobranca>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Verificar pagamentos", Icones.Atualizar, VerificarAsync, "Pergunta ao Asaas quais cobranças foram pagas e baixa as parcelas",
            destaque: true);
        Botao("Ver cobrança", Icones.Pagina, () => Selecionado() is { } c ? DialogosCobranca.ExibirAsync(this, _navegador, c.Id) : Task.CompletedTask,
            "Linha digitável, PIX e envio ao cliente (segunda via)");
        Botao("Cancelar cobrança", Icones.Cancelar, CancelarAsync, "O boleto deixa de valer; a parcela continua em aberto");
        Botao("Simular pagamento", Icones.Dinheiro, SimularAsync, "Só no ambiente simulado: paga a cobrança para treinar a baixa automática");
        Separador();
        Botao("Abrir evento", Icones.Bandeira, () => Selecionado() is { } c ? AoAbrirAsync(c) : Task.CompletedTask);
        Botao("Configuração", Icones.Configuracoes, async () =>
        {
            if (await DialogosCobranca.ConfigurarAsync(this, _navegador))
                await CarregarAsync();
        }, "Ambiente, conta que recebe, multa e juros");

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Grade.Data("Emissão", c => c.SolicitadaEm, peso: 12)
            .Texto("Cliente", c => c.Cliente, 30)
            .Texto("Evento", c => $"{c.EventoCodigo} - {c.Evento}", 34)
            .Texto("Parcela", c => c.Descricao, 16)
            .Texto("Tipo", c => Textos.Exibir(c.Tipo), 12)
            .Moeda("Valor", c => c.Valor, 14)
            .Data("Vencimento", c => c.Vencimento, peso: 12, cor: c => c.Vencida(hoje) ? Tema.Perigo : null)
            .Status("Situação", c => c.Status, 12)
            .Data("Pago em", c => c.DataPagamento, peso: 12)
            .Moeda("Pago", c => c.ValorPago, 14)
            .Moeda("Taxa", c => c.Taxa, 10)
            .Texto("Ambiente", c => c.Ambiente == AmbienteCobranca.Producao ? "Produção" : c.Ambiente == AmbienteCobranca.Sandbox ? "Sandbox" : "Simulado",
                11, c => c.Ambiente == AmbienteCobranca.Producao ? null : Tema.Alerta)
            .Texto("Mensagem", c => c.Mensagem ?? (c.Status == StatusCobranca.Paga && c.Baixada ? "Parcela baixada" : null), 40,
                c => c.Mensagem is not null ? Tema.Perigo : c.Baixada ? Tema.Sucesso : null)
            .Busca(c => $"{c.Cliente} {c.EventoCodigo} {c.Evento} {c.Descricao} {c.IdExterno}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarAsync(new FiltroCobrancas
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusCobranca>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<CobrancaResumo> v)
    {
        var pendentes = v.Where(c => c.Status == StatusCobranca.Pendente).ToList();
        var pagas = v.Where(c => c.Status == StatusCobranca.Paga).ToList();
        return $"{v.Count} cobrança(s)  •  pendentes: {pendentes.Count} ({Textos.Moeda(pendentes.Sum(c => c.Valor))})" +
               $"  •  pagas: {pagas.Count} ({Textos.Moeda(pagas.Sum(c => c.ValorPago ?? 0))}, taxas {Textos.Moeda(pagas.Sum(c => c.Taxa ?? 0))})";
    }

    protected override Task AoAbrirAsync(CobrancaResumo item)
    {
        _navegador.Abrir($"evento:{item.EventoId}", () => _navegador.Criar<FormEventoDetalhe>(item.EventoId));
        return Task.CompletedTask;
    }

    private async Task VerificarAsync()
    {
        var resultado = await _servico.VerificarPagamentosAsync();
        await CarregarAsync();
        var texto = resultado.Consultadas == 0 && resultado.Pagas == 0
            ? "Nenhuma cobrança pendente no Asaas para conferir."
            : $"{resultado.Consultadas} cobrança(s) conferida(s): {resultado.Pagas} paga(s), {resultado.Baixadas} parcela(s) baixada(s).";
        if (resultado.Avisos.Count > 0)
            Mensagens.Aviso(this, texto + "\n\n• " + string.Join("\n• ", resultado.Avisos), "Verificar pagamentos");
        else
            Mensagens.Info(this, texto, "Verificar pagamentos");
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } c)
            return;
        if (c.Status != StatusCobranca.Pendente)
        {
            Mensagens.Aviso(this, "Só uma cobrança pendente pode ser cancelada.");
            return;
        }
        if (!Mensagens.Confirmar(this, $"Cancelar a cobrança de {c.Descricao} ({c.Cliente}, {Textos.Moeda(c.Valor)})? O boleto e o PIX deixam " +
                                       "de valer; a parcela continua em aberto.", "Cancelar cobrança", perigoso: true))
            return;
        await _servico.CancelarAsync(c.Id);
        await CarregarAsync();
        Grade.Selecionar(x => x.Id == c.Id);
    }

    private async Task SimularAsync()
    {
        if (Selecionado() is not { } c)
            return;
        if (c.Ambiente != AmbienteCobranca.Simulado || c.Status != StatusCobranca.Pendente)
        {
            Mensagens.Aviso(this, "Selecione uma cobrança pendente do ambiente simulado. No sandbox, pague pelo painel de testes do Asaas.");
            return;
        }
        await _servico.SimularPagamentoAsync(c.Id);
        await CarregarAsync();
        Grade.Selecionar(x => x.Id == c.Id);
    }
}
