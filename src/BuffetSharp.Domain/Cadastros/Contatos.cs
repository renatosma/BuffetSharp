using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

public enum TipoContato
{
    Celular,
    Telefone,
    WhatsApp,
    Email
}

/// <summary>
/// Cadastros de pessoas (clientes, fornecedores e funcionários) que aceitam vários telefones, e-mails e endereços
/// além dos principais (que continuam nos campos do próprio cadastro e são os usados por padrão).
/// </summary>
public interface IPossuiContatos
{
    List<ContatoAdicional> Contatos { get; }
    List<EnderecoAdicional> Enderecos { get; }
}

/// <summary>Telefone, celular, WhatsApp ou e-mail adicional de uma pessoa.</summary>
public class ContatoAdicional : Entidade, IValidatableObject
{
    public TipoContato Tipo { get; set; } = TipoContato.Celular;

    /// <summary>Número (com DDD) ou e-mail.</summary>
    [Required(ErrorMessage = "Informe o telefone ou e-mail do contato adicional.")]
    [MaxLength(150)]
    public string Valor { get; set; } = string.Empty;

    /// <summary>A quem pertence ou para que serve (ex.: "Mãe da noiva", "Financeiro").</summary>
    [MaxLength(100)]
    public string? Observacao { get; set; }

    public bool EhEmail => Tipo == TipoContato.Email;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Valor))
            yield break;

        if (EhEmail)
        {
            if (!new EmailAddressAttribute().IsValid(Valor.Trim()))
                yield return new ValidationResult($"E-mail adicional inválido: {Valor}.", [nameof(Valor)]);
        }
        else if (Documento.SomenteDigitos(Valor).Length is < 8 or > 13)
        {
            yield return new ValidationResult($"Telefone adicional inválido: {Valor}. Informe o número com DDD.", [nameof(Valor)]);
        }
    }

    public override string ToString() => string.IsNullOrWhiteSpace(Observacao) ? $"{Tipo}: {Valor}" : $"{Tipo}: {Valor} ({Observacao})";
}

/// <summary>Endereço adicional de uma pessoa (cobrança, entrega, comercial...).</summary>
public class EnderecoAdicional : Entidade
{
    /// <summary>Identificação do endereço (ex.: "Cobrança", "Entrega", "Casa de praia").</summary>
    [MaxLength(60)]
    public string? Identificacao { get; set; }

    public Endereco Endereco { get; set; } = new();

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Identificacao) ? Endereco.ToString() : $"{Identificacao}: {Endereco}";
}

/// <summary>Regras comuns dos contatos e endereços adicionais.</summary>
public static class ValidacaoContatos
{
    /// <summary>
    /// Valida os itens das listas (o validador de DataAnnotations não percorre coleções) e normaliza os textos:
    /// telefones ficam formatados e e-mails sem espaços.
    /// </summary>
    public static IEnumerable<ValidationResult> Validar(IPossuiContatos dono)
    {
        foreach (var contato in dono.Contatos)
        {
            contato.Valor = contato.EhEmail ? (contato.Valor ?? string.Empty).Trim() : Documento.FormatarTelefone(contato.Valor);
            var erros = new List<ValidationResult>();
            Validator.TryValidateObject(contato, new ValidationContext(contato), erros, validateAllProperties: true);
            foreach (var erro in erros)
                yield return new ValidationResult(erro.ErrorMessage, [nameof(IPossuiContatos.Contatos)]);
        }

        var repetidos = dono.Contatos.GroupBy(c => (c.Tipo, Chave: c.EhEmail ? c.Valor.ToUpperInvariant() : Documento.SomenteDigitos(c.Valor)))
            .Where(g => g.Count() > 1).Select(g => g.First().Valor).ToList();
        if (repetidos.Count > 0)
            yield return new ValidationResult($"Contato adicional repetido: {string.Join(", ", repetidos)}.", [nameof(IPossuiContatos.Contatos)]);

        foreach (var endereco in dono.Enderecos)
        {
            if (string.IsNullOrWhiteSpace(endereco.Endereco.Logradouro) || string.IsNullOrWhiteSpace(endereco.Endereco.Cidade))
                yield return new ValidationResult("Informe ao menos o logradouro e a cidade de cada endereço adicional.",
                    [nameof(IPossuiContatos.Enderecos)]);
        }
    }

    /// <summary>Primeiro contato adicional de um dos tipos informados (ex.: um WhatsApp quando não há celular principal).</summary>
    public static string? Primeiro(IPossuiContatos dono, params TipoContato[] tipos) =>
        tipos.Select(t => dono.Contatos.FirstOrDefault(c => c.Tipo == t)?.Valor).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
