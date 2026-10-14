using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

public class Funcionario : Entidade, IAtivavel, IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(11)]
    public string? Cpf { get; set; }

    [MaxLength(20)]
    public string? Telefone { get; set; }

    [MaxLength(150)]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string? Email { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o cargo.")]
    public int CargoId { get; set; }

    public Cargo? Cargo { get; set; }

    /// <summary>Valor pago por evento trabalhado (freelancer) ou referência para escala.</summary>
    [Range(0, 100_000, ErrorMessage = "Valor da diária inválido.")]
    public decimal ValorDiaria { get; set; }

    public DateOnly? DataAdmissao { get; set; }

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Cpf) && !Documento.CpfValido(Cpf))
            yield return new ValidationResult("CPF inválido.", [nameof(Cpf)]);
    }

    public override string ToString() => Nome;
}
