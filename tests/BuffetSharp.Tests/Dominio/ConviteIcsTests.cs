using System.Text;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Convite de agenda (.ics): formato iCalendar, fuso de Brasília, atualização pela sequência e cancelamento.</summary>
public class ConviteIcsTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Inicio = new(2026, 6, 20, 19, 0, 0);

    private static CompromissoIcs Compromisso(bool cancelado = false, string? email = "ana@cliente.com.br") =>
        new("evento-7-cliente@buffetsharp", 2, Inicio, Inicio.AddHours(4), "Casamento Ana, Bruno; festa", "Salão Cristal - Av. das Festas, 1000",
            "Linha 1\nLinha 2", cancelado, email, "Ana Souza");

    [Fact]
    public void Convite_tem_o_evento_no_fuso_de_brasilia_com_lembrete_e_convidado()
    {
        var ics = ConviteIcs.Gerar([Compromisso()], "BuffetSharp Eventos", "contato@buffet.com.br", Agora);

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.Contains("METHOD:REQUEST\r\n", ics);
        Assert.Contains("TZID:America/Sao_Paulo\r\n", ics);
        Assert.Contains("DTSTART;TZID=America/Sao_Paulo:20260620T190000\r\n", ics);
        Assert.Contains("DTEND;TZID=America/Sao_Paulo:20260620T230000\r\n", ics);
        Assert.Contains("DTSTAMP:20260301T130000Z\r\n", ics);
        Assert.Contains("UID:evento-7-cliente@buffetsharp\r\nSEQUENCE:2\r\n", ics);
        Assert.Contains(@"SUMMARY:Casamento Ana\, Bruno\; festa", ics);
        Assert.Contains(@"DESCRIPTION:Linha 1\nLinha 2", ics);
        Assert.Contains("STATUS:CONFIRMED", ics);
        Assert.Contains("BEGIN:VALARM\r\nTRIGGER:-P1D", ics);
        Assert.Contains("mailto:contato@buffet.com.br", ics);
        Assert.Contains("mailto:ana@cliente.com.br", ics.Replace("\r\n ", ""));

        // Nenhuma linha passa de 75 bytes; as continuações começam com espaço.
        Assert.All(ics.Split("\r\n"), linha => Assert.True(Encoding.UTF8.GetByteCount(linha) <= 75, linha));
    }

    [Fact]
    public void Cancelamento_e_publicacao()
    {
        var cancelamento = ConviteIcs.Gerar([Compromisso(cancelado: true)], "Buffet", "contato@buffet.com.br", Agora);
        Assert.Contains("METHOD:CANCEL", cancelamento);
        Assert.Contains("STATUS:CANCELLED", cancelamento);
        Assert.DoesNotContain("VALARM", cancelamento);

        var semEmail = ConviteIcs.Gerar([Compromisso(email: null)], "Buffet", "contato@buffet.com.br", Agora);
        Assert.Contains("METHOD:PUBLISH", semEmail);
        Assert.DoesNotContain("ATTENDEE", semEmail);

        var agenda = ConviteIcs.Gerar([Compromisso(), Compromisso() with { Uid = "outro@buffetsharp" }], "Buffet", "contato@buffet.com.br", Agora,
            publicacao: true);
        Assert.Contains("METHOD:PUBLISH", agenda);
        Assert.DoesNotContain("ORGANIZER", agenda);
        Assert.Equal(2, agenda.Split("BEGIN:VEVENT").Length - 1);
    }

    [Fact]
    public void Convite_fica_desatualizado_quando_o_horario_muda_e_a_sequencia_sobe()
    {
        var envio = new ConviteEnviado("evento-7-cliente@buffetsharp", 0, Inicio, Inicio.AddHours(4), false, "Ana", "ana@cliente.com.br",
            CanalConvite.Email, "admin", Agora, eventoId: 7);
        ConviteAgenda Convite(DateTime inicio, bool cancelado = false, ConviteEnviado? ultimo = null) =>
            new(envio.Uid, DestinoConvite.Cliente, "Ana", null, null, "Casamento", inicio, inicio.AddHours(4), null, "", cancelado, 7, null, ultimo);

        var novo = Convite(Inicio);
        Assert.Equal((true, false, 0, "Não enviado"), (novo.Pendente, novo.Desatualizado, novo.Sequencia, novo.Situacao));

        var emDia = Convite(Inicio, ultimo: envio);
        Assert.Equal((false, false, 0), (emDia.Pendente, emDia.Desatualizado, emDia.Sequencia));

        var mudou = Convite(Inicio.AddHours(1), ultimo: envio);
        Assert.Equal((true, true, 1), (mudou.Pendente, mudou.Desatualizado, mudou.Sequencia));

        var cancelado = Convite(Inicio, cancelado: true, ultimo: envio);
        Assert.Equal((true, "Cancelado: avisar quem recebeu"), (cancelado.Pendente, cancelado.Situacao));
        Assert.False(Convite(Inicio, cancelado: true).Pendente);
        Assert.Throws<Domain.Comum.RegraNegocioException>(() =>
            new ConviteEnviado("x", 0, Inicio, Inicio, false, "Ana", null, CanalConvite.Email, null, Agora));
    }
}
