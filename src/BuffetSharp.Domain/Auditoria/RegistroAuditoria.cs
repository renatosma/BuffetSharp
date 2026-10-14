using System.Text.Json;

namespace BuffetSharp.Domain.Auditoria;

public enum AcaoAuditoria
{
    Inclusao,
    Alteracao,
    Exclusao
}

/// <summary>Campo alterado: valor anterior e novo, já formatados para exibição.</summary>
public sealed record AlteracaoCampo(string Campo, string? Anterior, string? Novo)
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Serializar(IEnumerable<AlteracaoCampo> alteracoes) => JsonSerializer.Serialize(alteracoes, Json);

    public static IReadOnlyList<AlteracaoCampo> Ler(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<AlteracaoCampo>>(json, Json) ?? [];
}

/// <summary>
/// Registro imutável de uma gravação no banco: quem, quando, em qual registro e o que mudou.
/// É gerado automaticamente pela persistência; nenhuma tela grava auditoria diretamente.
/// </summary>
public class RegistroAuditoria
{
    private RegistroAuditoria()
    {
    }

    public RegistroAuditoria(DateTime dataHora, int? usuarioId, string usuario, string entidade, string registroId,
        string? descricao, AcaoAuditoria acao, IEnumerable<AlteracaoCampo> alteracoes)
    {
        DataHora = dataHora;
        UsuarioId = usuarioId;
        Usuario = usuario;
        Entidade = entidade;
        RegistroId = registroId;
        Descricao = descricao is { Length: > 200 } ? descricao[..200] : descricao;
        Acao = acao;
        Alteracoes = AlteracaoCampo.Serializar(alteracoes);
    }

    public long Id { get; private set; }
    public DateTime DataHora { get; private set; }
    public int? UsuarioId { get; private set; }

    /// <summary>Login de quem fez a alteração ("sistema" para rotinas automáticas).</summary>
    public string Usuario { get; private set; } = string.Empty;

    /// <summary>Nome da classe da entidade (ex.: Cliente, Evento, Convidado).</summary>
    public string Entidade { get; private set; } = string.Empty;

    /// <summary>Chave do registro (chaves compostas separadas por "/").</summary>
    public string RegistroId { get; private set; } = string.Empty;

    /// <summary>Identificação legível (nome, código...) no momento da alteração.</summary>
    public string? Descricao { get; private set; }

    public AcaoAuditoria Acao { get; private set; }

    /// <summary>Lista de <see cref="AlteracaoCampo"/> em JSON.</summary>
    public string Alteracoes { get; private set; } = "[]";

    public IReadOnlyList<AlteracaoCampo> LerAlteracoes() => AlteracaoCampo.Ler(Alteracoes);
}
