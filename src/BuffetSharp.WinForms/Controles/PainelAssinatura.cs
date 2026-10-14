using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>
/// Área de assinatura: captura traços do mouse, caneta ou toque e exporta a assinatura em PNG
/// (fundo branco, recortada ao redor do traço).
/// </summary>
internal sealed class PainelAssinatura : Control
{
    private readonly List<List<Point>> _tracos = [];
    private List<Point>? _tracoAtual;

    public PainelAssinatura()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.White;
        Cursor = Cursors.Cross;
        MinimumSize = new Size(300, 120);
        AccessibleName = "Área de assinatura";
        AccessibleRole = AccessibleRole.Graphic;
    }

    /// <summary>Texto exibido enquanto a área está vazia.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Instrucao { get; set; } = "Assine aqui com o mouse, a caneta ou o dedo";

    public bool Vazio => _tracos.All(t => t.Count < 2);

    public event EventHandler? AssinaturaAlterada;

    public void Limpar()
    {
        _tracos.Clear();
        Invalidate();
        AssinaturaAlterada?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        Focus();
        _tracoAtual = [e.Location];
        _tracos.Add(_tracoAtual);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        // O traço vai do botão pressionado até ele ser solto (vale também para caneta e toque).
        if (_tracoAtual is null)
            return;
        _tracoAtual.Add(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_tracoAtual is null)
            return;
        _tracoAtual = null;
        AssinaturaAlterada?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        // Linha de base e instrução, como em um papel de assinatura.
        using (var linha = new Pen(Tema.Borda, 1) { DashStyle = DashStyle.Dash })
            g.DrawLine(linha, 16, Height - 28, Width - 16, Height - 28);
        if (Vazio)
            TextRenderer.DrawText(g, Instrucao, Tema.Fonte, ClientRectangle, Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        DesenharTracos(g, Point.Empty);

        using var borda = new Pen(Focused ? Tema.Primaria : Tema.Borda);
        g.DrawRectangle(borda, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>PNG da assinatura recortada (com margem), ou nulo se nada foi desenhado.</summary>
    public byte[]? ParaPng()
    {
        if (Vazio)
            return null;

        var pontos = _tracos.SelectMany(t => t).ToList();
        const int margem = 10;
        var area = Rectangle.FromLTRB(pontos.Min(p => p.X) - margem, pontos.Min(p => p.Y) - margem,
            pontos.Max(p => p.X) + margem, pontos.Max(p => p.Y) + margem);

        using var imagem = new Bitmap(Math.Max(area.Width, 40), Math.Max(area.Height, 20));
        using (var g = Graphics.FromImage(imagem))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            DesenharTracos(g, new Point(-area.Left, -area.Top));
        }
        using var saida = new MemoryStream();
        imagem.Save(saida, ImageFormat.Png);
        return saida.ToArray();
    }

    private void DesenharTracos(Graphics g, Point deslocamento)
    {
        using var caneta = new Pen(Color.FromArgb(20, 30, 70), LogicalToDeviceUnits(2) + 0.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        foreach (var traco in _tracos.Where(t => t.Count > 1))
            g.DrawLines(caneta, traco.Select(p => new Point(p.X + deslocamento.X, p.Y + deslocamento.Y)).ToArray());
    }
}
