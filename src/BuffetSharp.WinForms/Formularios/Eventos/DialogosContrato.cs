using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>Emissão, assinatura, impressão, envio e cancelamento de contratos.</summary>
internal static class DialogosContrato
{
    private const int ModeloEmBranco = -1;
    private const int ModeloDoSistema = -2;

    /// <summary>
    /// Gera a minuta pelo modelo escolhido (sugerido: o do tipo do evento ou o padrão), permite revisar o texto e emite o
    /// contrato. Também permite começar um contrato em branco. Retorna o Id ou nulo se cancelado.
    /// </summary>
    public static async Task<int?> EmitirAsync(IWin32Window dono, ContratoService servico, IConsultasLookup lookups, int eventoId)
    {
        var modelos = (await lookups.ListarAsync(TipoLookup.ModelosContrato)).ToList();
        if (modelos.Count == 0)
            modelos.Add(new ItemLookup { Id = ModeloDoSistema, Nome = "Modelo padrão do sistema" });
        modelos.Add(new ItemLookup { Id = ModeloEmBranco, Nome = "Contrato em branco (escrever do zero)" });

        int? modeloId = await servico.ModeloSugeridoAsync(eventoId) ?? modelos[0].Id;
        Task<string> GerarAsync(int? id) => servico.GerarMinutaAsync(eventoId, id > 0 ? id : null, id == ModeloEmBranco);

        var texto = await GerarAsync(modeloId);
        var textoGerado = texto;
        int? emitido = null;
        var e = new EditorCampos(1);
        var modelo = e.Lista("Modelo de contrato", modelos, () => modeloId, v => modeloId = v, "Modelo");
        var caixa = e.Texto("Texto do contrato", () => texto, v => texto = v, "Texto", 100_000, linhas: 24);

        static string Normalizar(string? valor) => (valor ?? "").ReplaceLineEndings("\n").Trim();
        modelo.SelectionChangeCommitted += async (_, _) =>
        {
            if (modelo.SelectedItem is not ItemLookup item)
                return;
            if (Normalizar(caixa.Text) != Normalizar(textoGerado) &&
                !Mensagens.Confirmar(dono, "O texto foi alterado. Substituir pelo texto do modelo escolhido?", "Trocar modelo"))
                return;
            try
            {
                textoGerado = await GerarAsync(item.Id);
                caixa.Text = textoGerado.ReplaceLineEndings();
            }
            catch (Exception ex)
            {
                Mensagens.Erro(dono, ex);
            }
        };

        using var dialogo = new FormEdicao("Emitir contrato",
            "Escolha o modelo, revise e ajuste o texto se necessário. Após a emissão ele é protegido por um código de integridade e não pode mais ser alterado.",
            e, async () => emitido = await servico.EmitirAsync(eventoId, texto ?? "", modeloId > 0 ? modeloId : null), 860);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? emitido : null;
    }

    /// <summary>Coleta as assinaturas do contratante (obrigatória) e do representante do buffet (opcional).</summary>
    public static bool Assinar(IWin32Window dono, ContratoService servico, Contrato contrato, string? representante)
    {
        var cliente = contrato.Evento?.Cliente;
        var input = new AssinaturaContratoInput
        {
            NomeSignatario = cliente?.Nome ?? "",
            DocumentoSignatario = cliente?.Documento,
            NomeRepresentante = representante
        };
        var concordou = false;
        var painelCliente = new PainelAssinatura { Height = 150, Instrucao = "Assinatura do contratante" };
        var painelEmpresa = new PainelAssinatura { Height = 120, Instrucao = "Assinatura do representante do buffet (opcional)" };

        var e = new EditorCampos();
        e.Informacao("Contrato", () => $"{contrato.Codigo}  •  {contrato.Evento?.Titulo}", span: 2);
        e.Secao("Contratante");
        e.Texto("Nome de quem assina", () => input.NomeSignatario, v => input.NomeSignatario = v ?? "", nameof(input.NomeSignatario), 150);
        e.Documento("CPF / CNPJ", () => input.DocumentoSignatario, v => input.DocumentoSignatario = v, nameof(input.DocumentoSignatario));
        e.Livre(ComBotaoLimpar(painelCliente), "Assinatura");
        e.Marcador("O contratante leu e concorda com todas as cláusulas do contrato", () => concordou, v => concordou = v, span: 2);
        e.Secao("Buffet");
        e.Texto("Representante", () => input.NomeRepresentante, v => input.NomeRepresentante = v, nameof(input.NomeRepresentante), 150, span: 2);
        e.Livre(ComBotaoLimpar(painelEmpresa), "Assinatura (opcional)");

        using var dialogo = new FormEdicao("Coletar assinaturas",
            "Peça ao contratante para assinar na área abaixo (mouse, caneta ou tela sensível ao toque).", e, () =>
            {
                Validador.Garantir(concordou, "Confirme que o contratante leu e concorda com o contrato.");
                input.AssinaturaCliente = painelCliente.ParaPng();
                Validador.Garantir(input.AssinaturaCliente is not null, "Colete a assinatura do contratante.");
                input.AssinaturaEmpresa = painelEmpresa.ParaPng();
                return servico.AssinarAsync(contrato.Id, input);
            }, 720);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    public static async Task<bool> EnviarEmailAsync(IWin32Window dono, ContratoService servico, IEnvioEmail email,
        BuffetOptions empresa, int contratoId)
    {
        var contrato = await servico.ObterAsync(contratoId);
        var mensagem = await servico.PrepararEmailAsync(contratoId);
        return DialogosEnvio.Email(dono, $"Enviar contrato {contrato.Codigo}", mensagem, "contrato",
            () => DialogosEnvio.Pdf($"Contrato {contrato.Codigo}", ImpressaoContrato.GerarPdf(contrato, empresa)),
            m => servico.EnviarEmailAsync(contratoId, m), email.Destino);
    }

    public static async Task ImprimirAsync(IWin32Window dono, ContratoService servico, BuffetOptions empresa, int contratoId) =>
        ImpressaoContrato.Visualizar(dono, await servico.ObterAsync(contratoId), empresa);

    public static async Task<bool> CancelarAsync(IWin32Window dono, ContratoService servico, Contrato contrato)
    {
        var assinado = contrato.Status == StatusContrato.Assinado;
        var motivo = Dialogos.PedirTexto(dono, assinado ? "Distrato do contrato" : "Cancelar contrato", "Motivo",
            subtitulo: assinado
                ? $"{contrato.Codigo} já está assinado: o cancelamento registra o distrato. Um novo contrato poderá ser emitido."
                : $"{contrato.Codigo} será cancelado e um novo contrato poderá ser emitido.");
        if (motivo is null)
            return false;
        await servico.CancelarAsync(contrato.Id, motivo);
        return true;
    }

    private static Control ComBotaoLimpar(PainelAssinatura painel)
    {
        var limpar = new LinkLabel { Text = "Limpar assinatura", AutoSize = true, Dock = DockStyle.Right, LinkColor = Tema.Primaria };
        limpar.LinkClicked += (_, _) => painel.Limpar();
        painel.Dock = DockStyle.Fill;
        var conteiner = new Panel { Height = painel.Height + 22 };
        conteiner.Controls.Add(painel);
        conteiner.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 20, Controls = { limpar } });
        return conteiner;
    }
}
