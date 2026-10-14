using BuffetSharp.Domain.Auditoria;

namespace BuffetSharp.Application.Consultas;

// ============================ Comissões ============================

/// <summary>Comissão apurada de um vendedor no período (sobre os valores recebidos).</summary>
public sealed record ComissaoVendedor
{
    public int VendedorId { get; init; }
    public string Vendedor { get; init; } = string.Empty;
    public int QuantidadeParcelas { get; init; }
    public int QuantidadeEventos { get; init; }
    public decimal BaseRecebida { get; init; }
    public decimal Comissao { get; init; }

    /// <summary>Fechamento que cobre (total ou parcialmente) o período, se houver.</summary>
    public int? FechamentoId { get; init; }
    public DateTime? FechamentoDe { get; init; }
    public DateTime? FechamentoAte { get; init; }
    public decimal? FechamentoValor { get; init; }
    public string? SituacaoConta { get; init; }

    public bool Fechada => FechamentoId is not null;
}

/// <summary>Parcela recebida que compõe a comissão.</summary>
public sealed record ComissaoParcela
{
    public int ParcelaId { get; init; }
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public DateTime DataPagamento { get; init; }
    public decimal ValorPago { get; init; }
    public decimal Percentual { get; init; }
    public decimal Comissao { get; init; }
    public string EventoCodigo => $"EVT-{EventoId:D5}";
}

public sealed record FechamentoComissaoResumo
{
    public int Id { get; init; }
    public int VendedorId { get; init; }
    public string Vendedor { get; init; } = string.Empty;
    public DateTime De { get; init; }
    public DateTime Ate { get; init; }
    public decimal BaseCalculo { get; init; }
    public decimal Valor { get; init; }
    public DateTime CriadoEm { get; init; }
    public int ContaPagarId { get; init; }
    public string SituacaoConta { get; init; } = string.Empty;
}

public interface IConsultasComissoes
{
    /// <summary>Comissões por vendedor sobre as parcelas pagas no período (percentual congelado em cada evento).</summary>
    Task<IReadOnlyList<ComissaoVendedor>> ApurarAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IReadOnlyList<ComissaoParcela>> DetalharAsync(int vendedorId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IReadOnlyList<FechamentoComissaoResumo>> ListarFechamentosAsync(CancellationToken ct = default);
}

// ============================ Auditoria ============================

public sealed record FiltroAuditoria
{
    public DateTime De { get; init; }
    public DateTime Ate { get; init; }
    public string? Usuario { get; init; }
    public string? Entidade { get; init; }
    public AcaoAuditoria? Acao { get; init; }

    /// <summary>Filtra um registro específico (ex.: o histórico de um evento).</summary>
    public string? RegistroId { get; init; }

    public string? Texto { get; init; }
    public int Limite { get; init; } = 2000;
}

public sealed record RegistroAuditoriaResumo
{
    public long Id { get; init; }
    public DateTime DataHora { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string Entidade { get; init; } = string.Empty;
    public string RegistroId { get; init; } = string.Empty;
    public string? Descricao { get; init; }
    public AcaoAuditoria Acao { get; init; }
    public string Alteracoes { get; init; } = "[]";

    public IReadOnlyList<AlteracaoCampo> Campos => AlteracaoCampo.Ler(Alteracoes);
}

public interface IConsultasAuditoria
{
    Task<IReadOnlyList<RegistroAuditoriaResumo>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct = default);

    /// <summary>Entidades e usuários que já aparecem na auditoria (para os filtros da tela).</summary>
    Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Usuarios)> OpcoesFiltroAsync(CancellationToken ct = default);
}
