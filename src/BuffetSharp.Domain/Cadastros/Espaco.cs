using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

/// <summary>Salão / ambiente onde os eventos acontecem.</summary>
public class Espaco : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(1, 10_000, ErrorMessage = "A capacidade deve estar entre 1 e 10.000 pessoas.")]
    public int CapacidadeMaxima { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Valor de locação inválido.")]
    public decimal ValorLocacao { get; set; }

    /// <summary>Onde fica o espaço (citado no contrato). Vazio: usa o endereço da empresa.</summary>
    public Endereco Endereco { get; set; } = new();

    public bool Ativo { get; set; } = true;

    public override string ToString() => $"{Nome} (até {CapacidadeMaxima} pessoas)";
}

/// <summary>DJ, decoração, fotografia, bolo, open bar...</summary>
public class ServicoAdicional : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Preço inválido.")]
    public decimal Preco { get; set; }

    /// <summary>Quando verdadeiro, o preço é multiplicado pela quantidade de convidados.</summary>
    public bool CobradoPorPessoa { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}
