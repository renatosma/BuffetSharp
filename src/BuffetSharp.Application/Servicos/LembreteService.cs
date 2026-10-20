using System.Globalization;
using System.Net;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Lembretes do dia: o que cada perfil precisa resolver hoje, com a mensagem pronta para o cliente (WhatsApp ou e-mail).
/// Depois de tratado, o lembrete some da lista (até a próxima ocorrência).
/// </summary>
public sealed class LembreteService(
    IBuffetDbContextFactory fabrica,
    IConsultasLembretes consultas,
    IEnvioEmail email,
    BuffetOptions empresa,
    LembretesOptions opcoes,
    PixService pix,
    ISessaoUsuario sessao,
    TimeProvider relogio,
    ILogger<LembreteService> log)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    /// <summary>Tipos de lembrete com mensagem para o cliente (os demais são tarefas internas).</summary>
    public static bool TemMensagem(TipoLembrete tipo) => tipo is TipoLembrete.OrcamentoVencendo or TipoLembrete.Degustacao
        or TipoLembrete.Aniversario or TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida or TipoLembrete.RetornoLead;

    /// <summary>Perfis que cuidam de cada tipo de lembrete (o administrador vê todos).</summary>
    public static PerfilUsuario[] Perfis(TipoLembrete tipo) => tipo switch
    {
        TipoLembrete.OrcamentoVencendo or TipoLembrete.Degustacao or TipoLembrete.Aniversario or TipoLembrete.RetornoLead =>
            [PerfilUsuario.Comercial],
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
        var lembretes = await consultas.CalcularAsync(Agora,
            new LembretesOptionsConsulta(opcoes.DiasOrcamento, opcoes.DiasParcela, opcoes.DiasContaPagar), ct);
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

        if (await db.LembretesTratados.AnyAsync(t => t.Chave == lembrete.Chave, ct))
            return;
        db.LembretesTratados.Add(new LembreteTratado(lembrete.Chave, lembrete.Tipo, canal, sessao.Usuario?.Login, Agora));
        await db.SaveChangesAsync(ct);
    }

    // ------------------------- Mensagens -------------------------

    /// <summary>Mensagem pronta para o WhatsApp do cliente (o telefone pode vir vazio); nula quando o lembrete não tem mensagem.</summary>
    public MensagemWhatsApp? PrepararWhatsApp(Lembrete lembrete) =>
        TemMensagem(lembrete.Tipo)
            ? new MensagemWhatsApp(lembrete.Telefone ?? string.Empty, Texto(lembrete, whatsApp: true))
            : null;

    /// <summary>E-mail pronto para revisão; nulo quando o lembrete não tem mensagem.</summary>
    public EmailInput? PrepararEmail(Lembrete lembrete) => TemMensagem(lembrete.Tipo)
        ? new EmailInput
        {
            Para = lembrete.Email ?? string.Empty,
            Assunto = Assunto(lembrete),
            Mensagem = Texto(lembrete, whatsApp: false)
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
    /// Envio automático (se ligado em "Lembretes:EmailAutomatico"): e-mail aos clientes com parcela a vencer, degustação
    /// marcada ou aniversário hoje. Cada lembrete é enviado uma única vez.
    /// </summary>
    /// <returns>Quantos e-mails foram enviados.</returns>
    public async Task<int> EnviarEmailsAutomaticosAsync(CancellationToken ct = default)
    {
        if (!opcoes.EmailAutomatico)
            return 0;

        var enviados = 0;
        var automaticos = (await PendentesAsync(ct)).Where(l =>
            l.Tipo is TipoLembrete.ParcelaAVencer or TipoLembrete.Degustacao or TipoLembrete.Aniversario &&
            System.Net.Mail.MailAddress.TryCreate(l.Email?.Trim(), out _));
        foreach (var lembrete in automaticos)
        {
            try
            {
                var mensagem = PrepararEmail(lembrete)!;
                await email.EnviarAsync(new MensagemEmail(lembrete.Email!.Trim(), mensagem.Assunto, Html(mensagem.Mensagem), []), ct);
                await MarcarTratadoAsync(lembrete, CanalLembrete.EmailAutomatico, ct);
                enviados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Lembrete automático não enviado para {Email} ({Chave})", lembrete.Email, lembrete.Chave);
            }
        }
        if (enviados > 0)
            log.LogInformation("{Quantidade} lembrete(s) enviado(s) por e-mail automaticamente", enviados);
        return enviados;
    }

    private string Assunto(Lembrete l) => l.Tipo switch
    {
        TipoLembrete.OrcamentoVencendo => $"Sua proposta ORC-{l.ReferenciaId:D5} está perto de vencer | {empresa.NomeEmpresa}",
        TipoLembrete.Degustacao => $"Lembrete da sua degustação em {l.Data:dd/MM} às {l.Data:HH:mm} | {empresa.NomeEmpresa}",
        TipoLembrete.Aniversario => $"Feliz aniversário! | {empresa.NomeEmpresa}",
        TipoLembrete.ParcelaAVencer => $"Parcela com vencimento em {l.Data:dd/MM/yyyy} | {empresa.NomeEmpresa}",
        TipoLembrete.ParcelaVencida => $"Parcela em aberto desde {l.Data:dd/MM/yyyy} | {empresa.NomeEmpresa}",
        _ => empresa.NomeEmpresa
    };

    private string Texto(Lembrete l, bool whatsApp)
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
            TipoLembrete.RetornoLead =>
                "Tudo bem? Estou retornando sobre o seu evento" + (l.Detalhe is { } tipo ? $" ({tipo.ToLower(PtBr)})" : "") +
                ". Podemos conversar sobre datas, cardápios e valores?",
            _ => l.Titulo
        });
        if (l.Tipo is TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida && pix.TextoCobranca(l.ReferenciaId, l.Valor ?? 0) is { } cobranca)
            texto.Append("\n\n").Append(cobranca);
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
