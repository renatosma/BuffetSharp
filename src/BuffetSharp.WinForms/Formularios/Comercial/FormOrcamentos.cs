using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Funil comercial: orçamentos com ciclo Rascunho → Enviado → Aprovado (gera evento) / Recusado.</summary>
internal sealed class FormOrcamentos : FormListagem<OrcamentoResumo>
{
    private readonly IConsultasComercial _consultas;
    private readonly OrcamentoService _servico;
    private readonly IConsultasLookup _lookups;
    private readonly IConsultasEstoqueCardapio _cardapio;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormOrcamentos(IConsultasComercial consultas, OrcamentoService servico, IConsultasLookup lookups,
        IConsultasEstoqueCardapio cardapio, BuffetOptions opcoes, Navegador navegador)
        : base("Orçamentos", "Propostas comerciais. Ao aprovar, o evento é criado automaticamente e a data do espaço é reservada.")
    {
        _consultas = consultas;
        _servico = servico;
        _lookups = lookups;
        _cardapio = cardapio;
        _opcoes = opcoes;
        _navegador = navegador;

        _de = FiltroData("Evento de", null);
        _ate = FiltroData("até", null);
        _status.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<StatusOrcamento>()).ToList();
        Filtro("Status", _status);
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Novo", Icones.Adicionar, () => AbrirEditorAsync(new AberturaOrcamento()), "Novo orçamento", destaque: true);
        Botao("Abrir", Icones.Editar, () => Selecionado() is { } o ? AoAbrirAsync(o) : Task.CompletedTask);
        Botao("Duplicar", Icones.Copiar, DuplicarAsync, "Cria uma cópia em rascunho");
        Separador();
        Botao("Enviar", Icones.Enviar, () => AlterarAsync(o => _servico.EnviarAsync(o.Id)), "Marca como enviado ao cliente");
        Botao("Aprovar", Icones.Aceitar, AprovarAsync, "Aprova e gera o evento");
        Botao("Recusar", Icones.Cancelar, RecusarAsync);
        Botao("Reabrir", Icones.Desfazer, () => AlterarAsync(o => _servico.ReabrirAsync(o.Id)), "Volta para rascunho");
        Separador();
        Botao("Imprimir", Icones.Imprimir, ImprimirAsync, "Proposta comercial");
        Botao("Excluir", Icones.Excluir, ExcluirAsync);

        Grade.Texto("Código", o => o.Codigo, 20)
            .Texto("Cliente", o => o.Cliente, 55)
            .Texto("Tipo", o => o.TipoEvento, 30)
            .Data("Data do evento", o => o.Inicio, "dd/MM/yyyy HH:mm", 30)
            .Numero("Conv.", o => o.QuantidadeConvidados, peso: 14)
            .Texto("Pacote", o => o.Pacote, 25)
            .Texto("Espaço", o => o.Espaco, 25)
            .Moeda("Valor total", o => o.ValorTotal, 25)
            .Data("Validade", o => o.ValidoAte, peso: 20, cor: o => o.Status == StatusOrcamento.Enviado && o.ValidoAte < DateTime.Today ? Tema.Perigo : null)
            .Status("Status", o => o.Status, 18)
            .Texto("Evento", o => o.EventoId is { } id ? $"EVT-{id:D5}" : "", 24)
            .Busca(o => $"{o.Codigo} {o.Cliente} {o.TipoEvento} {o.Pacote} {o.Espaco}");
    }

    protected override async Task CarregarAsync()
    {
        var filtro = new FiltroOrcamentos
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusOrcamento>)?.Valor
        };
        Exibir(await _consultas.ListarOrcamentosAsync(filtro));
    }

    protected override string TextoRodape(IReadOnlyList<OrcamentoResumo> visiveis)
    {
        var abertos = visiveis.Where(o => o.Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado).ToList();
        var decididos = visiveis.Count(o => o.Status is StatusOrcamento.Aprovado or StatusOrcamento.Recusado);
        var conversao = decididos == 0 ? 0 : visiveis.Count(o => o.Status == StatusOrcamento.Aprovado) * 100m / decididos;
        return $"{visiveis.Count} orçamento(s)  •  em negociação: {abertos.Count} ({Textos.Moeda(abertos.Sum(o => o.ValorTotal))})" +
               $"  •  aprovados: {Textos.Moeda(visiveis.Where(o => o.Status == StatusOrcamento.Aprovado).Sum(o => o.ValorTotal))}" +
               $"  •  conversão: {conversao:0.#}%";
    }

    protected override Task AoAbrirAsync(OrcamentoResumo item) => AbrirEditorAsync(new AberturaOrcamento(item.Id));

    private async Task AbrirEditorAsync(AberturaOrcamento abertura)
    {
        using var editor = _navegador.Criar<FormOrcamentoEdicao>(abertura);
        editor.ShowDialog(this);
        await CarregarAsync();
    }

    private async Task AlterarAsync(Func<OrcamentoResumo, Task> acao)
    {
        if (Selecionado() is not { } item)
            return;
        await acao(item);
        await CarregarAsync();
        Grade.Selecionar(o => o.Id == item.Id);
    }

    private async Task DuplicarAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var novoId = await _servico.DuplicarAsync(item.Id);
        await CarregarAsync();
        Grade.Selecionar(o => o.Id == novoId);
        Mensagens.Info(this, $"Criado o orçamento ORC-{novoId:D5} (rascunho) a partir de {item.Codigo}.");
    }

    private async Task AprovarAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var titulo = Dialogos.PedirTexto(this, "Aprovar orçamento", "Título do evento", $"{item.TipoEvento} - {item.Cliente}",
            linhas: 1, subtitulo: $"{item.Codigo} • {Textos.Moeda(item.ValorTotal)}. A data do espaço será reservada.");
        if (titulo is null)
            return;

        var eventoId = await _servico.AprovarAsync(item.Id, titulo);
        await CarregarAsync();
        if (Mensagens.Confirmar(this, $"Evento EVT-{eventoId:D5} criado com sucesso. Deseja abri-lo para gerar o plano de pagamento e escalar a equipe?",
                "Orçamento aprovado"))
            _navegador.Abrir($"evento:{eventoId}", () => _navegador.Criar<FormEventoDetalhe>(eventoId));
    }

    private async Task RecusarAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var motivo = Dialogos.PedirTexto(this, "Recusar orçamento", "Motivo da recusa", subtitulo: "O motivo ajuda a analisar perdas comerciais.");
        if (motivo is null)
            return;
        await _servico.RecusarAsync(item.Id, motivo);
        await CarregarAsync();
    }

    private async Task ExcluirAsync()
    {
        if (Selecionado() is not { } item)
            return;
        if (!Mensagens.Confirmar(this, $"Excluir o orçamento {item.Codigo}?", "Excluir", perigoso: true))
            return;
        await _servico.ExcluirAsync(item.Id);
        await CarregarAsync();
    }

    private async Task ImprimirAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var orcamento = await _servico.ObterAsync(item.Id);
        await ImpressaoProposta.VisualizarAsync(this, orcamento, _lookups, _cardapio, _opcoes);
    }
}
