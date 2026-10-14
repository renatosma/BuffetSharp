using System.Drawing.Text;

namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Identidade visual: paleta, fontes e estilos aplicados aos controles criados em código.
/// </summary>
internal static class Tema
{
    // Paleta (vinho + dourado, transmitindo o ar de "evento")
    public static readonly Color Primaria = Color.FromArgb(139, 30, 63);
    public static readonly Color PrimariaEscura = Color.FromArgb(104, 20, 46);
    public static readonly Color PrimariaClara = Color.FromArgb(248, 232, 238);
    public static readonly Color Destaque = Color.FromArgb(201, 162, 39);
    public static readonly Color Fundo = Color.FromArgb(246, 244, 241);
    public static readonly Color Superficie = Color.White;
    public static readonly Color Borda = Color.FromArgb(225, 220, 214);
    public static readonly Color Texto = Color.FromArgb(43, 43, 43);
    public static readonly Color TextoSuave = Color.FromArgb(110, 106, 102);
    public static readonly Color Lateral = Color.FromArgb(42, 26, 32);
    public static readonly Color LateralHover = Color.FromArgb(68, 40, 52);
    public static readonly Color LateralTexto = Color.FromArgb(232, 222, 226);

    public static readonly Color Sucesso = Color.FromArgb(46, 125, 50);
    public static readonly Color Alerta = Color.FromArgb(214, 108, 0);
    public static readonly Color Perigo = Color.FromArgb(198, 40, 40);
    public static readonly Color Info = Color.FromArgb(2, 119, 189);
    public static readonly Color Neutro = Color.FromArgb(117, 117, 117);

    public static readonly Color SerieA = Primaria;
    public static readonly Color SerieB = Destaque;

    public static readonly Font Fonte = new("Segoe UI", 9.75f);
    public static readonly Font FonteNegrito = new("Segoe UI Semibold", 9.75f);
    public static readonly Font FontePequena = new("Segoe UI", 8.5f);
    public static readonly Font FonteTitulo = new("Segoe UI Semibold", 15f);
    public static readonly Font FonteSubtitulo = new("Segoe UI Semibold", 11.5f);
    public static readonly Font FonteValor = new("Segoe UI Semibold", 17f);

    /// <summary>Fonte de ícones do Windows (Segoe Fluent Icons no Windows 11, MDL2 no Windows 10).</summary>
    public static readonly string? FonteIcones = DescobrirFonteIcones();

    private static string? DescobrirFonteIcones()
    {
        using var instaladas = new InstalledFontCollection();
        var nomes = instaladas.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }.FirstOrDefault(nomes.Contains);
    }

    public static void EstilizarGrade(DataGridView grade)
    {
        grade.BackgroundColor = Superficie;
        grade.BorderStyle = BorderStyle.None;
        grade.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grade.GridColor = Borda;
        grade.EnableHeadersVisualStyles = false;
        grade.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grade.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(250, 248, 246),
            ForeColor = TextoSuave,
            Font = FonteNegrito,
            SelectionBackColor = Color.FromArgb(250, 248, 246),
            SelectionForeColor = TextoSuave,
            Padding = new Padding(6, 0, 6, 0),
            WrapMode = DataGridViewTriState.False
        };
        grade.ColumnHeadersHeight = 38;
        grade.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grade.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Superficie,
            ForeColor = Texto,
            Font = Fonte,
            SelectionBackColor = PrimariaClara,
            SelectionForeColor = Texto,
            Padding = new Padding(6, 0, 6, 0)
        };
        grade.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 251, 250);
        grade.RowTemplate.Height = 32;
        grade.RowHeadersVisible = false;
        grade.AllowUserToAddRows = false;
        grade.AllowUserToDeleteRows = false;
        grade.AllowUserToResizeRows = false;
        grade.AllowUserToOrderColumns = true;
        grade.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grade.MultiSelect = false;
        grade.ReadOnly = true;
        grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grade.Dock = DockStyle.Fill;

        // Double buffering evita cintilação ao rolar grades grandes.
        typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(grade, true);
    }

    public static Button BotaoPrimario(string texto, char? icone = null) => Botao(texto, icone, Primaria, Color.White, Primaria);

    public static Button BotaoSecundario(string texto, char? icone = null) => Botao(texto, icone, Superficie, Texto, Borda);

    public static Button BotaoPerigo(string texto, char? icone = null) => Botao(texto, icone, Superficie, Perigo, Perigo);

    private static Button Botao(string texto, char? icone, Color fundo, Color frente, Color borda)
    {
        var botao = new Button
        {
            Text = texto,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            BackColor = fundo,
            ForeColor = frente,
            Font = FonteNegrito,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(6, 3, 0, 3),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        botao.FlatAppearance.BorderColor = borda;
        botao.FlatAppearance.MouseOverBackColor = fundo == Primaria ? PrimariaEscura : Color.FromArgb(245, 240, 238);
        if (icone is { } glifo)
        {
            botao.Image = Icones.Imagem(glifo, frente, 16);
            botao.ImageAlign = ContentAlignment.MiddleLeft;
            botao.TextImageRelation = TextImageRelation.ImageBeforeText;
        }

        // Botões "flat" com cor própria não indicam visualmente quando estão desabilitados.
        var desabilitado = Color.FromArgb(175, 170, 168);
        botao.EnabledChanged += (_, _) =>
        {
            botao.ForeColor = botao.Enabled ? frente : desabilitado;
            botao.BackColor = botao.Enabled ? fundo : Color.FromArgb(242, 240, 238);
            botao.FlatAppearance.BorderColor = botao.Enabled ? borda : Borda;
            if (icone is { } g)
                botao.Image = Icones.Imagem(g, botao.Enabled ? frente : desabilitado, 16);
        };
        return botao;
    }

    public static Label Titulo(string texto) => new()
    {
        Text = texto,
        Font = FonteTitulo,
        ForeColor = Texto,
        AutoSize = true,
        Margin = new Padding(0)
    };

    public static Label Subtitulo(string texto) => new()
    {
        Text = texto,
        Font = Fonte,
        ForeColor = TextoSuave,
        AutoSize = true,
        Margin = new Padding(0, 2, 0, 0)
    };

    /// <summary>Painel branco com borda suave, usado como "cartão".</summary>
    public static Panel Cartao(Padding? padding = null)
    {
        var painel = new Panel { BackColor = Superficie, Padding = padding ?? new Padding(14), Margin = new Padding(6) };
        painel.Paint += (_, e) =>
        {
            using var caneta = new Pen(Borda);
            e.Graphics.DrawRectangle(caneta, 0, 0, painel.Width - 1, painel.Height - 1);
        };
        return painel;
    }
}

/// <summary>
/// Ícones desenhados a partir da fonte de símbolos do Windows: nítidos em qualquer DPI e sem arquivos de imagem.
/// </summary>
internal static class Icones
{
    public const char Adicionar = '';
    public const char Editar = '';
    public const char Excluir = '';
    public const char Atualizar = '';
    public const char Salvar = '';
    public const char Imprimir = '';
    public const char Buscar = '';
    public const char Calendario = '';
    public const char Pessoas = '';
    public const char Contato = '';
    public const char Cartao = '';
    public const char Loja = '';
    public const char Inicio = '';
    public const char Configuracoes = '';
    public const char Documento = '';
    public const char Calculadora = '';
    public const char Confirmar = '';
    public const char Cancelar = '';
    public const char Aviso = '';
    public const char Informacao = '';
    public const char Copiar = '';
    public const char Enviar = '';
    public const char Aceitar = '';
    public const char Filtro = '';
    public const char Historico = '';
    public const char Cadeado = '';
    public const char Etiqueta = '';
    public const char Lista = '';
    public const char Biblioteca = '';
    public const char Carrinho = '';
    public const char Banco = '';
    public const char Bandeira = '';
    public const char Relogio = '';
    public const char Sair = '';
    public const char Exportar = '';
    public const char Desfazer = '';
    public const char Email = '';
    public const char Mensagem = '';
    public const char Pacote = '';
    public const char Pagina = '';
    public const char Caneta = '';
    public const char Estrela = '';
    public const char Escudo = '';
    public const char Dinheiro = '';

    private static readonly Dictionary<(char, int, int), Bitmap> Cache = [];

    private static readonly float FatorDpi = ObterFatorDpi();

    private static float ObterFatorDpi()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96f;
    }

    public static Bitmap? Imagem(char glifo, Color cor, int tamanho = 16)
    {
        if (Tema.FonteIcones is null)
            return null;

        var escala = (int)Math.Round(tamanho * FatorDpi);
        var chave = (glifo, cor.ToArgb(), escala);
        if (Cache.TryGetValue(chave, out var existente))
            return existente;

        var bitmap = new Bitmap(escala, escala);
        using (var g = Graphics.FromImage(bitmap))
        using (var fonte = new Font(Tema.FonteIcones, escala * 0.72f, GraphicsUnit.Pixel))
        using (var pincel = new SolidBrush(cor))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var formato = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(glifo.ToString(), fonte, pincel, new RectangleF(0, 0, escala, escala), formato);
        }
        Cache[chave] = bitmap;
        return bitmap;
    }
}
