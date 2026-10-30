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

    /// <summary>O que o prato contém além do que vem dos insumos da ficha técnica (ex.: o molho leva trigo e não está na ficha).</summary>
    public ConteudoAlimentar ContemInformado { get; set; }

    /// <summary>Prato servido no lugar deste a quem tem uma restrição que ele não atende (ex.: risoto de cogumelos no lugar do filé).</summary>
    public int? SubstitutoId { get; set; }
    public ItemCardapio? Substituto { get; set; }

    public IReadOnlyCollection<FichaTecnicaItem> Ingredientes => _ingredientes;

    /// <summary>O que o prato contém: o informado mais o que vem dos insumos (requer os produtos da ficha carregados).</summary>
    public ConteudoAlimentar Contem =>
        _ingredientes.Aggregate(ContemInformado, (soma, f) => soma | (f.Produto?.Contem ?? ConteudoAlimentar.Nenhum));

    /// <summary>Sem ficha técnica nem nada informado, não dá para dizer para quem o prato serve.</summary>
    public bool TemComposicao => _ingredientes.Count > 0 || ContemInformado != ConteudoAlimentar.Nenhum;

    public bool Serve(RestricaoAlimentar restricoes) => RestricoesAlimentares.Serve(Contem, restricoes);

    /// <summary>Confere o substituto escolhido: outro prato, ativo.</summary>
    public void DefinirSubstituto(ItemCardapio? substituto)
    {
        Validador.Garantir(substituto is null || !ReferenceEquals(substituto, this) && (Id == 0 || substituto.Id != Id),
            "O prato não pode ser o substituto dele mesmo.");
        Validador.Garantir(substituto is null || substituto.Ativo, $"O substituto '{substituto?.Nome}' está inativo.");
        Substituto = substituto;
        SubstitutoId = substituto?.Id;
    }

    /// <summary>Custo de insumos por pessoa, na referência (todos adultos, 5 horas); requer os produtos carregados.</summary>
    public decimal CustoPorPessoa => CustoInsumos.Referencia;

    /// <summary>Custo dos insumos separado pelo que muda com a duração e com os adultos (requer os produtos carregados).</summary>
    public CustoInsumos CustoInsumos => new(
        _ingredientes.Where(f => !f.PorHora && !f.SoAdultos).Sum(f => f.CustoUnitario),
        _ingredientes.Where(f => f.PorHora && !f.SoAdultos).Sum(f => f.CustoUnitario),
        _ingredientes.Where(f => !f.PorHora && f.SoAdultos).Sum(f => f.CustoUnitario),
        _ingredientes.Where(f => f.PorHora && f.SoAdultos).Sum(f => f.CustoUnitario));

    /// <summary>
    /// Substitui a ficha técnica preservando as linhas já existentes (para o EF detectar
    /// inclusões, alterações e exclusões corretamente).
    /// </summary>
    public void DefinirFichaTecnica(IEnumerable<(int ProdutoId, decimal QuantidadePorPessoa)> ingredientes) =>
        DefinirFichaTecnica(ingredientes.Select(i => (i.ProdutoId, i.QuantidadePorPessoa, false, false)));

    /// <param name="ingredientes">
    /// Por insumo: quantidade por pessoa (ou por pessoa e hora, se <c>PorHora</c>) e se só os adultos consomem (alcoólicos).
    /// </param>
    public void DefinirFichaTecnica(IEnumerable<(int ProdutoId, decimal QuantidadePorPessoa, bool PorHora, bool SoAdultos)> ingredientes)
    {
        var novos = ingredientes.ToList();
        Validador.Garantir(novos.All(i => i.QuantidadePorPessoa > 0),
            "A quantidade por pessoa de cada ingrediente deve ser maior que zero.");
        Validador.Garantir(novos.Select(i => i.ProdutoId).Distinct().Count() == novos.Count,
            "Há ingredientes repetidos na ficha técnica.");

        _ingredientes.RemoveAll(atual => novos.All(n => n.ProdutoId != atual.ProdutoId));

        foreach (var (produtoId, quantidade, porHora, soAdultos) in novos)
        {
            var existente = _ingredientes.FirstOrDefault(i => i.ProdutoId == produtoId);
            if (existente is null)
                _ingredientes.Add(existente = new FichaTecnicaItem(produtoId, quantidade));
            existente.QuantidadePorPessoa = quantidade;
            existente.PorHora = porHora;
            existente.SoAdultos = soAdultos;
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

    /// <summary>Quantidade do insumo (na unidade do produto) consumida por convidado (ou por convidado e hora, se <see cref="PorHora"/>).</summary>
    public decimal QuantidadePorPessoa { get; internal set; }

    /// <summary>A quantidade é por hora de evento (bebidas: quanto mais longo, mais se bebe).</summary>
    public bool PorHora { get; internal set; }

    /// <summary>Só os adultos consomem (bebidas alcoólicas): as crianças (meia e isentos) ficam de fora.</summary>
    public bool SoAdultos { get; internal set; }

    /// <summary>Custo por unidade de consumo (por pessoa, por pessoa e hora, por adulto...).</summary>
    public decimal CustoUnitario => QuantidadePorPessoa * (Produto?.CustoMedio ?? 0);

    /// <summary>Custo por pessoa na referência (todos adultos, 5 horas).</summary>
    public decimal CustoPorPessoa => CustoUnitario * (PorHora ? ContextoConsumo.HorasReferencia : 1);

    /// <summary>Quanto usar para as porções do prato, pela duração e pela parte de adultos do evento.</summary>
    public decimal Quantidade(int porcoes, ContextoConsumo contexto) =>
        QuantidadePorPessoa * porcoes * (SoAdultos ? contexto.FatorAdultos : 1) * (PorHora ? contexto.Horas : 1);
}
