using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

// =====================================================================
// Nível 1 - Cadastros básicos: tabelas de apoio com poucos campos e
// nenhuma regra além da validação de preenchimento.
// =====================================================================

/// <summary>Casamento, Aniversário, 15 Anos, Corporativo, Formatura...</summary>
public class TipoEvento : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}

/// <summary>Categoria de insumos do estoque (Carnes, Bebidas, Hortifrúti...).</summary>
public class CategoriaProduto : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}

/// <summary>Seção do cardápio (Entradas, Prato Principal, Sobremesas...).</summary>
public class CategoriaCardapio : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Ordem de exibição na proposta/cardápio.</summary>
    [Range(0, 999, ErrorMessage = "A ordem deve estar entre 0 e 999.")]
    public int Ordem { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}

public class UnidadeMedida : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe a sigla.")]
    [MaxLength(10)]
    public string Sigla { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição.")]
    [MaxLength(60)]
    public string Descricao { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public override string ToString() => $"{Sigla} - {Descricao}";
}

public class FormaPagamento : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(60)]
    public string Nome { get; set; } = string.Empty;

    [Range(1, 48, ErrorMessage = "O máximo de parcelas deve estar entre 1 e 48.")]
    public int MaximoParcelas { get; set; } = 1;

    /// <summary>Taxa cobrada pela operadora (cartão, boleto...), em %.</summary>
    [Range(0, 100, ErrorMessage = "A taxa deve estar entre 0% e 100%.")]
    public decimal TaxaPercentual { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}

/// <summary>Função exercida pela equipe (Garçom, Cozinheiro, Maître, Recepcionista...).</summary>
public class Cargo : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [Range(0, 100_000, ErrorMessage = "Valor da diária inválido.")]
    public decimal ValorDiariaPadrao { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}
