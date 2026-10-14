using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Visão geral dos contratos: pendências de assinatura, assinados e cancelados.</summary>
internal sealed class FormContratos : FormListagem<ContratoResumo>
{
    private readonly IConsultasComercial _consultas;
    private readonly ContratoService _servico;
    private readonly IEnvioEmail _email;
    private readonly BuffetOptions _opcoes;
    private readonly ISessaoUsuario _sessao;
    private readonly Navegador _navegador;
    private readonly ComboBox _status = new() { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormContratos(IConsultasComercial consultas, ContratoService servico, IEnvioEmail email, BuffetOptions opcoes,
        ISessaoUsuario sessao, Navegador navegador)
        : base("Contratos", "Contratos emitidos a partir dos eventos. Para emitir um novo contrato, abra o evento (aba Contrato).")
    {
        _consultas = consultas;
        _servico = servico;
        _email = email;
        _opcoes = opcoes;
        _sessao = sessao;
        _navegador = navegador;

        _status.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<StatusContrato>()).ToList();
        Filtro("Situação", _status);
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Abrir evento", Icones.Bandeira, () => Selecionado() is { } c ? AoAbrirAsync(c) : Task.CompletedTask, destaque: true);
        Botao("Coletar assinaturas", Icones.Caneta, AssinarAsync);
        Botao("Imprimir / PDF", Icones.Imprimir, () => Selecionado() is { } c
            ? DialogosContrato.ImprimirAsync(this, _servico, _opcoes, c.Id)
            : Task.CompletedTask);
        Botao("Enviar por e-mail", Icones.Email, async () =>
        {
            if (Selecionado() is { } c && await DialogosContrato.EnviarEmailAsync(this, _servico, _email, _opcoes, c.Id))
                Mensagens.Info(this, $"Contrato {c.Codigo} enviado por e-mail.");
        });
        Botao("Cancelar", Icones.Cancelar, async () =>
        {
            if (Selecionado() is { } c && await DialogosContrato.CancelarAsync(this, _servico, await _servico.ObterAsync(c.Id)))
                await CarregarAsync();
        });

        Grade.Texto("Contrato", c => c.Codigo, 18)
            .Texto("Evento", c => $"{c.EventoCodigo} - {c.Evento}", 60)
            .Texto("Cliente", c => c.Cliente, 45)
            .Data("Data do evento", c => c.DataEvento, peso: 22)
            .Status("Situação", c => c.Status, 34)
            .Data("Emitido em", c => c.EmitidoEm, peso: 20)
            .Data("Assinado em", c => c.AssinadoEm, peso: 20)
            .Texto("Signatário", c => c.NomeSignatario ?? c.MotivoCancelamento, 40)
            .Busca(c => $"{c.Codigo} {c.EventoCodigo} {c.Evento} {c.Cliente} {c.NomeSignatario}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarContratosAsync((_status.SelectedItem as OpcaoEnum<StatusContrato>)?.Valor));

    protected override string TextoRodape(IReadOnlyList<ContratoResumo> visiveis)
    {
        var pendentes = visiveis.Where(c => c.Status == StatusContrato.AguardandoAssinatura).ToList();
        return $"{visiveis.Count} contrato(s)  •  aguardando assinatura: {pendentes.Count}" +
               (pendentes.Count(c => c.DataEvento < DateTime.Today.AddDays(30)) is var urgentes and > 0
                   ? $" ({urgentes} com evento nos próximos 30 dias)"
                   : "") +
               $"  •  assinados: {visiveis.Count(c => c.Status == StatusContrato.Assinado)}";
    }

    protected override Task AoAbrirAsync(ContratoResumo item)
    {
        _navegador.Abrir($"evento:{item.EventoId}", () => _navegador.Criar<FormEventoDetalhe>(item.EventoId));
        return Task.CompletedTask;
    }

    private async Task AssinarAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var contrato = await _servico.ObterAsync(item.Id);
        if (DialogosContrato.Assinar(this, _servico, contrato, _sessao.Usuario?.Nome))
        {
            await CarregarAsync();
            Grade.Selecionar(c => c.Id == item.Id);
        }
    }
}
