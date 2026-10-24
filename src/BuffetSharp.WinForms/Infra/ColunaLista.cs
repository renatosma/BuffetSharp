namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Coluna de lista (combo) para as grades editáveis. Use no lugar de <see cref="DataGridViewComboBoxColumn"/>: com
/// monitores de escalas diferentes (ex.: notebook a 125% e monitor a 100%), a lista de edição é criada numa escala e
/// encaixada na grade em outra; o Windows avisa a mudança (WM_DPICHANGED_BEFOREPARENT) e o ComboBox tenta se recriar no
/// meio do encaixe, falhando com "Erro ao criar identificador de janela".
/// </summary>
internal sealed class ColunaLista : DataGridViewComboBoxColumn
{
    public ColunaLista()
    {
        CellTemplate = new CelulaLista();
    }
}

internal sealed class CelulaLista : DataGridViewComboBoxCell
{
    public override Type EditType => typeof(EdicaoLista);
}

/// <summary>Lista de edição que ignora o aviso de mudança de escala: a fonte vem do estilo da célula, aplicado pela grade.</summary>
internal sealed class EdicaoLista : DataGridViewComboBoxEditingControl
{
    private const int WmDpiChangedBeforeParent = 0x02E2;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDpiChangedBeforeParent)
            return;
        base.WndProc(ref m);
    }
}
