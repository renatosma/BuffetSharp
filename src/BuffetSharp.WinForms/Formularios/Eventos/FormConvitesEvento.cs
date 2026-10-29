using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>
/// Convites de agenda do evento: o cliente, cada funcionário escalado e cada fornecedor. Mostra quem já recebeu, quem falta e de
/// quem a agenda ficou desatualizada (horário mudou, evento cancelado, saiu da escala), para reenviar.
/// </summary>
internal sealed class FormConvitesEvento : FormBase
{
    private readonly ConviteService _convites;
    private readonly IEnvioEmail _email;
    private readonly int _eventoId;
    private readonly Grade<ConviteAgenda> _grade = new();
    private readonly Label _resumo = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(8, 8, 0, 0) };

    public FormConvitesEvento(ConviteService convites, IEnvioEmail email, int eventoId)
    {
        _convites = convites;
        _email = email;
        _eventoId = eventoId;
        Text = $"Convites de agenda - EVT-{eventoId:D5}";
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Px(1000), Px(520));

        _grade.Texto("Para", c => c.Destino switch
            {
                DestinoConvite.Cliente => "Cliente",
                DestinoConvite.Equipe => "Equipe",
                DestinoConvite.Fornecedor => "Fornecedor",
                _ => "Degustação"
            }, 12)
            .Texto("Nome", c => c.Nome, 26)
            .Texto("Contato", c => c.Email ?? c.Telefone, 24)
            .Texto("Compromisso", c => $"{c.Inicio:dd/MM HH:mm} às {c.Fim:HH:mm}", 18)
            .Texto("Situação", c => c.Situacao, 32, c => c.Desatualizado ? c.Cancelado ? Tema.Perigo : Tema.Alerta
                : c.UltimoEnvio is not null ? Tema.Sucesso : Tema.TextoSuave);

        var cabecalho = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(20, 16, 20, 4),
            BackColor = Tema.Superficie
        };
        cabecalho.Controls.Add(Tema.Titulo("Convites de agenda"));
        cabecalho.Controls.Add(Tema.Subtitulo("O convite (.ics) salva o compromisso na agenda do celular ou do computador. Se o horário mudar ou o " +
                                              "evento for cancelado, o reenvio atualiza o mesmo compromisso."));

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 4, 16, 4), BackColor = Tema.Superficie };
        Botao(barra, Tema.BotaoPrimario("Enviar pendentes", Icones.Enviar), EnviarPendentesAsync);
        Botao(barra, Tema.BotaoSecundario("E-mail", Icones.Email), () => Selecionado(c => Task.FromResult(DialogosConvite.Email(this, _convites, _email, c))));
        Botao(barra, Tema.BotaoSecundario("WhatsApp", Icones.Mensagem), () => Selecionado(c => DialogosConvite.WhatsAppAsync(this, _convites, c)));
        Botao(barra, Tema.BotaoSecundario("Salvar .ics", Icones.Salvar), () => Selecionado(c => DialogosConvite.SalvarAsync(this, _convites, c)));
        barra.Controls.Add(_resumo);

        var acoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(12, 10, 16, 10), BackColor = Tema.Fundo
        };
        var fechar = Tema.BotaoSecundario("Fechar");
        fechar.DialogResult = DialogResult.Cancel;
        acoes.Controls.Add(fechar);
        CancelButton = fechar;

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        corpo.Controls.Add(_grade.Controle);
        Controls.Add(corpo);
        Controls.Add(barra);
        Controls.Add(cabecalho);
        Controls.Add(acoes);
    }

    private void Botao(FlowLayoutPanel barra, Button botao, Func<Task> acao)
    {
        botao.Click += async (_, _) => await ExecutarAsync(acao, botao, cursorEspera: false);
        barra.Controls.Add(botao);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(CarregarAsync);
    }

    private async Task CarregarAsync()
    {
        var convites = await _convites.DoEventoAsync(_eventoId);
        var selecionado = _grade.Selecionado?.Uid;
        _grade.Carregar(convites);
        if (selecionado is not null)
            _grade.Selecionar(c => c.Uid == selecionado);
        var pendentes = convites.Count(c => c.Pendente);
        _resumo.Text = pendentes == 0 ? "Todos os convites estão em dia." : $"{pendentes} convite(s) para enviar ou atualizar.";
        _resumo.ForeColor = pendentes == 0 ? Tema.Sucesso : Tema.Alerta;
    }

    private async Task Selecionado(Func<ConviteAgenda, Task<bool>> acao)
    {
        if (_grade.Selecionado is { } convite && await acao(convite))
            await CarregarAsync();
    }

    private async Task EnviarPendentesAsync()
    {
        var resultado = await _convites.EnviarPendentesAsync(_eventoId);
        await CarregarAsync();
        var enviados = resultado.Emails + resultado.WhatsApps;
        Mensagens.Info(this, (enviados == 0 ? "Nenhum convite enviado." : $"{resultado.Emails} por e-mail e {resultado.WhatsApps} pelo WhatsApp.") +
                             (resultado.SemCanal.Count == 0 ? ""
                                 : $"{Environment.NewLine}Sem e-mail (e sem WhatsApp direto): {string.Join(", ", resultado.SemCanal)}. Use \"WhatsApp\" ou \"Salvar .ics\"."));
    }
}
