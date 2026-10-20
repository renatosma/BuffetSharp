using System.Globalization;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Application.Servicos;

/// <summary>Cobrança PIX pronta: o código "copia e cola" e o QR Code (PNG) com o mesmo conteúdo.</summary>
public sealed record CobrancaPix(string CopiaECola, byte[] QrCodePng, decimal Valor, string Identificador);

/// <summary>
/// PIX estático com valor (BR Code do Banco Central): o cliente paga lendo o QR Code ou colando o código no aplicativo do
/// banco. O identificador (txid) aparece no extrato e ajuda a localizar o pagamento.
/// </summary>
public sealed class PixService(PixOptions opcoes, BuffetOptions empresa, IGeradorQrCode qrCode)
{
    public bool Configurado => !string.IsNullOrWhiteSpace(opcoes.Chave);

    public const string MensagemNaoConfigurado =
        "Informe a chave PIX do buffet na seção \"Pix\" do appsettings.json (Chave, NomeRecebedor e Cidade) para cobrar por PIX.";

    public CobrancaPix Gerar(decimal valor, string identificador, string? descricao = null)
    {
        Validador.Garantir(Configurado, MensagemNaoConfigurado);
        var codigo = BrCodePix.Montar(opcoes.Chave!, opcoes.NomeRecebedor ?? empresa.NomeEmpresa, opcoes.Cidade, valor, identificador, descricao);
        return new CobrancaPix(codigo, qrCode.Png(codigo), valor, BrCodePix.Identificador(identificador));
    }

    /// <summary>Cobrança de uma parcela a receber (identificador "PARC" + número da parcela).</summary>
    public CobrancaPix Parcela(int parcelaId, decimal valor, string? descricao = null) =>
        Gerar(valor, $"PARC{parcelaId:D6}", descricao);

    /// <summary>Texto para incluir em mensagens de cobrança (WhatsApp/e-mail); nulo se o PIX não estiver configurado.</summary>
    public string? TextoCobranca(int parcelaId, decimal valor)
    {
        if (!Configurado || valor <= 0)
            return null;
        var pix = Parcela(parcelaId, valor);
        return $"Para pagar por PIX, use o código \"copia e cola\" abaixo no aplicativo do seu banco:\n{pix.CopiaECola}";
    }
}

/// <summary>
/// Montagem do BR Code (padrão EMV® QRCPS do Banco Central) de um PIX estático: campos ID + tamanho + valor e, no fim, o
/// CRC16-CCITT do conteúdo.
/// </summary>
public static class BrCodePix
{
    public static string Montar(string chave, string nome, string cidade, decimal? valor, string identificador, string? descricao = null)
    {
        var chaveNormalizada = NormalizarChave(chave);
        var descricaoLimpa = descricao is null ? null : Texto(descricao, 99 - 22 - chaveNormalizada.Length - 8);

        var contaPix = Campo("00", "br.gov.bcb.pix") + Campo("01", chaveNormalizada) +
                       (string.IsNullOrEmpty(descricaoLimpa) ? "" : Campo("02", descricaoLimpa));
        Validador.Garantir(contaPix.Length <= 99, "A chave PIX é longa demais.");

        var conteudo = new StringBuilder()
            .Append(Campo("00", "01"))
            .Append(Campo("26", contaPix))
            .Append(Campo("52", "0000"))
            .Append(Campo("53", "986"));
        if (valor is { } v)
        {
            Validador.Garantir(v > 0, "O valor do PIX deve ser maior que zero.");
            conteudo.Append(Campo("54", v.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        conteudo.Append(Campo("58", "BR"))
            .Append(Campo("59", Texto(nome, 25) is { Length: > 0 } recebedor ? recebedor : "RECEBEDOR"))
            .Append(Campo("60", Texto(cidade, 15) is { Length: > 0 } local ? local : "BRASIL"))
            .Append(Campo("62", Campo("05", Identificador(identificador))))
            .Append("6304");
        return conteudo + Crc16(conteudo.ToString());
    }

    /// <summary>Identificador (txid) aceito no PIX estático: até 25 letras e números ("***" quando vazio).</summary>
    public static string Identificador(string? texto)
    {
        var limpo = new string((texto ?? "").Where(char.IsAsciiLetterOrDigit).ToArray());
        return limpo.Length == 0 ? "***" : limpo[..Math.Min(limpo.Length, 25)];
    }

    /// <summary>CPF/CNPJ só com dígitos, celular com +55, e-mail em minúsculas; a chave aleatória fica como está.</summary>
    public static string NormalizarChave(string chave)
    {
        var texto = chave.Trim();
        Validador.Garantir(texto.Length > 0, "Informe a chave PIX.");
        if (texto.Contains('@'))
            return texto.ToLowerInvariant();
        if (Guid.TryParse(texto, out var aleatoria))
            return aleatoria.ToString("D");

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (texto.StartsWith('+'))
            return "+" + digitos;
        if (digitos.Length == 11 && Documento.CpfValido(digitos))
            return digitos;
        if (digitos.Length == 14 && Documento.CnpjValido(digitos))
            return digitos;
        if (digitos.Length is 10 or 11)
            return "+55" + digitos;
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55"))
            return "+" + digitos;
        throw new RegraNegocioException("Chave PIX inválida. Use CPF/CNPJ, e-mail, celular com DDD ou a chave aleatória.");
    }

    private static string Campo(string id, string valor) => id + valor.Length.ToString("00") + valor;

    /// <summary>Texto sem acentos e só com caracteres aceitos pelos bancos, em maiúsculas, no tamanho máximo.</summary>
    private static string Texto(string texto, int maximo)
    {
        var semAcento = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var limpo = new string(semAcento.ToUpperInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '.' or '-' or '/').ToArray())
            .Trim();
        return maximo <= 0 ? "" : limpo[..Math.Min(limpo.Length, maximo)].TrimEnd();
    }

    /// <summary>CRC16-CCITT-FALSE (polinômio 0x1021, valor inicial 0xFFFF), em hexadecimal maiúsculo com 4 dígitos.</summary>
    public static string Crc16(string texto)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(texto))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }
}
