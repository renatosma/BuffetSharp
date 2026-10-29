using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Conflitos da escala: outro evento (com deslocamento), descanso mínimo, indisponibilidade e a exceção registrada.</summary>
public class EscalaEquipeTests
{
    private static readonly DateTime Dia = new(2026, 6, 20);

    /// <summary>Trabalho no dia 20 das 15h à 0h30 (montagem às 15h, festa das 19h às 23h e desmontagem até 0h30).</summary>
    private static readonly PeriodoTrabalho Casamento = new(1, "Casamento", Dia.AddHours(15), Dia.AddHours(24.5));

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static Evento EventoConfirmado()
    {
        var data = DateOnly.FromDateTime(Dia);
        var agora = Dia.AddMonths(-3);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), validoAte: data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(agora);
        return orcamento.Aprovar("Casamento", agora);
    }

    private static PeriodoTrabalho Outro(DateTime inicio, double horas) => new(2, "Aniversário", inicio, inicio.AddHours(horas));

    private static IReadOnlyList<ConflitoEscala> Verificar(PeriodoTrabalho outro, int descanso = 11,
        params IndisponibilidadeFuncionario[] indisponibilidades) =>
        RegrasEscala.Verificar(Casamento, [outro], indisponibilidades, deslocamentoMinutos: 120, descansoHoras: descanso);

    [Fact]
    public void Eventos_que_se_sobrepoem_contando_o_deslocamento_conflitam()
    {
        var conflito = Assert.Single(Verificar(Outro(Dia.AddHours(10), 4)));
        Assert.Equal((TipoConflitoEscala.Sobreposicao, 2, true), (conflito.Tipo, conflito.EventoId, conflito.AdmiteExcecao));
        Assert.Contains("EVT-00002 - Aniversário (20/06 das 10:00 às 14:00)", conflito.Descricao);

        // Termina às 14h, mas o casamento começa às 15h: falta o deslocamento de 2 horas.
        Assert.Equal(TipoConflitoEscala.Sobreposicao, Verificar(Outro(Dia.AddHours(9), 5), descanso: 0).Single().Tipo);
        Assert.Empty(Verificar(Outro(Dia.AddHours(8), 5), descanso: 0));
    }

    [Fact]
    public void Descanso_minimo_entre_um_trabalho_e_o_proximo()
    {
        // Casamento termina 0h30 do dia 21; o próximo começa 8h: 7h30 de descanso.
        var conflito = Assert.Single(Verificar(Outro(Dia.AddDays(1).AddHours(8), 6)));
        Assert.Equal(TipoConflitoEscala.Descanso, conflito.Tipo);
        Assert.Contains("Só 7h30 de descanso antes de EVT-00002", conflito.Descricao);
        Assert.Contains("mínimo de 11h", conflito.Descricao);

        Assert.Empty(Verificar(Outro(Dia.AddDays(1).AddHours(12), 6)));
        Assert.Empty(Verificar(Outro(Dia.AddDays(1).AddHours(8), 6), descanso: 0));
        Assert.Empty(RegrasEscala.Verificar(Casamento, [Casamento], [], 120, 11));
    }

    [Fact]
    public void Indisponibilidade_no_periodo_nao_admite_excecao()
    {
        var ferias = new IndisponibilidadeFuncionario
        {
            FuncionarioId = 5, Motivo = MotivoIndisponibilidade.Ferias, Inicio = new DateOnly(2026, 6, 21), Fim = new DateOnly(2026, 7, 5)
        };
        var conflito = Assert.Single(RegrasEscala.Verificar(Casamento, [], [ferias], 120, 11));
        Assert.Equal((TipoConflitoEscala.Indisponivel, false, "Férias de 21/06 a 05/07/2026"), (conflito.Tipo, conflito.AdmiteExcecao, conflito.Descricao));

        var folga = new IndisponibilidadeFuncionario { Motivo = MotivoIndisponibilidade.Folga, Inicio = new DateOnly(2026, 6, 19), Fim = new DateOnly(2026, 6, 19) };
        Assert.Empty(RegrasEscala.Verificar(Casamento, [], [folga], 120, 11));
        Assert.Equal("Folga em 19/06/2026", folga.Descricao);
    }

    [Fact]
    public void Indisponibilidade_precisa_de_periodo_valido()
    {
        var invertida = new IndisponibilidadeFuncionario { FuncionarioId = 1, Inicio = new DateOnly(2026, 6, 10), Fim = new DateOnly(2026, 6, 9) };
        Assert.Throws<ValidacaoException>(() => Validador.Validar(invertida));
        var longa = new IndisponibilidadeFuncionario { FuncionarioId = 1, Inicio = new DateOnly(2026, 1, 1), Fim = new DateOnly(2027, 1, 2) };
        Assert.Throws<ValidacaoException>(() => Validador.Validar(longa));
        Validador.Validar(new IndisponibilidadeFuncionario { FuncionarioId = 1, Inicio = new DateOnly(2026, 6, 10), Fim = new DateOnly(2026, 6, 10) });
        Assert.Empty(new IndisponibilidadeFuncionario { FuncionarioId = 1 }.Validate(new ValidationContext(new object())));
    }

    [Fact]
    public void Periodo_de_trabalho_vai_da_montagem_a_desmontagem()
    {
        var evento = EventoConfirmado();
        Assert.Equal((evento.Inicio, evento.Fim), (PeriodoTrabalho.Do(evento).Inicio, PeriodoTrabalho.Do(evento).Fim));

        evento.AdicionarEtapa(evento.Inicio.AddHours(-3), 120, "Montagem");
        evento.AdicionarEtapa(evento.Fim.AddMinutes(30), 90, "Desmontagem");
        var periodo = PeriodoTrabalho.Do(evento);
        Assert.Equal((evento.Inicio.AddHours(-3), evento.Fim.AddHours(2)), (periodo.Inicio, periodo.Fim));
    }

    [Fact]
    public void Excecao_ao_conflito_exige_motivo()
    {
        var evento = EventoConfirmado();
        var funcionario = new Funcionario { Nome = "Carla", Ativo = true, ValorDiaria = 200 };
        var escala = evento.EscalarFuncionario(funcionario, "Maître");
        Assert.Throws<RegraNegocioException>(() => escala.AutorizarConflito(" ", "admin"));
        escala.AutorizarConflito("  Mesmo salão, um evento depois do outro ", "admin");
        Assert.Equal(("Mesmo salão, um evento depois do outro", "admin"), (escala.ExcecaoConflito, escala.ExcecaoAutorizadaPor));
    }
}
