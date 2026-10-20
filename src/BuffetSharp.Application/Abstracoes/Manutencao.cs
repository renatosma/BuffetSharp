namespace BuffetSharp.Application.Abstracoes;

/// <summary>Log em arquivo (seção "Log" do appsettings.json). Os níveis gravados vêm da seção "Logging".</summary>
public sealed class LogOptions
{
    public const string Secao = "Log";

    /// <summary>Pasta dos arquivos de log. Relativa: dentro da pasta do sistema (ou do perfil do usuário, se não der para gravar lá).</summary>
    public string Pasta { get; set; } = "Logs";

    /// <summary>Arquivos (um por dia) mais antigos que isto são apagados ao iniciar o sistema.</summary>
    public int DiasRetencao { get; set; } = 30;
}

/// <summary>Cópia de segurança do banco de dados (seção "Backup" do appsettings.json).</summary>
public sealed class BackupOptions
{
    public const string Secao = "Backup";

    /// <summary>
    /// Pasta das cópias. No SQL Server, o próprio servidor grava o arquivo: num servidor em outra máquina, informe uma
    /// pasta do servidor (ou compartilhada) que o serviço do SQL Server consiga gravar.
    /// </summary>
    public string Pasta { get; set; } = "Backups";

    /// <summary>Faz a cópia sozinho, depois do login, quando a última tiver mais de <see cref="IntervaloHoras"/>.</summary>
    public bool Automatico { get; set; } = true;

    public int IntervaloHoras { get; set; } = 24;

    /// <summary>Quantas cópias manter na pasta (as mais antigas são apagadas depois de cada cópia nova).</summary>
    public int Manter { get; set; } = 15;

    /// <summary>Caminho do pg_dump (PostgreSQL) ou do mysqldump (MySQL), quando não estiver no PATH nem na pasta padrão.</summary>
    public string? Ferramenta { get; set; }
}

public sealed record ArquivoBackup(string Caminho, DateTime CriadoEm, long Tamanho)
{
    public string Nome => Path.GetFileName(Caminho);
}

/// <summary>Cópia de segurança do banco em uso (cada banco tem a sua ferramenta).</summary>
public interface IBackupBanco
{
    /// <summary>Nulo quando o banco em uso tem cópia pelo sistema; senão, explica como fazer a cópia.</summary>
    string? Indisponivel { get; }

    /// <summary>Pasta onde as cópias são gravadas.</summary>
    string Pasta { get; }

    /// <summary>Cópias da pasta, da mais recente para a mais antiga.</summary>
    IReadOnlyList<ArquivoBackup> Listar();

    Task<ArquivoBackup> FazerAsync(CancellationToken ct = default);

    /// <summary>Cópia automática: só faz se estiver ligada e a última for mais antiga que o intervalo configurado.</summary>
    /// <returns>A cópia feita, ou nula se não era necessária.</returns>
    Task<ArquivoBackup?> FazerSeNecessarioAsync(CancellationToken ct = default);
}
