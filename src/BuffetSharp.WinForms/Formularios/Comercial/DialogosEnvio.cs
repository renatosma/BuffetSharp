using System.Diagnostics;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Revisão e envio de documentos ao cliente por e-mail (com PDF anexo) ou WhatsApp.</summary>
internal static class DialogosEnvio
{
    /// <param name="nomeAnexo">O que vai anexado (ex.: "a proposta"); nulo para e-mail sem anexo.</param>
    /// <param name="gerarAnexo">Gera o PDF anexado quando a opção estiver marcada.</param>
    /// <param name="destino">Para onde o e-mail vai (servidor SMTP ou pasta), exibido ao usuário.</param>
    public static bool Email(IWin32Window dono, string titulo, EmailInput email, string? nomeAnexo, Func<AnexoEmail>? gerarAnexo,
        Func<EmailInput, Task> enviar, string destino)
    {
        var anexar = gerarAnexo is not null;
        var e = new EditorCampos(1);
        e.Texto("Para", () => email.Para, v => email.Para = v ?? "", nameof(email.Para), 150, dica: "email@cliente.com.br");
        e.Texto("Assunto", () => email.Assunto, v => email.Assunto = v ?? "", nameof(email.Assunto), 200);
        e.Texto("Mensagem", () => email.Mensagem, v => email.Mensagem = v, nameof(email.Mensagem), 4000, linhas: 9);
        if (gerarAnexo is not null)
            e.Marcador($"Anexar {nomeAnexo} (PDF)", () => anexar, v => anexar = v);

        using var dialogo = new FormEdicao(titulo, $"Envio: {destino}", e, async () =>
        {
            email.Anexo = anexar && gerarAnexo is not null ? gerarAnexo() : null;
            await enviar(email);
        }, 640);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>
    /// Mostra a mensagem para revisão e abre o WhatsApp (aplicativo ou WhatsApp Web) com o texto preenchido.
    /// Retorna o telefone usado, ou nulo se o usuário cancelar.
    /// </summary>
    public static string? WhatsApp(IWin32Window dono, MensagemWhatsApp mensagem, string assunto)
    {
        var telefone = mensagem.Telefone;
        var texto = mensagem.Texto;
        var e = new EditorCampos(1);
        e.Mascara("Celular do cliente", "(00) 00000-0000", () => telefone, v => telefone = v ?? "", "Telefone");
        e.Texto("Mensagem", () => texto, v => texto = v ?? "", "Texto", 4000, linhas: 14);

        using var dialogo = new FormEdicao($"Enviar {assunto} por WhatsApp",
            "O WhatsApp será aberto com a mensagem pronta; confirme o envio por lá.", e, () =>
            {
                var final = new MensagemWhatsApp(telefone, texto);
                if (final.TelefoneInternacional.Length < 12)
                    throw new Domain.Comum.RegraNegocioException("Informe o celular do cliente com DDD.");
                Process.Start(new ProcessStartInfo(final.Link) { UseShellExecute = true });
                return Task.CompletedTask;
            }, 600);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? telefone : null;
    }

    /// <summary>Anexo PDF a partir do documento impresso.</summary>
    public static AnexoEmail Pdf(string nomeArquivo, byte[] conteudo) =>
        new(string.Concat(nomeArquivo.Split(Path.GetInvalidFileNameChars())) + ".pdf", conteudo);
}
