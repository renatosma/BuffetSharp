namespace BuffetSharp.Application.Abstracoes;

public enum ModoEnvioEmail
{
    /// <summary>Grava cada mensagem como arquivo .eml em uma pasta (útil para testes e para quem ainda não configurou o SMTP).</summary>
    Pasta,

    /// <summary>Envia pelo servidor SMTP configurado.</summary>
    Smtp
}

/// <summary>Configuração de e-mail (seção "Email" do appsettings.json).</summary>
public sealed class EmailOptions
{
    public const string Secao = "Email";

    public ModoEnvioEmail Modo { get; set; } = ModoEnvioEmail.Pasta;

    /// <summary>Pasta de saída no modo <see cref="ModoEnvioEmail.Pasta"/> (relativa à pasta do sistema).</summary>
    public string Pasta { get; set; } = "EmailsEnviados";

    public string? Servidor { get; set; }
    public int Porta { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string? Usuario { get; set; }
    public string? Senha { get; set; }
    public string Remetente { get; set; } = "contato@buffetsharp.exemplo.com.br";
    public string? NomeRemetente { get; set; }
}

public sealed record AnexoEmail(string NomeArquivo, byte[] Conteudo, string TipoMime = "application/pdf");

public sealed record MensagemEmail(string Para, string Assunto, string CorpoHtml, IReadOnlyList<AnexoEmail> Anexos);

/// <summary>Envio de e-mails (SMTP ou pasta local, conforme <see cref="EmailOptions"/>).</summary>
public interface IEnvioEmail
{
    /// <summary>Descreve o destino atual (ex.: "smtp.empresa.com:587" ou a pasta de saída).</summary>
    string Destino { get; }

    Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct = default);
}
