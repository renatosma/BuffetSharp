using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

public enum CategoriaMaterial
{
    Louca,
    Talheres,
    CoposETacas,
    Enxoval,
    Mobiliario,
    Decoracao,
    Utensilios,
    Outros
}

/// <summary>
/// Material reutilizável enviado aos eventos (louça, talheres, taças, toalhas, mobiliário...).
/// A quantidade própria é o patrimônio do buffet; o que faltar em uma data pode ser alugado de terceiros.
/// </summary>
public class Material : Entidade, IAtivavel, ICamposControlados
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    public CategoriaMaterial Categoria { get; set; } = CategoriaMaterial.Louca;

    /// <summary>Patrimônio próprio. Informado no cadastro e depois alterado só por entradas, baixas e perdas.</summary>
    [Range(0, 1_000_000, ErrorMessage = "Quantidade inválida.")]
    public int QuantidadePropria { get; set; }

    /// <summary>Valor unitário cobrado/contabilizado em caso de quebra ou extravio.</summary>
    [Range(0, 100_000, ErrorMessage = "Valor de reposição inválido.")]
    public decimal ValorReposicao { get; set; }

    /// <summary>Custo unitário para alugar de terceiros quando o patrimônio não é suficiente.</summary>
    [Range(0, 100_000, ErrorMessage = "Custo de locação inválido.")]
    public decimal CustoLocacao { get; set; }

    /// <summary>Locador padrão (fornecedor de quem o material é alugado).</summary>
    public int? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    [MaxLength(500)]
    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;

    public decimal ValorPatrimonio => QuantidadePropria * ValorReposicao;

    IReadOnlyCollection<string> ICamposControlados.CamposControladosPeloSistema => [nameof(QuantidadePropria)];

    /// <summary>Compra (diferença positiva) ou baixa por descarte/perda (diferença negativa).</summary>
    public void AjustarPatrimonio(int diferenca)
    {
        Validador.Garantir(diferenca != 0, "Informe a quantidade a ajustar.");
        Validador.Garantir(QuantidadePropria + diferenca >= 0,
            $"'{Nome}' tem {QuantidadePropria} unidade(s) no patrimônio; não é possível baixar {-diferenca}.");
        QuantidadePropria += diferenca;
    }

    public override string ToString() => Nome;
}
