using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Fiscal;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Notas fiscais de serviço (NFS-e) emitidas pelos eventos: situação na prefeitura, abrir a nota, baixar o XML e cancelar.
/// A emissão é feita no evento (botão "NFS-e").
/// </summary>
internal sealed class FormNotasServico : FormListagem<NotaServicoResumo>
{
    private readonly IConsultasFiscais _consultas;
    private readonly NfseService _servico;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormNotasServico(IConsultasFiscais consultas, NfseService servico)
        : base("Notas fiscais de serviço (NFS-e)", "Uma nota por evento, emitida pelo botão \"NFS-e\" do evento. A prefeitura responde em alguns segundos: " +
                                                   "use \"Atualizar situação\" nas notas aguardando. Notas recusadas mostram o motivo; corrija e emita de novo.")
    {
        _consultas = consultas;
        _servico = servico;

        _de = FiltroData("Emitidas de", DateTime.Today.AddMonths(-3));
        _ate = FiltroData("até", null);
        _status.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<StatusNfse>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Atualizar situação", Icones.Atualizar, AtualizarSituacaoAsync, "Pergunta à prefeitura a situação das notas aguardando", destaque: true);
        Botao("Abrir nota", Icones.Pagina, AbrirAsync, "Abre a nota no site da prefeitura");
        Botao("Baixar XML", Icones.Exportar, BaixarXmlAsync, "Salva o XML da nota (para a contabilidade)");
        Botao("Cancelar nota", Icones.Cancelar, CancelarAsync, "Cancela a nota na prefeitura (o evento pode receber outra)");
        Separador();
        Botao("Configuração fiscal", Icones.Configuracoes, async () =>
        {
            if (await DialogosNfse.ConfigurarAsync(this, _servico))
                await CarregarAsync();
        }, "CNPJ, inscrição municipal, ISS, descrição e ambiente");

        Grade.Texto("Número", n => n.Numero ?? "-", 12)
            .Data("Emissão", n => n.DataEmissao ?? n.SolicitadaEm, peso: 13)
            .Texto("Evento", n => n.Evento, 34)
            .Data("Data do evento", n => n.DataEvento, peso: 13)
            .Texto("Tomador", n => n.TomadorNome, 30)
            .Moeda("Valor", n => n.ValorServicos, 15)
            .Moeda("ISS", n => n.ValorIss, 12)
            .Status("Situação", n => n.Status, 18)
            .Texto("Ambiente", n => n.Ambiente == AmbienteNfse.Producao ? "Produção" : n.Ambiente == AmbienteNfse.Homologacao ? "Homologação" : "Simulado",
                13, n => n.Ambiente == AmbienteNfse.Producao ? null : Tema.Alerta)
            .Texto("Mensagem", n => n.MotivoCancelamento ?? n.Mensagem, 45, n => n.Status == StatusNfse.Erro ? Tema.Perigo : null)
            .Busca(n => $"{n.Numero} {n.Evento} {n.TomadorNome} {n.TomadorDocumento} {n.CodigoVerificacao}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarNotasServicoAsync(new FiltroNotasServico
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusNfse>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<NotaServicoResumo> visiveis)
    {
        var autorizadas = visiveis.Where(n => n.Status == StatusNfse.Autorizada).ToList();
        var aguardando = visiveis.Count(n => n.Status == StatusNfse.Processando);
        return $"{visiveis.Count} nota(s)  •  autorizadas: {autorizadas.Count} ({Textos.Moeda(autorizadas.Sum(n => n.ValorServicos))}, " +
               $"ISS {Textos.Moeda(autorizadas.Sum(n => n.ValorIss))})" + (aguardando > 0 ? $"  •  aguardando prefeitura: {aguardando}" : "");
    }

    protected override Task AoAbrirAsync(NotaServicoResumo item) => AbrirAsync();

    private async Task AtualizarSituacaoAsync()
    {
        var aguardando = Selecionado() is { Status: StatusNfse.Processando } selecionada
            ? [selecionada]
            : (await _consultas.ListarNotasServicoAsync(new FiltroNotasServico { Status = StatusNfse.Processando })).ToList();
        if (aguardando.Count == 0)
        {
            Mensagens.Info(this, "Nenhuma nota aguardando a prefeitura.");
            return;
        }
        foreach (var nota in aguardando)
            await _servico.AtualizarSituacaoAsync(nota.Id);
        await CarregarAsync();
    }

    private Task AbrirAsync()
    {
        if (Selecionado() is not { } nota)
            return Task.CompletedTask;
        if (nota.Ambiente == AmbienteNfse.Simulado)
            Mensagens.Info(this, $"Nota simulada nº {nota.Numero ?? "-"} (sem valor fiscal): não existe no site da prefeitura. Use \"Baixar XML\" para conferir.");
        else if (string.IsNullOrWhiteSpace(nota.Url))
            Mensagens.Aviso(this, nota.Status == StatusNfse.Processando
                ? "A nota ainda está aguardando a prefeitura. Use \"Atualizar situação\"."
                : "A prefeitura não informou o endereço desta nota.");
        else
            DialogosNfse.AbrirNaPrefeitura(nota.Url);
        return Task.CompletedTask;
    }

    private async Task BaixarXmlAsync()
    {
        if (Selecionado() is not { } nota)
            return;
        var (nome, conteudo) = await _servico.BaixarXmlAsync(nota.Id);
        using var dialogo = new SaveFileDialog { Filter = "XML (*.xml)|*.xml", FileName = nome };
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await File.WriteAllBytesAsync(dialogo.FileName, conteudo);
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } nota)
            return;
        if (nota.Status != StatusNfse.Autorizada)
        {
            Mensagens.Aviso(this, "Só uma nota autorizada pode ser cancelada.");
            return;
        }
        var motivo = Dialogos.PedirTexto(this, "Cancelar NFS-e", "Motivo (mínimo de 15 caracteres)",
            subtitulo: $"NFS-e nº {nota.Numero} - {nota.TomadorNome} ({Textos.Moeda(nota.ValorServicos)}). " +
                       (nota.Ambiente == AmbienteNfse.Producao ? "O cancelamento vai para a prefeitura; algumas só aceitam dentro do prazo." : ""));
        if (motivo is null)
            return;
        await _servico.CancelarAsync(nota.Id, motivo);
        await CarregarAsync();
        Grade.Selecionar(n => n.Id == nota.Id);
    }
}
