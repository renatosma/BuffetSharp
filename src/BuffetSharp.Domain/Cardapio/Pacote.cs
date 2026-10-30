using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cardapio;

/// <summary>
/// Pacote comercial (ex.: Bronze, Prata, Ouro): conjunto de itens do cardápio com preço por pessoa.
/// </summary>
public class Pacote : Entidade, IAtivavel, IValidatableObject
{
    private readonly List<PacoteItem> _itens = [];

    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [Range(0.01, 100_000, ErrorMessage = "Informe o preço por pessoa.")]
    public decimal PrecoPorPessoa { get; set; }

    /// <summary>Mínimo de convidados pagantes (inteira + meia); os isentos não contam.</summary>
    [Range(1, 10_000, ErrorMessage = "O mínimo de convidados deve estar entre 1 e 10.000.")]
    public int MinimoConvidados { get; set; } = 1;

    /// <summary>Crianças até esta idade não pagam.</summary>
    [Range(0, RegraFaixasEtarias.IdadeMaximaCrianca, ErrorMessage = "A idade dos isentos deve estar entre 0 e 17 anos.")]
    public int IdadeMaximaIsento { get; set; } = RegraFaixasEtarias.Padrao.IdadeMaximaIsento;

    /// <summary>Crianças acima da idade dos isentos e até esta idade pagam meia.</summary>
    [Range(0, RegraFaixasEtarias.IdadeMaximaCrianca, ErrorMessage = "A idade da meia deve estar entre 0 e 17 anos.")]
    public int IdadeMaximaMeia { get; set; } = RegraFaixasEtarias.Padrao.IdadeMaximaMeia;

    /// <summary>Percentual do valor por pessoa cobrado na meia.</summary>
    [Range(0, 100, ErrorMessage = "O percentual da meia deve estar entre 0% e 100%.")]
    public decimal PercentualMeia { get; set; } = RegraFaixasEtarias.Padrao.PercentualMeia;

    public bool Ativo { get; set; } = true;

    public IReadOnlyCollection<PacoteItem> Itens => _itens;

    public RegraFaixasEtarias RegraFaixas => new(IdadeMaximaIsento, IdadeMaximaMeia, PercentualMeia);

    /// <summary>Custo de insumos por pessoa na referência: todos adultos, 5 horas (requer itens e fichas técnicas carregados).</summary>
    public decimal CustoPorPessoa => CustoInsumos.Referencia;

    /// <summary>Custo dos insumos separado pelo que muda com a duração e com os adultos.</summary>
    public CustoInsumos CustoInsumos => _itens.Select(i => i.ItemCardapio?.CustoInsumos ?? CustoInsumos.Nenhum)
        .Aggregate(CustoInsumos.Nenhum, (soma, c) => new CustoInsumos(soma.PorPessoa + c.PorPessoa, soma.PorPessoaHora + c.PorPessoaHora,
            soma.PorAdulto + c.PorAdulto, soma.PorAdultoHora + c.PorAdultoHora));

    public decimal MargemPercentual =>
        PrecoPorPessoa == 0 ? 0 : Math.Round((PrecoPorPessoa - CustoPorPessoa) / PrecoPorPessoa * 100, 2);

    public void DefinirItens(IEnumerable<int> itensCardapioIds)
    {
        var ids = itensCardapioIds.Distinct().ToList();
        _itens.RemoveAll(i => !ids.Contains(i.ItemCardapioId));
        foreach (var id in ids.Where(id => _itens.All(i => i.ItemCardapioId != id)))
            _itens.Add(new PacoteItem(id));
    }

    /// <summary>
    /// Consumo total de insumos para a quantidade de convidados: ProdutoId → quantidade.
    /// </summary>
    public Dictionary<int, decimal> CalcularConsumo(int convidados) => CalcularConsumo(ContextoConsumo.Padrao(convidados));

    /// <summary>Consumo pela duração e pelos adultos do evento (bebidas por hora e alcoólicas só para adultos).</summary>
    public Dictionary<int, decimal> CalcularConsumo(ContextoConsumo contexto)
    {
        Validador.Garantir(contexto.Pessoas > 0, "A quantidade de convidados deve ser maior que zero.");

        return _itens
            .Where(i => i.ItemCardapio is not null)
            .SelectMany(i => i.ItemCardapio!.Ingredientes)
            .GroupBy(f => f.ProdutoId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => f.Quantidade(contexto.Pessoas, contexto)), 3));
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IdadeMaximaMeia < IdadeMaximaIsento)
            yield return new ValidationResult("A idade da meia não pode ser menor que a dos isentos.", [nameof(IdadeMaximaMeia)]);
    }

    public override string ToString() => Nome;
}

public class PacoteItem
{
    private PacoteItem()
    {
    }

    internal PacoteItem(int itemCardapioId) => ItemCardapioId = itemCardapioId;

    public int PacoteId { get; private set; }
    public int ItemCardapioId { get; private set; }
    public ItemCardapio? ItemCardapio { get; private set; }
}
