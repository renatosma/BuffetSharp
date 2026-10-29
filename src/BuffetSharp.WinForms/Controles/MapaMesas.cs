using System.Drawing.Drawing2D;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Controles;

/// <summary>
/// Planta do salão com as mesas desenhadas em GDI+: as cadeiras mostram a ocupação, a mesa selecionada fica em destaque e
/// as mesas podem ser arrastadas. Aceita convidados soltos sobre uma mesa (arrastar e soltar a partir de uma grade).
/// O mesmo desenho é usado na impressão do mapa.
/// </summary>
internal sealed class MapaMesas : Control
{
    /// <param name="Ocupados">Lugares já tomados (convidados e acompanhantes sentados).</param>
    public sealed record Mesa(int Id, int Numero, string? Nome, int Lugares, FormatoMesa Formato, int X, int Y, int Ocupados)
    {
        public string Descricao => PlantaSalao.Descricao(Numero, Nome);
    }

    private IReadOnlyList<Mesa> _mesas = [];
    private int? _selecionada;
    private Mesa? _arrastada;
    private Point _inicioArraste;
    private (int X, int Y) _posicaoArraste;
    private bool _moveu;
    private readonly ToolTip _dica = new() { InitialDelay = 300, ReshowDelay = 100 };
    private int? _mesaDica;

    public MapaMesas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Tema.Superficie;
        AllowDrop = true;
    }

    /// <summary>Se as mesas podem ser arrastadas (desligado em evento realizado ou cancelado).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool PermiteMover { get; set; } = true;

    /// <summary>Texto da dica ao parar o mouse sobre a mesa (ex.: quem está sentado nela).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<Mesa, string>? TextoDica { get; set; }

    public IReadOnlyList<Mesa> Mesas => _mesas;

    public Mesa? Selecionada => _mesas.FirstOrDefault(m => m.Id == _selecionada);

    /// <summary>Mesa clicada (nula: clique fora das mesas).</summary>
    public event Action<Mesa?>? SelecaoAlterada;

    /// <summary>Mesa arrastada para a nova posição (em pontos da planta).</summary>
    public event Action<Mesa, int, int>? MesaMovida;

    public event Action<Mesa>? MesaAberta;

    /// <summary>Algo arrastado de fora (ex.: um convidado da grade) foi solto sobre a mesa.</summary>
    public event Action<Mesa, IDataObject>? Solto;

    public void Definir(IReadOnlyList<Mesa> mesas)
    {
        _mesas = mesas;
        _arrastada = null;
        if (_mesas.All(m => m.Id != _selecionada))
            _selecionada = null;
        Invalidate();
    }

    public void Selecionar(int? mesaId)
    {
        _selecionada = _mesas.Any(m => m.Id == mesaId) ? mesaId : null;
        Invalidate();
        SelecaoAlterada?.Invoke(Selecionada);
    }

    // ------------------------- Desenho -------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var mesas = _arrastada is null ? _mesas
            : _mesas.Select(m => m.Id == _arrastada.Id ? m with { X = _posicaoArraste.X, Y = _posicaoArraste.Y } : m).ToList();
        Desenhar(g, AreaPlanta(), mesas, _selecionada, Font);
        if (_mesas.Count == 0)
            TextRenderer.DrawText(g, "Nenhuma mesa no mapa. Use \"Nova mesa\" ou \"Usar o mapa do salão\".", Tema.Fonte, ClientRectangle,
                Tema.TextoSuave, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>Desenha a planta com as mesas dentro da área (mantendo a proporção). Usado também na impressão.</summary>
    public static void Desenhar(Graphics g, RectangleF area, IReadOnlyList<Mesa> mesas, int? selecionada, Font fonte)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var (escala, origem) = Escala(area);
        var planta = new RectangleF(origem.X, origem.Y, PlantaSalao.Largura * escala, PlantaSalao.Altura * escala);

        using (var piso = new SolidBrush(Color.FromArgb(250, 248, 245)))
            g.FillRectangle(piso, planta);
        using (var grade = new Pen(Color.FromArgb(236, 231, 225)))
        {
            for (var x = 100; x < PlantaSalao.Largura; x += 100)
                g.DrawLine(grade, origem.X + x * escala, planta.Top, origem.X + x * escala, planta.Bottom);
            for (var y = 100; y < PlantaSalao.Altura; y += 100)
                g.DrawLine(grade, planta.Left, origem.Y + y * escala, planta.Right, origem.Y + y * escala);
        }
        using (var borda = new Pen(Tema.Borda, 2))
            g.DrawRectangle(borda, planta.X, planta.Y, planta.Width, planta.Height);

        using var fonteNumero = new Font(fonte.FontFamily, Math.Clamp(15f * escala, 6f, 16f), FontStyle.Bold);
        using var fontePequena = new Font(fonte.FontFamily, Math.Clamp(9.5f * escala, 5f, 10f));
        var centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        foreach (var mesa in mesas)
        {
            var (largura, altura) = PlantaSalao.Tamanho(mesa.Formato, mesa.Lugares);
            var retangulo = new RectangleF(origem.X + (mesa.X - largura / 2f) * escala, origem.Y + (mesa.Y - altura / 2f) * escala,
                largura * escala, altura * escala);

            DesenharCadeiras(g, mesa, retangulo, escala);

            var cor = mesa.Ocupados == 0 ? Color.White
                : mesa.Ocupados < mesa.Lugares ? Color.FromArgb(255, 243, 224)
                : mesa.Ocupados == mesa.Lugares ? Color.FromArgb(232, 245, 233)
                : Color.FromArgb(255, 235, 238);
            using (var fundo = new SolidBrush(cor))
            using (var contorno = new Pen(mesa.Id == selecionada ? Tema.Primaria : Color.FromArgb(150, 140, 130), mesa.Id == selecionada ? 3 : 1.5f))
            {
                if (mesa.Formato == FormatoMesa.Redonda)
                {
                    g.FillEllipse(fundo, retangulo);
                    g.DrawEllipse(contorno, retangulo);
                }
                else
                {
                    g.FillRectangle(fundo, retangulo);
                    g.DrawRectangle(contorno, retangulo.X, retangulo.Y, retangulo.Width, retangulo.Height);
                }
            }

            var alturaNumero = fonteNumero.GetHeight(g);
            var alturaPequena = fontePequena.GetHeight(g);
            var meio = retangulo.Top + retangulo.Height / 2;
            var temNome = mesa.Nome is not null && retangulo.Height > alturaNumero + alturaPequena * 2;
            var topo = meio - (alturaNumero + alturaPequena * (temNome ? 2 : 1)) / 2;
            if (temNome)
            {
                g.DrawString(mesa.Nome, fontePequena, Brushes.DimGray, new RectangleF(retangulo.X + 2, topo, retangulo.Width - 4, alturaPequena), centro);
                topo += alturaPequena;
            }
            using (var pincel = new SolidBrush(Tema.Texto))
                g.DrawString(mesa.Numero.ToString(), fonteNumero, pincel, new RectangleF(retangulo.X, topo, retangulo.Width, alturaNumero), centro);
            using (var pincelOcupacao = new SolidBrush(mesa.Ocupados > mesa.Lugares ? Tema.Perigo : Tema.TextoSuave))
                g.DrawString($"{mesa.Ocupados}/{mesa.Lugares}", fontePequena, pincelOcupacao,
                    new RectangleF(retangulo.X, topo + alturaNumero, retangulo.Width, alturaPequena), centro);
        }
    }

    /// <summary>Uma cadeira por lugar em volta da mesa; as ocupadas ficam preenchidas.</summary>
    private static void DesenharCadeiras(Graphics g, Mesa mesa, RectangleF mesaArea, float escala)
    {
        var raio = Math.Max(2f, 6f * escala);
        var afastamento = raio + 2 * escala;
        var posicoes = new List<PointF>();
        if (mesa.Formato == FormatoMesa.Retangular)
        {
            var porLado = (mesa.Lugares + 1) / 2;
            for (var i = 0; i < mesa.Lugares; i++)
            {
                var lado = i < porLado ? 0 : 1;
                var indice = lado == 0 ? i : i - porLado;
                var quantidade = lado == 0 ? porLado : mesa.Lugares - porLado;
                var x = mesaArea.Left + mesaArea.Width * (indice + 0.5f) / quantidade;
                posicoes.Add(new PointF(x, lado == 0 ? mesaArea.Top - afastamento : mesaArea.Bottom + afastamento));
            }
        }
        else if (mesa.Formato == FormatoMesa.Quadrada)
        {
            // Os lugares se dividem pelos quatro lados (cima, direita, baixo, esquerda).
            for (var lado = 0; lado < 4; lado++)
            {
                var quantidade = mesa.Lugares / 4 + (lado < mesa.Lugares % 4 ? 1 : 0);
                for (var i = 0; i < quantidade; i++)
                {
                    var t = (i + 0.5f) / quantidade;
                    posicoes.Add(lado switch
                    {
                        0 => new PointF(mesaArea.Left + mesaArea.Width * t, mesaArea.Top - afastamento),
                        1 => new PointF(mesaArea.Right + afastamento, mesaArea.Top + mesaArea.Height * t),
                        2 => new PointF(mesaArea.Right - mesaArea.Width * t, mesaArea.Bottom + afastamento),
                        _ => new PointF(mesaArea.Left - afastamento, mesaArea.Bottom - mesaArea.Height * t)
                    });
                }
            }
        }
        else
        {
            var cx = mesaArea.Left + mesaArea.Width / 2;
            var cy = mesaArea.Top + mesaArea.Height / 2;
            var r = mesaArea.Width / 2 + afastamento;
            for (var i = 0; i < mesa.Lugares; i++)
            {
                var angulo = -Math.PI / 2 + 2 * Math.PI * i / mesa.Lugares;
                posicoes.Add(new PointF(cx + (float)(Math.Cos(angulo) * r), cy + (float)(Math.Sin(angulo) * r)));
            }
        }

        using var ocupada = new SolidBrush(Tema.Primaria);
        using var livre = new SolidBrush(Color.White);
        using var contorno = new Pen(Color.FromArgb(170, 160, 150));
        for (var i = 0; i < posicoes.Count; i++)
        {
            var p = posicoes[i];
            g.FillEllipse(i < mesa.Ocupados ? ocupada : livre, p.X - raio, p.Y - raio, raio * 2, raio * 2);
            g.DrawEllipse(contorno, p.X - raio, p.Y - raio, raio * 2, raio * 2);
        }
    }

    private static (float Escala, PointF Origem) Escala(RectangleF area)
    {
        var escala = Math.Min(area.Width / PlantaSalao.Largura, area.Height / PlantaSalao.Altura);
        return (escala, new PointF(area.Left + (area.Width - PlantaSalao.Largura * escala) / 2,
            area.Top + (area.Height - PlantaSalao.Altura * escala) / 2));
    }

    private RectangleF AreaPlanta()
    {
        var margem = LogicalToDeviceUnits(12);
        return new RectangleF(margem, margem, Math.Max(1, Width - margem * 2), Math.Max(1, Height - margem * 2));
    }

    // ------------------------- Interação -------------------------

    private (int X, int Y) ParaPlanta(Point ponto)
    {
        var (escala, origem) = Escala(AreaPlanta());
        return ((int)Math.Round((ponto.X - origem.X) / escala), (int)Math.Round((ponto.Y - origem.Y) / escala));
    }

    public Mesa? MesaEm(Point ponto)
    {
        var (x, y) = ParaPlanta(ponto);
        return _mesas.LastOrDefault(m =>
        {
            var (largura, altura) = PlantaSalao.Tamanho(m.Formato, m.Lugares);
            return Math.Abs(x - m.X) <= largura / 2 + 8 && Math.Abs(y - m.Y) <= altura / 2 + 8;
        });
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
            return;
        var mesa = MesaEm(e.Location);
        Selecionar(mesa?.Id);
        if (mesa is not null && PermiteMover)
        {
            _arrastada = mesa;
            _inicioArraste = e.Location;
            _posicaoArraste = (mesa.X, mesa.Y);
            _moveu = false;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_arrastada is not null && e.Button == MouseButtons.Left)
        {
            if (!_moveu && Math.Abs(e.X - _inicioArraste.X) + Math.Abs(e.Y - _inicioArraste.Y) < 4)
                return;
            _moveu = true;
            var (x0, y0) = ParaPlanta(_inicioArraste);
            var (x1, y1) = ParaPlanta(e.Location);
            _posicaoArraste = PlantaSalao.Limitar(_arrastada.X + x1 - x0, _arrastada.Y + y1 - y0);
            Cursor = Cursors.SizeAll;
            Invalidate();
            return;
        }

        var sobre = MesaEm(e.Location);
        if (sobre?.Id == _mesaDica)
            return;
        _mesaDica = sobre?.Id;
        _dica.SetToolTip(this, sobre is null ? null : TextoDica?.Invoke(sobre) ?? sobre.Descricao);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var arrastada = _arrastada;
        _arrastada = null;
        Cursor = Cursors.Default;
        if (arrastada is null || !_moveu)
            return;
        _moveu = false;
        var (x, y) = _posicaoArraste;
        _mesas = _mesas.Select(m => m.Id == arrastada.Id ? m with { X = x, Y = y } : m).ToList();
        Invalidate();
        MesaMovida?.Invoke(arrastada, x, y);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (MesaEm(e.Location) is { } mesa)
            MesaAberta?.Invoke(mesa);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        var mesa = MesaEm(PointToClient(new Point(e.X, e.Y)));
        e.Effect = mesa is null ? DragDropEffects.None : DragDropEffects.Move;
        if (mesa is not null && mesa.Id != _selecionada)
        {
            _selecionada = mesa.Id;
            Invalidate();
        }
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        if (e.Data is not null && MesaEm(PointToClient(new Point(e.X, e.Y))) is { } mesa)
        {
            Selecionar(mesa.Id);
            Solto?.Invoke(mesa, e.Data);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dica.Dispose();
        base.Dispose(disposing);
    }
}
