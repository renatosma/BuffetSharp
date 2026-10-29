using System.Net;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public enum DestinoConvite
{
    Cliente,
    Equipe,
    Fornecedor,
    Degustacao
}

/// <summary>Convite de agenda de uma pessoa: o compromisso atual e o último envio (para saber se ficou desatualizado).</summary>
public sealed record ConviteAgenda(string Uid, DestinoConvite Destino, string Nome, string? Email, string? Telefone, string Titulo, DateTime Inicio,
    DateTime Fim, string? Local, string Descricao, bool Cancelado, int? EventoId, int? DegustacaoId, ConviteEnviado? UltimoEnvio)
{
    /// <summary>Já recebeu o convite, mas o horário mudou ou o compromisso foi cancelado depois.</summary>
    public bool Desatualizado => UltimoEnvio is { } envio && envio.Diferente(Inicio, Fim, Cancelado);

    /// <summary>Precisa de envio: nunca recebeu (e o compromisso vale) ou está desatualizado.</summary>
    public bool Pendente => UltimoEnvio is null ? !Cancelado : Desatualizado;

    public int Sequencia => UltimoEnvio is null ? 0 : Desatualizado ? UltimoEnvio.Sequencia + 1 : UltimoEnvio.Sequencia;

    public string Situacao => UltimoEnvio is not { } envio
        ? Cancelado ? "Cancelado (não tinha sido enviado)" : "Não enviado"
        : Desatualizado ? Cancelado ? "Cancelado: avisar quem recebeu" : $"Desatualizado (enviado {envio.Inicio:dd/MM HH:mm})"
        : $"Enviado em {envio.EnviadoEm:dd/MM HH:mm} ({envio.Canal})";
}

/// <summary>Resultado do envio dos convites pendentes do evento.</summary>
public sealed record ResultadoConvites(int Emails, int WhatsApps, IReadOnlyList<string> SemCanal);

/// <summary>
/// Convites de agenda (.ics) do evento (cliente, equipe escalada e fornecedores) e da degustação, por e-mail ou WhatsApp. Cada
/// compromisso tem um identificador fixo: quando o horário muda ou o evento é cancelado, o envio seguinte atualiza (ou remove)
/// o mesmo item na agenda de quem recebeu. Também exporta a agenda do buffet para importar no Google Agenda ou Outlook.
/// </summary>
public sealed class ConviteService(IBuffetDbContextFactory fabrica, IEnvioEmail email, IEnvioWhatsApp whatsApp, BuffetOptions empresa,
    ISessaoUsuario sessao, TimeProvider relogio)
{
    private const int DuracaoDegustacaoMinutos = 90;
    private const string Dominio = "buffetsharp";

    private DateTime Agora => relogio.GetLocalNow().DateTime;

    public static string UidCliente(int eventoId) => $"evento-{eventoId}-cliente@{Dominio}";
    public static string UidEquipe(int eventoId, int funcionarioId) => $"evento-{eventoId}-equipe-{funcionarioId}@{Dominio}";
    public static string UidFornecedor(int eventoId, int contratacaoId) => $"evento-{eventoId}-fornecedor-{contratacaoId}@{Dominio}";
    public static string UidDegustacao(int degustacaoId) => $"degustacao-{degustacaoId}@{Dominio}";

    /// <summary>
    /// Convites do evento: o cliente, cada funcionário escalado (da montagem à desmontagem) e cada fornecedor (da chegada ao fim).
    /// Quem recebeu e saiu (da escala, contratação cancelada) aparece como cancelado, para a agenda dele ser limpa.
    /// </summary>
    public async Task<IReadOnlyList<ConviteAgenda>> DoEventoAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var e = await db.Eventos.AsNoTrackingWithIdentityResolution()
                    .Include(x => x.Cliente).Include(x => x.Espaco).Include(x => x.Etapas)
                    .Include(x => x.Equipe).ThenInclude(q => q.Funcionario)
                    .Include(x => x.Fornecedores).ThenInclude(f => f.Fornecedor)
                    .Include(x => x.Fornecedores).ThenInclude(f => f.EtapaChegada)
                    .FirstOrDefaultAsync(x => x.Id == eventoId, ct)
                ?? throw new RegraNegocioException("Evento não encontrado.");
        var envios = await UltimosEnviosAsync(db, db.ConvitesEnviados.Where(c => c.EventoId == eventoId), ct);
        var cancelado = e.Status == StatusEvento.Cancelado;
        var local = Local(e);
        var contatoBuffet = empresa.Telefone is { Length: > 0 } tel ? $" Contato do {empresa.NomeEmpresa}: {tel}." : "";

        var convites = new List<ConviteAgenda>();
        var cliente = e.Cliente!;
        convites.Add(new ConviteAgenda(UidCliente(e.Id), DestinoConvite.Cliente, cliente.Nome, cliente.Email, cliente.Celular ?? cliente.Telefone,
            $"{e.Titulo} - {empresa.NomeEmpresa}", e.Inicio, e.Fim, local,
            $"Seu evento no {empresa.NomeEmpresa} ({e.Codigo}), para {e.QuantidadeConvidados} convidados.{contatoBuffet}", cancelado, e.Id, null,
            envios.GetValueOrDefault(UidCliente(e.Id))));

        var trabalho = PeriodoTrabalho.Do(e);
        foreach (var escala in e.Equipe.Where(q => q.Funcionario is not null))
        {
            var f = escala.Funcionario!;
            var uid = UidEquipe(e.Id, f.Id);
            convites.Add(new ConviteAgenda(uid, DestinoConvite.Equipe, f.Nome, f.Email, f.Telefone, $"Trabalho: {e.Titulo} ({escala.Funcao})",
                trabalho.Inicio, trabalho.Fim, local,
                $"{e.Codigo} - {e.Titulo}. Chegada às {trabalho.Inicio:HH:mm} (montagem); festa das {e.Inicio:HH:mm} às {e.Fim:HH:mm}. " +
                $"Função: {escala.Funcao}.{contatoBuffet}", cancelado, e.Id, null, envios.GetValueOrDefault(uid)));
        }

        foreach (var contratacao in e.Fornecedores.Where(f => f.Ativo))
        {
            var uid = UidFornecedor(e.Id, contratacao.Id);
            var fornecedor = contratacao.Fornecedor!;
            var chegada = contratacao.Chegada ?? e.Inicio;
            convites.Add(new ConviteAgenda(uid, DestinoConvite.Fornecedor, fornecedor.NomeExibicao, fornecedor.Email, contratacao.Telefone ?? fornecedor.Telefone,
                $"{contratacao.Servico}: {e.Titulo}", chegada, e.Fim > chegada ? e.Fim : chegada.AddHours(1), local,
                $"{e.Codigo} - {e.Titulo}, {e.QuantidadeConvidados} convidados. Chegada às {chegada:HH:mm}; festa das {e.Inicio:HH:mm} às {e.Fim:HH:mm}." +
                contatoBuffet, cancelado, e.Id, null, envios.GetValueOrDefault(uid)));
        }

        // Quem recebeu o convite e não está mais no evento (saiu da escala, contratação cancelada): o compromisso é cancelado.
        foreach (var envio in envios.Values.Where(v => convites.All(c => c.Uid != v.Uid)))
        {
            var porEmail = envio.Contato?.Contains('@') == true;
            convites.Add(new ConviteAgenda(envio.Uid, envio.Uid.Contains("-fornecedor-") ? DestinoConvite.Fornecedor : DestinoConvite.Equipe,
                envio.Destinatario, porEmail ? envio.Contato : null, porEmail ? null : envio.Contato, $"{e.Titulo} (cancelado)", envio.Inicio, envio.Fim,
                local, $"{e.Codigo} - {e.Titulo}: você não está mais neste evento.", true, e.Id, null, envio));
        }
        return convites;
    }

    public async Task<ConviteAgenda> DaDegustacaoAsync(int degustacaoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var d = await db.Degustacoes.AsNoTracking().Include(x => x.Cliente).Include(x => x.Pacote).FirstOrDefaultAsync(x => x.Id == degustacaoId, ct)
                ?? throw new RegraNegocioException("Degustação não encontrada.");
        var envios = await UltimosEnviosAsync(db, db.ConvitesEnviados.Where(c => c.DegustacaoId == degustacaoId), ct);
        var uid = UidDegustacao(d.Id);
        return new ConviteAgenda(uid, DestinoConvite.Degustacao, d.Cliente!.Nome, d.Cliente.Email, d.Cliente.Celular ?? d.Cliente.Telefone,
            $"Degustação - {empresa.NomeEmpresa}", d.DataHora, d.DataHora.AddMinutes(DuracaoDegustacaoMinutos), empresa.Endereco,
            $"Degustação para {d.Pessoas} pessoa(s)" + (d.Pacote is { } pacote ? $", pacote {pacote.Nome}" : "") + "." +
            (empresa.Telefone is { Length: > 0 } tel ? $" Contato: {tel}." : ""),
            d.Status == StatusDegustacao.Cancelada, null, d.Id, envios.GetValueOrDefault(uid));
    }

    /// <summary>O arquivo .ics do convite (atualização ou cancelamento, conforme o compromisso).</summary>
    public AnexoEmail Arquivo(ConviteAgenda convite)
    {
        var ics = ConviteIcs.Gerar([Compromisso(convite)], empresa.NomeEmpresa, empresa.Email, relogio.GetUtcNow().UtcDateTime);
        var nome = convite.Destino == DestinoConvite.Degustacao ? "degustacao" : convite.Cancelado ? "cancelamento" : "convite";
        return new AnexoEmail($"{nome}.ics", Encoding.UTF8.GetBytes(ics), ConviteIcs.TipoMime);
    }

    /// <summary>Texto da mensagem que acompanha o convite (WhatsApp ou corpo do e-mail).</summary>
    /// <param name="comAnexo">Falso quando o .ics não vai junto (WhatsApp aberto no celular): a mensagem leva só a data e o horário.</param>
    public string Mensagem(ConviteAgenda c, bool comAnexo = true)
    {
        var nome = c.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var quando = c.Inicio.Date == c.Fim.Date
            ? $"{c.Inicio.ToString("dddd, dd/MM/yyyy", Textos)}, das {c.Inicio:HH:mm} às {c.Fim:HH:mm}"
            : $"de {c.Inicio:dd/MM HH:mm} a {c.Fim:dd/MM HH:mm}";
        var texto = new StringBuilder($"Olá, {nome}! Aqui é do {empresa.NomeEmpresa}.\n\n");
        texto.Append(c.Cancelado
            ? $"O compromisso \"{c.Titulo}\" ({quando}) foi cancelado." + (comAnexo ? " O arquivo anexo tira ele da sua agenda." : "")
            : (c.Desatualizado ? "O horário mudou: " : "Segue o convite para a sua agenda: ") + $"{c.Titulo}, {quando}" +
              (c.Local is { } local ? $", em {local}" : "") +
              (comAnexo ? ". Abra o arquivo anexo para salvar na agenda do celular ou do computador." : "."));
        if (!c.Cancelado)
            texto.Append($"\n\n{c.Descricao}");
        return texto.ToString();
    }

    public string Assunto(ConviteAgenda c) => c.Cancelado ? $"Cancelado: {c.Titulo}" : c.Desatualizado ? $"Atualizado: {c.Titulo}" : $"Convite: {c.Titulo}";

    public async Task EnviarEmailAsync(ConviteAgenda convite, string para, string? assunto = null, string? mensagem = null, CancellationToken ct = default)
    {
        Validador.Garantir(System.Net.Mail.MailAddress.TryCreate(para?.Trim(), out _), $"Informe um e-mail válido para {convite.Nome}.");
        var corpo = new StringBuilder("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2b2b2b\">");
        foreach (var paragrafo in (mensagem ?? Mensagem(convite)).ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            corpo.Append("<p>").Append(WebUtility.HtmlEncode(paragrafo).Replace("\n", "<br>")).Append("</p>");
        corpo.Append("</div>");
        await email.EnviarAsync(new MensagemEmail(para!.Trim(), assunto ?? Assunto(convite), corpo.ToString(), [Arquivo(convite)]), ct);
        await RegistrarAsync(convite, CanalConvite.Email, para.Trim(), ct);
    }

    /// <summary>Envia pelo WhatsApp do buffet (Z-API), com o .ics anexo. Só no envio direto.</summary>
    public async Task EnviarWhatsAppAsync(ConviteAgenda convite, string telefone, CancellationToken ct = default)
    {
        Validador.Garantir(whatsApp.Direto, "O WhatsApp do buffet não está configurado para envio direto.");
        await whatsApp.EnviarAsync(telefone, Mensagem(convite), Arquivo(convite), ct);
        await RegistrarAsync(convite, CanalConvite.WhatsApp, telefone, ct);
    }

    /// <summary>Registra o envio feito por fora (WhatsApp aberto no celular, arquivo salvo).</summary>
    public async Task RegistrarAsync(ConviteAgenda c, CanalConvite canal, string? contato, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        db.ConvitesEnviados.Add(new ConviteEnviado(c.Uid, c.Sequencia, c.Inicio, c.Fim, c.Cancelado, c.Nome, contato, canal, sessao.Usuario?.Login, Agora,
            c.EventoId, c.DegustacaoId));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Envia os convites pendentes do evento (os que nunca foram e os desatualizados): por e-mail a quem tem, pelo WhatsApp direto
    /// a quem só tem celular. Retorna quem ficou sem canal.
    /// </summary>
    public async Task<ResultadoConvites> EnviarPendentesAsync(int eventoId, CancellationToken ct = default)
    {
        int emails = 0, mensagens = 0;
        var semCanal = new List<string>();
        foreach (var convite in (await DoEventoAsync(eventoId, ct)).Where(c => c.Pendente))
        {
            if (System.Net.Mail.MailAddress.TryCreate(convite.Email?.Trim(), out _))
            {
                await EnviarEmailAsync(convite, convite.Email!, ct: ct);
                emails++;
            }
            else if (whatsApp.Direto && !string.IsNullOrWhiteSpace(convite.Telefone))
            {
                await EnviarWhatsAppAsync(convite, convite.Telefone!, ct);
                mensagens++;
            }
            else
                semCanal.Add(convite.Nome);
        }
        return new ResultadoConvites(emails, mensagens, semCanal);
    }

    /// <summary>
    /// Agenda do buffet no período (eventos não cancelados e degustações agendadas) num arquivo .ics para importar no Google Agenda ou
    /// no Outlook da equipe. Reimportar o arquivo atualiza os mesmos compromissos.
    /// </summary>
    public async Task<string> AgendaAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await using var db = fabrica.Criar();
        var eventos = await db.Eventos.AsNoTracking().Include(e => e.Cliente).Include(e => e.Espaco).Include(e => e.TipoEvento)
            .Where(e => e.Status != StatusEvento.Cancelado && e.Inicio >= inicio && e.Inicio < fim).OrderBy(e => e.Inicio).ToListAsync(ct);
        var degustacoes = await db.Degustacoes.AsNoTracking().Include(d => d.Cliente)
            .Where(d => d.Status == StatusDegustacao.Agendada && d.DataHora >= inicio && d.DataHora < fim).ToListAsync(ct);

        var compromissos = eventos.Select(e => new CompromissoIcs($"agenda-evento-{e.Id}@{Dominio}", e.Versao, e.Inicio, e.Fim,
                $"{e.Codigo} {e.Titulo} - {e.Espaco?.Nome}", Local(e),
                $"{e.TipoEvento?.Nome} de {e.Cliente?.Nome}, {e.QuantidadeConvidados} convidados. Situação: {e.Status}."))
            .Concat(degustacoes.Select(d => new CompromissoIcs($"agenda-degustacao-{d.Id}@{Dominio}", d.Versao, d.DataHora,
                d.DataHora.AddMinutes(DuracaoDegustacaoMinutos), $"Degustação: {d.Cliente?.Nome}", empresa.Endereco, $"{d.Pessoas} pessoa(s).")))
            .ToList();
        return ConviteIcs.Gerar(compromissos, empresa.NomeEmpresa, empresa.Email, relogio.GetUtcNow().UtcDateTime, publicacao: true);
    }

    private CompromissoIcs Compromisso(ConviteAgenda c) => new(c.Uid, c.Sequencia, c.Inicio, c.Fim, c.Titulo, c.Local, c.Descricao, c.Cancelado,
        System.Net.Mail.MailAddress.TryCreate(c.Email?.Trim(), out _) ? c.Email!.Trim() : null, c.Nome);

    private string? Local(Evento e)
    {
        var endereco = e.Espaco?.Endereco is { Logradouro.Length: > 0 } doEspaco ? doEspaco.ToString() : empresa.Endereco;
        return e.Espaco is null ? endereco : string.IsNullOrWhiteSpace(endereco) ? e.Espaco.Nome : $"{e.Espaco.Nome} - {endereco}";
    }

    private static async Task<Dictionary<string, ConviteEnviado>> UltimosEnviosAsync(IBuffetDbContext db, IQueryable<ConviteEnviado> consulta,
        CancellationToken ct) =>
        (await consulta.AsNoTracking().ToListAsync(ct)).GroupBy(c => c.Uid)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EnviadoEm).ThenByDescending(c => c.Id).First());

    private static readonly System.Globalization.CultureInfo Textos = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
}
