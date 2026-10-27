using System.Diagnostics;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Fiscal;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>Configuração fiscal do buffet e emissão da NFS-e do evento.</summary>
internal static class DialogosNfse
{
    /// <summary>Dados do prestador, do serviço e o ambiente. Retorna verdadeiro se salvou.</summary>
    public static async Task<bool> ConfigurarAsync(IWin32Window dono, NfseService servico)
    {
        var c = await servico.ObterConfiguracaoAsync();
        var e = new EditorCampos();
        e.Opcoes("Ambiente de emissão", () => c.Ambiente, v => c.Ambiente = v, nameof(c.Ambiente), span: 2);
        e.Secao("Prestador (o buffet)");
        e.Documento("CNPJ", () => c.Cnpj, v => c.Cnpj = v, nameof(c.Cnpj));
        e.Texto("Inscrição municipal", () => c.InscricaoMunicipal, v => c.InscricaoMunicipal = v, nameof(c.InscricaoMunicipal), 20);
        e.Texto("Código IBGE do município", () => c.CodigoMunicipio, v => c.CodigoMunicipio = v, nameof(c.CodigoMunicipio), 7,
            dica: "7 dígitos (ex.: 3550308 = São Paulo)");
        e.Opcoes("Regime tributário", () => c.Regime, v => c.Regime = v, nameof(c.Regime));
        e.Secao("Serviço e ISS");
        e.Texto("Item da lista de serviços (LC 116)", () => c.ItemListaServico, v => c.ItemListaServico = v ?? "", nameof(c.ItemListaServico), 10,
            dica: "17.11 = organização de festas e bufê");
        e.Texto("Código do serviço na prefeitura", () => c.CodigoTributarioMunicipio, v => c.CodigoTributarioMunicipio = v,
            nameof(c.CodigoTributarioMunicipio), 20, dica: "Só se a prefeitura exigir");
        e.Texto("CNAE", () => c.CodigoCnae, v => c.CodigoCnae = v, nameof(c.CodigoCnae), 10, dica: "Ex.: 5620102");
        e.Decimal("Alíquota do ISS (%)", () => c.AliquotaIss, v => c.AliquotaIss = v, nameof(c.AliquotaIss), 2, 0, 5);
        e.Marcador("ISS retido pelo cliente (tomador)", () => c.IssRetido, v => c.IssRetido = v, nameof(c.IssRetido), span: 2);
        e.Texto("Descrição do serviço na nota", () => c.Discriminacao, v => c.Discriminacao = v ?? "", nameof(c.Discriminacao), 2000, span: 2,
            linhas: 4);
        e.Informacao("Campos da descrição", () => "{{EVENTO}} {{CODIGO}} {{DATA}} {{CONVIDADOS}} {{LOCAL}} {{CONTRATANTE}}", span: 2);

        using var dialogo = new FormEdicao("Configuração fiscal (NFS-e)",
            "Dados que a prefeitura pede na nota de serviço. O token do Focus NFe (homologação e produção) fica no appsettings.Local.json " +
            "desta máquina; no ambiente simulado a nota é autorizada na hora, sem valor fiscal, só para treinar.",
            e, () => servico.SalvarConfiguracaoAsync(c), 720);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>Emite a NFS-e do evento. Retorna a nota emitida (ou nulo se não emitiu).</summary>
    public static async Task<int?> EmitirAsync(IWin32Window dono, NfseService servico, int eventoId)
    {
        var preparo = await servico.PrepararAsync(eventoId);
        if (preparo.NotaAtiva is not null)
        {
            Mensagens.Info(dono, $"{preparo.NotaAtiva}.\n\nA nota pode ser consultada, baixada ou cancelada em Financeiro › Notas fiscais de serviço.",
                "NFS-e do evento");
            return null;
        }
        if (preparo.Pendencias.Count > 0)
        {
            var configurar = preparo.Pendencias.Any(p => p.StartsWith("Configuração fiscal", StringComparison.Ordinal));
            var texto = "Ainda não dá para emitir a nota:\n\n• " + string.Join("\n• ", preparo.Pendencias);
            if (!configurar)
            {
                Mensagens.Aviso(dono, texto, "NFS-e do evento");
                return null;
            }
            if (!Mensagens.Confirmar(dono, texto + "\n\nAbrir a configuração fiscal agora?", "NFS-e do evento")
                || !await ConfigurarAsync(dono, servico))
                return null;
            return await EmitirAsync(dono, servico, eventoId);
        }

        var input = new EmissaoNfseInput { EventoId = eventoId, Valor = preparo.Valor, Discriminacao = preparo.Discriminacao };
        var e = new EditorCampos();
        e.Informacao("Evento", () => preparo.Evento, span: 2);
        e.Informacao("Tomador (cliente)", () => preparo.Tomador);
        e.Informacao("CPF/CNPJ", () => preparo.TomadorDocumento ?? "");
        var valor = e.Decimal("Valor dos serviços", () => input.Valor, v => input.Valor = v, nameof(input.Valor),
            maximo: Math.Max(preparo.Valor, 0.01m));
        var iss = e.Informacao("ISS", () => "");
        e.Texto("Descrição do serviço", () => input.Discriminacao, v => input.Discriminacao = v ?? "", nameof(input.Discriminacao),
            NotaFiscalServico.TamanhoDiscriminacao, span: 2, linhas: 5);
        e.Informacao("Ambiente", () => Textos.Exibir(preparo.Ambiente), span: 2);
        void Iss() => iss.Text = $"{preparo.AliquotaIss:0.##}% = {Textos.Moeda(Math.Round(valor.Value * preparo.AliquotaIss / 100, 2, MidpointRounding.AwayFromZero))}" +
                                 (preparo.IssRetido ? " (retido pelo cliente)" : "");
        valor.ValueChanged += (_, _) => Iss();
        e.AoCarregar(Iss);

        var notaId = 0;
        using var dialogo = new FormEdicao("Emitir NFS-e",
            preparo.Ambiente == AmbienteNfse.Producao
                ? "Produção: a nota vai para a prefeitura e tem valor fiscal. Confira os dados antes de emitir."
                : "A nota sai sem valor fiscal (" + Textos.Exibir(preparo.Ambiente).ToLowerInvariant() + ").",
            e, async () => notaId = await servico.EmitirAsync(input), 700);
        dialogo.TextoSalvar("Emitir nota");
        if (dialogo.ShowDialog(dono) != DialogResult.OK)
            return null;
        await InformarResultadoAsync(dono, servico, notaId);
        return notaId;
    }

    private static async Task InformarResultadoAsync(IWin32Window dono, NfseService servico, int notaId)
    {
        var status = await servico.AtualizarSituacaoAsync(notaId);
        switch (status)
        {
            case StatusNfse.Autorizada:
                Mensagens.Info(dono, "NFS-e autorizada. Ela pode ser aberta, baixada ou cancelada em Financeiro › Notas fiscais de serviço.", "NFS-e");
                break;
            case StatusNfse.Processando:
                Mensagens.Info(dono, "Nota enviada. A prefeitura costuma responder em alguns segundos: use \"Atualizar situação\" em " +
                                     "Financeiro › Notas fiscais de serviço.", "NFS-e");
                break;
            default:
                Mensagens.Aviso(dono, "A prefeitura recusou a nota. Veja o motivo em Financeiro › Notas fiscais de serviço, corrija e emita de novo.",
                    "NFS-e");
                break;
        }
    }

    public static void AbrirNaPrefeitura(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
