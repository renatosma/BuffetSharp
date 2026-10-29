using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>
/// Pesquisas de satisfação dos eventos realizados: envio pelo WhatsApp ou e-mail, registro da resposta (colada da conversa),
/// pedido de avaliação no Google aos promotores e tratativa dos detratores.
/// </summary>
internal sealed class FormPesquisasSatisfacao : FormListagem<PesquisaResumo>
{
    private readonly PesquisaService _pesquisas;
    private readonly LembreteService _lembretes;
    private readonly IEnvioEmail _email;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormPesquisasSatisfacao(PesquisaService pesquisas, LembreteService lembretes, IEnvioEmail email, BuffetOptions opcoes, Navegador navegador)
        : base("Pesquisa de satisfação", "Eventos realizados no período e a pesquisa de cada um. O cliente responde pelo WhatsApp e a resposta é registrada aqui.")
    {
        _pesquisas = pesquisas;
        _lembretes = lembretes;
        _email = email;
        _opcoes = opcoes;
        _navegador = navegador;
        _de = FiltroData("Eventos de", DateTime.Today.AddDays(-90), opcional: false);
        _ate = FiltroData("até", DateTime.Today, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Enviar pesquisa", Icones.Mensagem, () => EnviarAsync(TipoLembrete.PesquisaSatisfacao, email: false), "Pelo WhatsApp, com as perguntas numeradas",
            destaque: true);
        Botao("Por e-mail", Icones.Email, () => EnviarAsync(TipoLembrete.PesquisaSatisfacao, email: true));
        Botao("Registrar resposta", Icones.Caneta, RegistrarRespostaAsync, "Cole a resposta do cliente: as notas são lidas sozinhas");
        Botao("Pedir avaliação no Google", Icones.Estrela, () => EnviarAsync(TipoLembrete.AvaliacaoGoogle, email: false), "Para quem deu 9 ou 10");
        Botao("Tratar detrator", Icones.Contato, TratarDetratorAsync, "Registra o que foi feito com o cliente insatisfeito");
        Botao("Painel", Icones.Calculadora, () =>
        {
            _navegador.Abrir<FormPainelSatisfacao>();
            return Task.CompletedTask;
        }, "NPS, médias por área, por tipo de evento e por vendedor");

        Grade.Data("Data", p => p.Data, "dd/MM/yyyy", 14)
            .Texto("Evento", p => $"{p.Codigo} - {p.Titulo}", 34)
            .Texto("Cliente", p => p.Cliente, 26)
            .Texto("Tipo", p => p.TipoEvento, 16)
            .Texto("Pesquisa", p => p.RespondidaEm is { } r ? $"Respondida em {r:dd/MM}" : p.EnviadaEm is { } e ? $"Enviada em {e:dd/MM}" : "Não enviada", 20,
                p => p.RespondidaEm is not null ? Tema.Sucesso : p.EnviadaEm is not null ? Tema.Info : Tema.TextoSuave)
            .Numero("NPS", p => p.Nps, peso: 9, cor: p => p.Categoria switch
            {
                CategoriaNps.Promotor => Tema.Sucesso,
                CategoriaNps.Detrator => Tema.Perigo,
                CategoriaNps.Neutro => Tema.Alerta,
                _ => null
            })
            .Numero("Comida", p => p.Comida, peso: 9)
            .Numero("Atend.", p => p.Atendimento, peso: 9)
            .Numero("Espaço", p => p.Espaco, peso: 9)
            .Numero("Organiz.", p => p.Organizacao, peso: 9)
            .Texto("Comentário", p => p.Comentario, 40)
            .Texto("Acompanhamento", p => p.DetratorPendente ? "⚠ detrator: ligar para o cliente"
                : p.TratativaDetrator ?? (p.AvaliacaoGooglePedidaEm is { } g ? $"Avaliação no Google pedida em {g:dd/MM}" : null), 34,
                p => p.DetratorPendente ? Tema.Perigo : null)
            .Busca(p => $"{p.Codigo} {p.Titulo} {p.Cliente} {p.TipoEvento} {p.Comentario}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _pesquisas.ListarAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<PesquisaResumo> visiveis)
    {
        var notas = visiveis.Where(p => p.Nps is not null).Select(p => p.Nps!.Value).ToList();
        var nps = PesquisaSatisfacao.CalcularNps(notas);
        return $"{visiveis.Count} evento(s)  •  {visiveis.Count(p => p.EnviadaEm is not null || p.RespondidaEm is not null)} pesquisa(s) enviada(s)  •  " +
               $"{notas.Count} resposta(s)" + (nps is { } valor
                   ? $"  •  NPS {valor:0.#} ({notas.Count(n => n >= 9)} promotores, {notas.Count(n => n is 7 or 8)} neutros, {notas.Count(n => n <= 6)} detratores)"
                   : "");
    }

    protected override Task AoAbrirAsync(PesquisaResumo item) => RegistrarRespostaAsync(item);

    /// <summary>Envia a pesquisa (ou o pedido de avaliação) com a mensagem pronta e registra o envio.</summary>
    private async Task EnviarAsync(TipoLembrete tipo, bool email)
    {
        if (Selecionado() is not { } pesquisa)
            return;
        if (tipo == TipoLembrete.AvaliacaoGoogle)
        {
            if (pesquisa.Categoria != CategoriaNps.Promotor)
            {
                Mensagens.Aviso(this, "O pedido de avaliação no Google vai só para quem deu nota 9 ou 10.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_opcoes.LinkAvaliacaoGoogle))
            {
                Mensagens.Aviso(this, "Configure o link de avaliação no Google em \"Buffet:LinkAvaliacaoGoogle\" (appsettings).");
                return;
            }
        }
        else if (pesquisa.RespondidaEm is not null && !Mensagens.Confirmar(this, "Esta pesquisa já foi respondida. Enviar de novo?"))
            return;

        var lembrete = PesquisaService.Lembrete(pesquisa, tipo);
        if (email)
        {
            if (await _lembretes.PrepararEmailAsync(lembrete) is { } mensagem &&
                DialogosEnvio.Email(this, "Enviar a pesquisa por e-mail", mensagem, null, null, m => _lembretes.EnviarEmailAsync(lembrete, m), _email.Destino))
                await CarregarAsync();
            return;
        }
        if (await _lembretes.PrepararWhatsAppAsync(lembrete) is not { } whatsApp ||
            DialogosEnvio.WhatsApp(this, whatsApp, tipo == TipoLembrete.AvaliacaoGoogle ? "pedido de avaliação" : "pesquisa de satisfação") is null)
            return;
        await _lembretes.MarcarTratadoAsync(lembrete, CanalLembrete.WhatsApp);
        await CarregarAsync();
    }

    private Task RegistrarRespostaAsync() => Selecionado() is { } pesquisa ? RegistrarRespostaAsync(pesquisa) : Task.CompletedTask;

    /// <summary>
    /// Registra as notas; colando a mensagem do cliente ("10, 5, 5, 4, 5 adoramos"), "Ler a resposta" preenche os campos.
    /// </summary>
    private async Task RegistrarRespostaAsync(PesquisaResumo pesquisa)
    {
        var input = new RespostaPesquisaInput
        {
            Nps = pesquisa.Nps ?? 10, Comida = pesquisa.Comida, Atendimento = pesquisa.Atendimento, Espaco = pesquisa.Espaco,
            Organizacao = pesquisa.Organizacao, Comentario = pesquisa.Comentario
        };
        string? colada = null;
        var editor = new EditorCampos();
        var resposta = editor.Texto("Resposta do cliente (cole a mensagem)", () => colada, v => colada = v, null, 2000, span: 2, linhas: 3,
            dica: "Ex.: 10, 5, 5, 4, 5 a comida estava maravilhosa");
        var nps = editor.Inteiro("De 0 a 10, indicaria o buffet?", () => input.Nps, v => input.Nps = v, nameof(input.Nps), 0, 10, span: 2);
        var comida = editor.Inteiro("Comida e bebida (1 a 5; 0 = sem nota)", () => input.Comida ?? 0, v => input.Comida = v == 0 ? null : v, null, 0, 5);
        var atendimento = editor.Inteiro("Atendimento (1 a 5)", () => input.Atendimento ?? 0, v => input.Atendimento = v == 0 ? null : v, null, 0, 5);
        var espaco = editor.Inteiro("Espaço e decoração (1 a 5)", () => input.Espaco ?? 0, v => input.Espaco = v == 0 ? null : v, null, 0, 5);
        var organizacao = editor.Inteiro("Organização (1 a 5)", () => input.Organizacao ?? 0, v => input.Organizacao = v == 0 ? null : v, null, 0, 5);
        var comentario = editor.Texto("Comentário", () => input.Comentario, v => input.Comentario = v, nameof(input.Comentario),
            PesquisaSatisfacao.TamanhoComentario, span: 2, linhas: 3);

        using var dialogo = new FormEdicao($"Resposta da pesquisa - {pesquisa.Cliente}", $"{pesquisa.Codigo} - {pesquisa.Titulo} ({pesquisa.Data:dd/MM/yyyy})",
            editor, () => _pesquisas.RegistrarRespostaAsync(pesquisa.EventoId, input), 700);
        var ler = Tema.BotaoSecundario("Ler a resposta", Icones.Buscar);
        ler.Click += (_, _) =>
        {
            var lida = RespostaPesquisa.Interpretar(resposta.Text);
            if (lida.Nps is null)
            {
                Mensagens.Aviso(dialogo, "Não encontrei a nota de 0 a 10 na resposta. Preencha os campos.");
                return;
            }
            nps.Value = lida.Nps.Value;
            (comida.Value, atendimento.Value, espaco.Value, organizacao.Value) = (lida.Comida ?? 0, lida.Atendimento ?? 0, lida.Espaco ?? 0, lida.Organizacao ?? 0);
            if (lida.Comentario is { } texto)
                comentario.Text = texto;
        };
        dialogo.AdicionarAcao(ler);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        await CarregarAsync();

        var categoria = PesquisaSatisfacao.Classificar(input.Nps);
        if (categoria == CategoriaNps.Detrator)
            Mensagens.Aviso(this, $"Nota {input.Nps}: cliente insatisfeito. O administrador recebe um lembrete para ligar para ele.");
        else if (categoria == CategoriaNps.Promotor && !string.IsNullOrWhiteSpace(_opcoes.LinkAvaliacaoGoogle) &&
                 Mensagens.Confirmar(this, $"Nota {input.Nps}! Pedir agora a avaliação no Google?", "Avaliação no Google") &&
                 Grade.Itens.FirstOrDefault(p => p.EventoId == pesquisa.EventoId) is { } atualizada)
        {
            Grade.Selecionar(p => p.EventoId == atualizada.EventoId);
            await EnviarAsync(TipoLembrete.AvaliacaoGoogle, email: false);
        }
    }

    private async Task TratarDetratorAsync()
    {
        if (Selecionado() is not { } pesquisa)
            return;
        if (pesquisa.Categoria != CategoriaNps.Detrator)
        {
            Mensagens.Aviso(this, "Somente notas de 0 a 6 (detratores) têm tratativa.");
            return;
        }
        var texto = Dialogos.PedirTexto(this, $"Tratativa - {pesquisa.Cliente} (nota {pesquisa.Nps})", "O que foi feito com o cliente?", pesquisa.TratativaDetrator);
        if (texto is null)
            return;
        await _pesquisas.TratarDetratorAsync(pesquisa.EventoId, texto);
        await CarregarAsync();
    }
}
