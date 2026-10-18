using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

public class Fornecedor : Entidade, IAtivavel, IValidatableObject, IPossuiContatos
{
    [Required(ErrorMessage = "Informe a razão social.")]
    [MaxLength(150)]
    public string RazaoSocial { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? NomeFantasia { get; set; }

    /// <summary>CNPJ ou CPF (somente dígitos).</summary>
    [MaxLength(14)]
    public string? Documento { get; set; }

    [MaxLength(100)]
    public string? Contato { get; set; }

    [MaxLength(150)]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Telefone { get; set; }

    public Endereco Endereco { get; set; } = new();

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Telefones e e-mails além dos principais (vendedor, financeiro, entregas...).</summary>
    public List<ContatoAdicional> Contatos { get; set; } = [];

    /// <summary>Endereços além do principal (depósito, retirada...).</summary>
    public List<EnderecoAdicional> Enderecos { get; set; } = [];

    public string NomeExibicao => string.IsNullOrWhiteSpace(NomeFantasia) ? RazaoSocial : NomeFantasia;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Documento) &&
            !Comum.Documento.CnpjValido(Documento) && !Comum.Documento.CpfValido(Documento))
            yield return new ValidationResult("CNPJ/CPF inválido.", [nameof(Documento)]);

        foreach (var erro in ValidacaoContatos.Validar(this))
            yield return erro;
    }

    public override string ToString() => NomeExibicao;
}
