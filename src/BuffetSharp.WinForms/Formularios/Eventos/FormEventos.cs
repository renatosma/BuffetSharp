using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

internal sealed class FormEventos : FormListagem<EventoResumo>
{
    private readonly IConsultasEventos _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly EventoService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _espaco = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormEventos(IConsultasEventos consultas, IConsultasLookup lookups, EventoService servico, Navegador navegador)
        : base("Eventos", "Eventos contratados. Abra um evento para gerenciar equipe, convidados, pagamentos e encerramento.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;
        _navegador = navegador;

        _de = FiltroData("De", DateTime.Today.AddMonths(-1));
        _ate = FiltroData("Até", DateTime.Today.AddMonths(6));
        _status.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<StatusEvento>()).ToList();
        Filtro("Status", _status);
        Filtro("Espaço", _espaco);
        foreach (var controle in new Control[] { _de, _ate })
            ((DateTimePicker)controle).ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();
        _espaco.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Abrir", Icones.Editar, () => Selecionado() is { } e ? AoAbrirAsync(e) : Task.CompletedTask, "Abrir o evento", destaque: true);
        Botao("Novo evento", Icones.Adicionar, NovoAsync, "Venda direta, sem orçamento");

        Grade.Texto("Código", e => e.Codigo, 24)
            .Data("Data", e => e.Inicio, "dd/MM/yyyy HH:mm", 34)
            .Texto("Evento", e => e.Titulo, 60)
            .Texto("Cliente", e => e.Cliente, 45)
            .Texto("Tipo", e => e.TipoEvento, 25)
            .Texto("Espaço", e => e.Espaco, 25)
            .Numero("Conv.", e => e.QuantidadeConvidados, peso: 15)
            .Moeda("Valor", e => e.ValorTotal, 24)
            .Moeda("Recebido", e => e.Recebido, 24)
            .Moeda("Saldo", e => e.SaldoAReceber, 24, e => e.SaldoAReceber > 0 && e.Status == StatusEvento.Realizado ? Tema.Perigo : null)
            .Status("Status", e => e.Status, 24)
            .Busca(e => $"{e.Codigo} {e.Titulo} {e.Cliente} {e.TipoEvento} {e.Espaco}");
    }

    protected override async Task CarregarAsync()
    {
        if (_espaco.DataSource is null)
        {
            var espacos = (await _lookups.ListarAsync(TipoLookup.Espacos, somenteAtivos: false)).ToList();
            espacos.Insert(0, new ItemLookup { Id = 0, Nome = "(todos)" });
            _espaco.DataSource = espacos;
        }

        var espacoId = (_espaco.SelectedItem as ItemLookup)?.Id;
        Exibir(await _consultas.ListarAsync(new FiltroEventos
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusEvento>)?.Valor,
            EspacoId = espacoId is 0 ? null : espacoId
        }));
    }

    protected override string TextoRodape(IReadOnlyList<EventoResumo> visiveis)
    {
        var validos = visiveis.Where(e => e.Status != StatusEvento.Cancelado).ToList();
        return $"{visiveis.Count} evento(s)  •  {validos.Sum(e => e.QuantidadeConvidados):N0} convidados  •  " +
               $"contratado: {Textos.Moeda(validos.Sum(e => e.ValorTotal))}  •  recebido: {Textos.Moeda(validos.Sum(e => e.Recebido))}  •  " +
               $"a receber: {Textos.Moeda(validos.Sum(e => e.SaldoAReceber))}";
    }

    protected override Task AoAbrirAsync(EventoResumo item)
    {
        _navegador.Abrir($"evento:{item.Id}", () => _navegador.Criar<FormEventoDetalhe>(item.Id));
        return Task.CompletedTask;
    }

    private async Task NovoAsync()
    {
        if (await DialogosEvento.EditarAsync(this, _servico, _lookups, 0) is { } id)
        {
            await CarregarAsync();
            _navegador.Abrir($"evento:{id}", () => _navegador.Criar<FormEventoDetalhe>(id));
        }
    }
}
