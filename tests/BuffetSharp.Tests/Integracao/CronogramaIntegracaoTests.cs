using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Cronograma do evento gravado no banco: modelo padrão no evento novo, etapas, escala e mudança de horário.</summary>
[Collection(ColecaoBanco.Nome)]
public class CronogramaIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<int> CriarEventoAsync()
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var espaco = (await lookups.ListarAsync(TipoLookup.Espacos, ct: Ct)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = $"Cronograma de teste {Guid.NewGuid():N}"[..30],
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espaco.Id,
            Data = Hoje.AddYears(3).AddDays(Random.Shared.Next(1, 300)),
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(11, 0),
            Convidados = 40,
            ValorTotal = 6000m
        }, Ct);
    }

    [Fact]
    public async Task Evento_novo_vem_com_o_cronograma_padrao_e_a_etapa_leva_o_nome_do_escalado()
    {
        var eventos = banco.Obter<EventoService>();
        var id = await CriarEventoAsync();
        var evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;

        Assert.Equal(11, evento.Etapas.Count);
        Assert.Contains(evento.Etapas, t => t.Atividade == "Recepção dos convidados" && t.Horario == evento.Inicio);
        Assert.Equal(evento.Inicio.AddHours(-4), evento.Etapas.Min(t => t.Horario));
        Assert.Equal(0, await eventos.AplicarCronogramaAsync(id, ct: Ct));

        var funcionario = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Funcionarios, ct: Ct)).First();
        await eventos.EscalarFuncionarioAsync(id, funcionario.Id, "Maître", null, Ct);
        await eventos.SalvarEtapaAsync(id, new EtapaInput
        {
            Horario = evento.Inicio.AddMinutes(45), DuracaoMinutos = 0, Atividade = "Abertura da pista", Local = "Pista",
            FuncionarioId = funcionario.Id, Observacao = "Avisar o DJ"
        }, Ct);
        evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;
        var etapa = evento.Etapas.Single(t => t.Atividade == "Abertura da pista");
        Assert.Equal((funcionario.Id, funcionario.Nome, "Pista", "Avisar o DJ"), (etapa.FuncionarioId, etapa.Responsavel, etapa.Local, etapa.Observacao));

        await eventos.SalvarEtapaAsync(id, new EtapaInput
        {
            Id = etapa.Id, Horario = etapa.Horario.AddMinutes(15), DuracaoMinutos = 10, Atividade = "Abertura da pista", Responsavel = "DJ"
        }, Ct);
        evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;
        etapa = evento.Etapas.Single(t => t.Id == etapa.Id);
        Assert.Equal((evento.Inicio.AddMinutes(60), 10, (int?)null, "DJ"), (etapa.Horario, etapa.DuracaoMinutos, etapa.FuncionarioId, etapa.Responsavel));

        await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.SalvarEtapaAsync(id,
            new EtapaInput { Horario = evento.Inicio.AddDays(2), Atividade = "Fora do evento" }, Ct));
        await eventos.RemoverEtapaAsync(id, etapa.Id, Ct);
        Assert.Equal(11, (await eventos.ObterDetalheAsync(id, Ct)).Evento.Etapas.Count);
    }

    [Fact]
    public async Task Mudar_o_horario_do_evento_leva_o_cronograma_junto()
    {
        var eventos = banco.Obter<EventoService>();
        var id = await CriarEventoAsync();
        var antes = (await eventos.ObterDetalheAsync(id, Ct)).Evento.Etapas.ToDictionary(t => t.Id, t => t.Horario);

        var input = await eventos.ObterParaEdicaoAsync(id, Ct);
        input.HoraInicio = new TimeOnly(9, 30);
        input.HoraFim = new TimeOnly(12, 0);
        await eventos.SalvarAsync(input, Ct);

        var depois = (await eventos.ObterDetalheAsync(id, Ct)).Evento.Etapas;
        Assert.All(depois, t => Assert.Equal(antes[t.Id].AddMinutes(90), t.Horario));
    }
}
