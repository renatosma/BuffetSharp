using System.Net;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Contratos do evento: geração da minuta a partir dos dados contratados, emissão (texto congelado com hash),
/// coleta de assinaturas, cancelamento/distrato e envio por e-mail.
/// </summary>
public sealed class ContratoService(
    IBuffetDbContextFactory fabrica,
    IConsultasEstoqueCardapio cardapio,
    IEnvioEmail email,
    BuffetOptions opcoes,
    ConviteService convites,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    public async Task<List<Contrato>> ListarDoEventoAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.Contratos.AsNoTracking().Where(c => c.EventoId == eventoId)
            .OrderByDescending(c => c.EmitidoEm).ToListAsync(ct);
    }

    public async Task<Contrato> ObterAsync(int contratoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.Contratos.AsNoTracking()
                   .Include(c => c.Evento!).ThenInclude(e => e.Cliente!).ThenInclude(c => c.Contatos)
                   .FirstOrDefaultAsync(c => c.Id == contratoId, ct)
               ?? throw new RegraNegocioException("Contrato não encontrado.");
    }

    /// <summary>Texto do contrato montado com os dados atuais do evento (pode ser revisado antes da emissão).</summary>
    public Task<string> GerarMinutaAsync(int eventoId, CancellationToken ct = default) => GerarMinutaAsync(eventoId, null, false, ct);

    /// <summary>
    /// Minuta a partir de um modelo de contrato. Sem modelo, usa o sugerido para o evento (o do tipo de evento ou o
    /// padrão); <paramref name="emBranco"/> começa um contrato novo, só com as partes e o fechamento.
    /// </summary>
    public async Task<string> GerarMinutaAsync(int eventoId, int? modeloContratoId, bool emBranco = false, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await CarregarEventoAsync(db, eventoId, ct);
        Validador.Garantir(evento.Status == StatusEvento.Confirmado, "Somente eventos confirmados podem ter contrato emitido.");

        string? modelo;
        if (emBranco)
            modelo = GeradorContrato.ModeloEmBranco;
        else if (modeloContratoId is { } id)
            modelo = (await db.ModelosContrato.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
                      ?? throw new RegraNegocioException("Modelo de contrato não encontrado.")).Conteudo;
        else
            modelo = (await ModeloSugeridoAsync(db, evento.TipoEventoId, ct))?.Conteudo;

        return await GerarAsync(evento, modelo, ct);
    }

    /// <summary>Modelo sugerido para o evento: o ativo do tipo do evento (o padrão primeiro) ou, na falta dele, o padrão.</summary>
    public async Task<int?> ModeloSugeridoAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var tipoEventoId = await db.Eventos.Where(e => e.Id == eventoId).Select(e => e.TipoEventoId).FirstOrDefaultAsync(ct);
        return (await ModeloSugeridoAsync(db, tipoEventoId, ct))?.Id;
    }

    /// <summary>Texto que um modelo (ainda não salvo) gera para o evento: pré-visualização na tela de modelos.</summary>
    public async Task<string> PreVisualizarAsync(int eventoId, string modelo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await GerarAsync(await CarregarEventoAsync(db, eventoId, ct), modelo, ct);
    }

    private static async Task<ModeloContrato?> ModeloSugeridoAsync(IBuffetDbContext db, int tipoEventoId, CancellationToken ct)
    {
        var modelos = await db.ModelosContrato.AsNoTracking()
            .Where(m => m.Ativo && (m.TipoEventoId == tipoEventoId || m.Padrao))
            .ToListAsync(ct);
        return modelos.OrderByDescending(m => m.TipoEventoId == tipoEventoId).ThenByDescending(m => m.Padrao).ThenBy(m => m.Id)
            .FirstOrDefault();
    }

    private static async Task<Evento> CarregarEventoAsync(IBuffetDbContext db, int eventoId, CancellationToken ct) =>
        await db.Eventos.AsNoTracking()
            .Include(e => e.Cliente!).ThenInclude(c => c.Contatos)
            .Include(e => e.TipoEvento)
            .Include(e => e.Espaco)
            .Include(e => e.Pacote)
            .Include(e => e.CondicaoPagamento!).ThenInclude(c => c.Parcelas)
            .Include(e => e.FormaPagamento)
            .Include(e => e.Servicos)
            .Include(e => e.Parcelas).ThenInclude(p => p.FormaPagamento)
            .FirstOrDefaultAsync(e => e.Id == eventoId, ct)
        ?? throw new RegraNegocioException("Evento não encontrado.");

    private async Task<string> GerarAsync(Evento evento, string? modelo, CancellationToken ct)
    {
        var composicao = evento.PacoteId is { } pacoteId ? await cardapio.ComposicaoPacoteAsync(pacoteId, ct) : [];
        return GeradorContrato.Gerar(opcoes, evento, composicao, DateOnly.FromDateTime(Agora), modelo);
    }

    /// <summary>Emite o contrato com o texto informado. Só pode haver um contrato vigente por evento.</summary>
    public Task<int> EmitirAsync(int eventoId, string conteudo, CancellationToken ct = default) => EmitirAsync(eventoId, conteudo, null, ct);

    /// <param name="modeloContratoId">Modelo que originou o texto (registrado no contrato).</param>
    public async Task<int> EmitirAsync(int eventoId, string conteudo, int? modeloContratoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        Validador.Garantir(evento.Status == StatusEvento.Confirmado, "Somente eventos confirmados podem ter contrato emitido.");

        var vigente = await db.Contratos.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EventoId == eventoId && c.Status != StatusContrato.Cancelado, ct);
        Validador.Garantir(vigente is null,
            $"O evento já possui o contrato {vigente?.Codigo} ({(vigente?.Status == StatusContrato.Assinado ? "assinado" : "aguardando assinatura")}). Cancele-o antes de emitir outro.");

        var contrato = new Contrato(eventoId, conteudo, Agora, modeloContratoId);
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync(ct);
        return contrato.Id;
    }

    public Task AssinarAsync(int contratoId, AssinaturaContratoInput assinatura, CancellationToken ct = default) =>
        AlterarAsync(contratoId, c => c.Assinar(assinatura.NomeSignatario, assinatura.DocumentoSignatario ?? "",
            assinatura.AssinaturaCliente ?? [], assinatura.NomeRepresentante, assinatura.AssinaturaEmpresa, Agora), ct);

    public Task CancelarAsync(int contratoId, string motivo, CancellationToken ct = default) =>
        AlterarAsync(contratoId, c => c.Cancelar(motivo, Agora), ct);

    // ------------------------- E-mail -------------------------

    public async Task<EmailInput> PrepararEmailAsync(int contratoId, CancellationToken ct = default)
    {
        var contrato = await ObterAsync(contratoId, ct);
        var cliente = contrato.Evento!.Cliente!;
        var assinado = contrato.Status == StatusContrato.Assinado;
        return new EmailInput
        {
            Para = cliente.Email ?? ValidacaoContatos.Primeiro(cliente, TipoContato.Email) ?? string.Empty,
            Assunto = $"{(assinado ? "Contrato assinado" : "Contrato para assinatura")} {contrato.Codigo} - {contrato.Evento.Titulo}",
            Mensagem = $"Olá, {cliente.Nome.Split(' ')[0]}!\n\n" +
                       (assinado
                           ? "Segue em anexo a via do contrato assinado do seu evento. Guarde este documento."
                           : "Segue em anexo o contrato do seu evento para leitura. A assinatura será coletada no nosso atendimento.") +
                       $"\n\nQualquer dúvida, estamos à disposição.\n{opcoes.NomeEmpresa}"
        };
    }

    public async Task EnviarEmailAsync(int contratoId, EmailInput input, CancellationToken ct = default)
    {
        var contrato = await ObterAsync(contratoId, ct);
        Validador.Garantir(contrato.Status != StatusContrato.Cancelado, "Contrato cancelado não pode ser enviado.");
        Validador.Garantir(System.Net.Mail.MailAddress.TryCreate(input.Para, out _), "Informe um e-mail válido para o destinatário.");

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2b2b2b\">");
        foreach (var paragrafo in (input.Mensagem ?? "").ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            html.Append("<p>").Append(WebUtility.HtmlEncode(paragrafo).Replace("\n", "<br>")).Append("</p>");
        html.Append($"<p style=\"color:#6e6a66;font-size:12px\">Contrato {contrato.Codigo} • código de integridade (SHA-256): {contrato.HashConteudo[..16]}…</p>");
        html.Append("</div>");

        // O e-mail do contrato leva o convite do evento para a agenda do cliente (atualizado, se o horário mudou).
        var convite = (await convites.DoEventoAsync(contrato.EventoId, ct)).First(c => c.Destino == DestinoConvite.Cliente);
        List<AnexoEmail> anexos = input.Anexo is null ? [] : [input.Anexo];
        if (!convite.Cancelado)
            anexos.Add(convites.Arquivo(convite));
        await email.EnviarAsync(new MensagemEmail(input.Para, input.Assunto, html.ToString(), anexos), ct);
        if (!convite.Cancelado)
            await convites.RegistrarAsync(convite, CanalConvite.Email, input.Para, ct);
    }

    private async Task AlterarAsync(int contratoId, Action<Contrato> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var contrato = await db.Contratos.FirstOrDefaultAsync(c => c.Id == contratoId, ct)
                       ?? throw new RegraNegocioException("Contrato não encontrado.");
        acao(contrato);
        await db.SaveChangesAsync(ct);
    }
}
