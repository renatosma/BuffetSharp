using System.IO.Compression;
using System.Text;

namespace BuffetSharp.Infrastructure.Persistencia.Seed;

/// <summary>
/// Gera uma "assinatura" fictícia (rabisco cursivo) em PNG para os contratos assinados da base de
/// demonstração, sem depender de bibliotecas gráficas (a infraestrutura não referencia o Windows Forms).
/// </summary>
internal static class AssinaturaSintetica
{
    private const int Largura = 360;
    private const int Altura = 110;

    public static byte[] Gerar(int semente)
    {
        var rnd = new Random(semente);
        var pixels = new byte[Largura * Altura];
        Array.Fill(pixels, (byte)255);

        // Traço contínuo: senoides sobrepostas imitam letras ligadas, com uma laçada inicial e um sublinhado.
        double a = 0.9 + rnd.NextDouble() * 0.8, b = 0.15 + rnd.NextDouble() * 0.2, fase = rnd.NextDouble() * Math.PI;
        for (var t = 0.0; t <= 1.0; t += 0.0004)
        {
            var x = 20 + t * (Largura - 60);
            var y = Altura / 2.0 - 26 * Math.Sin(t * Math.PI * 12 * a + fase) * Math.Cos(t * Math.PI * 3 * b) - 8 * Math.Sin(t * 40);
            Pintar(pixels, x, y);
        }
        for (var t = 0.0; t <= 1.0; t += 0.002)
            Pintar(pixels, 30 + t * (Largura - 90), Altura - 22 + 4 * Math.Sin(t * Math.PI));

        return CodificarPng(pixels);
    }

    private static void Pintar(byte[] pixels, double x, double y)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            var px = (int)Math.Round(x) + dx;
            var py = (int)Math.Round(y) + dy;
            if (px is >= 0 and < Largura && py is >= 0 and < Altura)
                pixels[py * Largura + px] = 30;
        }
    }

    /// <summary>PNG em tons de cinza (8 bits), sem filtro, compactado com zlib.</summary>
    private static byte[] CodificarPng(byte[] pixels)
    {
        using var linhas = new MemoryStream();
        using (var zlib = new ZLibStream(linhas, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < Altura; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(pixels, y * Largura, Largura);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var cabecalho = new byte[13];
        EscreverInteiro(cabecalho, 0, Largura);
        EscreverInteiro(cabecalho, 4, Altura);
        cabecalho[8] = 8; // bits por canal
        cabecalho[9] = 0; // tons de cinza
        Bloco(png, "IHDR", cabecalho);
        Bloco(png, "IDAT", linhas.ToArray());
        Bloco(png, "IEND", []);
        return png.ToArray();
    }

    private static void Bloco(Stream saida, string tipo, byte[] dados)
    {
        var tamanho = new byte[4];
        EscreverInteiro(tamanho, 0, dados.Length);
        saida.Write(tamanho);
        var conteudo = Encoding.ASCII.GetBytes(tipo).Concat(dados).ToArray();
        saida.Write(conteudo);
        var crc = new byte[4];
        EscreverInteiro(crc, 0, (int)Crc32(conteudo));
        saida.Write(crc);
    }

    private static void EscreverInteiro(byte[] destino, int posicao, int valor)
    {
        destino[posicao] = (byte)(valor >> 24);
        destino[posicao + 1] = (byte)(valor >> 16);
        destino[posicao + 2] = (byte)(valor >> 8);
        destino[posicao + 3] = (byte)valor;
    }

    private static uint Crc32(byte[] dados)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in dados)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }
}
