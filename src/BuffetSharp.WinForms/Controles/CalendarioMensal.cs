using System.Drawing.Drawing2D;
using BuffetSharp.Application.Consultas;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>
/// Calendário mensal desenhado em GDI+: cada dia lista os eventos (cor por espaço) e as
/// pré-reservas (orçamentos enviados) com borda tracejada.
/// </summary>
internal sealed class CalendarioMensal : Control
{
    private static readonly Color[] Paleta =
    [
        Color.FromArgb(139, 30, 63), Color.FromArgb(2, 119, 189), Color.FromArgb(46, 125, 50),
        Color.FromArgb(214, 108, 0), Color.FromArgb(106, 27, 154), Color.FromArgb(0, 121, 107)
    ];

    private DateOnly _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private IReadOnlyList<ItemAgenda> _itens = [];
    private readonly List<(RectangleF Area, ItemAgenda Item)> _areasItens = [];
    private readonly List<(RectangleF Area, DateOnly Dia)> _areasDias = [];
    private readonly ToolTip _dica = new() { InitialDelay = 300 };
    private ItemAgenda? _itemDica;

    public CalendarioMensal()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Tema.Superficie;
        Font = Tema.FontePequena;
    }

    public event Action<ItemAgenda>? ItemAberto;
    public event Action<DateOnly>? DiaSelecionado;

    public DateOnly DiaAtual { get; private set; } = DateOnly.FromDateTime(DateTime.Today);

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DateOnly Mes
    {
        get => _mes;
        set
        {
            _mes = new DateOnly(value.Year, value.Month, 1);
            Invalidate();
        }
    }

    /// <summary>Primeiro dia exibido (domingo da semana que contém o dia 1).</summary>
    public DateOnly InicioGrade => _mes.AddDays(-(int)_mes.DayOfWeek);

    public void DefinirItens(IReadOnlyList<ItemAgenda> itens)
    {
        _itens = itens;
        Invalidate();
    }

    public static Color CorDoEspaco(int espacoId) => Paleta[Math.Abs(espacoId - 1) % Paleta.Length];

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        _areasItens.Clear();
        _areasDias.Clear();

        var alturaCabecalho = LogicalToDeviceUnits(28);
        var largura = Width / 7f;
        var altura = (Height - alturaCabecalho) / 6f;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        string[] dias = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

        for (var c = 0; c < 7; c++)
            TextRenderer.DrawText(g, dias[c], Tema.FonteNegrito,
                new Rectangle((int)(c * largura), 0, (int)largura, alturaCabecalho), Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        using var linha = new Pen(Tema.Borda);
        var alturaItem = Font.Height + LogicalToDeviceUnits(4);

        for (var i = 0; i < 42; i++)
        {
            var dia = InicioGrade.AddDays(i);
            var area = new RectangleF(i % 7 * largura, alturaCabecalho + i / 7 * altura, largura, altura);
            _areasDias.Add((area, dia));

            if (dia.Month != _mes.Month)
                using (var fora = new SolidBrush(Color.FromArgb(250, 249, 248)))
                    g.FillRectangle(fora, area);
            if (dia == DiaAtual)
                using (var selecionado = new SolidBrush(Tema.PrimariaClara))
                    g.FillRectangle(selecionado, area);
            g.DrawRectangle(linha, area.X, area.Y, area.Width, area.Height);

            var corDia = dia.Month != _mes.Month ? Color.Silver : Tema.Texto;
            var textoDia = dia.Day.ToString();
            if (dia == hoje)
            {
                var circulo = new RectangleF(area.Right - LogicalToDeviceUnits(28), area.Top + 3, LogicalToDeviceUnits(24), LogicalToDeviceUnits(20));
                using var pincel = new SolidBrush(Tema.Primaria);
                g.FillEllipse(pincel, circulo);
                TextRenderer.DrawText(g, textoDia, Tema.FonteNegrito, Rectangle.Round(circulo), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(g, textoDia, Tema.FonteNegrito,
                    new Rectangle((int)area.Right - LogicalToDeviceUnits(30), (int)area.Top + 3, LogicalToDeviceUnits(26), LogicalToDeviceUnits(20)),
                    corDia, TextFormatFlags.Right);
            }

            var doDia = _itens.Where(x => DateOnly.FromDateTime(x.Inicio) == dia).OrderBy(x => x.Inicio).ToList();
            var y = area.Top + LogicalToDeviceUnits(24);
            var capacidade = Math.Max(0, (int)((area.Bottom - y - 2) / alturaItem));
            for (var k = 0; k < doDia.Count && k < capacidade; k++)
            {
                var item = doDia[k];
                if (k == capacidade - 1 && doDia.Count > capacidade)
                {
                    TextRenderer.DrawText(g, $"+{doDia.Count - k} mais", Font, new Point((int)area.Left + 6, (int)y), Tema.TextoSuave);
                    break;
                }
                var chip = new RectangleF(area.Left + 3, y, area.Width - 6, alturaItem - 2);
                DesenharItem(g, chip, item);
                _areasItens.Add((chip, item));
                y += alturaItem;
            }
        }
    }

    private void DesenharItem(Graphics g, RectangleF chip, ItemAgenda item)
    {
        var cor = CorDoEspaco(item.EspacoId);
        if (item.Provisorio)
        {
            using var fundo = new SolidBrush(Color.FromArgb(30, cor));
            using var borda = new Pen(cor) { DashStyle = DashStyle.Dash };
            g.FillRectangle(fundo, chip);
            g.DrawRectangle(borda, chip.X, chip.Y, chip.Width - 1, chip.Height - 1);
        }
        else
        {
            using var fundo = new SolidBrush(item.Status == Domain.Eventos.StatusEvento.Realizado ? Color.FromArgb(150, cor) : cor);
            g.FillRectangle(fundo, chip);
        }
        TextRenderer.DrawText(g, $"{item.Inicio:HH:mm} {item.Titulo}", Font, Rectangle.Round(chip),
            item.Provisorio ? cor : Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = _areasItens.FirstOrDefault(a => a.Area.Contains(e.Location)).Item;
        Cursor = item is null ? Cursors.Default : Cursors.Hand;
        if (item == _itemDica)
            return;
        _itemDica = item;
        if (item is null)
        {
            _dica.Hide(this);
            return;
        }
        var texto = $"{item.Codigo} - {item.Titulo}\n{item.Inicio:dd/MM HH:mm} às {item.Fim:HH:mm}\n" +
                    $"{item.Espaco} • {item.Convidados} convidados\nCliente: {item.Cliente}" +
                    (item.Provisorio ? "\n(pré-reserva: orçamento enviado)" : "");
        _dica.Show(texto, this, e.X + 14, e.Y + 14);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
        var dia = _areasDias.FirstOrDefault(a => a.Area.Contains(e.Location));
        if (dia.Dia == default)
            return;
        DiaAtual = dia.Dia;
        Invalidate();
        DiaSelecionado?.Invoke(dia.Dia);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_areasItens.FirstOrDefault(a => a.Area.Contains(e.Location)).Item is { } item)
            ItemAberto?.Invoke(item);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dica.Dispose();
        base.Dispose(disposing);
    }
}
