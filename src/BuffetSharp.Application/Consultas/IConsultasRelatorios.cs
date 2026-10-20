using System.Globalization;

namespace BuffetSharp.Application.Consultas;

public sealed record FaturamentoTipoEvento
{
    public string TipoEvento { get; init; } = string.Empty;
    public int QuantidadeEventos { get; init; }
    public int Convidados { get; init; }
    public decimal ValorTotal { get; init; }
    public decimal TicketMedio => QuantidadeEventos == 0 ? 0 : Math.Round(ValorTotal / QuantidadeEventos, 2);
    public decimal ValorPorConvidado => Convidados == 0 ? 0 : Math.Round(ValorTotal / Convidados, 2);
}

public sealed record RankingPacote
{
    public string Pacote { get; init; } = string.Empty;
    public int QuantidadeEventos { get; init; }
    public int Convidados { get; init; }
    public decimal ValorTotal { get; init; }
}

public sealed record LucratividadeEvento
{
    public int Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Convidados { get; init; }
    public decimal Receita { get; init; }
    public decimal CustoInsumos { get; init; }
    public decimal CustoEquipe { get; init; }

    /// <summary>Locação de materiais de terceiros + perdas na devolução.</summary>
    public decimal CustoMateriais { get; init; }

    public decimal Despesas { get; init; }
    public decimal Lucro => Receita - CustoInsumos - CustoEquipe - CustoMateriais - Despesas;
    public decimal MargemPercentual => Receita == 0 ? 0 : Math.Round(Lucro / Receita * 100, 1);
    public string Codigo => $"EVT-{Id:D5}";
}

public sealed record FluxoCaixaMes
{
    public int Ano { get; init; }
    public int Mes { get; init; }
    public decimal ReceberPrevisto { get; init; }
    public decimal Recebido { get; init; }
    public decimal PagarPrevisto { get; init; }
    public decimal Pago { get; init; }
    public decimal SaldoPrevisto => ReceberPrevisto - PagarPrevisto;
    public decimal SaldoRealizado => Recebido - Pago;
    public string Rotulo => new DateTime(Ano, Mes, 1).ToString("MMM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
}

public sealed record ConversaoOrcamentos
{
    public string Status { get; init; } = string.Empty;
    public int Quantidade { get; init; }
    public decimal Valor { get; init; }
}

public enum RegimeDre
{
    /// <summary>Pelo dinheiro que entrou e saiu (data do pagamento).</summary>
    Caixa,

    /// <summary>Pelo fato gerador: receita na data do evento, despesa no vencimento.</summary>
    Competencia
}

/// <summary>Linha do demonstrativo de resultado, mês a mês (valores de custos e despesas negativos).</summary>
public sealed record LinhaDre
{
    public string Descricao { get; init; } = string.Empty;

    /// <summary>0 = resultado, 1 = grupo, 2 = detalhe (tipo de evento ou centro de custo).</summary>
    public int Nivel { get; init; }

    public IReadOnlyList<decimal> Meses { get; init; } = new decimal[12];
    public decimal Total => Meses.Sum();

    /// <summary>Participação do total da linha na receita bruta do ano.</summary>
    public decimal? PercentualReceita { get; init; }
}

/// <summary>DRE gerencial de um ano: receita por tipo de evento, custos e despesas por centro de custo (categoria).</summary>
public sealed record Dre(int Ano, RegimeDre Regime, IReadOnlyList<LinhaDre> Linhas);

/// <summary>Relatórios gerenciais (Dapper, SQL otimizado com agregações).</summary>
public interface IConsultasRelatorios
{
    Task<IReadOnlyList<FaturamentoTipoEvento>> FaturamentoPorTipoAsync(DateTime de, DateTime ate, CancellationToken ct = default);
    Task<IReadOnlyList<RankingPacote>> RankingPacotesAsync(DateTime de, DateTime ate, CancellationToken ct = default);
    Task<IReadOnlyList<LucratividadeEvento>> LucratividadeAsync(DateTime de, DateTime ate, CancellationToken ct = default);
    Task<IReadOnlyList<FluxoCaixaMes>> FluxoCaixaAsync(DateTime de, DateTime ate, CancellationToken ct = default);
    Task<IReadOnlyList<ConversaoOrcamentos>> ConversaoOrcamentosAsync(DateTime de, DateTime ate, CancellationToken ct = default);
    Task<Dre> DreAsync(int ano, RegimeDre regime, CancellationToken ct = default);
}
