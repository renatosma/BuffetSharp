namespace BuffetSharp.Domain.Comum;

/// <summary>Números e valores em reais por extenso (usado no contrato).</summary>
public static class Extenso
{
    private static readonly string[] Unidades =
    [
        "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez", "onze", "doze", "treze",
        "quatorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove"
    ];

    private static readonly string[] Dezenas = ["", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa"];

    private static readonly string[] Centenas =
        ["", "cento", "duzentos", "trezentos", "quatrocentos", "quinhentos", "seiscentos", "setecentos", "oitocentos", "novecentos"];

    private static readonly (long Valor, string Singular, string Plural)[] Grupos =
        [(1_000_000_000_000, "trilhão", "trilhões"), (1_000_000_000, "bilhão", "bilhões"), (1_000_000, "milhão", "milhões"), (1_000, "mil", "mil"), (1, "", "")];

    /// <summary>1 a 999.</summary>
    private static string Centena(int n)
    {
        if (n == 100)
            return "cem";
        var partes = new List<string>();
        if (n / 100 > 0)
            partes.Add(Centenas[n / 100]);
        var resto = n % 100;
        if (resto > 0)
            partes.Add(resto < 20 ? Unidades[resto] : Dezenas[resto / 10] + (resto % 10 > 0 ? " e " + Unidades[resto % 10] : ""));
        return string.Join(" e ", partes);
    }

    /// <summary>Número inteiro não negativo por extenso: 1234 → "mil duzentos e trinta e quatro".</summary>
    public static string Numero(long numero)
    {
        if (numero < 0)
            return "menos " + Numero(-numero);
        if (numero == 0)
            return "zero";

        var partes = new List<(int Quantidade, string Texto)>();
        var resto = numero;
        foreach (var (valor, singular, plural) in Grupos)
        {
            var quantidade = (int)(resto / valor);
            resto %= valor;
            if (quantidade == 0)
                continue;
            var texto = valor switch
            {
                1 => Centena(quantidade),
                1_000 => quantidade == 1 ? "mil" : $"{Centena(quantidade)} mil",
                _ => $"{Centena(quantidade)} {(quantidade == 1 ? singular : plural)}"
            };
            partes.Add((quantidade, texto));
        }

        // "mil e quinhentos", "dois mil e trinta", mas "mil duzentos e trinta": o "e" só entra antes do último grupo
        // quando ele é menor que cem ou uma centena redonda.
        var resultado = partes[0].Texto;
        for (var i = 1; i < partes.Count; i++)
        {
            var (quantidade, texto) = partes[i];
            var ultimo = i == partes.Count - 1;
            var separador = ultimo && (quantidade < 100 || quantidade % 100 == 0) ? " e " : ultimo ? " " : ", ";
            resultado += separador + texto;
        }
        return resultado;
    }

    /// <summary>Valor em reais por extenso: 1500.50 → "mil e quinhentos reais e cinquenta centavos".</summary>
    public static string Moeda(decimal valor)
    {
        valor = Math.Round(Math.Abs(valor), 2);
        var reais = (long)Math.Truncate(valor);
        var centavos = (int)((valor - reais) * 100);

        var partes = new List<string>();
        if (reais > 0)
        {
            // "um milhão de reais", "dois bilhões de reais"
            var de = reais >= 1_000_000 && reais % 1_000_000 == 0 ? " de" : "";
            partes.Add($"{Numero(reais)}{de} {(reais == 1 ? "real" : "reais")}");
        }
        if (centavos > 0)
            partes.Add($"{Numero(centavos)} {(centavos == 1 ? "centavo" : "centavos")}");
        return partes.Count == 0 ? "zero reais" : string.Join(" e ", partes);
    }
}
