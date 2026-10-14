namespace BuffetSharp.Domain.Comum;

/// <summary>
/// Validação e formatação de CPF e CNPJ.
/// </summary>
public static class Documento
{
    public static string SomenteDigitos(string? valor) =>
        valor is null ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    public static bool CpfValido(string? cpf)
    {
        var d = SomenteDigitos(cpf);
        if (d.Length != 11 || d.Distinct().Count() == 1)
            return false;

        return d[9] - '0' == DigitoCpf(d, 9) && d[10] - '0' == DigitoCpf(d, 10);
    }

    public static bool CnpjValido(string? cnpj)
    {
        var d = SomenteDigitos(cnpj);
        if (d.Length != 14 || d.Distinct().Count() == 1)
            return false;

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return d[12] - '0' == DigitoCnpj(d, pesos1) && d[13] - '0' == DigitoCnpj(d, pesos2);
    }

    /// <summary>Calcula o dígito verificador do CPF considerando as <paramref name="tamanho"/> primeiras posições.</summary>
    public static int DigitoCpf(string digitos, int tamanho)
    {
        var soma = 0;
        for (var i = 0; i < tamanho; i++)
            soma += (digitos[i] - '0') * (tamanho + 1 - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int DigitoCnpj(string digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (digitos[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    public static string Formatar(string? documento)
    {
        var d = SomenteDigitos(documento);
        return d.Length switch
        {
            11 => Convert.ToUInt64(d).ToString(@"000\.000\.000\-00"),
            14 => Convert.ToUInt64(d).ToString(@"00\.000\.000\/0000\-00"),
            _ => documento ?? string.Empty
        };
    }
}
