using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>Linha da grade de tarefas do modelo de checklist.</summary>
internal sealed class LinhaChecklist
{
    private ModeloChecklistItem? _origem;

    public string? Descricao { get; set; }

    /// <summary>Dias antes do evento (0 = no dia; negativo = depois).</summary>
    public int DiasAntes { get; set; } = 7;

    public string? Responsavel { get; set; }

    public static LinhaChecklist De(ModeloChecklistItem item) => new()
    {
        _origem = item,
        Descricao = item.Descricao,
        DiasAntes = item.DiasAntes,
        Responsavel = item.Responsavel
    };

    /// <summary>Devolve o item do cadastro (o mesmo, quando já existia) com os valores da linha.</summary>
    public ModeloChecklistItem ParaItem(int ordem)
    {
        var item = _origem ?? new ModeloChecklistItem();
        item.Descricao = Descricao?.Trim() ?? string.Empty;
        item.DiasAntes = DiasAntes;
        item.Responsavel = string.IsNullOrWhiteSpace(Responsavel) ? null : Responsavel.Trim();
        item.Ordem = ordem;
        return item;
    }
}
