using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

public enum MotivoIndisponibilidade
{
    Folga,
    Ferias,
    Atestado,
    Outro
}

/// <summary>Período em que o funcionário não pode ser escalado (folga, férias, atestado...).</summary>
public class IndisponibilidadeFuncionario : Entidade, IValidatableObject
{
    public const int DiasMaximo = 366;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o funcionário.")]
    public int FuncionarioId { get; set; }

    public Funcionario? Funcionario { get; set; }

    public DateOnly Inicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly Fim { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public MotivoIndisponibilidade Motivo { get; set; }

    [MaxLength(300)]
    public string? Observacao { get; set; }

    /// <summary>O período cobre algum dia entre <paramref name="de"/> e <paramref name="ate"/>.</summary>
    public bool Cobre(DateTime de, DateTime ate) => Inicio <= DateOnly.FromDateTime(ate) && Fim >= DateOnly.FromDateTime(de);

    public string Descricao => Inicio == Fim ? $"{Nome(Motivo)} em {Inicio:dd/MM/yyyy}" : $"{Nome(Motivo)} de {Inicio:dd/MM} a {Fim:dd/MM/yyyy}";

    public static string Nome(MotivoIndisponibilidade motivo) => motivo switch
    {
        MotivoIndisponibilidade.Ferias => "Férias",
        MotivoIndisponibilidade.Atestado => "Atestado",
        MotivoIndisponibilidade.Folga => "Folga",
        _ => "Indisponível"
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Fim < Inicio)
            yield return new ValidationResult("O fim deve ser igual ou posterior ao início.", [nameof(Fim)]);
        else if (Fim.DayNumber - Inicio.DayNumber >= DiasMaximo)
            yield return new ValidationResult($"O período pode ter no máximo {DiasMaximo} dias.", [nameof(Fim)]);
    }
}
