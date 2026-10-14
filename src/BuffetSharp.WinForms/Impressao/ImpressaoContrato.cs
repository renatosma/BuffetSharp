using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>Contrato impresso/PDF: texto congelado na emissão, assinaturas capturadas e código de integridade no rodapé.</summary>
internal static class ImpressaoContrato
{
    public static void Visualizar(IWin32Window dono, Contrato contrato, BuffetOptions empresa) =>
        Montar(contrato, empresa).Visualizar(dono);

    public static byte[] GerarPdf(Contrato contrato, BuffetOptions empresa) => Montar(contrato, empresa).GerarPdf();

    private static DocumentoImpresso Montar(Contrato contrato, BuffetOptions empresa)
    {
        var doc = new DocumentoImpresso($"Contrato {contrato.Codigo}", empresa.NomeEmpresa);
        doc.Subtitulo($"{contrato.Evento?.Codigo} - {contrato.Evento?.Titulo}  •  emitido em {contrato.EmitidoEm:dd/MM/yyyy HH:mm}  •  " +
                      Textos.Exibir(contrato.Status).ToLowerInvariant());
        if (contrato.Status == StatusContrato.Cancelado)
            doc.Paragrafo($"CONTRATO CANCELADO em {contrato.CanceladoEm:dd/MM/yyyy}: {contrato.MotivoCancelamento}", negrito: true);
        if (!contrato.ConteudoIntegro)
            doc.Paragrafo("ATENÇÃO: o texto deste contrato não confere com o código de integridade da emissão.", negrito: true);
        doc.Espaco(4);

        // Títulos e cláusulas em negrito; cada bloco de texto vira um parágrafo.
        foreach (var bloco in contrato.Conteudo.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var linhas = bloco.Split('\n');
            var corpo = linhas;
            if (linhas[0].StartsWith("CLÁUSULA", StringComparison.Ordinal) || linhas[0].StartsWith("CONTRATO DE", StringComparison.Ordinal))
            {
                doc.Paragrafo(linhas[0], negrito: true);
                corpo = linhas[1..];
            }
            if (corpo.Length > 0)
                doc.Paragrafo(string.Join(Environment.NewLine, corpo));
        }

        doc.Espaco(24);
        var cliente = contrato.Evento?.Cliente;
        if (contrato.Status == StatusContrato.Assinado)
        {
            doc.Assinaturas(
                (contrato.NomeRepresentante is null ? empresa.NomeEmpresa : $"{empresa.NomeEmpresa} - {contrato.NomeRepresentante}",
                    contrato.AssinaturaEmpresa, "CONTRATADA"),
                (contrato.NomeSignatario ?? cliente?.Nome ?? "CONTRATANTE", contrato.AssinaturaCliente,
                    $"{Documento.Formatar(contrato.DocumentoSignatario)} • assinado em {contrato.AssinadoEm:dd/MM/yyyy HH:mm}"));
        }
        else
        {
            doc.Assinaturas((empresa.NomeEmpresa, null, "CONTRATADA"), (cliente?.Nome ?? "CONTRATANTE", null, "CONTRATANTE"));
        }

        doc.Rodape($"{contrato.Codigo} • integridade (SHA-256): {contrato.HashConteudo[..32]}");
        return doc;
    }
}
