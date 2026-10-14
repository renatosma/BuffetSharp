namespace BuffetSharp.Application.Abstracoes;

/// <summary>
/// Parâmetros do negócio (seção "Buffet" do appsettings.json).
/// </summary>
public sealed class BuffetOptions
{
    public const string Secao = "Buffet";

    public string NomeEmpresa { get; set; } = "BuffetSharp Eventos";
    public string? Documento { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Endereco { get; set; }

    /// <summary>Tempo de montagem/desmontagem exigido entre dois eventos no mesmo espaço.</summary>
    public int IntervaloMinimoEntreEventosMinutos { get; set; } = 120;

    public int DiasValidadeOrcamento { get; set; } = 15;

    /// <summary>Foro de eleição citado no contrato.</summary>
    public string CidadeForo { get; set; } = "São Paulo/SP";

    /// <summary>Antecedência para o cliente confirmar o número final de convidados (cláusula do contrato).</summary>
    public int DiasConfirmacaoConvidados { get; set; } = 10;

    /// <summary>Degustações simultâneas permitidas (mesmo horário, com tolerância de 1 hora).</summary>
    public int LimiteDegustacoesPorHorario { get; set; } = 3;
}
