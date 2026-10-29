using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>
/// Agenda da equipe: onde cada funcionário trabalha em cada dia do período (da montagem à desmontagem) e as folgas, férias e
/// atestados. Escalas em conflito (outro evento, descanso ou indisponibilidade) aparecem em vermelho.
/// </summary>
internal sealed class FormAgendaEquipe : FormListagem<ItemAgendaEquipe>
{
    private readonly EscalaService _escalas;
    private readonly IConsultasLookup _lookups;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _funcionario = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _somenteConflitos = new() { Text = "Somente conflitos", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    public FormAgendaEquipe(EscalaService escalas, IConsultasLookup lookups, Navegador navegador)
        : base("Agenda da equipe", "Escalas de cada funcionário no período, com montagem e desmontagem, e as folgas, férias e atestados.")
    {
        _escalas = escalas;
        _lookups = lookups;
        _navegador = navegador;

        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje, opcional: false);
        _ate = FiltroData("até", hoje.AddDays(30), opcional: false);
        Filtro("Funcionário", _funcionario);
        Filtros.Controls.Add(_somenteConflitos);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _somenteConflitos.CheckedChanged += async (_, _) => await RecarregarAsync();

        Botao("Abrir evento", Icones.Bandeira, () => Selecionado() is { } item ? AoAbrirAsync(item) : Task.CompletedTask, destaque: true);
        Botao("Indisponibilidades", Icones.Calendario, () =>
        {
            _navegador.Abrir($"cadastro:{nameof(Domain.Cadastros.IndisponibilidadeFuncionario)}",
                () => _navegador.Criar<Cadastros.FormCadastro<Domain.Cadastros.IndisponibilidadeFuncionario>>(Cadastros.DefinicoesCadastros.Indisponibilidades));
            return Task.CompletedTask;
        }, "Folgas, férias e atestados da equipe");

        Grade.Texto("Funcionário", i => i.Funcionario, 30)
            .Texto("Período", i => i.Indisponibilidade
                ? (i.Inicio.Date == i.Fim.Date ? $"{i.Inicio:ddd dd/MM}" : $"{i.Inicio:dd/MM} a {i.Fim:dd/MM}")
                : i.Inicio.Date == i.Fim.Date ? $"{i.Inicio:ddd dd/MM} {i.Inicio:HH:mm}-{i.Fim:HH:mm}" : $"{i.Inicio:dd/MM HH:mm} a {i.Fim:dd/MM HH:mm}", 26)
            .Texto("Evento / motivo", i => i.Descricao, 50)
            .Texto("Função", i => i.Funcao, 20)
            .Texto("Local", i => i.Local, 22)
            .Texto("Conflito", i => i.Conflito, 55, i => i.Excecao is null ? Tema.Perigo : Tema.Alerta)
            .Texto("Exceção / observação", i => i.Excecao, 30)
            .CorDaLinha(i => i.Indisponibilidade ? Tema.Neutro : null)
            .Busca(i => $"{i.Funcionario} {i.Descricao} {i.Funcao} {i.Local} {i.Conflito}");
    }

    protected override async Task CarregarAsync()
    {
        if (_funcionario.DataSource is null)
        {
            var funcionarios = (await _lookups.ListarAsync(TipoLookup.Funcionarios)).ToList();
            funcionarios.Insert(0, new ItemLookup { Id = 0, Nome = "(todos)" });
            _funcionario.DataSource = funcionarios;
            _funcionario.SelectedIndexChanged += async (_, _) => await RecarregarAsync();
        }
        var funcionarioId = _funcionario.SelectedItem is ItemLookup { Id: > 0 } f ? f.Id : (int?)null;
        var itens = await _escalas.AgendaAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value), funcionarioId);
        Exibir(_somenteConflitos.Checked ? itens.Where(i => i.Conflito is not null) : itens);
    }

    protected override string TextoRodape(IReadOnlyList<ItemAgendaEquipe> visiveis)
    {
        var escalas = visiveis.Where(i => !i.Indisponibilidade).ToList();
        return $"{escalas.Count} escala(s) de {escalas.Select(i => i.FuncionarioId).Distinct().Count()} funcionário(s)  •  " +
               $"{escalas.Count(i => i.Conflito is not null)} em conflito  •  {visiveis.Count(i => i.Indisponibilidade)} indisponibilidade(s)";
    }

    protected override Task AoAbrirAsync(ItemAgendaEquipe item)
    {
        if (item.EventoId is { } eventoId)
            _navegador.Abrir($"evento:{eventoId}", () => _navegador.Criar<FormEventoDetalhe>(eventoId));
        return Task.CompletedTask;
    }
}
