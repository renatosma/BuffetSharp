using System.Security.Cryptography;
using BuffetSharp.Application.Abstracoes;

namespace BuffetSharp.Infrastructure.Seguranca;

/// <summary>
/// Hash de senha com PBKDF2-SHA256 e salt aleatório.
/// Formato: PBKDF2$iteracoes$salt(base64)$hash(base64)
/// </summary>
internal sealed class HasherSenhaPbkdf2 : IHasherSenha
{
    private const int Iteracoes = 210_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;

    public string Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"PBKDF2${Iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string senha, string hashArmazenado)
    {
        var partes = hashArmazenado.Split('$');
        if (partes.Length != 4 || partes[0] != "PBKDF2" || !int.TryParse(partes[1], out var iteracoes))
            return false;

        try
        {
            var salt = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
