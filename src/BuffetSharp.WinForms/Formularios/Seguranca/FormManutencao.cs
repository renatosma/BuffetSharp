using System.Diagnostics;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Infrastructure.Manutencao;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Seguranca;

/// <summary>Cópias de segurança do banco (manual e automática) e acesso aos arquivos de log.</summary>
internal sealed class FormManutencao : FormListagem<ArquivoBackup>
{
    private readonly IBackupBanco _backup;
    private readonly BackupOptions _opcoes;
    private readonly LogOptions _log;
    private readonly IProvedorBanco _provedor;

    public FormManutencao(IBackupBanco backup, BackupOptions opcoes, LogOptions log, FabricaConexao conexoes)
        : base("Backup e logs", Descrever(backup, opcoes))
    {
        _backup = backup;
        _opcoes = opcoes;
        _log = log;
        _provedor = conexoes.Provedor;

        var copiar = Botao("Fazer cópia agora", Icones.Salvar, FazerCopiaAsync, "Grava uma cópia completa do banco de dados", destaque: true);
        copiar.Enabled = backup.Indisponivel is null;
        Botao("Abrir pasta das cópias", Icones.Exportar, () => AbrirPasta(_backup.Pasta));
        Botao("Como restaurar", Icones.Informacao, () =>
        {
            Mensagens.Info(this, ComoRestaurar(), "Restaurar uma cópia");
            return Task.CompletedTask;
        });
        Separador();
        Botao("Abrir pasta de logs", Icones.Historico, () => AbrirPasta(PastasLocais.Resolver(_log.Pasta)),
            "Arquivos com os erros e acessos registrados pelo sistema (um por dia)");

        Grade.Texto("Arquivo", a => a.Nome, 60)
            .Data("Data", a => a.CriadoEm, "dd/MM/yyyy HH:mm", 25)
            .Numero("Tamanho (MB)", a => Math.Round(a.Tamanho / 1024m / 1024m, 1), "N1", 18)
            .Busca(a => a.Nome);
    }

    private static string Descrever(IBackupBanco backup, BackupOptions opcoes) => backup.Indisponivel ?? (opcoes.Automatico
        ? $"Cópia automática a cada {opcoes.IntervaloHoras} h (feita depois do login), mantendo as últimas {opcoes.Manter}. Pasta: {backup.Pasta}"
        : $"A cópia automática está desligada (\"Backup:Automatico\" no appsettings.json). Pasta: {backup.Pasta}");

    protected override Task CarregarAsync()
    {
        Exibir(_backup.Listar());
        return Task.CompletedTask;
    }

    protected override string TextoRodape(IReadOnlyList<ArquivoBackup> visiveis) =>
        visiveis.Count == 0
            ? "Nenhuma cópia na pasta."
            : $"{visiveis.Count} cópia(s)  •  última em {visiveis.Max(a => a.CriadoEm):dd/MM/yyyy HH:mm}  •  " +
              $"{visiveis.Sum(a => a.Tamanho) / 1024m / 1024m:N1} MB no total";

    private async Task FazerCopiaAsync()
    {
        UseWaitCursor = true;
        try
        {
            var copia = await Task.Run(() => _backup.FazerAsync());
            await CarregarAsync();
            Mensagens.Info(this, $"Cópia gravada em:{Environment.NewLine}{copia.Caminho}", "Cópia de segurança concluída");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private Task AbrirPasta(string pasta)
    {
        if (!Directory.Exists(pasta))
        {
            Mensagens.Aviso(this, $"A pasta ainda não existe (nenhum arquivo foi gravado):{Environment.NewLine}{pasta}");
            return Task.CompletedTask;
        }
        Process.Start(new ProcessStartInfo(pasta) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private string ComoRestaurar()
    {
        var aviso = Environment.NewLine + Environment.NewLine +
                    "Antes de restaurar, feche o BuffetSharp em todos os computadores. A restauração substitui todos os dados atuais " +
                    "pelos da cópia: faça uma cópia nova antes, por segurança.";
        return _provedor.Tipo switch
        {
            TipoBancoDados.SqlServer =>
                "No SQL Server Management Studio: clique com o botão direito em \"Bancos de Dados\" › \"Restaurar Banco de Dados...\", " +
                "escolha \"Dispositivo\" e o arquivo .bak. Pela linha de comando:" + Environment.NewLine +
                "RESTORE DATABASE BuffetSharp FROM DISK = 'C:\\caminho\\arquivo.bak' WITH REPLACE" + aviso,
            TipoBancoDados.PostgreSql =>
                "Use o pg_restore (ferramentas cliente do PostgreSQL):" + Environment.NewLine +
                "pg_restore --clean --if-exists --host=servidor --username=usuario --dbname=buffetsharp arquivo.dump" + aviso,
            TipoBancoDados.MySql =>
                "Use o cliente mysql (ferramentas do MySQL):" + Environment.NewLine +
                "mysql --host=servidor --user=usuario --password buffetsharp < arquivo.sql" + aviso,
            _ => _provedor.BackupIndisponivel ?? "Use a ferramenta de restauração do banco de dados." + aviso
        };
    }
}
