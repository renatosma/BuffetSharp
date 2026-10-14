using System.Globalization;

namespace BuffetSharp.Application.Consultas;

public sealed class ResumoDashboard
{
    public int EventosNoMes { get; set; }
    public decimal FaturamentoPrevistoMes { get; set; }
    public decimal RecebidoNoMes { get; set; }
    public decimal AReceberVencido { get; set; }
    public decimal APagarProximos7Dias { get; set; }
    public int OrcamentosEmAberto { get; set; }
    public decimal ValorOrcamentosEmAberto { get; set; }
    public decimal TaxaConversao90Dias { get; set; }
    public int ProdutosAbaixoMinimo { get; set; }

    public IReadOnlyList<ProximoEvento> ProximosEventos { get; set; } = [];
    public IReadOnlyList<ValorMensal> FaturamentoMensal { get; set; } = [];
    public IReadOnlyList<TituloVencido> ParcelasVencidas { get; set; } = [];
    public IReadOnlyList<ProdutoAlerta> ProdutosEmAlerta { get; set; } = [];
}

public sealed record ProximoEvento
{
    public int Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string Espaco { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public int QuantidadeConvidados { get; init; }
    public decimal ValorTotal { get; init; }
    public decimal SaldoAReceber { get; init; }
    public int EquipeEscalada { get; init; }
    public string Codigo => $"EVT-{Id:D5}";
}

public sealed record ValorMensal
{
    public int Ano { get; init; }
    public int Mes { get; init; }

    /// <summary>Série principal (ex.: valor contratado pela data do evento).</summary>
    public decimal Valor { get; init; }

    /// <summary>Série secundária (ex.: valor recebido no mês).</summary>
    public decimal Valor2 { get; init; }

    public string Rotulo =>
        $"{CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetAbbreviatedMonthName(Mes)}/{Ano % 100:00}";
}

public sealed record TituloVencido
{
    public int Id { get; init; }
    public int EventoId { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public DateTime Vencimento { get; init; }
    public decimal Valor { get; init; }
    public int DiasAtraso { get; init; }
}

public sealed record ProdutoAlerta
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Unidade { get; init; } = string.Empty;
    public decimal EstoqueAtual { get; init; }
    public decimal EstoqueMinimo { get; init; }
}

public interface IConsultasDashboard
{
    Task<ResumoDashboard> ObterAsync(DateOnly hoje, CancellationToken ct = default);
}
