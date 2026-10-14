using System.Drawing.Printing;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>
/// Mini motor de relatórios sobre <see cref="PrintDocument"/>: blocos de texto, pares rótulo/valor e
/// tabelas fluem pelas páginas com quebra automática, cabeçalho e numeração de páginas.
/// </summary>
internal sealed class DocumentoImpresso
{
    private abstract record Bloco;
    private sealed record BlocoTitulo(string Texto, float Tamanho) : Bloco;
    private sealed record BlocoTexto(string Texto, bool Negrito, Color Cor) : Bloco;
    private sealed record BlocoCampos((string Rotulo, string Valor)[] Pares, int Colunas) : Bloco;
    private sealed record BlocoTabela(string[] Cabecalho, List<string[]> Linhas, float[]? Pesos, bool[]? Direita) : Bloco;
    private sealed record BlocoEspaco(float Altura) : Bloco;
    private sealed record BlocoTotal(string Rotulo, string Valor) : Bloco;
    private sealed record BlocoAssinaturas(string[] Nomes) : Bloco;

    private readonly string _titulo;
    private readonly string? _empresa;
    private readonly List<Bloco> _blocos = [];

    private int _blocoAtual;
    private int _linhaTabela;
    private int _pagina;

    private static readonly Font FonteTexto = new("Segoe UI", 9.5f);
    private static readonly Font FonteNegrito = new("Segoe UI Semibold", 9.5f);
    private static readonly Font FontePequena = new("Segoe UI", 8f);

    public DocumentoImpresso(string titulo, string? empresa = null)
    {
        _titulo = titulo;
        _empresa = empresa;
    }

    // ------------------------- Construção -------------------------

    public DocumentoImpresso Secao(string texto) => Add(new BlocoTitulo(texto, 12f));

    public DocumentoImpresso Subtitulo(string texto) => Add(new BlocoTexto(texto, false, Color.DimGray));

    public DocumentoImpresso Paragrafo(string texto, bool negrito = false) => Add(new BlocoTexto(texto, negrito, Color.Black));

    public DocumentoImpresso Campos(int colunas, params (string Rotulo, string Valor)[] pares) => Add(new BlocoCampos(pares, colunas));

    public DocumentoImpresso Tabela(string[] cabecalho, List<string[]> linhas, float[]? pesos = null, bool[]? direita = null) =>
        Add(new BlocoTabela(cabecalho, linhas, pesos, direita));

    public DocumentoImpresso Total(string rotulo, string valor) => Add(new BlocoTotal(rotulo, valor));

    public DocumentoImpresso Espaco(float altura = 12) => Add(new BlocoEspaco(altura));

    public DocumentoImpresso Assinaturas(params string[] nomes) => Add(new BlocoAssinaturas(nomes));

    private DocumentoImpresso Add(Bloco bloco)
    {
        _blocos.Add(bloco);
        return this;
    }

    // ------------------------- Impressão -------------------------

    public void Visualizar(IWin32Window dono, bool paisagem = false)
    {
        using var documento = new PrintDocument { DocumentName = _titulo };
        documento.DefaultPageSettings.Landscape = paisagem;
        documento.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
        documento.BeginPrint += (_, _) =>
        {
            _blocoAtual = 0;
            _linhaTabela = 0;
            _pagina = 0;
        };
        documento.PrintPage += ImprimirPagina;

        using var visualizacao = new PrintPreviewDialog
        {
            Document = documento,
            Width = 1000,
            Height = 800,
            StartPosition = FormStartPosition.CenterScreen,
            Text = $"Visualizar impressão - {_titulo}",
            UseAntiAlias = true
        };
        visualizacao.PrintPreviewControl.Zoom = 1.0;
        visualizacao.ShowDialog(dono);
    }

    private void ImprimirPagina(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        var area = e.MarginBounds;
        _pagina++;
        var y = DesenharCabecalho(g, area);

        while (_blocoAtual < _blocos.Count)
        {
            var bloco = _blocos[_blocoAtual];
            var (novoY, terminou) = Desenhar(g, bloco, area, y);
            if (!terminou)
            {
                e.HasMorePages = true;
                DesenharRodape(g, area);
                return;
            }
            y = novoY;
            _blocoAtual++;
            _linhaTabela = 0;
        }

        DesenharRodape(g, area);
        e.HasMorePages = false;
    }

    private float DesenharCabecalho(Graphics g, Rectangle area)
    {
        using var fonteTitulo = new Font("Segoe UI Semibold", 15f);
        using var pincel = new SolidBrush(Tema.Primaria);
        var y = (float)area.Top;
        if (_empresa is not null)
        {
            g.DrawString(_empresa, FonteNegrito, Brushes.DimGray, area.Left, y);
            y += FonteNegrito.GetHeight(g) + 2;
        }
        g.DrawString(_titulo, fonteTitulo, pincel, area.Left, y);
        y += fonteTitulo.GetHeight(g) + 4;
        using var caneta = new Pen(Tema.Destaque, 2);
        g.DrawLine(caneta, area.Left, y, area.Right, y);
        return y + 10;
    }

    private void DesenharRodape(Graphics g, Rectangle area)
    {
        var texto = $"BuffetSharp  •  Página {_pagina}  •  {DateTime.Now:dd/MM/yyyy HH:mm}";
        var tamanho = g.MeasureString(texto, FontePequena);
        g.DrawString(texto, FontePequena, Brushes.Gray, area.Right - tamanho.Width, area.Bottom + 10);
    }

    /// <summary>Desenha o bloco a partir de y. Retorna (novo y, concluído?). Tabelas podem continuar na próxima página.</summary>
    private (float Y, bool Terminou) Desenhar(Graphics g, Bloco bloco, Rectangle area, float y)
    {
        switch (bloco)
        {
            case BlocoEspaco espaco:
                return (y + espaco.Altura, true);

            case BlocoTitulo titulo:
            {
                using var fonte = new Font("Segoe UI Semibold", titulo.Tamanho);
                var altura = fonte.GetHeight(g) + 8;
                if (y + altura + 30 > area.Bottom)
                    return (y, false);
                g.DrawString(titulo.Texto, fonte, new SolidBrush(Tema.Primaria), area.Left, y + 4);
                return (y + altura, true);
            }

            case BlocoTexto texto:
            {
                var fonte = texto.Negrito ? FonteNegrito : FonteTexto;
                var tamanho = g.MeasureString(texto.Texto, fonte, area.Width);
                if (y + tamanho.Height > area.Bottom)
                    return (y, false);
                using var pincel = new SolidBrush(texto.Cor);
                g.DrawString(texto.Texto, fonte, pincel, new RectangleF(area.Left, y, area.Width, tamanho.Height + 2));
                return (y + tamanho.Height + 4, true);
            }

            case BlocoCampos campos:
            {
                var largura = area.Width / (float)campos.Colunas;
                var alturaLinha = FontePequena.GetHeight(g) + FonteNegrito.GetHeight(g) + 8;
                var linhas = (int)Math.Ceiling(campos.Pares.Length / (double)campos.Colunas);
                if (y + linhas * alturaLinha > area.Bottom)
                    return (y, false);
                for (var i = 0; i < campos.Pares.Length; i++)
                {
                    var x = area.Left + (i % campos.Colunas) * largura;
                    var yy = y + (i / campos.Colunas) * alturaLinha;
                    g.DrawString(campos.Pares[i].Rotulo, FontePequena, Brushes.Gray, x, yy);
                    g.DrawString(campos.Pares[i].Valor, FonteNegrito, Brushes.Black,
                        new RectangleF(x, yy + FontePequena.GetHeight(g), largura - 8, FonteNegrito.GetHeight(g) + 2));
                }
                return (y + linhas * alturaLinha + 4, true);
            }

            case BlocoTotal total:
            {
                using var fonte = new Font("Segoe UI Semibold", 12f);
                var altura = fonte.GetHeight(g) + 8;
                if (y + altura > area.Bottom)
                    return (y, false);
                var tamanhoValor = g.MeasureString(total.Valor, fonte);
                var tamanhoRotulo = g.MeasureString(total.Rotulo, FonteTexto);
                g.DrawString(total.Rotulo, FonteTexto, Brushes.DimGray, area.Right - tamanhoValor.Width - tamanhoRotulo.Width - 10, y + 3);
                g.DrawString(total.Valor, fonte, new SolidBrush(Tema.Primaria), area.Right - tamanhoValor.Width, y);
                return (y + altura, true);
            }

            case BlocoAssinaturas assinaturas:
            {
                var altura = 70f;
                if (y + altura > area.Bottom)
                    return (y, false);
                var largura = area.Width / (float)assinaturas.Nomes.Length;
                for (var i = 0; i < assinaturas.Nomes.Length; i++)
                {
                    var x = area.Left + i * largura + 20;
                    g.DrawLine(Pens.Gray, x, y + 45, x + largura - 40, y + 45);
                    var formato = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(assinaturas.Nomes[i], FontePequena, Brushes.DimGray,
                        new RectangleF(x, y + 50, largura - 40, 20), formato);
                }
                return (y + altura, true);
            }

            case BlocoTabela tabela:
                return DesenharTabela(g, tabela, area, y);

            default:
                return (y, true);
        }
    }

    private (float Y, bool Terminou) DesenharTabela(Graphics g, BlocoTabela tabela, Rectangle area, float y)
    {
        var colunas = tabela.Cabecalho.Length;
        var pesos = tabela.Pesos ?? Enumerable.Repeat(1f, colunas).ToArray();
        var soma = pesos.Sum();
        var larguras = pesos.Select(p => area.Width * p / soma).ToArray();
        var alturaLinha = FonteTexto.GetHeight(g) + 8;

        if (y + alturaLinha * 2 > area.Bottom)
            return (y, false);

        // Cabeçalho (repetido em cada página)
        using (var fundo = new SolidBrush(Color.FromArgb(245, 240, 238)))
            g.FillRectangle(fundo, area.Left, y, area.Width, alturaLinha);
        DesenharLinha(g, tabela.Cabecalho, larguras, area.Left, y, alturaLinha, FonteNegrito, tabela.Direita);
        y += alturaLinha;

        while (_linhaTabela < tabela.Linhas.Count)
        {
            if (y + alturaLinha > area.Bottom)
                return (y, false);
            if (_linhaTabela % 2 == 1)
                using (var zebra = new SolidBrush(Color.FromArgb(250, 249, 248)))
                    g.FillRectangle(zebra, area.Left, y, area.Width, alturaLinha);
            DesenharLinha(g, tabela.Linhas[_linhaTabela], larguras, area.Left, y, alturaLinha, FonteTexto, tabela.Direita);
            y += alturaLinha;
            _linhaTabela++;
        }

        g.DrawLine(Pens.Gainsboro, area.Left, y, area.Right, y);
        return (y + 8, true);
    }

    private static void DesenharLinha(Graphics g, string[] celulas, float[] larguras, float x, float y, float altura, Font fonte, bool[]? direita)
    {
        for (var i = 0; i < larguras.Length && i < celulas.Length; i++)
        {
            var formato = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
                LineAlignment = StringAlignment.Center,
                Alignment = direita is not null && i < direita.Length && direita[i] ? StringAlignment.Far : StringAlignment.Near
            };
            g.DrawString(celulas[i], fonte, Brushes.Black, new RectangleF(x + 4, y, larguras[i] - 8, altura), formato);
            x += larguras[i];
        }
    }
}
