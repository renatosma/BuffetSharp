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
                   .Include(c => c.Evento!).ThenInclude(e => e.Cliente)
                   .FirstOrDefaultAsync(c => c.Id == contratoId, ct)
               ?? throw new RegraNegocioException("Contrato não encontrado.");
    }

    /// <summary>Texto do contrato montado com os dados atuais do evento (pode ser revisado antes da emissão).</summary>
    public async Task<string> GerarMinutaAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking()
                         .Include(e => e.Cliente)
                         .Include(e => e.TipoEvento)
                         .Include(e => e.Espaco)
                         .Include(e => e.Pacote)
                         .Include(e => e.Servicos)
                         .Include(e => e.Parcelas).ThenInclude(p => p.FormaPagamento)
                         .FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        Validador.Garantir(evento.Status == StatusEvento.Confirmado, "Somente eventos confirmados podem ter contrato emitido.");

        var composicao = evento.PacoteId is { } pacoteId ? await cardapio.ComposicaoPacoteAsync(pacoteId, ct) : [];
        return ModeloContrato.Gerar(opcoes, evento, composicao, DateOnly.FromDateTime(Agora));
    }

    /// <summary>Emite o contrato com o texto informado. Só pode haver um contrato vigente por evento.</summary>
    public async Task<int> EmitirAsync(int eventoId, string conteudo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        Validador.Garantir(evento.Status == StatusEvento.Confirmado, "Somente eventos confirmados podem ter contrato emitido.");

        var vigente = await db.Contratos.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EventoId == eventoId && c.Status != StatusContrato.Cancelado, ct);
        Validador.Garantir(vigente is null,
            $"O evento já possui o contrato {vigente?.Codigo} ({(vigente?.Status == StatusContrato.Assinado ? "assinado" : "aguardando assinatura")}). Cancele-o antes de emitir outro.");

        var contrato = new Contrato(eventoId, conteudo, Agora);
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
            Para = cliente.Email ?? string.Empty,
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

        await email.EnviarAsync(new MensagemEmail(input.Para, input.Assunto, html.ToString(),
            input.Anexo is null ? [] : [input.Anexo]), ct);
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
