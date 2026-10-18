using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

public enum TipoPessoa
{
    Fisica,
    Juridica
}

/// <summary>
/// Nível 2 - Cadastro intermediário: dados pessoais, endereço e validação de documento.
/// </summary>
public class Cliente : Entidade, IAtivavel, IValidatableObject, IPossuiContatos
{
    public TipoPessoa TipoPessoa { get; set; } = TipoPessoa.Fisica;

    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? NomeFantasia { get; set; }

    /// <summary>CPF ou CNPJ (somente dígitos).</summary>
    [MaxLength(14)]
    public string? Documento { get; set; }

    [MaxLength(150)]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Telefone { get; set; }

    [MaxLength(20)]
    public string? Celular { get; set; }

    public DateOnly? DataNascimento { get; set; }

    public Endereco Endereco { get; set; } = new();

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Telefones e e-mails além dos principais.</summary>
    public List<ContatoAdicional> Contatos { get; set; } = [];

    /// <summary>Endereços além do principal (cobrança, entrega...).</summary>
    public List<EnderecoAdicional> Enderecos { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Telefone) && string.IsNullOrWhiteSpace(Celular) && string.IsNullOrWhiteSpace(Email) &&
            Contatos.All(c => string.IsNullOrWhiteSpace(c.Valor)))
            yield return new ValidationResult("Informe ao menos um contato (telefone, celular ou e-mail).", [nameof(Celular)]);

        foreach (var erro in ValidacaoContatos.Validar(this))
            yield return erro;

        if (!string.IsNullOrWhiteSpace(Documento))
        {
            var valido = TipoPessoa == TipoPessoa.Fisica
                ? Comum.Documento.CpfValido(Documento)
                : Comum.Documento.CnpjValido(Documento);
            if (!valido)
                yield return new ValidationResult(
                    TipoPessoa == TipoPessoa.Fisica ? "CPF inválido." : "CNPJ inválido.", [nameof(Documento)]);
        }

        if (DataNascimento is { } nascimento && nascimento > DateOnly.FromDateTime(DateTime.Today))
            yield return new ValidationResult("A data de nascimento não pode estar no futuro.", [nameof(DataNascimento)]);
    }

    public override string ToString() => Nome;
}
