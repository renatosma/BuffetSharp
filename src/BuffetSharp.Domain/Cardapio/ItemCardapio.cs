using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.Domain.Cardapio;

/// <summary>
/// Prato/bebida servido no evento, com ficha técnica (insumos consumidos por pessoa).
/// </summary>
public class ItemCardapio : Entidade, IAtivavel
{
    private readonly List<FichaTecnicaItem> _ingredientes = [];

    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a categoria do cardápio.")]
    public int CategoriaCardapioId { get; set; }
    public CategoriaCardapio? CategoriaCardapio { get; set; }

    public bool Ativo { get; set; } = true;

    public IReadOnlyCollection<FichaTecnicaItem> Ingredientes => _ingredientes;

    /// <summary>Custo de insumos por pessoa (requer os produtos carregados).</summary>
    public decimal CustoPorPessoa => _ingredientes.Sum(i => i.CustoPorPessoa);

    /// <summary>
    /// Substitui a ficha técnica preservando as linhas já existentes (para o EF detectar
    /// inclusões, alterações e exclusões corretamente).
    /// </summary>
    public void DefinirFichaTecnica(IEnumerable<(int ProdutoId, decimal QuantidadePorPessoa)> ingredientes)
    {
        var novos = ingredientes.ToList();
        Validador.Garantir(novos.All(i => i.QuantidadePorPessoa > 0),
            "A quantidade por pessoa de cada ingrediente deve ser maior que zero.");
        Validador.Garantir(novos.Select(i => i.ProdutoId).Distinct().Count() == novos.Count,
            "Há ingredientes repetidos na ficha técnica.");

        _ingredientes.RemoveAll(atual => novos.All(n => n.ProdutoId != atual.ProdutoId));

        foreach (var (produtoId, quantidade) in novos)
        {
            var existente = _ingredientes.FirstOrDefault(i => i.ProdutoId == produtoId);
            if (existente is null)
                _ingredientes.Add(new FichaTecnicaItem(produtoId, quantidade));
            else
                existente.QuantidadePorPessoa = quantidade;
        }
    }

    public override string ToString() => Nome;
}

public class FichaTecnicaItem
{
    private FichaTecnicaItem()
    {
    }

    internal FichaTecnicaItem(int produtoId, decimal quantidadePorPessoa)
    {
        ProdutoId = produtoId;
        QuantidadePorPessoa = quantidadePorPessoa;
    }

    public int ItemCardapioId { get; private set; }
    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Quantidade do insumo (na unidade do produto) consumida por convidado.</summary>
    public decimal QuantidadePorPessoa { get; internal set; }

    public decimal CustoPorPessoa => QuantidadePorPessoa * (Produto?.CustoMedio ?? 0);
}
