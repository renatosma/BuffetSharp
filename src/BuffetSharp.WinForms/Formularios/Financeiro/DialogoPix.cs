using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Cobrança de uma parcela por PIX: QR Code para o cliente ler e o código "copia e cola", com envio pelo WhatsApp ou por
/// e-mail (com o QR Code anexo). O recebimento continua sendo registrado em "Registrar recebimento".
/// </summary>
internal static class DialogoPix
{
    public static async Task ExibirAsync(IWin32Window dono, Navegador navegador, int parcelaId, decimal valor, string descricao,
        int clienteId, string nomeCliente)
    {
        var pix = navegador.Servico<PixService>();
        if (!pix.Configurado)
        {
            Mensagens.Aviso(dono, PixService.MensagemNaoConfigurado, "PIX não configurado");
            return;
        }

        var cobranca = pix.Parcela(parcelaId, valor, descricao);
        var cliente = await navegador.Servico<ICadastroService<Cliente>>().ObterAsync(clienteId);
        var empresa = navegador.Servico<BuffetOptions>().NomeEmpresa;
        var primeiroNome = nomeCliente.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nomeCliente;
        var texto = $"Olá, {primeiroNome}! Segue o PIX da parcela {descricao}, no valor de {Textos.Moeda(valor)}.\n\n" +
                    $"Código \"copia e cola\":\n{cobranca.CopiaECola}\n\n{empresa}";

        using var janela = new FormBase
        {
            Text = $"PIX - {descricao}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            BackColor = Tema.Superficie
        };
        janela.ClientSize = new Size(janela.LogicalToDeviceUnits(560), janela.LogicalToDeviceUnits(600));

        using var imagem = new MemoryStream(cobranca.QrCodePng);
        var qr = new PictureBox
        {
            Image = Image.FromStream(imagem),
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Top,
            Height = janela.LogicalToDeviceUnits(300)
        };
        var resumo = new Label
        {
            Dock = DockStyle.Top,
            Height = janela.LogicalToDeviceUnits(48),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = Tema.FonteSubtitulo,
            Text = $"{nomeCliente}\n{Textos.Moeda(valor)}  •  identificador {cobranca.Identificador}"
        };
        var codigo = new TextBox
        {
            Dock = DockStyle.Top,
            Multiline = true,
            ReadOnly = true,
            Height = janela.LogicalToDeviceUnits(70),
            Text = cobranca.CopiaECola,
            Font = Tema.FontePequena
        };

        var acoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12, 10, 12, 10), BackColor = Tema.Fundo };
        var copiar = Tema.BotaoPrimario("Copiar código", Icones.Copiar);
        copiar.Click += (_, _) =>
        {
            Clipboard.SetText(cobranca.CopiaECola);
            copiar.Text = "Copiado!";
        };
        var whatsApp = Tema.BotaoSecundario("WhatsApp", Icones.Mensagem);
        whatsApp.Click += (_, _) => DialogosEnvio.WhatsApp(janela,
            new MensagemWhatsApp(cliente?.Celular ?? cliente?.Telefone ?? "", texto), "PIX");
        var email = Tema.BotaoSecundario("E-mail", Icones.Email);
        email.Click += (_, _) =>
        {
            var envio = new EmailInput
            {
                Para = cliente?.Email ?? "",
                Assunto = $"PIX da parcela {descricao} | {empresa}",
                Mensagem = texto.Replace("\n", Environment.NewLine)
            };
            var servicoEmail = navegador.Servico<IEnvioEmail>();
            DialogosEnvio.Email(janela, "Enviar PIX por e-mail", envio, "o QR Code", () => new AnexoEmail("pix.png", cobranca.QrCodePng, "image/png"),
                e => servicoEmail.EnviarAsync(new MensagemEmail(e.Para.Trim(), e.Assunto.Trim(),
                    "<p>" + System.Net.WebUtility.HtmlEncode(e.Mensagem ?? "").Replace("\r\n", "<br>").Replace("\n", "<br>") + "</p>",
                    e.Anexo is null ? [] : [e.Anexo])), servicoEmail.Destino);
        };
        var salvar = Tema.BotaoSecundario("Salvar imagem", Icones.Salvar);
        salvar.Click += (_, _) =>
        {
            using var dialogo = new SaveFileDialog { Filter = "Imagem PNG (*.png)|*.png", FileName = $"PIX_{cobranca.Identificador}.png" };
            if (dialogo.ShowDialog(janela) == DialogResult.OK)
                File.WriteAllBytes(dialogo.FileName, cobranca.QrCodePng);
        };
        var fechar = Tema.BotaoSecundario("Fechar");
        fechar.DialogResult = DialogResult.Cancel;
        acoes.Controls.AddRange([copiar, whatsApp, email, salvar, fechar]);
        janela.CancelButton = fechar;

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        corpo.Controls.Add(codigo);
        corpo.Controls.Add(new Label { Text = "PIX copia e cola", Dock = DockStyle.Top, Height = 24, ForeColor = Tema.TextoSuave });
        corpo.Controls.Add(resumo);
        corpo.Controls.Add(qr);
        janela.Controls.Add(corpo);
        janela.Controls.Add(acoes);
        janela.ShowDialog(dono);
    }
}
