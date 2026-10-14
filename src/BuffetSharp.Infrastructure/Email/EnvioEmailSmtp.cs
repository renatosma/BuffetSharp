using System.Net;
using System.Net.Mail;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Infrastructure.Email;

/// <summary>
/// Envio por SMTP ou, no modo "Pasta", gravação de cada mensagem como arquivo .eml (pode ser aberto no
/// Outlook/Thunderbird) — permite usar o sistema antes de configurar um servidor de e-mail.
/// </summary>
internal sealed class EnvioEmailSmtp(EmailOptions opcoes) : IEnvioEmail
{
    public string Destino => opcoes.Modo == ModoEnvioEmail.Smtp
        ? $"servidor {opcoes.Servidor}:{opcoes.Porta}"
        : $"arquivo .eml na pasta \"{opcoes.Pasta}\" (modo de teste - configure o SMTP no appsettings.json)";

    /// <summary>Pasta onde as mensagens são gravadas no modo <see cref="ModoEnvioEmail.Pasta"/>.</summary>
    public string PastaSaida => Path.IsPathRooted(opcoes.Pasta) ? opcoes.Pasta : Path.Combine(AppContext.BaseDirectory, opcoes.Pasta);

    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct = default)
    {
        using var email = new MailMessage
        {
            From = new MailAddress(opcoes.Remetente, opcoes.NomeRemetente),
            Subject = mensagem.Assunto,
            Body = mensagem.CorpoHtml,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        email.To.Add(mensagem.Para);
        foreach (var anexo in mensagem.Anexos)
            email.Attachments.Add(new Attachment(new MemoryStream(anexo.Conteudo), anexo.NomeArquivo, anexo.TipoMime));

        using var smtp = new SmtpClient();
        if (opcoes.Modo == ModoEnvioEmail.Pasta)
        {
            Directory.CreateDirectory(PastaSaida);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = PastaSaida;
        }
        else
        {
            Validador.Garantir(!string.IsNullOrWhiteSpace(opcoes.Servidor),
                "O servidor de e-mail não está configurado (seção \"Email\" do appsettings.json).");
            smtp.Host = opcoes.Servidor!;
            smtp.Port = opcoes.Porta;
            smtp.EnableSsl = opcoes.UsarSsl;
            if (!string.IsNullOrWhiteSpace(opcoes.Usuario))
                smtp.Credentials = new NetworkCredential(opcoes.Usuario, opcoes.Senha);
        }

        try
        {
            await smtp.SendMailAsync(email, ct);
        }
        catch (SmtpException ex)
        {
            throw new RegraNegocioException($"Não foi possível enviar o e-mail: {ex.GetBaseException().Message}");
        }
    }
}
