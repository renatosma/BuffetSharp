using System.Text;

namespace BuffetSharp.Application.Servicos;

/// <summary>Compromisso de agenda que vira um VEVENT do arquivo .ics.</summary>
/// <param name="Uid">Identificador fixo do compromisso: a agenda de quem recebe atualiza (ou remove) o mesmo item a cada envio.</param>
/// <param name="Sequencia">Aumenta a cada mudança, para a agenda aceitar a versão nova.</param>
/// <param name="EmailConvidado">Quem recebe o convite (vira ATTENDEE quando há e-mail).</param>
public sealed record CompromissoIcs(string Uid, int Sequencia, DateTime Inicio, DateTime Fim, string Titulo, string? Local, string? Descricao,
    bool Cancelado = false, string? EmailConvidado = null, string? NomeConvidado = null);

/// <summary>
/// Gera arquivos iCalendar (RFC 5545) que o Google Agenda, o Outlook e o celular importam: horário de Brasília, lembrete na véspera,
/// convite (REQUEST), cancelamento (CANCEL) ou só publicação (PUBLISH, na exportação da agenda).
/// </summary>
public static class ConviteIcs
{
    public const string Fuso = "America/Sao_Paulo";
    public const string TipoMime = "text/calendar";

    public static string Gerar(IReadOnlyCollection<CompromissoIcs> compromissos, string organizador, string? emailOrganizador, DateTime agoraUtc,
        bool publicacao = false)
    {
        var metodo = publicacao ? "PUBLISH" : compromissos.All(c => c.Cancelado) ? "CANCEL" : compromissos.Any(c => c.EmailConvidado is not null) ? "REQUEST" : "PUBLISH";
        var ics = new StringBuilder();
        void Linha(string texto) => Dobrar(ics, texto);

        Linha("BEGIN:VCALENDAR");
        Linha("VERSION:2.0");
        Linha("PRODID:-//BuffetSharp//Agenda//PT-BR");
        Linha("CALSCALE:GREGORIAN");
        Linha($"METHOD:{metodo}");
        Linha($"X-WR-CALNAME:{Escapar(organizador)}");
        Linha($"X-WR-TIMEZONE:{Fuso}");
        Linha("BEGIN:VTIMEZONE");
        Linha($"TZID:{Fuso}");
        Linha("BEGIN:STANDARD");
        Linha("DTSTART:19700101T000000");
        Linha("TZOFFSETFROM:-0300");
        Linha("TZOFFSETTO:-0300");
        Linha("TZNAME:-03");
        Linha("END:STANDARD");
        Linha("END:VTIMEZONE");

        foreach (var c in compromissos)
        {
            Linha("BEGIN:VEVENT");
            Linha($"UID:{c.Uid}");
            Linha($"SEQUENCE:{c.Sequencia}");
            Linha($"DTSTAMP:{agoraUtc:yyyyMMdd'T'HHmmss'Z'}");
            Linha($"DTSTART;TZID={Fuso}:{c.Inicio:yyyyMMdd'T'HHmmss}");
            Linha($"DTEND;TZID={Fuso}:{c.Fim:yyyyMMdd'T'HHmmss}");
            Linha($"SUMMARY:{Escapar(c.Titulo)}");
            if (!string.IsNullOrWhiteSpace(c.Local))
                Linha($"LOCATION:{Escapar(c.Local)}");
            if (!string.IsNullOrWhiteSpace(c.Descricao))
                Linha($"DESCRIPTION:{Escapar(c.Descricao)}");
            Linha($"STATUS:{(c.Cancelado ? "CANCELLED" : "CONFIRMED")}");
            Linha("TRANSP:OPAQUE");
            if (!publicacao && emailOrganizador is { Length: > 0 })
                Linha($"ORGANIZER;CN={Parametro(organizador)}:mailto:{emailOrganizador}");
            if (!publicacao && c.EmailConvidado is { Length: > 0 } email)
                Linha($"ATTENDEE;CN={Parametro(c.NomeConvidado ?? email)};ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:{email}");
            if (!c.Cancelado)
            {
                Linha("BEGIN:VALARM");
                Linha("TRIGGER:-P1D");
                Linha("ACTION:DISPLAY");
                Linha($"DESCRIPTION:{Escapar(c.Titulo)}");
                Linha("END:VALARM");
            }
            Linha("END:VEVENT");
        }
        Linha("END:VCALENDAR");
        return ics.ToString();
    }

    /// <summary>Texto: barra invertida, ponto e vírgula, vírgula e quebras de linha escapados.</summary>
    public static string Escapar(string texto) => texto.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .ReplaceLineEndings("\n").Replace("\n", "\\n");

    private static string Parametro(string texto) => "\"" + texto.Replace("\"", "'") + "\"";

    /// <summary>Linhas de no máximo 75 bytes (UTF-8), continuadas na seguinte com um espaço, terminadas em CRLF.</summary>
    private static void Dobrar(StringBuilder ics, string linha)
    {
        var bytes = 0;
        const int limite = 75;
        foreach (var caractere in linha.EnumerateRunes())
        {
            var tamanho = caractere.Utf8SequenceLength;
            if (bytes + tamanho > limite)
            {
                ics.Append("\r\n ");
                bytes = 1;
            }
            ics.Append(caractere.ToString());
            bytes += tamanho;
        }
        ics.Append("\r\n");
    }
}
