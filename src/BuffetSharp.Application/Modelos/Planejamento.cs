namespace BuffetSharp.Application.Modelos;

// Planejamento da operação: o que comprar e o que produzir para os eventos confirmados.

/// <summary>Evento considerado no planejamento.</summary>
public sealed record EventoPlanejado(int Id, string Titulo, DateTime Inicio, int Pessoas, string? Pacote)
{
    public string Codigo => $"EVT-{Id:D5}";
}

/// <summary>Insumo a comprar: necessidade dos eventos (ficha técnica × pessoas) menos o estoque.</summary>
public sealed record ItemListaCompras
{
    public int ProdutoId { get; init; }
    public string Produto { get; init; } = string.Empty;
    public string Unidade { get; init; } = string.Empty;
    public string? Categoria { get; init; }
    public int? FornecedorId { get; init; }
    public string Fornecedor { get; init; } = "(sem fornecedor padrão)";
    public string? TelefoneFornecedor { get; init; }
    public string? EmailFornecedor { get; init; }

    /// <summary>Consumo previsto dos eventos do período, na unidade do estoque.</summary>
    public decimal Necessidade { get; init; }

    public decimal Estoque { get; init; }
    public decimal EstoqueMinimo { get; init; }

    /// <summary>Quanto comprar, na unidade do estoque.</summary>
    public decimal AComprar { get; init; }

    public decimal CustoUnitario { get; init; }

    /// <summary>Sugestão na embalagem do fornecedor (ex.: "3 CX de 12"), quando houver.</summary>
    public string? Embalagem { get; init; }

    public IReadOnlyList<string> Eventos { get; init; } = [];

    public decimal CustoEstimado => Math.Round(AComprar * CustoUnitario, 2);
}

public sealed record ListaCompras(DateOnly De, DateOnly Ate, bool ReporMinimo, IReadOnlyList<EventoPlanejado> Eventos,
    IReadOnlyList<ItemListaCompras> Itens)
{
    /// <summary>Eventos sem pacote não entram na conta (não têm ficha técnica).</summary>
    public IEnumerable<EventoPlanejado> SemPacote => Eventos.Where(e => e.Pacote is null);

    public decimal CustoTotal => Itens.Sum(i => i.CustoEstimado);
}

/// <summary>Ordem de produção para a cozinha: pratos do evento por seção, com os insumos já multiplicados.</summary>
public sealed record OrdemProducao
{
    public int EventoId { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public DateTime Fim { get; init; }
    public string Espaco { get; init; } = string.Empty;

    /// <summary>Pessoas servidas (inteira + meia + isentos).</summary>
    public int Pessoas { get; init; }

    /// <summary>Ex.: "120 convidados (100 inteira, 12 meia, 8 isentos)".</summary>
    public string Convidados { get; init; } = string.Empty;

    public string? Pacote { get; init; }
    public string? Observacoes { get; init; }
    public IReadOnlyList<PratoProducao> Pratos { get; init; } = [];
    public string Codigo => $"EVT-{EventoId:D5}";
}

public sealed record PratoProducao(string Secao, int OrdemSecao, string Prato, int Porcoes, IReadOnlyList<IngredienteProducao> Ingredientes);

public sealed record IngredienteProducao(string Produto, decimal Quantidade, string Unidade);

/// <summary>Tarefa nova (ou alterada) do checklist do evento.</summary>
public sealed class TarefaInput
{
    public string Descricao { get; set; } = string.Empty;
    public DateOnly Prazo { get; set; }
    public string? Responsavel { get; set; }
}
