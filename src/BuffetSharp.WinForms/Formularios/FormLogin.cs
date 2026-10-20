using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;
using Microsoft.Extensions.Configuration;

namespace BuffetSharp.WinForms.Formularios;

internal sealed class FormLogin : FormBase
{
    private readonly UsuarioService _usuarios;
    private readonly TextBox _login;
    private readonly TextBox _senha;
    private readonly Label _erro;
    private readonly Button _entrar;

    public FormLogin(UsuarioService usuarios, IConfiguration configuracao)
    {
        _usuarios = usuarios;

        Text = "BuffetSharp - Entrar";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 480);
        ShowInTaskbar = true;
        BackColor = Tema.Superficie;

        // Painel da marca
        var marca = new Panel { Dock = DockStyle.Left, Width = 360, BackColor = Tema.Primaria, Padding = new Padding(36) };
        marca.Paint += (_, e) =>
        {
            using var pincel = new SolidBrush(Color.FromArgb(40, Color.White));
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillEllipse(pincel, marca.Width - 140, marca.Height - 160, 260, 260);
            e.Graphics.FillEllipse(pincel, -60, -50, 180, 180);
        };
        var textosMarca = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        textosMarca.Controls.Add(new Label { Text = "BuffetSharp", Font = new Font("Segoe UI Semibold", 26f), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 80, 0, 0) });
        textosMarca.Controls.Add(new Label { Text = "Gestão completa para buffets", Font = Tema.FonteSubtitulo, ForeColor = Tema.Destaque, AutoSize = true });
        textosMarca.Controls.Add(new Label
        {
            Text = "Orçamentos • Eventos • Agenda\nCardápio • Estoque • Financeiro",
            ForeColor = Color.FromArgb(240, 225, 230),
            AutoSize = true,
            Margin = new Padding(0, 28, 0, 0)
        });
        marca.Controls.Add(textosMarca);

        // Formulário
        var formulario = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(56, 70, 56, 30),
            ColumnCount = 1,
            BackColor = Tema.Superficie
        };
        formulario.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        formulario.Controls.Add(Tema.Titulo("Entrar"));
        formulario.Controls.Add(Tema.Subtitulo("Informe suas credenciais para acessar o sistema."));
        formulario.Controls.Add(new Label { Text = "Usuário", AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 28, 0, 2) });
        _login = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 11f) };
        formulario.Controls.Add(_login);
        formulario.Controls.Add(new Label { Text = "Senha", AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 14, 0, 2) });
        _senha = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 11f) };
        formulario.Controls.Add(_senha);
        _erro = new Label
        {
            AutoSize = true,
            ForeColor = Tema.Perigo,
            Margin = new Padding(0, 10, 0, 0),
            MaximumSize = new Size(LogicalToDeviceUnits(380), 0)
        };
        formulario.Controls.Add(_erro);
        _entrar = Tema.BotaoPrimario("Entrar", Icones.Cadeado);
        _entrar.Dock = DockStyle.Top;
        _entrar.Margin = new Padding(0, 14, 0, 0);
        _entrar.Height = 40;
        _entrar.AutoSize = false;
        _entrar.Click += async (_, _) => await EntrarAsync();
        formulario.Controls.Add(_entrar);

        if (configuracao.GetValue("Inicializacao:CriarDadosDemonstracao", true))
        {
            formulario.Controls.Add(new Label
            {
                Text = "Base de demonstração: admin / Admin@123\n(também: comercial, financeiro, operacional — senha <Perfil>@123)",
                AutoSize = true,
                ForeColor = Tema.TextoSuave,
                Font = Tema.FontePequena,
                MaximumSize = new Size(LogicalToDeviceUnits(380), 0),
                Margin = new Padding(0, 24, 0, 0)
            });
        }

        Controls.Add(formulario);
        Controls.Add(marca);
        AcceptButton = _entrar;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _login.Focus();
    }

    private async Task EntrarAsync()
    {
        _erro.Text = string.Empty;
        _entrar.Enabled = false;
        UseWaitCursor = true;
        try
        {
            var usuario = await _usuarios.AutenticarAsync(_login.Text, _senha.Text);
            if (usuario is null)
            {
                _erro.Text = "Usuário ou senha inválidos.";
                _senha.SelectAll();
                _senha.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
        }
        catch (Domain.Comum.RegraNegocioException ex)
        {
            // Usuário bloqueado por senhas erradas: o aviso fica na própria tela.
            _erro.Text = ex.Message;
            _senha.Clear();
        }
        catch (Exception ex)
        {
            Mensagens.Erro(this, ex);
        }
        finally
        {
            UseWaitCursor = false;
            _entrar.Enabled = true;
        }
    }
}
