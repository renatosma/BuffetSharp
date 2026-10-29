using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

/// <summary>
/// Modelo de cronograma (o minuto a minuto da festa) de um tipo de evento: as etapas com o horário em minutos a partir do
/// início do evento (negativo = antes, ex.: montagem). O evento novo já nasce com o cronograma do modelo do seu tipo.
/// </summary>
public class ModeloCronograma : Entidade, IAtivavel, IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Tipo de evento a que o modelo se aplica (vazio = qualquer tipo).</summary>
    public int? TipoEventoId { get; set; }
    public TipoEvento? TipoEvento { get; set; }

    public bool Ativo { get; set; } = true;

    public List<ModeloCronogramaItem> Itens { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Itens.Count == 0)
            yield return new ValidationResult("Inclua ao menos uma etapa no modelo.", [nameof(Itens)]);
        if (Itens.Any(i => string.IsNullOrWhiteSpace(i.Atividade) || i.Atividade.Trim().Length > EventoEtapa.TamanhoAtividade))
            yield return new ValidationResult($"Cada etapa precisa da atividade (até {EventoEtapa.TamanhoAtividade} caracteres).", [nameof(Itens)]);
        if (Itens.Any(i => i.MinutosDoInicio is < EventoEtapa.MinutosAntesMaximo or > EventoEtapa.MinutosDepoisMaximo))
            yield return new ValidationResult("O horário das etapas vai de 12 horas antes a 18 horas depois do início do evento.", [nameof(Itens)]);
        if (Itens.Any(i => i.DuracaoMinutos is < 0 or > EventoEtapa.DuracaoMaxima))
            yield return new ValidationResult("A duração de cada etapa vai de 0 a 12 horas.", [nameof(Itens)]);
        if (Itens.Any(i => i.Responsavel?.Length > EventoEtapa.TamanhoResponsavel || i.Local?.Length > EventoEtapa.TamanhoLocal))
            yield return new ValidationResult($"O responsável pode ter até {EventoEtapa.TamanhoResponsavel} caracteres e o local até {EventoEtapa.TamanhoLocal}.",
                [nameof(Itens)]);
    }

    public override string ToString() => Nome;
}

public class ModeloCronogramaItem : Entidade
{
    public int ModeloCronogramaId { get; set; }

    public string Atividade { get; set; } = string.Empty;

    /// <summary>Minutos a partir do início do evento (0 = no início; negativo = antes, como a montagem).</summary>
    public int MinutosDoInicio { get; set; }

    public int DuracaoMinutos { get; set; } = 30;

    /// <summary>Quem conduz (texto livre: "Cerimonial", "DJ", "Cozinha"...).</summary>
    public string? Responsavel { get; set; }

    public string? Local { get; set; }

    public int Ordem { get; set; }
}

/// <summary>Etapa do cronograma de um evento: horário, duração, atividade, local, responsável e observação.</summary>
public class EventoEtapa
{
    public const int TamanhoAtividade = 150;
    public const int TamanhoResponsavel = 60;
    public const int TamanhoLocal = 80;
    public const int TamanhoObservacao = 300;
    public const int MinutosAntesMaximo = -12 * 60;
    public const int MinutosDepoisMaximo = 18 * 60;
    public const int DuracaoMaxima = 12 * 60;

    private EventoEtapa()
    {
    }

    internal EventoEtapa(DateTime horario, int duracaoMinutos, string atividade, string? local, string? responsavel, int? funcionarioId,
        string? observacao, int ordem)
    {
        Horario = horario;
        DuracaoMinutos = duracaoMinutos;
        Atividade = atividade;
        Local = local;
        Responsavel = responsavel;
        FuncionarioId = funcionarioId;
        Observacao = observacao;
        Ordem = ordem;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public DateTime Horario { get; private set; }
    public int DuracaoMinutos { get; private set; }
    public string Atividade { get; private set; } = string.Empty;
    public string? Local { get; private set; }

    /// <summary>Quem conduz: o nome do funcionário escalado ou um texto livre ("DJ", "Cerimonial"...).</summary>
    public string? Responsavel { get; private set; }

    /// <summary>Funcionário da escala do evento que conduz a etapa (quando for alguém da equipe).</summary>
    public int? FuncionarioId { get; private set; }

    public string? Observacao { get; private set; }
    public int Ordem { get; private set; }

    public DateTime Fim => Horario.AddMinutes(DuracaoMinutos);

    /// <summary>A etapa está acontecendo agora (as etapas sem duração valem até a próxima começar).</summary>
    public bool EmAndamento(DateTime agora) => Horario <= agora && agora < Fim;

    internal void Alterar(DateTime horario, int duracaoMinutos, string atividade, string? local, string? responsavel, int? funcionarioId,
        string? observacao)
    {
        Horario = horario;
        DuracaoMinutos = duracaoMinutos;
        Atividade = atividade;
        Local = local;
        Responsavel = responsavel;
        FuncionarioId = funcionarioId;
        Observacao = observacao;
    }

    internal void Deslocar(TimeSpan diferenca) => Horario += diferenca;

    internal void DesvincularFuncionario() => FuncionarioId = null;
}

/// <summary>Onde a festa está: a etapa em andamento (ou a última que começou) e a próxima.</summary>
public sealed record SituacaoCronograma(EventoEtapa? Atual, EventoEtapa? Proxima)
{
    public static SituacaoCronograma Calcular(IEnumerable<EventoEtapa> etapas, DateTime agora)
    {
        var ordenadas = etapas.OrderBy(e => e.Horario).ThenBy(e => e.Ordem).ToList();
        var ultimaIniciada = ordenadas.LastOrDefault(e => e.Horario <= agora);
        var atual = ordenadas.LastOrDefault(e => e.EmAndamento(agora)) ?? (ultimaIniciada is { DuracaoMinutos: 0 } ? ultimaIniciada : null);
        var proxima = ordenadas.FirstOrDefault(e => e.Horario > agora);
        return new SituacaoCronograma(atual, proxima);
    }
}
