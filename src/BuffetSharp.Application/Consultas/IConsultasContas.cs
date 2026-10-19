using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Consultas;

public sealed record ContaFinanceiraResumo
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public TipoContaFinanceira Tipo { get; init; }
    public string? Banco { get; init; }
    public string? Agencia { get; init; }
    public string? Numero { get; init; }
    public decimal SaldoInicial { get; init; }
    public DateTime DataSaldoInicial { get; init; }
    public DateTime? FechadaAte { get; init; }
    public bool Ativo { get; init; }

    /// <summary>Saldo inicial + todos os movimentos.</summary>
    public decimal Saldo { get; init; }

    public DateTime? UltimoMovimento { get; init; }
}

public sealed record MovimentoExtrato
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public OrigemMovimento Origem { get; init; }

    /// <summary>Positivo = entrada; negativo = saída.</summary>
    public decimal Valor { get; init; }

    /// <summary>Saldo da conta depois deste movimento.</summary>
    public decimal Saldo { get; init; }

    /// <summary>Dentro de um período já fechado (conferido).</summary>
    public bool Conferido { get; init; }

    public decimal? Entrada => Valor > 0 ? Valor : null;
    public decimal? Saida => Valor < 0 ? -Valor : null;
}

public sealed record ExtratoConta(ContaFinanceiraResumo Conta, decimal SaldoAnterior, IReadOnlyList<MovimentoExtrato> Movimentos)
{
    public decimal SaldoFinal => Movimentos.Count == 0 ? SaldoAnterior : Movimentos[^1].Saldo;
}

public sealed record FechamentoContaResumo
{
    public int Id { get; init; }
    public DateTime Data { get; init; }
    public decimal SaldoSistema { get; init; }
    public decimal SaldoInformado { get; init; }
    public string? Observacao { get; init; }
    public string? Usuario { get; init; }
    public DateTime CriadoEm { get; init; }
    public decimal Diferenca => SaldoInformado - SaldoSistema;
}

/// <summary>Conta em aberto de um cliente (parcela) ou fornecedor, para a tela de recebimento/pagamento.</summary>
public sealed record TituloEmAberto
{
    public int Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public int? EventoId { get; init; }
    public string? EventoTitulo { get; init; }
    public DateTime Vencimento { get; init; }
    public decimal Valor { get; init; }
    public int? PessoaId { get; init; }
    public string? Pessoa { get; init; }

    /// <summary>Categoria da despesa (somente contas a pagar).</summary>
    public CategoriaDespesa? Categoria { get; init; }
    public string? Documento { get; init; }
    public string? Referencia => EventoId is { } id ? $"EVT-{id:D5} - {EventoTitulo}" : null;
}

/// <summary>Caixa e contas bancárias: saldos, extrato, fechamentos e as contas em aberto para baixa.</summary>
public interface IConsultasContas
{
    Task<IReadOnlyList<ContaFinanceiraResumo>> ListarContasAsync(bool somenteAtivas = true, CancellationToken ct = default);

    Task<ExtratoConta> ExtratoAsync(int contaId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IReadOnlyList<FechamentoContaResumo>> ListarFechamentosAsync(int contaId, CancellationToken ct = default);

    /// <param name="pessoaId">Cliente (recebimento) ou fornecedor (pagamento); no pagamento, nulo = contas sem fornecedor.</param>
    Task<IReadOnlyList<TituloEmAberto>> TitulosEmAbertoAsync(TipoBaixa tipo, int? pessoaId, CancellationToken ct = default);
}
