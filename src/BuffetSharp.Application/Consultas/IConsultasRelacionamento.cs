using BuffetSharp.Domain.Comercial;

namespace BuffetSharp.Application.Consultas;

// ============================ Leads (funil) ============================

public sealed record FiltroLeads
{
    public StatusLead? Status { get; init; }

    /// <summary>Somente leads em negociação (novos e em contato).</summary>
    public bool SomenteEmAberto { get; init; }

    /// <summary>Somente os com retorno combinado até esta data (inclusive).</summary>
    public DateTime? RetornoAte { get; init; }

    public DateTime? CriadoDe { get; init; }
    public DateTime? CriadoAte { get; init; }
}

public sealed record LeadResumo
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public OrigemLead Origem { get; init; }
    public string? DetalheOrigem { get; init; }
    public int? TipoEventoId { get; init; }
    public string? TipoEvento { get; init; }
    public DateTime? DataPrevista { get; init; }
    public int? Convidados { get; init; }
    public StatusLead Status { get; init; }
    public DateTime? ProximoContato { get; init; }
    public string? Responsavel { get; init; }
    public int? ResponsavelId { get; init; }
    public DateTime CriadoEm { get; init; }
    public DateTime? UltimoContato { get; init; }
    public int Contatos { get; init; }
    public int? ClienteId { get; init; }
    public string? Cliente { get; init; }
    public string? MotivoPerda { get; init; }
    public string Codigo => $"LEAD-{Id:D5}";
    public bool EmAberto => Status is StatusLead.Novo or StatusLead.EmContato;
}

public sealed record InteracaoLeadResumo(DateTime Data, string? Usuario, string Descricao);

/// <summary>Conversão dos leads de um canal de origem no período.</summary>
public sealed record FunilOrigem
{
    public OrigemLead Origem { get; init; }
    public int Leads { get; init; }
    public int EmAberto { get; init; }
    public int Convertidos { get; init; }
    public int Perdidos { get; init; }

    /// <summary>Eventos fechados pelos clientes vindos desses leads (depois do primeiro contato).</summary>
    public int EventosFechados { get; init; }

    public decimal ValorFechado { get; init; }
    public decimal TaxaConversao => Leads == 0 ? 0 : Math.Round(Convertidos * 100m / Leads, 1);
    public decimal TaxaFechamento => Leads == 0 ? 0 : Math.Round(Math.Min(EventosFechados, Leads) * 100m / Leads, 1);
}

public interface IConsultasLeads
{
    Task<IReadOnlyList<LeadResumo>> ListarAsync(FiltroLeads filtro, CancellationToken ct = default);
    Task<IReadOnlyList<InteracaoLeadResumo>> InteracoesAsync(int leadId, CancellationToken ct = default);
    Task<IReadOnlyList<FunilOrigem>> FunilPorOrigemAsync(DateTime de, DateTime ate, CancellationToken ct = default);
}

// ============================ Lembretes ============================

/// <summary>
/// Algo que pede atenção hoje (proposta vencendo, parcela a cobrar, aniversário, retorno de lead...). A
/// <see cref="Chave"/> identifica a ocorrência, para o lembrete sumir depois de tratado.
/// </summary>
public sealed record Lembrete
{
    public TipoLembrete Tipo { get; init; }
    public int ReferenciaId { get; init; }

    /// <summary>Quando acontece (vencimento, data da degustação, aniversário, prazo da tarefa...).</summary>
    public DateTime Data { get; init; }

    public string Titulo { get; init; } = string.Empty;
    public string? Detalhe { get; init; }

    /// <summary>Com quem falar (cliente, lead, fornecedor).</summary>
    public string? Pessoa { get; init; }

    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public decimal? Valor { get; init; }
    public int? EventoId { get; init; }
    public int? ClienteId { get; init; }
    public string Chave { get; init; } = string.Empty;
}

public interface IConsultasLembretes
{
    /// <summary>Todos os lembretes de hoje (sem descontar os já tratados).</summary>
    Task<IReadOnlyList<Lembrete>> CalcularAsync(DateTime agora, LembretesOptionsConsulta opcoes, CancellationToken ct = default);
}

/// <summary>Antecedências usadas no cálculo dos lembretes.</summary>
public sealed record LembretesOptionsConsulta(int DiasOrcamento, int DiasParcela, int DiasContaPagar);
