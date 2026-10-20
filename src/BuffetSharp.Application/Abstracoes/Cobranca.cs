namespace BuffetSharp.Application.Abstracoes;

/// <summary>Recebimento por PIX (seção "Pix" do appsettings.json). Sem chave, a cobrança por PIX fica desligada.</summary>
public sealed class PixOptions
{
    public const string Secao = "Pix";

    /// <summary>Chave PIX do buffet: CPF/CNPJ, e-mail, celular ou chave aleatória.</summary>
    public string? Chave { get; set; }

    /// <summary>Nome do recebedor como aparece no banco (até 25 caracteres; sem o nome, usa o da empresa).</summary>
    public string? NomeRecebedor { get; set; }

    /// <summary>Cidade do recebedor (até 15 caracteres).</summary>
    public string Cidade { get; set; } = "SAO PAULO";
}

/// <summary>Imagem do QR Code (PNG) de um texto.</summary>
public interface IGeradorQrCode
{
    byte[] Png(string texto, int pixelsPorModulo = 8);
}
