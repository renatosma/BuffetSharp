using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Lista de compras, ordem de produção, checklist do evento e pagamento da equipe.</summary>
[Collection(ColecaoBanco.Nome)]
public class OperacaoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<int> CriarEventoAsync(DateOnly data)
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var espaco = (await lookups.ListarAsync(TipoLookup.Espacos, ct: Ct)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = $"Checklist de teste {Guid.NewGuid():N}"[..30],
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espaco.Id,
            Data = data,
            HoraInicio = new TimeOnly(7, 0),
            HoraFim = new TimeOnly(8, 0),
            Convidados = 40,
            ValorTotal = 8_000m
        }, Ct);
    }

    [Fact]
    public async Task Lista_de_compras_desconta_o_estoque_da_necessidade_dos_eventos()
    {
        var lista = await banco.Obter<PlanejamentoService>().ListaComprasAsync(Hoje, Hoje.AddDays(60), reporMinimo: false, Ct);

        Assert.NotEmpty(lista.Eventos);
        Assert.NotEmpty(lista.Itens);
        Assert.All(lista.Itens, i =>
        {
            Assert.True(i.AComprar > 0);
            Assert.Equal(Math.Round(i.Necessidade - Math.Max(i.Estoque, 0), 3), i.AComprar);
            Assert.NotEmpty(i.Eventos);
        });

        var comMinimo = await banco.Obter<PlanejamentoService>().ListaComprasAsync(Hoje, Hoje.AddDays(60), reporMinimo: true, Ct);
        Assert.True(comMinimo.Itens.Sum(i => i.AComprar) >= lista.Itens.Sum(i => i.AComprar));
    }

    [Fact]
    public async Task Ordem_de_producao_traz_os_pratos_do_pacote_com_os_insumos_multiplicados()
    {
        var proximo = (await banco.Obter<IConsultasEventos>().ListarAsync(new FiltroEventos
        {
            De = DateTime.Today, Ate = DateTime.Today.AddMonths(3), Status = StatusEvento.Confirmado
        }, Ct)).First();

        var ordens = await banco.Obter<PlanejamentoService>().OrdensDoDiaAsync(DateOnly.FromDateTime(proximo.Inicio), Ct);
        var ordem = Assert.Single(ordens, o => o.EventoId == proximo.Id);

        Assert.NotEmpty(ordem.Pratos);
        Assert.All(ordem.Pratos, p => Assert.Equal(proximo.QuantidadeConvidados, p.Porcoes));
        Assert.Contains(ordem.Pratos, p => p.Ingredientes.Any(i => i.Quantidade > 0));
    }

    [Fact]
    public async Task Evento_novo_recebe_o_checklist_padrao_e_tarefas_atrasadas_viram_lembretes()
    {
        var eventoId = await CriarEventoAsync(Hoje.AddYears(6).AddDays(11));
        var eventos = banco.Obter<EventoService>();
        var evento = (await eventos.ObterDetalheAsync(eventoId, Ct)).Evento;
        Assert.True(evento.Tarefas.Count >= 10);

        var tarefa = evento.Tarefas.OrderBy(t => t.Prazo).First();
        await eventos.ConcluirTarefaAsync(eventoId, tarefa.Id, Ct);
        Assert.True((await eventos.ObterDetalheAsync(eventoId, Ct)).Evento.Tarefas.Single(t => t.Id == tarefa.Id).Concluida);
        Assert.Equal(0, await eventos.AplicarChecklistAsync(eventoId, ct: Ct)); // não repete as tarefas

        // A base de demonstração deixa algumas tarefas de eventos confirmados atrasadas.
        var lembretes = await banco.Obter<LembreteService>().ListarAsync(Ct);
        var atrasada = lembretes.First(l => l.Tipo == TipoLembrete.TarefaEvento);
        await banco.Obter<LembreteService>().MarcarTratadoAsync(atrasada, CanalLembrete.Manual, Ct);
        var dono = (await eventos.ObterDetalheAsync(atrasada.EventoId!.Value, Ct)).Evento;
        Assert.True(dono.Tarefas.Single(t => t.Id == atrasada.ReferenciaId).Concluida);
    }

    [Fact]
    public async Task Diarias_do_periodo_viram_contas_a_pagar_e_o_fechamento_pode_ser_desfeito()
    {
        var consultas = banco.Obter<IConsultasEquipe>();
        var servico = banco.Obter<EquipeService>();
        var inicioMes = new DateOnly(Hoje.Year, Hoje.Month, 1);
        DateOnly de = inicioMes.AddMonths(-1), ate = inicioMes.AddDays(-1);

        // O mês passado ainda não foi pago na base de demonstração.
        var apurado = (await consultas.ApurarAsync(de, ate, Ct)).First(d => d.ValorEmAberto > 0);
        var fechamentoId = await servico.FecharAsync(apurado.FuncionarioId, de, ate, Hoje.AddDays(3), Ct);

        var depois = (await consultas.ApurarAsync(de, ate, Ct)).Single(d => d.FuncionarioId == apurado.FuncionarioId);
        Assert.Equal(0, depois.ValorEmAberto);
        var fechamento = (await consultas.ListarFechamentosAsync(Ct)).Single(f => f.Id == fechamentoId);
        Assert.Equal(apurado.ValorEmAberto, fechamento.Valor);
        Assert.Equal(nameof(StatusTitulo.Aberto), fechamento.SituacaoConta);
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.FecharAsync(apurado.FuncionarioId, de, ate, ct: Ct));

        await servico.DesfazerAsync(fechamentoId, Ct);
        Assert.Equal(apurado.ValorEmAberto,
            (await consultas.ApurarAsync(de, ate, Ct)).Single(d => d.FuncionarioId == apurado.FuncionarioId).ValorEmAberto);
    }
}
