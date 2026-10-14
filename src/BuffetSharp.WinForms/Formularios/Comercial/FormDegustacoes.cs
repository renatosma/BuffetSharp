using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Agenda de degustações: agendamento, realização com nota, ausência, cancelamento e conversão em contrato.</summary>
internal sealed class FormDegustacoes : FormListagem<DegustacaoResumo>
{
    private readonly IConsultasComercial _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly DegustacaoService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormDegustacoes(IConsultasComercial consultas, IConsultasLookup lookups, DegustacaoService servico, Navegador navegador)
        : base("Degustações", "Agende a degustação do cardápio para clientes em negociação e registre a avaliação. \"Convertida\" indica que o cliente fechou um orçamento depois.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;
        _navegador = navegador;

        _de = FiltroData("De", DateTime.Today.AddMonths(-3));
        _ate = FiltroData("Até", DateTime.Today.AddMonths(2));
        _status.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<StatusDegustacao>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Agendar", Icones.Adicionar, () => EditarAsync(0), "Nova degustação", destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } d ? EditarAsync(d.Id) : Task.CompletedTask, "Reagendar ou alterar");
        Separador();
        Botao("Realizada", Icones.Estrela, RealizarAsync, "Registra a degustação com a avaliação do cliente");
        Botao("Não compareceu", Icones.Aviso, () => AlterarAsync(d => _servico.RegistrarAusenciaAsync(d.Id)));
        Botao("Cancelar", Icones.Cancelar, CancelarAsync);
        Separador();
        Botao("Novo orçamento", Icones.Documento, NovoOrcamentoAsync, "Abre um orçamento para o cliente da degustação");

        Grade.Data("Data e hora", d => d.DataHora, "ddd dd/MM/yy HH:mm", 36, d => d.Status == StatusDegustacao.Agendada && d.DataHora < DateTime.Now ? Tema.Alerta : null)
            .Texto("Cliente", d => d.Cliente, 45)
            .Texto("Contato", d => d.Contato, 28)
            .Numero("Pessoas", d => d.Pessoas, peso: 14)
            .Texto("Pacote", d => d.Pacote, 25)
            .Texto("Orçamento", d => d.OrcamentoCodigo, 18)
            .Status("Situação", d => d.Status, 22)
            .Texto("Nota", d => d.Avaliacao is { } n ? new string('★', n) + new string('☆', 5 - n) : "", 18, _ => Tema.Destaque)
            .Texto("Convertida", d => d.Status == StatusDegustacao.Realizada ? d.Convertida ? "Sim" : "Não" : "", 15,
                d => d.Convertida ? Tema.Sucesso : Tema.Neutro)
            .Texto("Comentário / observações", d => d.Comentario ?? d.Observacoes, 60)
            .Busca(d => $"{d.Cliente} {d.Pacote} {d.OrcamentoCodigo} {d.Comentario} {d.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarDegustacoesAsync(new FiltroDegustacoes
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusDegustacao>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<DegustacaoResumo> visiveis)
    {
        var realizadas = visiveis.Where(d => d.Status == StatusDegustacao.Realizada).ToList();
        var conversao = realizadas.Count == 0 ? 0 : realizadas.Count(d => d.Convertida) * 100m / realizadas.Count;
        var nota = realizadas.Count == 0 ? 0 : realizadas.Average(d => d.Avaliacao ?? 0);
        return $"{visiveis.Count} degustação(ões)  •  agendadas: {visiveis.Count(d => d.Status == StatusDegustacao.Agendada)}  •  " +
               $"realizadas: {realizadas.Count} (nota média {nota:0.0})  •  conversão em contrato: {conversao:0.#}%  •  " +
               $"faltas: {visiveis.Count(d => d.Status == StatusDegustacao.NaoCompareceu)}";
    }

    protected override Task AoAbrirAsync(DegustacaoResumo item) => EditarAsync(item.Id);

    private async Task EditarAsync(int id)
    {
        var input = id == 0 ? _servico.Novo() : await _servico.ObterAsync(id);
        var clientes = await _lookups.ListarAsync(TipoLookup.Clientes);
        var pacotes = await _lookups.ListarAsync(TipoLookup.Pacotes);
        var orcamentos = (await _consultas.ListarOrcamentosAsync(new FiltroOrcamentos()))
            .Where(o => o.Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado || o.Id == input.OrcamentoId)
            .Select(o => new ItemLookup { Id = o.Id, Nome = $"{o.Codigo} - {o.Cliente} ({o.Inicio:dd/MM/yyyy})", Numero = o.ClienteId })
            .ToList();

        var e = new EditorCampos();
        var cliente = e.Lista("Cliente", clientes, () => input.ClienteId, v => input.ClienteId = v ?? 0, nameof(input.ClienteId), span: 2);
        var orcamento = e.Lista("Orçamento em negociação", orcamentos, () => input.OrcamentoId, v => input.OrcamentoId = v,
            nameof(input.OrcamentoId), permitirVazio: true, span: 2);
        e.Data("Data", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        e.Hora("Horário", () => input.Hora, v => input.Hora = v);
        e.Inteiro("Pessoas", () => input.Pessoas, v => input.Pessoas = v, nameof(input.Pessoas), 1, Degustacao.MaximoPessoas);
        e.Lista("Pacote a degustar", pacotes, () => input.PacoteId, v => input.PacoteId = v, nameof(input.PacoteId), permitirVazio: true);
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 1000, span: 2, linhas: 3,
            dica: "Restrições alimentares, preferências, quem virá...");

        // Ao escolher o orçamento, o cliente dele é selecionado (Numero = Id do cliente).
        orcamento.SelectionChangeCommitted += (_, _) =>
        {
            if (orcamento.SelectedItem is ItemLookup { Id: > 0 } o && clientes.FirstOrDefault(c => c.Id == o.Numero) is { } dono)
                cliente.SelectedItem = dono;
        };

        using var dialogo = new FormEdicao(id == 0 ? "Agendar degustação" : "Editar degustação",
            "Degustações acontecem entre 9h e 20h, para até 10 pessoas.", e, () => _servico.SalvarAsync(input), 640);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private async Task RealizarAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var nota = 5;
        string? comentario = null;
        var e = new EditorCampos(1);
        e.Informacao("Degustação", () => $"{item.Cliente} - {item.DataHora:dd/MM/yyyy HH:mm}");
        e.Inteiro("Avaliação do cliente (1 a 5)", () => nota, v => nota = v, minimo: 1, maximo: 5);
        e.Texto("Comentários do cliente", () => comentario, v => comentario = v, maximo: 1000, linhas: 4,
            dica: "O que gostou, o que mudaria, próximos passos...");
        using var dialogo = new FormEdicao("Degustação realizada", null, e, () => _servico.RealizarAsync(item.Id, nota, comentario), 520);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } item)
            return;
        var motivo = Dialogos.PedirTexto(this, "Cancelar degustação", "Motivo", subtitulo: $"{item.Cliente} - {item.DataHora:dd/MM/yyyy HH:mm}");
        if (motivo is not null)
            await AlterarAsync(d => _servico.CancelarAsync(d.Id, motivo));
    }

    private Task NovoOrcamentoAsync()
    {
        if (Selecionado() is { } item)
        {
            using var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(0, item.ClienteId));
            editor.ShowDialog(this);
        }
        return Task.CompletedTask;
    }

    private async Task AlterarAsync(Func<DegustacaoResumo, Task> acao)
    {
        if (Selecionado() is not { } item)
            return;
        await acao(item);
        await CarregarAsync();
        Grade.Selecionar(d => d.Id == item.Id);
    }
}
