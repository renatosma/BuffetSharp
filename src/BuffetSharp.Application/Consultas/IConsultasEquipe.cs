namespace BuffetSharp.Application.Consultas;

/// <summary>Diárias de um funcionário nos eventos realizados do período.</summary>
public sealed record DiariaFuncionario
{
    public int FuncionarioId { get; init; }
    public string Funcionario { get; init; } = string.Empty;
    public string? Cargo { get; init; }

    /// <summary>Escalas com presença registrada.</summary>
    public int Diarias { get; init; }

    public decimal Valor { get; init; }
    public int Ausencias { get; init; }

    /// <summary>Diárias já lançadas em contas a pagar (em algum fechamento).</summary>
    public int DiariasFechadas { get; init; }

    public decimal ValorFechado { get; init; }
    public decimal ValorEmAberto => Valor - ValorFechado;
    public int DiariasEmAberto => Diarias - DiariasFechadas;
}

/// <summary>Escala de um funcionário num evento (detalhe das diárias).</summary>
public sealed record EscalaApurada
{
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public string Funcao { get; init; } = string.Empty;
    public decimal ValorDiaria { get; init; }
    public bool Presente { get; init; }
    public int? FechamentoEquipeId { get; init; }
    public string EventoCodigo => $"EVT-{EventoId:D5}";
}

public sealed record FechamentoEquipeResumo
{
    public int Id { get; init; }
    public int FuncionarioId { get; init; }
    public string Funcionario { get; init; } = string.Empty;
    public DateTime De { get; init; }
    public DateTime Ate { get; init; }
    public int Diarias { get; init; }
    public decimal Valor { get; init; }
    public DateTime CriadoEm { get; init; }
    public DateTime Vencimento { get; init; }
    public string SituacaoConta { get; init; } = string.Empty;
}

/// <summary>Pagamento da equipe (Dapper): apuração das diárias e fechamentos já lançados.</summary>
public interface IConsultasEquipe
{
    /// <summary>Diárias dos eventos realizados com início no período, por funcionário.</summary>
    Task<IReadOnlyList<DiariaFuncionario>> ApurarAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IReadOnlyList<EscalaApurada>> DetalharAsync(int funcionarioId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IReadOnlyList<FechamentoEquipeResumo>> ListarFechamentosAsync(CancellationToken ct = default);
}
