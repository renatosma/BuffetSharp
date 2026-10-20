using BuffetSharp.Application.Abstracoes;
using QRCoder;

namespace BuffetSharp.Infrastructure.Manutencao;

/// <summary>QR Code em PNG (QRCoder), com correção de erro média, como recomendado para o PIX.</summary>
internal sealed class GeradorQrCode : IGeradorQrCode
{
    public byte[] Png(string texto, int pixelsPorModulo = 8)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M, forceUtf8: false);
        return new PngByteQRCode(dados).GetGraphic(pixelsPorModulo);
    }
}
