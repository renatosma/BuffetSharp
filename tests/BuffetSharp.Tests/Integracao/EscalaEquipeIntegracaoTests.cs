using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Conflito de escala no banco: outro evento, exceção do administrador, indisponibilidade, disponibilidade e agenda.</summary>
[Collection(ColecaoBanco.Nome)]
public class EscalaEquipeIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> CriarEventoAsync(DateOnly data, TimeOnly inicio, TimeOnly fim)
    {
        // Salão próprio a cada evento: a agenda do espaço não interfere no teste da equipe.
        var lookups = banco.Obter<IConsultasLookup>();
        var espaco = await banco.Obter<ICadastroService<Espaco>>().SalvarAsync(new Espaco
        {
            Nome = $"Salão escala {Guid.NewGuid():N}"[..30], CapacidadeMaxima = 100, ValorLocacao = 1000
        }, Ct);
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = "Escala de teste",
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espaco.Id,
            Data = data,
            HoraInicio = inicio,
            HoraFim = fim,
            Convidados = 40,
            ValorTotal = 5000m
        }, Ct);
    }

    [Fact]
    public async Task Funcionario_em_dois_eventos_ao_mesmo_tempo_so_com_excecao_e_indisponivel_nunca()
    {
        var eventos = banco.Obter<EventoService>();
        var escalas = banco.Obter<EscalaService>();
        var cargoId = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Cargos, ct: Ct)).First().Id;
        var funcionario = await banco.Obter<ICadastroService<Funcionario>>().SalvarAsync(new Funcionario
        {
            Nome = $"Garçom de teste {Guid.NewGuid():N}"[..28], CargoId = cargoId, ValorDiaria = 180, Ativo = true
        }, Ct);
        var dia = DateOnly.FromDateTime(DateTime.Today).AddYears(5).AddDays(Random.Shared.Next(1, 300));

        var casamento = await CriarEventoAsync(dia, new TimeOnly(19, 0), new TimeOnly(23, 0));
        var almoco = await CriarEventoAsync(dia, new TimeOnly(12, 0), new TimeOnly(15, 0));
        var ferias = await CriarEventoAsync(dia.AddDays(5), new TimeOnly(19, 0), new TimeOnly(23, 0));
        await eventos.EscalarFuncionarioAsync(casamento, funcionario.Id, "Garçom", null, Ct);

        // O almoço vai até 20h30 com a desmontagem, e o casamento monta a partir das 15h.
        var conflito = await Assert.ThrowsAsync<ConflitoEscalaException>(() => eventos.EscalarFuncionarioAsync(almoco, funcionario.Id, null, null, Ct));
        Assert.Equal(TipoConflitoEscala.Sobreposicao, conflito.Conflitos.Single().Tipo);
        Assert.Contains($"EVT-{casamento:D5}", conflito.Message);
        var situacao = (await escalas.DisponibilidadeAsync(almoco, Ct)).Single(s => s.FuncionarioId == funcionario.Id);
        Assert.StartsWith("ocupado", situacao.Resumo);

        await eventos.EscalarComExcecaoAsync(almoco, funcionario.Id, null, null, "Mesmo prédio, sai antes da desmontagem", Ct);
        var escala = (await eventos.ObterDetalheAsync(almoco, Ct)).Evento.Equipe.Single();
        Assert.Equal(("Mesmo prédio, sai antes da desmontagem", "admin"), (escala.ExcecaoConflito, escala.ExcecaoAutorizadaPor));
        Assert.Contains(funcionario.Id, (await escalas.ConflitosDaEquipeAsync(casamento, Ct)).Keys);

        // Férias: nem o administrador escala.
        await banco.Obter<ICadastroService<IndisponibilidadeFuncionario>>().SalvarAsync(new IndisponibilidadeFuncionario
        {
            FuncionarioId = funcionario.Id, Motivo = MotivoIndisponibilidade.Ferias, Inicio = dia.AddDays(4), Fim = dia.AddDays(8)
        }, Ct);
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            eventos.EscalarComExcecaoAsync(ferias, funcionario.Id, null, null, "Precisamos dele", Ct));
        Assert.Contains("indisponível: Férias", erro.Message);
        Assert.StartsWith("indisponível", (await escalas.DisponibilidadeAsync(ferias, Ct)).Single(s => s.FuncionarioId == funcionario.Id).Resumo);

        var agenda = await escalas.AgendaAsync(dia, dia.AddDays(8), funcionario.Id, Ct);
        Assert.Equal(3, agenda.Count);
        Assert.All(agenda.Where(i => !i.Indisponibilidade), i => Assert.NotNull(i.Conflito));
        Assert.Equal("Mesmo prédio, sai antes da desmontagem", agenda.Single(i => i.EventoId == almoco).Excecao);
        Assert.Single(agenda, i => i.Indisponibilidade);
    }
}
