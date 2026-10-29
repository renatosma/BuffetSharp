using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>Notas que o fornecedor recebeu nos eventos, com a média, para escolher quem contratar nas próximas festas.</summary>
internal sealed class FormAvaliacoesFornecedor : FormBase
{
    private readonly EventoService _eventos;
    private readonly Fornecedor _fornecedor;
    private readonly Grade<AvaliacaoFornecedor> _avaliacoes = new();
    private readonly Label _media = Tema.Subtitulo("");

    public FormAvaliacoesFornecedor(EventoService eventos, Fornecedor fornecedor)
    {
        _eventos = eventos;
        _fornecedor = fornecedor;
        Text = $"Avaliações - {fornecedor.NomeExibicao}";
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Px(860), Px(480));

        _avaliacoes.Data("Data", a => a.Data, "dd/MM/yyyy", 14)
            .Texto("Evento", a => $"{a.CodigoEvento} - {a.Evento}", 34)
            .Texto("Serviço", a => a.Servico, 24)
            .Texto("Nota", a => new string('★', a.Nota) + new string('☆', 5 - a.Nota), 12)
            .Texto("Comentário", a => a.Comentario, 50);

        var cabecalho = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            Padding = new Padding(20, 16, 20, 8), BackColor = Tema.Superficie
        };
        cabecalho.Controls.Add(Tema.Titulo(Text));
        cabecalho.Controls.Add(_media);

        var acoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(12, 10, 16, 10),
            BackColor = Tema.Fundo
        };
        var fechar = Tema.BotaoSecundario("Fechar");
        fechar.DialogResult = DialogResult.Cancel;
        acoes.Controls.Add(fechar);
        CancelButton = fechar;

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        corpo.Controls.Add(_avaliacoes.Controle);
        Controls.Add(corpo);
        Controls.Add(cabecalho);
        Controls.Add(acoes);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(async () =>
        {
            var avaliacoes = await _eventos.AvaliacoesDoFornecedorAsync(_fornecedor.Id);
            _avaliacoes.Carregar(avaliacoes);
            _media.Text = avaliacoes.Count == 0
                ? "Ainda sem avaliações. A nota é dada na aba Fornecedores do evento, a partir do início da festa."
                : $"Média {avaliacoes.Average(a => a.Nota):0.0} em {avaliacoes.Count} avaliação(ões)";
        });
    }
}
