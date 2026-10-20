using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

/// <summary>
/// Modelo de checklist: as tarefas de preparação de um evento (montagem, decoração, som, confirmações com o cliente),
/// com o prazo contado em dias antes da data do evento. Um modelo por tipo de evento e um geral; o do tipo tem prioridade.
/// </summary>
public class ModeloChecklist : Entidade, IAtivavel, IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Tipo de evento a que o modelo se aplica (vazio = qualquer tipo).</summary>
    public int? TipoEventoId { get; set; }
    public TipoEvento? TipoEvento { get; set; }

    public bool Ativo { get; set; } = true;

    public List<ModeloChecklistItem> Itens { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Itens.Count == 0)
            yield return new ValidationResult("Inclua ao menos uma tarefa no modelo.", [nameof(Itens)]);
        if (Itens.Any(i => string.IsNullOrWhiteSpace(i.Descricao) || i.Descricao.Trim().Length > ModeloChecklistItem.TamanhoDescricao))
            yield return new ValidationResult($"Cada tarefa precisa de uma descrição (até {ModeloChecklistItem.TamanhoDescricao} caracteres).",
                [nameof(Itens)]);
        if (Itens.Any(i => i.DiasAntes is < ModeloChecklistItem.DiasMinimo or > ModeloChecklistItem.DiasMaximo))
            yield return new ValidationResult(
                $"O prazo das tarefas deve ficar entre {ModeloChecklistItem.DiasMaximo} dias antes e {-ModeloChecklistItem.DiasMinimo} dias depois do evento.",
                [nameof(Itens)]);
        if (Itens.Any(i => i.Responsavel?.Length > 60))
            yield return new ValidationResult("O responsável pode ter no máximo 60 caracteres.", [nameof(Itens)]);
    }

    public override string ToString() => Nome;
}

public class ModeloChecklistItem : Entidade
{
    public const int TamanhoDescricao = 200;
    public const int DiasMinimo = -30;
    public const int DiasMaximo = 365;

    public int ModeloChecklistId { get; set; }

    public string Descricao { get; set; } = string.Empty;

    /// <summary>Prazo em dias antes do evento (0 = no dia; negativo = depois, ex.: pesquisa de satisfação).</summary>
    public int DiasAntes { get; set; }

    /// <summary>Quem cuida (texto livre: "Comercial", "Cozinha", "Decoração"...).</summary>
    public string? Responsavel { get; set; }

    public int Ordem { get; set; }
}

/// <summary>Tarefa do checklist de um evento.</summary>
public class EventoTarefa
{
    public const int TamanhoDescricao = 200;

    private EventoTarefa()
    {
    }

    internal EventoTarefa(string descricao, DateOnly prazo, string? responsavel, int ordem)
    {
        Descricao = descricao;
        Prazo = prazo;
        Responsavel = responsavel;
        Ordem = ordem;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public DateOnly Prazo { get; private set; }
    public string? Responsavel { get; private set; }
    public int Ordem { get; private set; }
    public DateTime? ConcluidaEm { get; private set; }
    public string? ConcluidaPor { get; private set; }

    public bool Concluida => ConcluidaEm is not null;

    public bool Atrasada(DateOnly hoje) => !Concluida && Prazo < hoje;

    internal void Concluir(string? usuario, DateTime agora)
    {
        Validador.Garantir(!Concluida, $"A tarefa \"{Descricao}\" já foi concluída.");
        ConcluidaEm = agora;
        ConcluidaPor = usuario;
    }

    internal void Reabrir()
    {
        ConcluidaEm = null;
        ConcluidaPor = null;
    }
}
