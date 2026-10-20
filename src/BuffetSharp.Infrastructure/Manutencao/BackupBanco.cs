using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure.Persistencia;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Infrastructure.Manutencao;

/// <summary>
/// Cópias de segurança do banco em uso, com a ferramenta de cada banco (veja <see cref="Persistencia.Provedores.IProvedorBanco.FazerBackupAsync"/>).
/// Os arquivos se chamam BuffetSharp_AAAAMMDD_HHMMSS.extensão; só as últimas <see cref="BackupOptions.Manter"/> ficam na pasta.
/// </summary>
public sealed class BackupBanco(FabricaConexao conexoes, BackupOptions opcoes, TimeProvider relogio, ILogger<BackupBanco> log)
    : IBackupBanco
{
    private const string Prefixo = "BuffetSharp_";
    private static readonly SemaphoreSlim EmAndamento = new(1, 1);
    private string? _pasta;

    public string? Indisponivel => conexoes.Provedor.BackupIndisponivel;

    public string Pasta => _pasta ??= PastasLocais.Resolver(opcoes.Pasta);

    public IReadOnlyList<ArquivoBackup> Listar()
    {
        if (conexoes.Provedor.ExtensaoBackup is not { } extensao || !Directory.Exists(Pasta))
            return [];
        return Directory.EnumerateFiles(Pasta, $"{Prefixo}*{extensao}")
            .Select(f => new FileInfo(f))
            .Select(f => new ArquivoBackup(f.FullName, f.LastWriteTime, f.Length))
            .OrderByDescending(a => a.CriadoEm)
            .ToList();
    }

    public async Task<ArquivoBackup> FazerAsync(CancellationToken ct = default)
    {
        if (Indisponivel is { } motivo)
            throw new Domain.Comum.RegraNegocioException(motivo);

        await EmAndamento.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Pasta);
            var agora = relogio.GetLocalNow().DateTime;
            var arquivo = Path.Combine(Pasta, $"{Prefixo}{agora:yyyyMMdd_HHmmss}{conexoes.Provedor.ExtensaoBackup}");
            log.LogInformation("Iniciando cópia de segurança em {Arquivo}", arquivo);
            try
            {
                await conexoes.Provedor.FazerBackupAsync(conexoes.ConnectionString, arquivo, opcoes.Ferramenta, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "A cópia de segurança em {Arquivo} falhou", arquivo);
                throw;
            }

            var copia = Listar().FirstOrDefault(a => string.Equals(a.Caminho, arquivo, StringComparison.OrdinalIgnoreCase))
                        ?? new ArquivoBackup(arquivo, agora, 0);
            log.LogInformation("Cópia de segurança concluída: {Arquivo} ({Tamanho:N0} bytes)", arquivo, copia.Tamanho);
            ApagarExcedentes();
            return copia;
        }
        finally
        {
            EmAndamento.Release();
        }
    }

    public async Task<ArquivoBackup?> FazerSeNecessarioAsync(CancellationToken ct = default)
    {
        if (!opcoes.Automatico || Indisponivel is not null)
            return null;
        var ultima = Listar().FirstOrDefault();
        if (ultima is not null && ultima.CriadoEm > relogio.GetLocalNow().DateTime.AddHours(-Math.Max(1, opcoes.IntervaloHoras)))
            return null;
        return await FazerAsync(ct);
    }

    private void ApagarExcedentes()
    {
        foreach (var antiga in Listar().Skip(Math.Max(1, opcoes.Manter)))
        {
            try
            {
                File.Delete(antiga.Caminho);
                log.LogInformation("Cópia de segurança antiga apagada: {Arquivo}", antiga.Caminho);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.LogWarning(ex, "Não foi possível apagar a cópia antiga {Arquivo}", antiga.Caminho);
            }
        }
    }
}
