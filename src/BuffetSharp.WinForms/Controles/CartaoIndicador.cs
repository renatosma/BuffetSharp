using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>Cartão de indicador (KPI): título, valor em destaque, detalhe opcional e faixa colorida.</summary>
internal sealed class CartaoIndicador : Control
{
    private string _valor;
    private string? _detalhe;

    public CartaoIndicador(string titulo, string valor, Color cor, string? detalhe = null)
    {
        Titulo = titulo;
        _valor = valor;
        _detalhe = detalhe;
        Cor = cor;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        Size = new Size(LogicalToDeviceUnits(196), LogicalToDeviceUnits(92));
        Margin = new Padding(6);
        BackColor = Tema.Superficie;
    }

    public string Titulo { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Cor { get; set; }

    public void Atualizar(string valor, string? detalhe = null, Color? cor = null)
    {
        _valor = valor;
        _detalhe = detalhe;
        if (cor is { } c)
            Cor = c;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Tema.Superficie);
        using (var borda = new Pen(Tema.Borda))
            g.DrawRectangle(borda, 0, 0, Width - 1, Height - 1);
        using (var faixa = new SolidBrush(Cor))
            g.FillRectangle(faixa, 0, 0, LogicalToDeviceUnits(4), Height);

        var x = LogicalToDeviceUnits(16);
        var largura = Width - x - LogicalToDeviceUnits(8);
        TextRenderer.DrawText(g, Titulo, Tema.Fonte, new Rectangle(x, LogicalToDeviceUnits(10), largura, LogicalToDeviceUnits(20)),
            Tema.TextoSuave, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, _valor, Tema.FonteValor, new Rectangle(x, LogicalToDeviceUnits(30), largura, LogicalToDeviceUnits(34)),
            Tema.Texto, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (_detalhe is not null)
            TextRenderer.DrawText(g, _detalhe, Tema.FontePequena, new Rectangle(x, LogicalToDeviceUnits(66), largura, LogicalToDeviceUnits(18)),
                Cor, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}
