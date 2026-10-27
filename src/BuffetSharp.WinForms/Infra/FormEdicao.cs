using BuffetSharp.Domain.Comum;

namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Diálogo genérico de edição: cabeçalho, campos (EditorCampos) com rolagem e barra Salvar/Cancelar.
/// </summary>
internal sealed class FormEdicao : FormBase
{
    private readonly EditorCampos _editor;
    private readonly Func<Task> _salvar;
    private readonly Button _botaoSalvar;
    private readonly FlowLayoutPanel _acoes;

    public FormEdicao(string titulo, string? subtitulo, EditorCampos editor, Func<Task> salvar, int largura = 680,
        bool somenteLeitura = false)
    {
        _editor = editor;
        _salvar = salvar;

        Text = titulo;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var cabecalho = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(20, 16, 20, 8),
            BackColor = Tema.Superficie
        };
        cabecalho.Controls.Add(Tema.Titulo(titulo));
        if (subtitulo is not null)
            cabecalho.Controls.Add(Tema.Subtitulo(subtitulo));

        var corpo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 8, 8, 8), BackColor = Tema.Superficie };
        corpo.Controls.Add(editor.Painel);

        _acoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 10, 16, 10),
            BackColor = Tema.Fundo
        };
        var cancelar = Tema.BotaoSecundario(somenteLeitura ? "Fechar" : "Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        _botaoSalvar = Tema.BotaoPrimario("Salvar", Icones.Salvar);
        _botaoSalvar.Click += async (_, _) => await SalvarAsync();
        _botaoSalvar.Visible = !somenteLeitura;
        _acoes.Controls.Add(cancelar);
        _acoes.Controls.Add(_botaoSalvar);

        Controls.Add(corpo);
        Controls.Add(cabecalho);
        Controls.Add(_acoes);

        AcceptButton = somenteLeitura ? null : _botaoSalvar;
        CancelButton = cancelar;

        // Altura ajustada ao conteúdo, limitada a 85% da tela.
        var larguraPx = LogicalToDeviceUnits(largura);
        var conteudo = editor.Painel.GetPreferredSize(new Size(larguraPx - 40, 0)).Height;
        var alturaMaxima = (int)((Screen.FromPoint(Cursor.Position).WorkingArea.Height) * 0.85);
        ClientSize = new Size(larguraPx,
            Math.Min(alturaMaxima, conteudo + cabecalho.GetPreferredSize(Size.Empty).Height + _acoes.GetPreferredSize(Size.Empty).Height + 40));

        if (somenteLeitura)
            editor.DefinirSomenteLeitura(true);
    }

    /// <summary>Troca o texto do botão de confirmar (ex.: "Emitir nota").</summary>
    public void TextoSalvar(string texto) => _botaoSalvar.Text = texto;

    /// <summary>Botões extras na barra inferior (ex.: "Imprimir").</summary>
    public Button AdicionarAcao(Button botao)
    {
        _acoes.Controls.Add(botao);
        return botao;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _editor.Carregar();
    }

    private async Task SalvarAsync()
    {
        _editor.LimparErros();
        try
        {
            _botaoSalvar.Enabled = false;
            UseWaitCursor = true;
            _editor.Aplicar();
            await _salvar();
            DialogResult = DialogResult.OK;
        }
        catch (ValidacaoException ex)
        {
            if (!_editor.MostrarErros(ex) || ex.Erros.Count > 1)
                Mensagens.Erro(this, ex);
        }
        catch (Exception ex)
        {
            Mensagens.Erro(this, ex);
        }
        finally
        {
            UseWaitCursor = false;
            _botaoSalvar.Enabled = true;
        }
    }
}
