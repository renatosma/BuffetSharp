using System.Globalization;
using System.Security;

namespace BuffetSharp.Tests;

/// <summary>Monta o XML de uma NF-e autorizada (nfeProc), no leiaute da SEFAZ, para os testes de importação.</summary>
internal static class NfeExemplo
{
    public sealed record Item(string Codigo, string Descricao, string Unidade, decimal Quantidade, decimal ValorUnitario, decimal Desconto = 0);

    public static string Xml(string numero, string cnpjEmitente, string nomeEmitente, IReadOnlyList<Item> itens, decimal frete = 0,
        IReadOnlyList<(DateOnly Vencimento, decimal Valor)>? duplicatas = null, DateOnly? emissao = null, string? chave = null, string serie = "1")
    {
        static string V(decimal valor) => valor.ToString("0.00##", CultureInfo.InvariantCulture);
        var data = emissao ?? DateOnly.FromDateTime(DateTime.Today);
        chave ??= Chave(cnpjEmitente, numero);
        var produtos = itens.Sum(i => Math.Round(i.Quantidade * i.ValorUnitario, 2));
        var desconto = itens.Sum(i => i.Desconto);
        var total = produtos - desconto + frete;
        var det = string.Concat(itens.Select((i, n) => $"""
            <det nItem="{n + 1}"><prod><cProd>{i.Codigo}</cProd><cEAN>SEM GTIN</cEAN><xProd>{SecurityElement.Escape(i.Descricao)}</xProd>
            <NCM>10063021</NCM><CFOP>5102</CFOP><uCom>{i.Unidade}</uCom><qCom>{i.Quantidade.ToString("0.0000", CultureInfo.InvariantCulture)}</qCom>
            <vUnCom>{V(i.ValorUnitario)}</vUnCom><vProd>{V(Math.Round(i.Quantidade * i.ValorUnitario, 2))}</vProd>
            {(i.Desconto > 0 ? $"<vDesc>{V(i.Desconto)}</vDesc>" : "")}<indTot>1</indTot></prod>
            <imposto><ICMS><ICMSSN102><orig>0</orig><CSOSN>102</CSOSN></ICMSSN102></ICMS></imposto></det>
            """));
        var cobr = duplicatas is { Count: > 0 }
            ? "<cobr><fat><nFat>" + numero + "</nFat></fat>" + string.Concat(duplicatas.Select((d, n) =>
                $"<dup><nDup>{n + 1:000}</nDup><dVenc>{d.Vencimento:yyyy-MM-dd}</dVenc><vDup>{V(d.Valor)}</vDup></dup>")) + "</cobr>"
            : "";
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe><infNFe Id="NFe{chave}" versao="4.00">
                <ide><cUF>35</cUF><natOp>VENDA</natOp><mod>55</mod><serie>{serie}</serie><nNF>{numero}</nNF>
                  <dhEmi>{data:yyyy-MM-dd}T10:15:00-03:00</dhEmi><tpNF>1</tpNF></ide>
                <emit><CNPJ>{cnpjEmitente}</CNPJ><xNome>{SecurityElement.Escape(nomeEmitente)} Ltda</xNome><xFant>{SecurityElement.Escape(nomeEmitente)}</xFant>
                  <enderEmit><xLgr>Rua das Flores</xLgr><nro>100</nro><xBairro>Centro</xBairro><cMun>3550308</cMun><xMun>São Paulo</xMun>
                  <UF>SP</UF><CEP>01001000</CEP><fone>1133334444</fone></enderEmit><IE>123456789</IE><CRT>1</CRT></emit>
                <dest><CNPJ>11222333000181</CNPJ><xNome>BuffetSharp Eventos</xNome></dest>
                {det}
                <total><ICMSTot><vBC>0.00</vBC><vICMS>0.00</vICMS><vProd>{V(produtos)}</vProd><vFrete>{V(frete)}</vFrete><vSeg>0.00</vSeg>
                  <vDesc>{V(desconto)}</vDesc><vIPI>0.00</vIPI><vST>0.00</vST><vOutro>0.00</vOutro><vNF>{V(total)}</vNF></ICMSTot></total>
                {cobr}
                <pag><detPag><tPag>{(duplicatas is { Count: > 0 } ? "15" : "17")}</tPag><vPag>{V(total)}</vPag></detPag></pag>
              </infNFe></NFe>
              <protNFe versao="4.00"><infProt><chNFe>{chave}</chNFe><cStat>100</cStat><xMotivo>Autorizado o uso da NF-e</xMotivo></infProt></protNFe>
            </nfeProc>
            """;
    }

    /// <summary>Chave de 44 dígitos (sem conferir o dígito verificador, que a importação não exige).</summary>
    public static string Chave(string cnpj, string numero) =>
        $"3526{DateTime.Today:MM}{cnpj.PadLeft(14, '0')[..14]}55001{numero.PadLeft(9, '0')[..9]}1{Random.Shared.Next(10_000_000, 99_999_999)}0";

    /// <summary>CNPJ válido aleatório (para fornecedores que ainda não estão cadastrados).</summary>
    public static string NovoCnpj()
    {
        var d = Enumerable.Range(0, 8).Select(_ => Random.Shared.Next(10)).Concat([0, 0, 0, 1]).ToList();
        int Digito(int[] pesos)
        {
            var resto = d.Select((x, i) => x * pesos[i]).Sum() % 11;
            return resto < 2 ? 0 : 11 - resto;
        }
        d.Add(Digito([5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]));
        d.Add(Digito([6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]));
        return string.Concat(d);
    }
}
