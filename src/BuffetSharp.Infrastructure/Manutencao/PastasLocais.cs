namespace BuffetSharp.Infrastructure.Manutencao;

/// <summary>Pastas de trabalho do sistema (logs, cópias de segurança).</summary>
public static class PastasLocais
{
    /// <summary>
    /// Caminho absoluto da pasta configurada. Uma pasta relativa fica dentro da pasta do sistema; se o sistema estiver
    /// instalado onde o usuário não pode gravar (ex.: Arquivos de Programas), fica em %LOCALAPPDATA%\BuffetSharp.
    /// </summary>
    public static string Resolver(string pasta)
    {
        if (Path.IsPathRooted(pasta))
            return pasta;

        var aoLadoDoSistema = Path.Combine(AppContext.BaseDirectory, pasta);
        if (PodeGravar(aoLadoDoSistema))
            return aoLadoDoSistema;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BuffetSharp", pasta);
    }

    private static bool PodeGravar(string pasta)
    {
        try
        {
            Directory.CreateDirectory(pasta);
            var teste = Path.Combine(pasta, $".teste-{Guid.NewGuid():N}");
            File.WriteAllText(teste, string.Empty);
            File.Delete(teste);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
