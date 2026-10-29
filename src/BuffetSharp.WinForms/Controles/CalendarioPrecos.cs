using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>
/// Mês com o ajuste de preço de cada dia: acréscimos em tons de laranja, descontos em verde e o mínimo de convidados quando
/// houver. A dica mostra as regras que valem no dia.
/// </summary>
internal sealed class CalendarioPrecos : Control
{
    private static readonly string[] DiasSemana = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    private DateOnly _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private IReadOnlyList<DiaPreco> _dias = [];
    private readonly List<(RectangleF Area, DiaPreco Dia)> _areas = [];
    private readonly ToolTip _dica = new() { InitialDelay = 200 };
    private DateOnly? _diaDica;

    public CalendarioPrecos()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Superficie;
    }

    public IReadOnlyList<DiaPreco> Dias => _dias;

    public void Definir(DateOnly mes, IReadOnlyList<DiaPreco> dias)
    {
        _mes = new DateOnly(mes.Year, mes.Month, 1);
        _dias = dias;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        _areas.Clear();

        var margem = LogicalToDeviceUnits(8);
        var cabecalho = LogicalToDeviceUnits(26);
        var largura = (Width - margem * 2) / 7f;
        var semanas = (int)Math.Ceiling(((int)_mes.DayOfWeek + DateTime.DaysInMonth(_mes.Year, _mes.Month)) / 7.0);
        var altura = (Height - margem * 2 - cabecalho) / (float)Math.Max(semanas, 1);

        for (var i = 0; i < 7; i++)
            TextRenderer.DrawText(g, DiasSemana[i], Tema.FonteNegrito,
                Rectangle.Round(new RectangleF(margem + i * largura, margem, largura, cabecalho)), Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        using var fonteAjuste = new Font(Tema.Fonte.FontFamily, 13f, FontStyle.Bold);
        using var borda = new Pen(Tema.Borda);
        using var hoje = new Pen(Tema.Primaria, 2);
        foreach (var dia in _dias)
        {
            var posicao = (int)_mes.DayOfWeek + dia.Data.Day - 1;
            var area = new RectangleF(margem + posicao % 7 * largura, margem + cabecalho + posicao / 7 * altura, largura, altura);
            _areas.Add((area, dia));

            var pessoa = dia.Ajuste.PercentualPessoa;
            var locacao = dia.Ajuste.PercentualLocacao;
            var referencia = pessoa != 0 ? pessoa : locacao;
            using (var fundo = new SolidBrush(Cor(referencia)))
                g.FillRectangle(fundo, area);
            g.DrawRectangle(dia.Data == DateOnly.FromDateTime(DateTime.Today) ? hoje : borda,
                area.X, area.Y, area.Width, area.Height);

            var interno = Rectangle.Round(RectangleF.Inflate(area, -6, -4));
            TextRenderer.DrawText(g, dia.Data.Day.ToString(), Tema.FonteNegrito, interno, Tema.Texto, TextFormatFlags.Left | TextFormatFlags.Top);
            if (dia.Ajuste.Regras.Count > 0)
            {
                var corTexto = referencia > 0 ? Color.FromArgb(150, 70, 0) : referencia < 0 ? Tema.Sucesso : Tema.Texto;
                TextRenderer.DrawText(g, Percentual(pessoa != 0 ? pessoa : locacao), fonteAjuste, interno, corTexto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                var rodape = new List<string>();
                if (pessoa != locacao && pessoa != 0 && locacao != 0)
                    rodape.Add($"locação {Percentual(locacao)}");
                else if (pessoa == 0 && locacao != 0)
                    rodape.Add("só locação");
                if (dia.Ajuste.MinimoConvidados is { } minimo)
                    rodape.Add($"mín. {minimo}");
                if (rodape.Count > 0)
                    TextRenderer.DrawText(g, string.Join("  •  ", rodape), Tema.FontePequena, interno, Tema.TextoSuave,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
            }
        }
    }

    private static string Percentual(decimal valor) => $"{(valor >= 0 ? "+" : "")}{valor:0.##}%";

    /// <summary>Quanto maior o acréscimo, mais forte o laranja; descontos em verde.</summary>
    private static Color Cor(decimal percentual)
    {
        if (percentual == 0)
            return Color.White;
        var intensidade = (int)(Math.Min(1m, Math.Abs(percentual) / 40m) * 120);
        return percentual > 0
            ? Color.FromArgb(255, 250 - intensidade / 2, 235 - intensidade)
            : Color.FromArgb(235 - intensidade, 250 - intensidade / 3, 235 - intensidade);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var sobre = _areas.FirstOrDefault(a => a.Area.Contains(e.Location));
        var dia = sobre.Area.IsEmpty ? (DateOnly?)null : sobre.Dia.Data;
        if (dia == _diaDica)
            return;
        _diaDica = dia;
        _dica.SetToolTip(this, dia is null ? null
            : $"{sobre.Dia.Data.ToString("dddd, dd/MM/yyyy", Textos.PtBr)}: " +
              (sobre.Dia.Ajuste.Regras.Count == 0 ? "preço de tabela" : sobre.Dia.Ajuste.Descricao) +
              (sobre.Dia.Ajuste.MinimoConvidados is { } minimo ? $" (mínimo de {minimo} pagantes)" : ""));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dica.Dispose();
        base.Dispose(disposing);
    }
}
