using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>Linha da grade de etapas do modelo de cronograma.</summary>
internal sealed class LinhaCronograma
{
    private ModeloCronogramaItem? _origem;

    /// <summary>Minutos a partir do início do evento (negativo = antes).</summary>
    public int MinutosDoInicio { get; set; }

    public int DuracaoMinutos { get; set; } = 30;
    public string? Atividade { get; set; }
    public string? Local { get; set; }
    public string? Responsavel { get; set; }

    public static LinhaCronograma De(ModeloCronogramaItem item) => new()
    {
        _origem = item,
        MinutosDoInicio = item.MinutosDoInicio,
        DuracaoMinutos = item.DuracaoMinutos,
        Atividade = item.Atividade,
        Local = item.Local,
        Responsavel = item.Responsavel
    };

    /// <summary>Devolve o item do cadastro (o mesmo, quando já existia) com os valores da linha.</summary>
    public ModeloCronogramaItem ParaItem(int ordem)
    {
        var item = _origem ?? new ModeloCronogramaItem();
        item.MinutosDoInicio = MinutosDoInicio;
        item.DuracaoMinutos = DuracaoMinutos;
        item.Atividade = Atividade?.Trim() ?? string.Empty;
        item.Local = string.IsNullOrWhiteSpace(Local) ? null : Local.Trim();
        item.Responsavel = string.IsNullOrWhiteSpace(Responsavel) ? null : Responsavel.Trim();
        item.Ordem = ordem;
        return item;
    }
}
