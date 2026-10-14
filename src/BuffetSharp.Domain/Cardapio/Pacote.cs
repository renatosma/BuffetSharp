using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cardapio;

/// <summary>
/// Pacote comercial (ex.: Bronze, Prata, Ouro): conjunto de itens do cardápio com preço por pessoa.
/// </summary>
public class Pacote : Entidade, IAtivavel
{
    private readonly List<PacoteItem> _itens = [];

    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [Range(0.01, 100_000, ErrorMessage = "Informe o preço por pessoa.")]
    public decimal PrecoPorPessoa { get; set; }

    [Range(1, 10_000, ErrorMessage = "O mínimo de convidados deve estar entre 1 e 10.000.")]
    public int MinimoConvidados { get; set; } = 1;

    public bool Ativo { get; set; } = true;

    public IReadOnlyCollection<PacoteItem> Itens => _itens;

    /// <summary>Custo de insumos por pessoa (requer itens e fichas técnicas carregados).</summary>
    public decimal CustoPorPessoa => _itens.Sum(i => i.ItemCardapio?.CustoPorPessoa ?? 0);

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
    public Dictionary<int, decimal> CalcularConsumo(int convidados)
    {
        Validador.Garantir(convidados > 0, "A quantidade de convidados deve ser maior que zero.");

        return _itens
            .Where(i => i.ItemCardapio is not null)
            .SelectMany(i => i.ItemCardapio!.Ingredientes)
            .GroupBy(f => f.ProdutoId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => f.QuantidadePorPessoa) * convidados, 3));
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
