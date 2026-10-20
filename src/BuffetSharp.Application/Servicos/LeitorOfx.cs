using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Application.Servicos;

/// <summary>Lançamento do extrato bancário.</summary>
/// <param name="Id">Identificador do lançamento no banco (FITID); quando o banco não informa, é montado com data, valor e posição.</param>
/// <param name="Valor">Positivo = crédito (entrada); negativo = débito (saída).</param>
public sealed record LancamentoExtrato(string Id, DateOnly Data, decimal Valor, string Descricao, string? Tipo);

/// <summary>Extrato lido de um arquivo OFX.</summary>
public sealed record ExtratoOfx(string? Banco, string? Conta, DateOnly? Inicio, DateOnly? Fim, decimal? Saldo, DateOnly? DataSaldo,
    IReadOnlyList<LancamentoExtrato> Lancamentos);

/// <summary>
/// Leitura de extratos OFX exportados pelos bancos (internet banking): a versão 1.x, em SGML com marcas sem fechamento, e a
/// 2.x, em XML. Lê a conta, o período, o saldo e os lançamentos (STMTTRN).
/// </summary>
public static partial class LeitorOfx
{
    public static ExtratoOfx Ler(byte[] conteudo)
    {
        Validador.Garantir(conteudo.Length > 0, "O arquivo do extrato está vazio.");
        var texto = Decodificar(conteudo);
        Validador.Garantir(texto.Contains("<OFX>", StringComparison.OrdinalIgnoreCase),
            "O arquivo não é um extrato OFX. No internet banking, exporte o extrato no formato OFX (Money).");

        var lancamentos = new List<LancamentoExtrato>();
        var posicao = 0;
        foreach (Match bloco in Transacao().Matches(texto))
        {
            posicao++;
            var corpo = bloco.Groups[1].Value;
            var data = Data(Valor(corpo, "DTPOSTED")) ?? Data(Valor(corpo, "DTUSER"));
            var valor = Numero(Valor(corpo, "TRNAMT"));
            if (data is null || valor is null)
                continue;

            var descricao = string.Join(" - ", new[] { Valor(corpo, "NAME"), Valor(corpo, "MEMO") }
                .Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase));
            var id = Valor(corpo, "FITID") is { Length: > 0 } fitid
                ? fitid
                : $"{data:yyyyMMdd}-{valor.Value.ToString("0.00", CultureInfo.InvariantCulture)}-{posicao}";
            lancamentos.Add(new LancamentoExtrato(id.Length > 60 ? id[..60] : id, data.Value, valor.Value,
                descricao.Length == 0 ? "(sem descrição)" : descricao, Valor(corpo, "TRNTYPE")));
        }

        var saldo = Bloco(texto, "LEDGERBAL");
        return new ExtratoOfx(
            Valor(texto, "BANKID"),
            Valor(texto, "ACCTID"),
            Data(Valor(texto, "DTSTART")),
            Data(Valor(texto, "DTEND")),
            saldo is null ? null : Numero(Valor(saldo, "BALAMT")),
            saldo is null ? null : Data(Valor(saldo, "DTASOF")),
            lancamentos.OrderBy(l => l.Data).ToList());
    }

    /// <summary>OFX 1.x costuma vir em Windows-1252 (CHARSET:1252) ou ASCII; o 2.x, em UTF-8.</summary>
    private static string Decodificar(byte[] conteudo)
    {
        var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            return utf8.GetString(conteudo).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(conteudo);
        }
    }

    /// <summary>Valor de uma marca: "&lt;TRNAMT&gt;-12.50" (SGML, sem fechamento) ou "&lt;TRNAMT&gt;-12.50&lt;/TRNAMT&gt;" (XML).</summary>
    private static string? Valor(string texto, string marca)
    {
        var achado = Regex.Match(texto, $@"<{marca}>\s*([^<\r\n]*)", RegexOptions.IgnoreCase);
        return achado.Success ? System.Net.WebUtility.HtmlDecode(achado.Groups[1].Value.Trim()) : null;
    }

    private static string? Bloco(string texto, string marca)
    {
        var achado = Regex.Match(texto, $@"<{marca}>(.*?)</{marca}>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return achado.Success ? achado.Groups[1].Value : null;
    }

    /// <summary>Datas OFX: AAAAMMDD[HHMMSS[.XXX]][[-3:BRT]]. Só o dia interessa.</summary>
    private static DateOnly? Data(string? valor) =>
        valor is { Length: >= 8 } && DateOnly.TryParseExact(valor[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    /// <summary>Valores com ponto ou vírgula decimal (alguns bancos brasileiros usam vírgula).</summary>
    private static decimal? Numero(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        var normalizado = valor.Trim().Replace(" ", "");
        if (normalizado.Contains(',') && normalizado.Contains('.'))
            normalizado = normalizado.Replace(".", "");
        normalizado = normalizado.Replace(',', '.');
        return decimal.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero) ? numero : null;
    }

    [GeneratedRegex(@"<STMTTRN>(.*?)(?:</STMTTRN>|(?=<STMTTRN>)|(?=</BANKTRANLIST>))", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Transacao();
}
