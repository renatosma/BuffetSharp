using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Envio de um convite de agenda (.ics) por e-mail, WhatsApp ou arquivo, com o registro do envio.</summary>
internal static class DialogosConvite
{
    /// <returns>Verdadeiro se o convite foi enviado (ou salvo).</returns>
    public static bool Email(IWin32Window dono, ConviteService convites, IEnvioEmail email, ConviteAgenda convite)
    {
        var envio = new EmailInput { Para = convite.Email ?? "", Assunto = convites.Assunto(convite), Mensagem = convites.Mensagem(convite) };
        return DialogosEnvio.Email(dono, $"Convite de agenda - {convite.Nome}", envio, null, null,
            m => convites.EnviarEmailAsync(convite, m.Para, m.Assunto, m.Mensagem), email.Destino);
    }

    public static async Task<bool> WhatsAppAsync(IWin32Window dono, ConviteService convites, ConviteAgenda convite)
    {
        var direto = DialogosEnvio.EnvioDireto;
        var telefone = DialogosEnvio.WhatsApp(dono, new MensagemWhatsApp(convite.Telefone ?? "", convites.Mensagem(convite, comAnexo: direto)),
            "o convite de agenda", "o convite", () => convites.Arquivo(convite), "agenda .ics");
        if (telefone is null)
            return false;
        await convites.RegistrarAsync(convite, CanalConvite.WhatsApp, telefone);
        return true;
    }

    /// <summary>Salva o .ics para entregar por fora (pendrive, outro aplicativo).</summary>
    public static async Task<bool> SalvarAsync(IWin32Window dono, ConviteService convites, ConviteAgenda convite)
    {
        var arquivo = convites.Arquivo(convite);
        using var salvar = new SaveFileDialog { FileName = arquivo.NomeArquivo, Filter = "Convite de agenda (*.ics)|*.ics", Title = "Salvar convite" };
        if (salvar.ShowDialog(dono) != DialogResult.OK)
            return false;
        await File.WriteAllBytesAsync(salvar.FileName, arquivo.Conteudo);
        await convites.RegistrarAsync(convite, CanalConvite.Arquivo, null);
        return true;
    }
}
