using System.Text;
using BuffetSharp.Application.Abstracoes;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Infrastructure.Manutencao;

/// <summary>
/// Grava o log do sistema em arquivos de texto, um por dia (buffetsharp-AAAAMMDD.log), para investigar erros depois
/// que a mensagem já foi fechada na tela. Os níveis gravados seguem a seção "Logging" do appsettings.json.
/// </summary>
public sealed class ProvedorLogArquivo : ILoggerProvider
{
    private const string Prefixo = "buffetsharp-";
    private readonly Lock _trava = new();

    public ProvedorLogArquivo(LogOptions opcoes)
    {
        Pasta = PastasLocais.Resolver(opcoes.Pasta);
        ApagarAntigos(opcoes.DiasRetencao);
    }

    public string Pasta { get; }

    public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

    public void Dispose()
    {
    }

    private void Escrever(LogLevel nivel, string categoria, string mensagem, Exception? erro)
    {
        var agora = DateTime.Now;
        var texto = new StringBuilder()
            .Append(agora.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(Sigla(nivel)).Append("] ")
            .Append(categoria[(categoria.LastIndexOf('.') + 1)..]).Append(": ").AppendLine(mensagem);
        if (erro is not null)
            texto.AppendLine(erro.ToString());

        lock (_trava)
        {
            try
            {
                Directory.CreateDirectory(Pasta);
                File.AppendAllText(Path.Combine(Pasta, $"{Prefixo}{agora:yyyyMMdd}.log"), texto.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Sem onde gravar o log, o sistema segue funcionando.
            }
        }
    }

    private void ApagarAntigos(int dias)
    {
        if (dias <= 0 || !Directory.Exists(Pasta))
            return;
        var limite = DateTime.Now.Date.AddDays(-dias);
        foreach (var arquivo in Directory.EnumerateFiles(Pasta, $"{Prefixo}*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(arquivo) < limite)
                    File.Delete(arquivo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static string Sigla(LogLevel nivel) => nivel switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "AVS",
        LogLevel.Error => "ERR",
        _ => "CRT"
    };

    private sealed class Registrador(ProvedorLogArquivo provedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                provedor.Escrever(logLevel, categoria, formatter(state, exception), exception);
        }
    }
}
