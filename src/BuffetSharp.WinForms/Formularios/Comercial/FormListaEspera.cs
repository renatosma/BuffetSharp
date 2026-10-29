using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>
/// Lista de espera de datas: quem quer uma data ocupada, em fila pela ordem de entrada. Quando a data libera, o primeiro da fila
/// é avisado (WhatsApp ou e-mail com a mensagem pronta) e tem alguns dias para responder; depois, a vez passa ao próximo.
/// </summary>
internal sealed class FormListaEspera : FormListagem<EsperaResumo>
{
    private readonly EsperaService _esperas;
    private readonly LembreteService _lembretes;
    private readonly IEnvioEmail _email;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly CheckBox _encerradas = new() { Text = "Mostrar encerradas", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    public FormListaEspera(EsperaService esperas, LembreteService lembretes, IEnvioEmail email, BuffetOptions opcoes, Navegador navegador)
        : base("Lista de espera", "Interessados em datas ocupadas, em fila pela ordem de entrada. Em verde, a data liberou e é a vez da pessoa.")
    {
        _esperas = esperas;
        _lembretes = lembretes;
        _email = email;
        _opcoes = opcoes;
        _navegador = navegador;
        Filtros.Controls.Add(_encerradas);
        _encerradas.CheckedChanged += async (_, _) => await RecarregarAsync();

        Botao("Nova espera", Icones.Adicionar, NovaAsync, destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } e ? EditarAsync(e) : Task.CompletedTask);
        Botao("Avisar (WhatsApp)", Icones.Mensagem, () => AvisarAsync(email: false), "Avisa que a data liberou (com a mensagem pronta)");
        Botao("Avisar (e-mail)", Icones.Email, () => AvisarAsync(email: true));
        Botao("Criar orçamento", Icones.Documento, CriarOrcamentoAsync, "O lead vira cliente e o orçamento já vem com a data e os convidados");
        Botao("Datas alternativas", Icones.Calendario, () => Selecionado() is { } e
            ? DialogosEspera.MostrarAlternativasAsync(this, _esperas, DateOnly.FromDateTime(e.Inicio), TimeOnly.FromDateTime(e.Inicio),
                TimeOnly.FromDateTime(e.Fim), e.EspacoId, e.Convidados)
            : Task.CompletedTask);
        Botao("Voltar para a fila", Icones.Desfazer, () => Alterar(e => _esperas.VoltarParaFilaAsync(e.Id)), "Quem foi avisado volta a esperar");
        Botao("Desistiu", Icones.Cancelar, () => Alterar(e => Mensagens.Confirmar(this, $"{e.Interessado} desistiu da data {e.Inicio:dd/MM/yyyy}?")
            ? _esperas.DesistirAsync(e.Id) : Task.CompletedTask));

        Grade.Data("Data", e => e.Inicio, "ddd dd/MM/yyyy", 18)
            .Texto("Horário", e => $"{e.Inicio:HH:mm} às {e.Fim:HH:mm}", 14)
            .Texto("Espaço", e => e.Espaco, 22)
            .Texto("Interessado", e => e.Lead ? $"{e.Interessado} (lead)" : e.Interessado, 30)
            .Texto("Contato", e => e.Telefone ?? e.Email, 22)
            .Numero("Conv.", e => e.Convidados, peso: 10)
            .Texto("Situação", Situacao, 34, Cor)
            .Data("Entrou em", e => e.EntrouEm, "dd/MM/yy HH:mm", 16)
            .Texto("Observação", e => e.Observacao, 30)
            .CorDaLinha(e => e.Situacao is SituacaoEspera.Convertida or SituacaoEspera.Desistiu ? Tema.Neutro : null)
            .Busca(e => $"{e.Interessado} {e.Telefone} {e.Email} {e.Espaco} {e.Observacao}");
    }

    private bool SemResposta(EsperaResumo e) =>
        e.Situacao == SituacaoEspera.Avisado && e.AvisadoEm < DateTime.Now.AddDays(-_opcoes.DiasRespostaEspera);

    private string Situacao(EsperaResumo e) => e.Situacao switch
    {
        SituacaoEspera.Convertida => e.OrcamentoId is { } orcamento ? $"Virou ORC-{orcamento:D5}" : "Virou orçamento",
        SituacaoEspera.Desistiu => "Desistiu",
        SituacaoEspera.Avisado when SemResposta(e) => "Sem resposta: a vez passou",
        SituacaoEspera.Avisado => $"Avisado em {e.AvisadoEm:dd/MM HH:mm} (responde até {e.AvisadoEm!.Value.AddDays(_opcoes.DiasRespostaEspera):dd/MM})",
        _ when e.SuaVez => $"Data livre ({e.EspacosLivres}): é a vez dele",
        _ when e.DataLivre => $"Data livre, {e.Posicao}º na fila",
        _ => $"Esperando, {e.Posicao}º na fila"
    };

    private Color? Cor(EsperaResumo e) =>
        e.SuaVez ? Tema.Sucesso : SemResposta(e) ? Tema.Alerta : e.Situacao == SituacaoEspera.Avisado ? Tema.Info : null;

    protected override async Task CarregarAsync() => Exibir(await _esperas.ListarAsync(somenteAtivas: !_encerradas.Checked));

    protected override string TextoRodape(IReadOnlyList<EsperaResumo> visiveis) =>
        $"{visiveis.Count(e => e.Situacao is SituacaoEspera.Aguardando or SituacaoEspera.Avisado)} espera(s) ativa(s)  •  " +
        $"{visiveis.Count(e => e.SuaVez)} com a data livre aguardando aviso  •  {visiveis.Count(e => e.Situacao == SituacaoEspera.Avisado)} avisada(s)";

    protected override Task AoAbrirAsync(EsperaResumo item) => EditarAsync(item);

    private async Task NovaAsync()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (await DialogosEspera.EditarAsync(this, _navegador, new EsperaInput { Data = hoje.AddDays(30) }))
            await CarregarAsync();
    }

    private async Task EditarAsync(EsperaResumo e)
    {
        if (e.Situacao is not (SituacaoEspera.Aguardando or SituacaoEspera.Avisado))
            return;
        var input = new EsperaInput
        {
            Id = e.Id, ClienteId = e.ClienteId, Data = DateOnly.FromDateTime(e.Inicio), HoraInicio = TimeOnly.FromDateTime(e.Inicio),
            HoraFim = TimeOnly.FromDateTime(e.Fim), EspacoId = e.EspacoId, TipoEventoId = e.TipoEventoId, Convidados = e.Convidados, Observacao = e.Observacao
        };
        if (await DialogosEspera.EditarAsync(this, _navegador, input))
            await CarregarAsync();
    }

    private async Task Alterar(Func<EsperaResumo, Task> acao)
    {
        if (Selecionado() is not { } espera)
            return;
        await acao(espera);
        await CarregarAsync();
    }

    /// <summary>Avisa o interessado de que a data liberou; o aviso começa o prazo de resposta.</summary>
    private async Task AvisarAsync(bool email)
    {
        if (Selecionado() is not { } espera)
            return;
        if (!espera.SuaVez && espera.Situacao != SituacaoEspera.Avisado)
        {
            Mensagens.Aviso(this, espera.DataLivre
                ? $"A data está livre, mas antes é a vez de quem entrou primeiro na fila ({espera.Posicao}º lugar)."
                : "A data ainda está ocupada. O aviso fica disponível quando ela liberar.");
            return;
        }

        var lembrete = EsperaService.Lembrete(espera);
        if (email)
        {
            if (await _lembretes.PrepararEmailAsync(lembrete) is { } mensagem &&
                DialogosEnvio.Email(this, "Avisar que a data liberou", mensagem, null, null, m => _lembretes.EnviarEmailAsync(lembrete, m), _email.Destino))
                await CarregarAsync();
            return;
        }
        if (await _lembretes.PrepararWhatsAppAsync(lembrete) is not { } whatsApp || DialogosEnvio.WhatsApp(this, whatsApp, "aviso de data liberada") is null)
            return;
        await _lembretes.MarcarTratadoAsync(lembrete, CanalLembrete.WhatsApp);
        await CarregarAsync();
    }

    /// <summary>O lead vira cliente e o orçamento abre com a data, os convidados e o tipo de evento da espera.</summary>
    private async Task CriarOrcamentoAsync()
    {
        if (Selecionado() is not { Situacao: SituacaoEspera.Aguardando or SituacaoEspera.Avisado } espera)
            return;
        if (espera.Lead && !Mensagens.Confirmar(this, $"{espera.Interessado} é um lead: cadastrar como cliente para fazer o orçamento?"))
            return;
        var clienteId = await _esperas.ClienteParaOrcamentoAsync(espera.Id);
        using var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(0, clienteId, espera.TipoEventoId,
            DateOnly.FromDateTime(espera.Inicio), espera.Convidados));
        editor.ShowDialog(this);
        if (editor.OrcamentoSalvo() is > 0 and var orcamentoId)
            await _esperas.ConverterAsync(espera.Id, orcamentoId);
        await CarregarAsync();
    }
}
