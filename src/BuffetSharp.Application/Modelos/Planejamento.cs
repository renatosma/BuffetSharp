using BuffetSharp.Domain.Eventos;

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

    /// <summary>Embalagem sugerida, quantas comprar e quantas unidades do estoque há em cada uma (para gerar o pedido).</summary>
    public int? EmbalagemId { get; init; }
    public decimal? QuantidadeEmbalagens { get; init; }
    public decimal? FatorEmbalagem { get; init; }

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

/// <summary>Etapa do cronograma do evento (Id 0 = nova).</summary>
public sealed class EtapaInput
{
    public int Id { get; set; }
    public DateTime Horario { get; set; }
    public int DuracaoMinutos { get; set; } = 30;
    public string Atividade { get; set; } = string.Empty;
    public string? Local { get; set; }

    /// <summary>Texto livre ("DJ", "Cerimonial"...) quando não é alguém da escala.</summary>
    public string? Responsavel { get; set; }

    /// <summary>Funcionário da escala que conduz a etapa.</summary>
    public int? FuncionarioId { get; set; }

    public string? Observacao { get; set; }
}

/// <summary>Mesa do mapa do evento ou do mapa padrão do espaço (Id 0 = nova).</summary>
public sealed class MesaInput : IMesaPlanta
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public string? Nome { get; set; }
    public int Lugares { get; set; } = PlantaSalao.LugaresPadrao;
    public FormatoMesa Formato { get; set; }

    /// <summary>Posição na planta; nula na mesa nova, que vai para o primeiro lugar livre.</summary>
    public int? X { get; set; }

    public int? Y { get; set; }

    int IMesaPlanta.X => X ?? 0;
    int IMesaPlanta.Y => Y ?? 0;

    public static MesaInput De(IMesaPlanta mesa, int id = 0) => new()
    {
        Id = id, Numero = mesa.Numero, Nome = mesa.Nome, Lugares = mesa.Lugares, Formato = mesa.Formato, X = mesa.X, Y = mesa.Y
    };
}

/// <summary>Tarefa nova (ou alterada) do checklist do evento.</summary>
public sealed class TarefaInput
{
    public string Descricao { get; set; } = string.Empty;
    public DateOnly Prazo { get; set; }
    public string? Responsavel { get; set; }
}
