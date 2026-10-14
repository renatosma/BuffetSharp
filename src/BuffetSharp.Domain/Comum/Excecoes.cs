using System.ComponentModel.DataAnnotations;

namespace BuffetSharp.Domain.Comum;

/// <summary>
/// Violação de uma regra de negócio. A mensagem é exibida diretamente ao usuário.
/// </summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);

/// <summary>
/// Um ou mais campos inválidos. Cada erro informa a propriedade de origem, permitindo
/// que a tela destaque o campo correspondente.
/// </summary>
public class ValidacaoException : RegraNegocioException
{
    public ValidacaoException(IEnumerable<ValidationResult> erros)
        : this(erros.ToList())
    {
    }

    private ValidacaoException(List<ValidationResult> erros)
        : base(string.Join(Environment.NewLine, erros.Select(e => e.ErrorMessage)))
    {
        Erros = erros;
    }

    public IReadOnlyList<ValidationResult> Erros { get; }
}

public static class Validador
{
    /// <summary>
    /// Executa as validações por DataAnnotations e <see cref="IValidatableObject"/>.
    /// </summary>
    public static void Validar(object objeto)
    {
        var erros = new List<ValidationResult>();
        var contexto = new ValidationContext(objeto);
        if (!Validator.TryValidateObject(objeto, contexto, erros, validateAllProperties: true))
            throw new ValidacaoException(erros);
    }

    public static void Garantir(bool condicao, string mensagem)
    {
        if (!condicao)
            throw new RegraNegocioException(mensagem);
    }
}
