using BuffetSharp.Infrastructure.Persistencia.Seed;
using BuffetSharp.WinForms.Infra;
using Microsoft.Extensions.Configuration;

namespace BuffetSharp.WinForms.Formularios;

/// <summary>Tela de abertura: aplica migrations e prepara os dados iniciais.</summary>
internal sealed class FormInicializacao : FormBase
{
    private readonly InicializadorBanco _inicializador;
    private readonly bool _dadosDemonstracao;
    private readonly Label _status;

    public FormInicializacao(InicializadorBanco inicializador, IConfiguration configuracao)
    {
        _inicializador = inicializador;
        _dadosDemonstracao = configuracao.GetValue("Inicializacao:CriarDadosDemonstracao", true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 260);
        BackColor = Tema.Lateral;
        ShowInTaskbar = true;
        Text = "BuffetSharp";

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(36, 36, 36, 28), ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = "BuffetSharp",
            Font = new Font("Segoe UI Semibold", 28f),
            ForeColor = Color.White,
            AutoSize = true
        });
        layout.Controls.Add(new Label
        {
            Text = "Gestão completa para buffets e eventos",
            Font = Tema.FonteSubtitulo,
            ForeColor = Tema.Destaque,
            AutoSize = true,
            Margin = new Padding(3, 0, 3, 30)
        });
        layout.Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Top, Height = 6, MarqueeAnimationSpeed = 25 });
        _status = new Label { Text = "Iniciando...", ForeColor = Tema.LateralTexto, AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        layout.Controls.Add(_status);
        Controls.Add(layout);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var progresso = new Progress<string>(texto => _status.Text = texto);
            await Task.Run(() => _inicializador.InicializarAsync(_dadosDemonstracao, progresso));
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            Hide();
            Mensagens.Aviso(null,
                "Não foi possível conectar ou preparar o banco de dados." + Environment.NewLine + Environment.NewLine +
                ex.GetBaseException().Message + Environment.NewLine + Environment.NewLine +
                "Verifique a connection string \"Buffet\" no arquivo appsettings.json e se o SQL Server (LocalDB) está instalado.",
                "Falha na inicialização");
            DialogResult = DialogResult.Abort;
        }
    }
}
