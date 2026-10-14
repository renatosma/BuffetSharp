using System.Drawing.Drawing2D;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>
/// Gráfico de barras agrupadas desenhado com GDI+ (sem dependências externas): grade, eixos,
/// legenda e dica com o valor ao passar o mouse.
/// </summary>
internal sealed class GraficoBarras : Control
{
    public sealed record Serie(string Nome, Color Cor, IReadOnlyList<decimal> Valores);

    private IReadOnlyList<string> _rotulos = [];
    private IReadOnlyList<Serie> _series = [];
    private readonly List<(RectangleF Area, string Texto)> _barras = [];
    private readonly ToolTip _dica = new() { InitialDelay = 100, ReshowDelay = 50 };
    private string? _dicaAtual;

    public GraficoBarras()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Superficie;
        Font = Tema.FontePequena;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Titulo { get; set; } = string.Empty;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<decimal, string> FormatoValor { get; set; } = FormatarCompacto;

    public void Definir(IReadOnlyList<string> rotulos, params Serie[] series)
    {
        _rotulos = rotulos;
        _series = series;
        Invalidate();
    }

    public static string FormatarCompacto(decimal valor) => Math.Abs(valor) switch
    {
        >= 1_000_000 => $"R$ {valor / 1_000_000:0.#} mi",
        >= 1_000 => $"R$ {valor / 1_000:0.#} mil",
        _ => $"R$ {valor:0}"
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        _barras.Clear();

        var margem = LogicalToDeviceUnits(12);
        TextRenderer.DrawText(g, Titulo, Tema.FonteNegrito, new Point(margem, margem), Tema.Texto);

        // Legenda
        var xLegenda = Width - margem;
        foreach (var serie in _series.Reverse())
        {
            var tamanho = TextRenderer.MeasureText(serie.Nome, Font);
            xLegenda -= tamanho.Width;
            TextRenderer.DrawText(g, serie.Nome, Font, new Point(xLegenda, margem + 2), Tema.TextoSuave);
            xLegenda -= LogicalToDeviceUnits(14);
            using var pincel = new SolidBrush(serie.Cor);
            g.FillRectangle(pincel, xLegenda, margem + 5, LogicalToDeviceUnits(10), LogicalToDeviceUnits(10));
            xLegenda -= LogicalToDeviceUnits(14);
        }

        if (_rotulos.Count == 0 || _series.Count == 0)
            return;

        var maximo = _series.SelectMany(s => s.Valores).DefaultIfEmpty(0).Max();
        maximo = maximo <= 0 ? 1 : ArredondarEscala(maximo);

        var larguraEixo = LogicalToDeviceUnits(70);
        var area = new RectangleF(margem + larguraEixo, margem + LogicalToDeviceUnits(34),
            Width - margem * 2 - larguraEixo, Height - margem * 2 - LogicalToDeviceUnits(58));
        if (area.Width <= 10 || area.Height <= 10)
            return;

        // Linhas de grade e valores do eixo Y
        using var grade = new Pen(Color.FromArgb(236, 232, 228)) { DashStyle = DashStyle.Dot };
        const int divisoes = 4;
        for (var i = 0; i <= divisoes; i++)
        {
            var y = area.Bottom - area.Height * i / divisoes;
            g.DrawLine(grade, area.Left, y, area.Right, y);
            var texto = FormatoValor(maximo * i / divisoes);
            var tamanho = TextRenderer.MeasureText(texto, Font);
            TextRenderer.DrawText(g, texto, Font, new Point((int)area.Left - tamanho.Width - 6, (int)(y - tamanho.Height / 2f)), Tema.TextoSuave);
        }

        // Barras
        var larguraGrupo = area.Width / _rotulos.Count;
        var larguraBarra = Math.Max(2, larguraGrupo * 0.7f / _series.Count);
        for (var i = 0; i < _rotulos.Count; i++)
        {
            var xGrupo = area.Left + larguraGrupo * i + (larguraGrupo - larguraBarra * _series.Count) / 2;
            for (var s = 0; s < _series.Count; s++)
            {
                var valor = i < _series[s].Valores.Count ? _series[s].Valores[i] : 0;
                var altura = (float)(valor / maximo) * area.Height;
                var barra = new RectangleF(xGrupo + larguraBarra * s, area.Bottom - altura, larguraBarra - 2, Math.Max(altura, 0));
                using var pincel = new SolidBrush(_series[s].Cor);
                g.FillRectangle(pincel, barra);
                _barras.Add((new RectangleF(barra.X, area.Top, barra.Width, area.Height),
                    $"{_rotulos[i]} - {_series[s].Nome}: {Textos.Moeda(valor)}"));
            }

            var rotulo = _rotulos[i];
            var tamanhoRotulo = TextRenderer.MeasureText(rotulo, Font);
            TextRenderer.DrawText(g, rotulo, Font,
                new Point((int)(area.Left + larguraGrupo * i + (larguraGrupo - tamanhoRotulo.Width) / 2), (int)area.Bottom + 6), Tema.TextoSuave);
        }

        using var eixo = new Pen(Tema.Borda);
        g.DrawLine(eixo, area.Left, area.Bottom, area.Right, area.Bottom);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var texto = _barras.FirstOrDefault(b => b.Area.Contains(e.Location)).Texto;
        if (texto == _dicaAtual)
            return;
        _dicaAtual = texto;
        if (texto is null)
            _dica.Hide(this);
        else
            _dica.Show(texto, this, e.X + 12, e.Y + 12);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _dicaAtual = null;
        _dica.Hide(this);
    }

    private static decimal ArredondarEscala(decimal valor)
    {
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)valor)));
        foreach (var passo in new[] { 1m, 1.5m, 2m, 2.5m, 3m, 4m, 5m, 6m, 8m, 10m })
            if (passo * magnitude >= valor)
                return passo * magnitude;
        return valor;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dica.Dispose();
        base.Dispose(disposing);
    }
}
