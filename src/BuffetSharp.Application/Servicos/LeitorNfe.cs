using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Lê o XML da NF-e (modelo 55) ou da NFC-e (65) enviado pelo fornecedor: o arquivo autorizado (nfeProc) ou só a nota
/// (NFe). Os nomes das tags são os do leiaute da SEFAZ; o namespace não importa.
/// </summary>
public static class LeitorNfe
{
    public static NotaFiscalEletronica Ler(Stream arquivo)
    {
        XDocument documento;
        try
        {
            documento = XDocument.Load(arquivo);
        }
        catch (XmlException)
        {
            throw new RegraNegocioException("O arquivo não é um XML válido.");
        }
        return Ler(documento);
    }

    public static NotaFiscalEletronica Ler(string xml)
    {
        XDocument documento;
        try
        {
            documento = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            throw new RegraNegocioException("O arquivo não é um XML válido.");
        }
        return Ler(documento);
    }

    private static NotaFiscalEletronica Ler(XDocument documento)
    {
        var inf = documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe")
                  ?? throw new RegraNegocioException("O arquivo não é o XML de uma NF-e (nota fiscal eletrônica).");
        var ide = Filho(inf, "ide") ?? throw new RegraNegocioException("XML da NF-e incompleto: falta a identificação (ide).");
        var emit = Filho(inf, "emit") ?? throw new RegraNegocioException("XML da NF-e incompleto: falta o emitente.");
        var dest = Filho(inf, "dest");
        var total = Filho(Filho(inf, "total"), "ICMSTot");

        // A chave vem no protocolo de autorização ou no atributo Id ("NFe" + 44 dígitos).
        var chave = Texto(documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "chNFe"))
                    ?? (string?)inf.Attribute("Id")?.Value.Replace("NFe", "", StringComparison.OrdinalIgnoreCase);
        var endereco = Filho(emit, "enderEmit");
        var itens = inf.Elements().Where(e => e.Name.LocalName == "det").Select(det =>
        {
            var prod = Filho(det, "prod") ?? throw new RegraNegocioException("XML da NF-e incompleto: item sem produto.");
            return new ItemNfe(
                int.TryParse((string?)det.Attribute("nItem"), out var numero) ? numero : 0,
                Texto(prod, "cProd") ?? "",
                Texto(prod, "xProd") ?? "",
                (Texto(prod, "uCom") ?? "").ToUpperInvariant(),
                Decimal(prod, "qCom"),
                Decimal(prod, "vUnCom"),
                Decimal(prod, "vProd"),
                Decimal(prod, "vDesc"),
                prod.Elements().Where(e => e.Name.LocalName == "rastro")
                    .Where(r => Texto(r, "dVal") is not null)
                    .Select(r => new RastroNfe(Texto(r, "nLote"), Decimal(r, "qLote"), Data(Texto(r, "dVal")),
                        Texto(r, "dFab") is { } fabricacao ? Data(fabricacao) : null))
                    .ToList());
        }).ToList();
        if (itens.Count == 0)
            throw new RegraNegocioException("A NF-e não tem itens.");

        var duplicatas = Filho(inf, "cobr")?.Elements().Where(e => e.Name.LocalName == "dup")
            .Select(d => new DuplicataNfe(Texto(d, "nDup"), Data(Texto(d, "dVenc")), Decimal(d, "vDup")))
            .Where(d => d.Valor > 0)
            .OrderBy(d => d.Vencimento)
            .ToList() ?? [];

        var valorProdutos = total is null ? itens.Sum(i => i.ValorTotal) : Decimal(total, "vProd");
        var desconto = total is null ? itens.Sum(i => i.Desconto) : Decimal(total, "vDesc");
        var frete = Decimal(total, "vFrete");
        var outros = Decimal(total, "vSeg") + Decimal(total, "vOutro") + Decimal(total, "vIPI") + Decimal(total, "vST");
        return new NotaFiscalEletronica
        {
            ChaveAcesso = Digitos(chave),
            Modelo = Texto(ide, "mod") ?? "55",
            Numero = Texto(ide, "nNF") ?? throw new RegraNegocioException("XML da NF-e incompleto: falta o número da nota."),
            Serie = Texto(ide, "serie"),
            DataEmissao = Data(Texto(ide, "dhEmi") ?? Texto(ide, "dEmi")),
            Emitente = new EmitenteNfe
            {
                Documento = Digitos(Texto(emit, "CNPJ") ?? Texto(emit, "CPF")),
                RazaoSocial = Texto(emit, "xNome") ?? "",
                NomeFantasia = Texto(emit, "xFant"),
                Telefone = Texto(endereco, "fone"),
                Endereco = new Endereco
                {
                    Logradouro = Texto(endereco, "xLgr"),
                    Numero = Texto(endereco, "nro"),
                    Complemento = Texto(endereco, "xCpl"),
                    Bairro = Texto(endereco, "xBairro"),
                    Cidade = Texto(endereco, "xMun"),
                    Uf = Texto(endereco, "UF"),
                    Cep = Texto(endereco, "CEP") is { Length: 8 } cep ? $"{cep[..5]}-{cep[5..]}" : Texto(endereco, "CEP")
                }
            },
            DestinatarioDocumento = Digitos(Texto(dest, "CNPJ") ?? Texto(dest, "CPF")) is { Length: > 0 } documentoDestino ? documentoDestino : null,
            DestinatarioNome = Texto(dest, "xNome"),
            ValorProdutos = valorProdutos,
            ValorFrete = frete,
            ValorDesconto = desconto,
            ValorOutros = outros,
            ValorTotal = total is null ? valorProdutos - desconto + frete + outros : Decimal(total, "vNF"),
            Itens = itens,
            Duplicatas = duplicatas,
            MeioPagamento = Meio(Texto(Filho(Filho(inf, "pag"), "detPag"), "tPag"))
        };
    }

    /// <summary>Código tPag da SEFAZ convertido no tipo de forma de pagamento.</summary>
    private static TipoFormaPagamento? Meio(string? tPag) => tPag switch
    {
        "01" => TipoFormaPagamento.Dinheiro,
        "02" => TipoFormaPagamento.Cheque,
        "03" => TipoFormaPagamento.CartaoCredito,
        "04" => TipoFormaPagamento.CartaoDebito,
        "15" => TipoFormaPagamento.Boleto,
        "16" or "18" => TipoFormaPagamento.Transferencia,
        "17" => TipoFormaPagamento.Pix,
        _ => null
    };

    private static XElement? Filho(XElement? pai, string nome) => pai?.Elements().FirstOrDefault(e => e.Name.LocalName == nome);

    private static string? Texto(XElement? elemento) => string.IsNullOrWhiteSpace(elemento?.Value) ? null : elemento.Value.Trim();

    private static string? Texto(XElement? pai, string nome) => Texto(Filho(pai, nome));

    private static decimal Decimal(XElement? pai, string nome) =>
        decimal.TryParse(Texto(pai, nome), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : 0;

    /// <summary>"2026-10-20" ou "2026-10-20T10:15:00-03:00" (vale a data do fuso da nota).</summary>
    private static DateOnly Data(string? texto) =>
        texto is { Length: >= 10 } && DateOnly.TryParseExact(texto[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : throw new RegraNegocioException($"Data inválida no XML da NF-e: '{texto}'.");

    private static string Digitos(string? texto) => new((texto ?? "").Where(char.IsDigit).ToArray());
}
