using System.Drawing.Drawing2D;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>
/// Gráfico de linhas no tempo desenhado com GDI+ (sem dependências externas): uma linha por série (ex.: fornecedor), com os
/// pontos nas datas, eixo de valores ajustado à faixa dos dados, legenda e dica com o valor ao passar o mouse.
/// </summary>
internal sealed class GraficoLinhas : Control
{
    public sealed record Ponto(DateTime Data, decimal Valor, string? Detalhe = null);

    public sealed record Serie(string Nome, Color Cor, IReadOnlyList<Ponto> Pontos);

    private IReadOnlyList<Serie> _series = [];
    private readonly List<(RectangleF Area, string Texto)> _pontos = [];
    private readonly ToolTip _dica = new() { InitialDelay = 100, ReshowDelay = 50 };
    private string? _dicaAtual;

    /// <summary>Cores das séries, na ordem.</summary>
    public static readonly Color[] Paleta =
    [
        Color.FromArgb(37, 99, 235), Color.FromArgb(234, 88, 12), Color.FromArgb(22, 163, 74), Color.FromArgb(147, 51, 234),
        Color.FromArgb(219, 39, 119), Color.FromArgb(13, 148, 136), Color.FromArgb(202, 138, 4), Color.FromArgb(100, 116, 139)
    ];

    public GraficoLinhas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Superficie;
        Font = Tema.FontePequena;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Texto exibido quando não há pontos.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TextoVazio { get; set; } = "Sem dados no período.";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<decimal, string> FormatoValor { get; set; } = v => v.ToString("C2", Textos.PtBr);

    public IReadOnlyList<Serie> Series => _series;

    public void Definir(IReadOnlyList<Serie> series)
    {
        _series = series.Where(s => s.Pontos.Count > 0).ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        _pontos.Clear();

        var margem = LogicalToDeviceUnits(12);
        TextRenderer.DrawText(g, Titulo, Tema.FonteNegrito, new Point(margem, margem), Tema.Texto);

        // Legenda (à direita do título)
        var xLegenda = Width - margem;
        foreach (var serie in _series.Reverse())
        {
            var tamanho = TextRenderer.MeasureText(serie.Nome, Font);
            xLegenda -= tamanho.Width;
            TextRenderer.DrawText(g, serie.Nome, Font, new Point(xLegenda, margem + 2), Tema.TextoSuave);
            xLegenda -= LogicalToDeviceUnits(14);
            using var pincel = new SolidBrush(serie.Cor);
            g.FillEllipse(pincel, xLegenda, margem + 5, LogicalToDeviceUnits(10), LogicalToDeviceUnits(10));
            xLegenda -= LogicalToDeviceUnits(14);
        }

        var todos = _series.SelectMany(s => s.Pontos).ToList();
        if (todos.Count == 0)
        {
            TextRenderer.DrawText(g, TextoVazio, Font, ClientRectangle, Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        // Faixa de valores com folga, para a variação aparecer (o eixo não começa no zero).
        var minimo = todos.Min(p => p.Valor);
        var maximo = todos.Max(p => p.Valor);
        var folga = Math.Max((maximo - minimo) * 0.15m, Math.Max(maximo * 0.05m, 0.01m));
        minimo = Math.Max(0, minimo - folga);
        maximo += folga;
        var inicio = todos.Min(p => p.Data);
        var fim = todos.Max(p => p.Data);
        if (fim <= inicio)
        {
            inicio = inicio.AddDays(-15);
            fim = fim.AddDays(15);
        }

        var larguraEixo = LogicalToDeviceUnits(80);
        var area = new RectangleF(margem + larguraEixo, margem + LogicalToDeviceUnits(34),
            Width - margem * 2 - larguraEixo - LogicalToDeviceUnits(10), Height - margem * 2 - LogicalToDeviceUnits(58));
        if (area.Width <= 10 || area.Height <= 10)
            return;

        float X(DateTime data) => area.Left + (float)((data - inicio).TotalSeconds / (fim - inicio).TotalSeconds) * area.Width;
        float Y(decimal valor) => area.Bottom - (float)((valor - minimo) / (maximo - minimo)) * area.Height;

        // Grade e eixo de valores
        using var grade = new Pen(Color.FromArgb(236, 232, 228)) { DashStyle = DashStyle.Dot };
        const int divisoes = 4;
        for (var i = 0; i <= divisoes; i++)
        {
            var valor = minimo + (maximo - minimo) * i / divisoes;
            var y = Y(valor);
            g.DrawLine(grade, area.Left, y, area.Right, y);
            var texto = FormatoValor(valor);
            var tamanho = TextRenderer.MeasureText(texto, Font);
            TextRenderer.DrawText(g, texto, Font, new Point((int)area.Left - tamanho.Width - 6, (int)(y - tamanho.Height / 2f)), Tema.TextoSuave);
        }

        // Eixo das datas: o início, o fim e marcas intermediárias
        const int marcas = 4;
        for (var i = 0; i <= marcas; i++)
        {
            var data = inicio.AddSeconds((fim - inicio).TotalSeconds * i / marcas);
            var texto = data.ToString("dd/MM/yy", Textos.PtBr);
            var tamanho = TextRenderer.MeasureText(texto, Font);
            var x = X(data) - tamanho.Width / 2f;
            TextRenderer.DrawText(g, texto, Font, new Point((int)Math.Clamp(x, area.Left - 20, area.Right - tamanho.Width + 20), (int)area.Bottom + 6),
                Tema.TextoSuave);
        }

        // Linhas e pontos
        var raio = LogicalToDeviceUnits(4);
        foreach (var serie in _series)
        {
            var pontos = serie.Pontos.OrderBy(p => p.Data).Select(p => (Ponto: p, Local: new PointF(X(p.Data), Y(p.Valor)))).ToList();
            using var caneta = new Pen(serie.Cor, LogicalToDeviceUnits(2));
            if (pontos.Count > 1)
                g.DrawLines(caneta, pontos.Select(p => p.Local).ToArray());
            using var pincel = new SolidBrush(serie.Cor);
            foreach (var (ponto, local) in pontos)
            {
                g.FillEllipse(pincel, local.X - raio, local.Y - raio, raio * 2, raio * 2);
                _pontos.Add((new RectangleF(local.X - raio * 2, local.Y - raio * 2, raio * 4, raio * 4),
                    $"{ponto.Data:dd/MM/yyyy} - {serie.Nome}: {FormatoValor(ponto.Valor)}" + (ponto.Detalhe is null ? "" : $"\n{ponto.Detalhe}")));
            }
        }

        using var eixo = new Pen(Tema.Borda);
        g.DrawLine(eixo, area.Left, area.Bottom, area.Right, area.Bottom);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var texto = _pontos.FirstOrDefault(p => p.Area.Contains(e.Location)).Texto;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dica.Dispose();
        base.Dispose(disposing);
    }
}
