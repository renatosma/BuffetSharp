using System.Globalization;
using System.Net;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Application.Servicos;

/// <summary>Quantos lembretes saíram sozinhos por e-mail e pelo WhatsApp.</summary>
public sealed record ResultadoLembretesAutomaticos(int Emails, int WhatsApps);

/// <summary>
/// Lembretes do dia: o que cada perfil precisa resolver hoje, com a mensagem pronta para o cliente (WhatsApp ou e-mail).
/// Depois de tratado, o lembrete some da lista (até a próxima ocorrência).
/// </summary>
public sealed class LembreteService(
    IBuffetDbContextFactory fabrica,
    IConsultasLembretes consultas,
    IEnvioEmail email,
    IEnvioWhatsApp whatsApp,
    WhatsAppOptions opcoesWhatsApp,
    BuffetOptions empresa,
    LembretesOptions opcoes,
    PixService pix,
    EsperaService esperas,
    PesquisaService pesquisas,
    ValidadeService validades,
    ISessaoUsuario sessao,
    TimeProvider relogio,
    ILogger<LembreteService> log)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    /// <summary>Tipos de lembrete com mensagem para o cliente (os demais são tarefas internas).</summary>
    public static bool TemMensagem(TipoLembrete tipo) => tipo is TipoLembrete.OrcamentoVencendo or TipoLembrete.Degustacao
        or TipoLembrete.Aniversario or TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida or TipoLembrete.RetornoLead
        or TipoLembrete.EsperaData or TipoLembrete.PesquisaSatisfacao or TipoLembrete.AvaliacaoGoogle;

    /// <summary>Perfis que cuidam de cada tipo de lembrete (o administrador vê todos).</summary>
    public static PerfilUsuario[] Perfis(TipoLembrete tipo) => tipo switch
    {
        TipoLembrete.OrcamentoVencendo or TipoLembrete.Degustacao or TipoLembrete.Aniversario or TipoLembrete.RetornoLead
            or TipoLembrete.EsperaData or TipoLembrete.PesquisaSatisfacao or TipoLembrete.AvaliacaoGoogle => [PerfilUsuario.Comercial],
        TipoLembrete.Detrator => [PerfilUsuario.Administrador],
        TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida or TipoLembrete.ContaPagarAVencer => [PerfilUsuario.Financeiro],
        _ => [PerfilUsuario.Operacional]
    };

    /// <summary>Lembretes de hoje do perfil do usuário logado, sem os já tratados.</summary>
    public async Task<IReadOnlyList<Lembrete>> ListarAsync(CancellationToken ct = default)
    {
        var usuario = sessao.Usuario;
        var todos = (await PendentesAsync(ct)).Where(l => usuario is null || usuario.PossuiPerfil(Perfis(l.Tipo))).ToList();
        return todos;
    }

    private async Task<List<Lembrete>> PendentesAsync(CancellationToken ct)
    {
        var lembretes = (await consultas.CalcularAsync(Agora,
            new LembretesOptionsConsulta(opcoes.DiasOrcamento, opcoes.DiasParcela, opcoes.DiasContaPagar), ct)).ToList();

        // A espera sai da lista quando o aviso é registrado (a fila anda sozinha), sem precisar de lembrete tratado.
        lembretes.AddRange(await esperas.LembretesAsync(ct));
        lembretes.AddRange(await pesquisas.LembretesAsync(ct));
        lembretes.AddRange(await validades.LembretesAsync(ct));
        if (lembretes.Count == 0)
            return [];

        var chaves = lembretes.Select(l => l.Chave).Distinct().ToList();
        await using var db = fabrica.Criar();
        var tratadas = new HashSet<string>();
        foreach (var bloco in chaves.Chunk(500))
            tratadas.UnionWith(await db.LembretesTratados.Where(t => bloco.Contains(t.Chave)).Select(t => t.Chave).ToListAsync(ct));
        return lembretes.Where(l => !tratadas.Contains(l.Chave)).ToList();
    }

    /// <summary>Tira o lembrete da lista (já resolvido). Numa tarefa de evento, conclui a tarefa.</summary>
    public async Task MarcarTratadoAsync(Lembrete lembrete, CanalLembrete canal, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        if (lembrete.Tipo == TipoLembrete.TarefaEvento && lembrete.EventoId is { } eventoId)
        {
            var evento = await db.Eventos.Include(e => e.Tarefas).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                         ?? throw new RegraNegocioException("Evento não encontrado.");
            evento.ConcluirTarefa(lembrete.ReferenciaId, sessao.Usuario?.Login, Agora);
            await db.SaveChangesAsync(ct);
            return;
        }

        // Espera e pesquisa guardam o andamento nelas mesmas (a fila anda, a pesquisa fica enviada), sem lembrete tratado.
        switch (lembrete.Tipo)
        {
            case TipoLembrete.EsperaData:
                await esperas.RegistrarAvisoAsync(lembrete.ReferenciaId, ct);
                return;
            case TipoLembrete.PesquisaSatisfacao:
                await pesquisas.RegistrarEnvioAsync(lembrete.ReferenciaId, ct);
                return;
            case TipoLembrete.AvaliacaoGoogle:
                await pesquisas.RegistrarPedidoAvaliacaoAsync(lembrete.ReferenciaId, ct);
                return;
            case TipoLembrete.Detrator:
                await pesquisas.TratarDetratorAsync(lembrete.ReferenciaId, $"Resolvido nos lembretes por {sessao.Usuario?.Login ?? "usuário"}.", ct);
                return;
        }

        if (await db.LembretesTratados.AnyAsync(t => t.Chave == lembrete.Chave, ct))
            return;
        db.LembretesTratados.Add(new LembreteTratado(lembrete.Chave, lembrete.Tipo, canal, sessao.Usuario?.Login, Agora));
        await db.SaveChangesAsync(ct);
    }

    // ------------------------- Mensagens -------------------------

    /// <summary>Mensagem pronta para o WhatsApp do cliente (o telefone pode vir vazio); nula quando o lembrete não tem mensagem.</summary>
    public async Task<MensagemWhatsApp?> PrepararWhatsAppAsync(Lembrete lembrete, CancellationToken ct = default) =>
        TemMensagem(lembrete.Tipo)
            ? new MensagemWhatsApp(lembrete.Telefone ?? string.Empty, Texto(lembrete, whatsApp: true, await PagamentoAsync(lembrete, ct)))
            : null;

    /// <summary>E-mail pronto para revisão; nulo quando o lembrete não tem mensagem.</summary>
    public async Task<EmailInput?> PrepararEmailAsync(Lembrete lembrete, CancellationToken ct = default) => TemMensagem(lembrete.Tipo)
        ? new EmailInput
        {
            Para = lembrete.Email ?? string.Empty,
            Assunto = Assunto(lembrete),
            Mensagem = Texto(lembrete, whatsApp: false, await PagamentoAsync(lembrete, ct))
        }
        : null;

    public async Task EnviarEmailAsync(Lembrete lembrete, EmailInput input, CancellationToken ct = default)
    {
        Validador.Garantir(System.Net.Mail.MailAddress.TryCreate(input.Para?.Trim(), out _), "Informe um e-mail válido para o destinatário.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(input.Assunto), "Informe o assunto do e-mail.");
        await email.EnviarAsync(new MensagemEmail(input.Para!.Trim(), input.Assunto.Trim(), Html(input.Mensagem),
            input.Anexo is null ? [] : [input.Anexo]), ct);
        await MarcarTratadoAsync(lembrete, CanalLembrete.Email, ct);
    }

    /// <summary>
    /// Envio automático dos lembretes ao cliente, cada um uma única vez: por e-mail (se ligado em "Lembretes:EmailAutomatico":
    /// parcela a vencer, degustação e aniversário) e pelo WhatsApp do buffet (se ligado em "Lembretes:WhatsAppAutomatico",
    /// com o envio direto configurado: também a parcela vencida, só no horário comercial). Fora do horário, o lembrete de quem
    /// tem celular espera o horário para sair pelos dois canais juntos.
    /// </summary>
    public async Task<ResultadoLembretesAutomaticos> EnviarAutomaticosAsync(CancellationToken ct = default)
    {
        var porWhatsApp = opcoes.WhatsAppAutomatico && whatsApp.Direto;
        if (!opcoes.EmailAutomatico && !porWhatsApp)
            return new ResultadoLembretesAutomaticos(0, 0);

        int emails = 0, mensagens = 0;
        var horario = opcoesWhatsApp.DentroDoHorario(Agora);
        foreach (var lembrete in (await PendentesAsync(ct)).Where(l => l.Tipo is TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida
                     or TipoLembrete.Degustacao or TipoLembrete.Aniversario or TipoLembrete.PesquisaSatisfacao))
        {
            var celular = porWhatsApp && new MensagemWhatsApp(lembrete.Telefone ?? "", "").TelefoneInternacional.Length is 12 or 13;
            var comEmail = opcoes.EmailAutomatico && lembrete.Tipo != TipoLembrete.ParcelaVencida &&
                           System.Net.Mail.MailAddress.TryCreate(lembrete.Email?.Trim(), out _);
            if (celular && !horario || !celular && !comEmail)
                continue;

            var canal = (CanalLembrete?)null;
            var pagamento = await PagamentoAsync(lembrete, ct);
            if (celular)
            {
                try
                {
                    await whatsApp.EnviarAsync(lembrete.Telefone!, Texto(lembrete, whatsApp: true, pagamento), ct: ct);
                    canal = CanalLembrete.WhatsAppAutomatico;
                    mensagens++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "Lembrete automático não enviado pelo WhatsApp para {Telefone} ({Chave})", lembrete.Telefone, lembrete.Chave);
                }
            }
            if (comEmail)
            {
                try
                {
                    await email.EnviarAsync(new MensagemEmail(lembrete.Email!.Trim(), Assunto(lembrete),
                        Html(Texto(lembrete, whatsApp: false, pagamento)), []), ct);
                    canal ??= CanalLembrete.EmailAutomatico;
                    emails++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "Lembrete automático não enviado para {Email} ({Chave})", lembrete.Email, lembrete.Chave);
                }
            }
            if (canal is { } enviado)
                await MarcarTratadoAsync(lembrete, enviado, ct);
        }
        if (emails + mensagens > 0)
            log.LogInformation("Lembretes automáticos: {Emails} e-mail(s) e {WhatsApps} WhatsApp(s)", emails, mensagens);
        return new ResultadoLembretesAutomaticos(emails, mensagens);
    }

    /// <summary>
    /// Como pagar a parcela do lembrete: o boleto/PIX registrado pendente (link da fatura, PIX ou linha digitável) ou, sem ele,
    /// o PIX estático do buffet. Nulo nos outros lembretes.
    /// </summary>
    private async Task<string?> PagamentoAsync(Lembrete l, CancellationToken ct)
    {
        if (l.Tipo is not (TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida))
            return null;
        await using var db = fabrica.Criar();
        var cobranca = await db.CobrancasRegistradas.AsNoTracking()
            .Where(c => c.ParcelaId == l.ReferenciaId && c.Status == StatusCobranca.Pendente)
            .Select(c => new { c.UrlFatura, c.PixCopiaECola, c.LinhaDigitavel, c.Vencimento }).FirstOrDefaultAsync(ct);
        if (cobranca is null)
            return pix.TextoCobranca(l.ReferenciaId, l.Valor ?? 0);
        if (cobranca.UrlFatura is { } url)
            return $"Boleto e PIX para pagamento: {url}";
        if (cobranca.PixCopiaECola is { } codigo)
            return $"Para pagar por PIX, use o código \"copia e cola\" abaixo no aplicativo do seu banco:\n{codigo}";
        return cobranca.LinhaDigitavel is { } linha ? $"Linha digitável do boleto (vence em {cobranca.Vencimento:dd/MM/yyyy}):\n{linha}" : null;
    }

    private string Assunto(Lembrete l) => l.Tipo switch
    {
        TipoLembrete.OrcamentoVencendo => $"Sua proposta ORC-{l.ReferenciaId:D5} está perto de vencer | {empresa.NomeEmpresa}",
        TipoLembrete.Degustacao => $"Lembrete da sua degustação em {l.Data:dd/MM} às {l.Data:HH:mm} | {empresa.NomeEmpresa}",
        TipoLembrete.Aniversario => $"Feliz aniversário! | {empresa.NomeEmpresa}",
        TipoLembrete.ParcelaAVencer => $"Parcela com vencimento em {l.Data:dd/MM/yyyy} | {empresa.NomeEmpresa}",
        TipoLembrete.ParcelaVencida => $"Parcela em aberto desde {l.Data:dd/MM/yyyy} | {empresa.NomeEmpresa}",
        TipoLembrete.EsperaData => $"A data que você queria ficou livre: {l.Data:dd/MM/yyyy} | {empresa.NomeEmpresa}",
        TipoLembrete.PesquisaSatisfacao => $"Como foi a sua festa? (1 minuto) | {empresa.NomeEmpresa}",
        TipoLembrete.AvaliacaoGoogle => $"Obrigado pela sua avaliação! | {empresa.NomeEmpresa}",
        _ => empresa.NomeEmpresa
    };

    private string Texto(Lembrete l, bool whatsApp, string? pagamento = null)
    {
        var nome = (l.Pessoa ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var negrito = whatsApp ? "*" : "";
        var valor = l.Valor is { } v ? v.ToString("C2", PtBr) : "";
        var texto = new StringBuilder($"Olá, {nome}! Aqui é do {negrito}{empresa.NomeEmpresa}{negrito}.\n\n");
        texto.Append(l.Tipo switch
        {
            TipoLembrete.OrcamentoVencendo =>
                $"A sua proposta {negrito}ORC-{l.ReferenciaId:D5}{negrito} ({l.Detalhe}) é válida até {l.Data:dd/MM/yyyy}. " +
                "A data ainda está reservada para você: podemos confirmar? Se quiser ajustar algo, é só falar.",
            TipoLembrete.Degustacao =>
                $"Passando para lembrar da sua degustação {(l.Data.Date == Agora.Date ? "hoje" : "amanhã")}, " +
                $"{l.Data.ToString("dddd, dd/MM", PtBr)}, às {l.Data:HH:mm}" +
                (empresa.Endereco is { } endereco ? $", no endereço {endereco}" : "") + ". Estamos esperando por você!",
            TipoLembrete.Aniversario =>
                $"Hoje é um dia especial: feliz aniversário! A equipe do {empresa.NomeEmpresa} deseja muita saúde e alegria. 🎉",
            TipoLembrete.ParcelaAVencer =>
                $"A parcela {negrito}{Descricao(l)}{negrito}, no valor de {valor}, vence em {l.Data:dd/MM/yyyy}.",
            TipoLembrete.ParcelaVencida =>
                $"Consta em aberto a parcela {negrito}{Descricao(l)}{negrito}, no valor de {valor}, com vencimento em {l.Data:dd/MM/yyyy}. " +
                "Se o pagamento já foi feito, por favor desconsidere esta mensagem e nos envie o comprovante.",
            TipoLembrete.EsperaData =>
                $"Boa notícia: a data que você queria para o seu evento, {negrito}{l.Data.ToString("dddd, dd/MM/yyyy", PtBr)}{negrito}, " +
                $"ficou disponível. Como você está na nossa lista de espera, a prioridade é sua por {empresa.DiasRespostaEspera} dia(s). " +
                "Vamos garantir a data?",
            TipoLembrete.PesquisaSatisfacao =>
                $"Esperamos que a sua festa{(l.Detalhe is { } festa ? $" ({festa.Split(" - ", 2).Last()})" : "")} tenha sido inesquecível! " +
                "Pode nos ajudar com 1 minuto?\n\n" +
                $"1) De 0 a 10, quanto você indicaria o {empresa.NomeEmpresa} a um amigo?\n" +
                "2) De 1 a 5, que nota você dá para:\n   a) comida e bebida\n   b) atendimento da equipe\n   c) espaço e decoração\n" +
                "   d) organização e pontualidade\n3) Quer deixar um comentário?\n\n" +
                "Pode responder só com os números, por exemplo: 10, 5, 5, 4, 5.",
            TipoLembrete.AvaliacaoGoogle =>
                "Muito obrigado pela nota! Ficamos felizes de ter feito parte da sua festa. Se puder, deixe também a sua avaliação no Google: " +
                $"{empresa.LinkAvaliacaoGoogle}\nIsso ajuda outras famílias a conhecerem o nosso trabalho.",
            TipoLembrete.RetornoLead =>
                "Tudo bem? Estou retornando sobre o seu evento" + (l.Detalhe is { } tipo ? $" ({tipo.ToLower(PtBr)})" : "") +
                ". Podemos conversar sobre datas, cardápios e valores?",
            _ => l.Titulo
        });
        if (pagamento is not null)
            texto.Append("\n\n").Append(pagamento);
        if (!string.IsNullOrWhiteSpace(empresa.Telefone) && l.Tipo != TipoLembrete.Aniversario)
            texto.Append($"\n\nQualquer dúvida, estamos à disposição: {empresa.Telefone}.");
        return texto.ToString();
    }

    private static string Descricao(Lembrete l) => l.Detalhe?.Split(" • ")[0] ?? "";

    private string Html(string? texto)
    {
        var corpo = new StringBuilder("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2b2b2b\">");
        foreach (var paragrafo in (texto ?? "").Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            corpo.Append("<p>").Append(WebUtility.HtmlEncode(paragrafo).Replace("\n", "<br>")).Append("</p>");
        corpo.Append($"<p style=\"color:#6e6a66\">{WebUtility.HtmlEncode(empresa.NomeEmpresa)}</p></div>");
        return corpo.ToString();
    }
}
