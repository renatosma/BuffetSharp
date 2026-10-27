using System.Diagnostics;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>Boleto e PIX registrados (Asaas): configuração, emissão de uma parcela e a cobrança pronta para enviar ao cliente.</summary>
internal static class DialogosCobranca
{
    /// <summary>Ambiente, tipo padrão, multa e juros e a conta onde os pagamentos caem. Retorna verdadeiro se salvou.</summary>
    public static async Task<bool> ConfigurarAsync(IWin32Window dono, Navegador navegador)
    {
        var servico = navegador.Servico<CobrancaService>();
        var c = await servico.ObterConfiguracaoAsync();
        var contas = (await navegador.Servico<IConsultasLookup>().ListarAsync(TipoLookup.ContasFinanceiras)).Where(x => !x.Marcador).ToList();

        var e = new EditorCampos();
        e.Opcoes("Ambiente", () => c.Ambiente, v => c.Ambiente = v, nameof(c.Ambiente), span: 2);
        e.Opcoes("Cobrar por", () => c.TipoPadrao, v => c.TipoPadrao = v, nameof(c.TipoPadrao));
        e.Lista("Conta que recebe os pagamentos", contas, () => c.ContaFinanceiraId, v => c.ContaFinanceiraId = v is > 0 ? v : null,
            nameof(c.ContaFinanceiraId), permitirVazio: true);
        e.Decimal("Multa por atraso (%)", () => c.MultaPercentual, v => c.MultaPercentual = v, nameof(c.MultaPercentual), 2, 0, 2);
        e.Decimal("Juros ao mês (%)", () => c.JurosMensalPercentual, v => c.JurosMensalPercentual = v, nameof(c.JurosMensalPercentual), 2, 0, 10);

        using var dialogo = new FormEdicao("Configuração da cobrança (boleto e PIX)",
            "O pagamento confirmado pelo Asaas baixa a parcela sozinho nesta conta (crie em Caixa e bancos uma conta para o Asaas). " +
            "A chave da API fica no appsettings.Local.json desta máquina; no ambiente simulado o pagamento é simulado por um botão.",
            e, () => servico.SalvarConfiguracaoAsync(c), 680);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>Emite o boleto/PIX da parcela (ou mostra o que já está pendente) e abre a cobrança para enviar ao cliente.</summary>
    public static async Task<bool> EmitirAsync(IWin32Window dono, Navegador navegador, int parcelaId)
    {
        var servico = navegador.Servico<CobrancaService>();
        var preparo = await servico.PrepararAsync(parcelaId);
        if (preparo.CobrancaPendenteId is { } pendente)
        {
            await ExibirAsync(dono, navegador, pendente);
            return false;
        }
        if (preparo.Pendencias.Count > 0)
        {
            var texto = "Ainda não dá para emitir a cobrança:\n\n• " + string.Join("\n• ", preparo.Pendencias);
            if (!preparo.Pendencias.Any(p => p.StartsWith("Configuração da cobrança", StringComparison.Ordinal)))
            {
                Mensagens.Aviso(dono, texto, "Boleto / PIX registrado");
                return false;
            }
            if (!Mensagens.Confirmar(dono, texto + "\n\nAbrir a configuração da cobrança agora?", "Boleto / PIX registrado")
                || !await ConfigurarAsync(dono, navegador))
                return false;
            return await EmitirAsync(dono, navegador, parcelaId);
        }

        var input = new EmissaoCobrancaInput { ParcelaId = parcelaId, Tipo = preparo.TipoPadrao, Vencimento = preparo.VencimentoSugerido };
        var e = new EditorCampos();
        e.Informacao("Parcela", () => preparo.Parcela, span: 2);
        e.Informacao("Cliente", () => $"{preparo.Cliente} ({preparo.ClienteDocumento})", span: 2);
        e.Informacao("Valor", () => Textos.Moeda(preparo.Valor));
        e.Informacao("Multa e juros", () => $"{preparo.MultaPercentual:0.##}% + {preparo.JurosMensalPercentual:0.##}% ao mês");
        e.Opcoes("Cobrar por", () => input.Tipo, v => input.Tipo = v, nameof(input.Tipo));
        e.Data("Vencimento", () => input.Vencimento, v => input.Vencimento = v ?? input.Vencimento, nameof(input.Vencimento));
        e.Informacao("Ambiente", () => Textos.Exibir(preparo.Ambiente), span: 2);

        var cobrancaId = 0;
        using var dialogo = new FormEdicao("Emitir boleto / PIX registrado",
            preparo.VencimentoParcela < preparo.VencimentoSugerido
                ? $"A parcela venceu em {preparo.VencimentoParcela:dd/MM/yyyy}: a cobrança sai com um novo vencimento."
                : "O pagamento é conferido sozinho e baixa a parcela na conta configurada.",
            e, async () => cobrancaId = await servico.EmitirAsync(input), 640);
        dialogo.TextoSalvar("Emitir cobrança");
        if (dialogo.ShowDialog(dono) != DialogResult.OK)
            return false;
        await ExibirAsync(dono, navegador, cobrancaId);
        return true;
    }

    /// <summary>A cobrança pronta: linha digitável, PIX (QR Code e "copia e cola"), boleto e fatura, e envio ao cliente.</summary>
    public static async Task ExibirAsync(IWin32Window dono, Navegador navegador, int cobrancaId)
    {
        var c = await navegador.Servico<CobrancaService>().ObterAsync(cobrancaId);
        var cliente = await navegador.Servico<ICadastroService<Cliente>>().ObterAsync(c.ClienteId);
        var empresa = navegador.Servico<BuffetOptions>().NomeEmpresa;
        var primeiroNome = c.Cliente.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? c.Cliente;
        var texto = MensagemCliente(c, primeiroNome, empresa);
        var qrCode = c.PixCopiaECola is null ? null : navegador.Servico<IGeradorQrCode>().Png(c.PixCopiaECola);

        using var janela = new FormBase
        {
            Text = $"Cobrança - {c.Parcela}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            BackColor = Tema.Superficie
        };
        janela.ClientSize = new Size(janela.LogicalToDeviceUnits(600), janela.LogicalToDeviceUnits(qrCode is null ? 380 : 640));

        var corpo = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16)
        };
        var largura = janela.LogicalToDeviceUnits(560);
        corpo.Controls.Add(new Label
        {
            AutoSize = false,
            Width = largura,
            Height = janela.LogicalToDeviceUnits(52),
            Font = Tema.FonteSubtitulo,
            Text = $"{c.Cliente}\n{Textos.Moeda(c.Valor)}  •  vence em {c.Vencimento:dd/MM/yyyy}  •  {Textos.Exibir(c.Status)}" +
                   (c.Ambiente == AmbienteCobranca.Producao ? "" : $"  •  {(c.Ambiente == AmbienteCobranca.Simulado ? "SIMULADO" : "SANDBOX")}")
        });

        void Campo(string rotulo, string valor)
        {
            corpo.Controls.Add(new Label { Text = rotulo, AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 8, 0, 2) });
            corpo.Controls.Add(new TextBox
            {
                Text = valor,
                ReadOnly = true,
                Multiline = valor.Length > 70,
                Width = largura,
                Height = janela.LogicalToDeviceUnits(valor.Length > 70 ? 58 : 26),
                Font = valor.Length > 70 ? Tema.FontePequena : Tema.Fonte
            });
        }
        if (c.LinhaDigitavel is not null)
            Campo("Linha digitável do boleto", c.LinhaDigitavel);
        if (c.PixCopiaECola is not null)
            Campo("PIX copia e cola", c.PixCopiaECola);
        if (qrCode is not null)
        {
            using var imagem = new MemoryStream(qrCode);
            corpo.Controls.Add(new PictureBox
            {
                Image = Image.FromStream(imagem),
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = largura,
                Height = janela.LogicalToDeviceUnits(230)
            });
        }

        var acoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12, 10, 12, 10), BackColor = Tema.Fundo };
        void Botao(Button botao, Action clique)
        {
            botao.Click += (_, _) => clique();
            acoes.Controls.Add(botao);
        }
        if (c.LinhaDigitavel is not null)
        {
            var copiar = Tema.BotaoPrimario("Copiar linha", Icones.Copiar);
            Botao(copiar, () =>
            {
                Clipboard.SetText(c.LinhaDigitavel);
                copiar.Text = "Copiada!";
            });
        }
        if (c.PixCopiaECola is not null)
        {
            var copiarPix = c.LinhaDigitavel is null ? Tema.BotaoPrimario("Copiar PIX", Icones.Copiar) : Tema.BotaoSecundario("Copiar PIX", Icones.Copiar);
            Botao(copiarPix, () =>
            {
                Clipboard.SetText(c.PixCopiaECola);
                copiarPix.Text = "Copiado!";
            });
        }
        if ((c.UrlBoleto ?? c.UrlFatura) is { } url)
            Botao(Tema.BotaoSecundario(c.UrlBoleto is null ? "Abrir fatura" : "Abrir boleto", Icones.Pagina), () => Abrir(url));
        if (c.Status == StatusCobranca.Pendente)
        {
            Botao(Tema.BotaoSecundario("WhatsApp", Icones.Mensagem), () => DialogosEnvio.WhatsApp(janela,
                new MensagemWhatsApp(cliente?.Celular ?? cliente?.Telefone ?? "", texto), "a cobrança"));
            Botao(Tema.BotaoSecundario("E-mail", Icones.Email), () =>
            {
                var envio = new EmailInput
                {
                    Para = cliente?.Email ?? "",
                    Assunto = $"Cobrança {c.Parcela} | {empresa}",
                    Mensagem = texto.Replace("\n", Environment.NewLine)
                };
                var servicoEmail = navegador.Servico<IEnvioEmail>();
                DialogosEnvio.Email(janela, "Enviar cobrança por e-mail", envio, null, null,
                    e => servicoEmail.EnviarAsync(new MensagemEmail(e.Para.Trim(), e.Assunto.Trim(),
                        "<p>" + System.Net.WebUtility.HtmlEncode(e.Mensagem ?? "").Replace("\r\n", "<br>").Replace("\n", "<br>") + "</p>", [])),
                    servicoEmail.Destino);
            });
        }
        var fechar = Tema.BotaoSecundario("Fechar");
        fechar.DialogResult = DialogResult.Cancel;
        acoes.Controls.Add(fechar);
        janela.CancelButton = fechar;

        janela.Controls.Add(corpo);
        janela.Controls.Add(acoes);
        janela.ShowDialog(dono);
    }

    /// <summary>Texto para o cliente (WhatsApp/e-mail) com o link da fatura, a linha digitável e o PIX.</summary>
    internal static string MensagemCliente(CobrancaDetalhe c, string primeiroNome, string empresa)
    {
        var partes = new List<string>
        {
            $"Olá, {primeiroNome}! Segue a cobrança de {c.Parcela}, no valor de {Textos.Moeda(c.Valor)}, com vencimento em {c.Vencimento:dd/MM/yyyy}."
        };
        if (c.UrlFatura is not null)
            partes.Add($"Boleto e PIX: {c.UrlFatura}");
        if (c.LinhaDigitavel is not null)
            partes.Add($"Linha digitável do boleto:\n{c.LinhaDigitavel}");
        if (c.PixCopiaECola is not null)
            partes.Add($"PIX copia e cola:\n{c.PixCopiaECola}");
        if (c.MultaPercentual > 0 || c.JurosMensalPercentual > 0)
            partes.Add($"Após o vencimento: multa de {c.MultaPercentual:0.##}% e juros de {c.JurosMensalPercentual:0.##}% ao mês.");
        partes.Add(empresa);
        return string.Join("\n\n", partes);
    }

    public static void Abrir(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
