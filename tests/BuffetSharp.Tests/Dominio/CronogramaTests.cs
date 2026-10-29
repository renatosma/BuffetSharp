using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Cronograma do evento: modelo aplicado, validações, mudança de horário, escala e a etapa em andamento.</summary>
public class CronogramaTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly Data = new(2026, 6, 20);
    private static readonly DateTime Inicio = Data.ToDateTime(new TimeOnly(19, 0));

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    /// <summary>Festa das 19h às 23h.</summary>
    private static Evento Evento()
    {
        var orcamento = new Orcamento(1, 1, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), validoAte: Data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(Agora);
        return orcamento.Aprovar("Casamento Ana e Bruno", Agora);
    }

    private static ModeloCronograma Modelo() => new()
    {
        Nome = "Casamento",
        Itens =
        [
            new ModeloCronogramaItem { MinutosDoInicio = -180, DuracaoMinutos = 120, Atividade = "Montagem", Responsavel = "Operacional", Ordem = 1 },
            new ModeloCronogramaItem { MinutosDoInicio = 0, DuracaoMinutos = 60, Atividade = "Recepção", Local = "Entrada", Ordem = 2 },
            new ModeloCronogramaItem { MinutosDoInicio = 90, DuracaoMinutos = 0, Atividade = "Entrada dos noivos", Responsavel = "Cerimonial", Ordem = 3 },
            new ModeloCronogramaItem { MinutosDoInicio = 120, DuracaoMinutos = 90, Atividade = "Jantar", Ordem = 4 }
        ]
    };

    [Fact]
    public void Modelo_vira_etapas_a_partir_do_inicio_e_nao_se_repete()
    {
        var evento = Evento();
        Assert.Equal(4, evento.AplicarCronograma(Modelo()));

        var etapas = evento.Etapas.OrderBy(e => e.Horario).ToList();
        Assert.Equal(Inicio.AddHours(-3), etapas[0].Horario);
        Assert.Equal(Inicio.AddHours(-1), etapas[0].Fim);
        Assert.Equal(("Recepção", "Entrada", Inicio), (etapas[1].Atividade, etapas[1].Local, etapas[1].Horario));
        Assert.Equal(("Entrada dos noivos", "Cerimonial", Inicio.AddMinutes(90)), (etapas[2].Atividade, etapas[2].Responsavel, etapas[2].Horario));

        Assert.Equal(0, evento.AplicarCronograma(Modelo()));
        Assert.Equal(4, evento.Etapas.Count);
    }

    [Fact]
    public void Etapa_recusa_dados_invalidos_e_responsavel_fora_da_escala()
    {
        var evento = Evento();
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarEtapa(Inicio, 30, " "));
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarEtapa(Inicio, 13 * 60, "Festa"));
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarEtapa(Inicio.AddHours(-13), 30, "Montagem"));
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarEtapa(Inicio.AddHours(19), 30, "Desmontagem"));
        var erro = Assert.Throws<RegraNegocioException>(() => evento.AdicionarEtapa(Inicio, 30, "Recepção", funcionarioId: 9));
        Assert.Contains("escalado", erro.Message);

        var etapa = evento.AdicionarEtapa(Inicio.AddHours(-12), 0, "  Chegada da equipe  ", " ", "Maître");
        Assert.Equal(("Chegada da equipe", null, "Maître"), (etapa.Atividade, etapa.Local, etapa.Responsavel));
    }

    [Fact]
    public void Mudar_data_ou_horario_do_evento_desloca_o_cronograma()
    {
        var evento = Evento();
        evento.AplicarCronograma(Modelo());

        evento.AlterarDados(evento.Titulo, evento.EspacoId, Data.AddDays(7), new TimeOnly(20, 30), new TimeOnly(1, 0), evento.QuantidadesPorFaixa, null);

        var novoInicio = Data.AddDays(7).ToDateTime(new TimeOnly(20, 30));
        Assert.Equal([novoInicio.AddHours(-3), novoInicio, novoInicio.AddMinutes(90), novoInicio.AddMinutes(120)],
            evento.Etapas.OrderBy(e => e.Horario).Select(e => e.Horario));
    }

    [Fact]
    public void Sair_da_escala_mantem_o_nome_do_responsavel_sem_o_vinculo()
    {
        var evento = Evento();
        var funcionario = ComId(new Funcionario { Nome = "Carla Maître", Ativo = true, ValorDiaria = 200 }, 5);
        evento.EscalarFuncionario(funcionario, "Maître");
        var etapa = evento.AdicionarEtapa(Inicio, 30, "Abertura do salão", responsavel: "Carla Maître", funcionarioId: 5);
        Assert.Equal(5, etapa.FuncionarioId);

        evento.RemoverDaEquipe(5);
        Assert.Null(etapa.FuncionarioId);
        Assert.Equal("Carla Maître", etapa.Responsavel);
    }

    [Fact]
    public void Painel_do_dia_mostra_a_etapa_em_andamento_e_a_proxima()
    {
        var evento = Evento();
        evento.AplicarCronograma(Modelo());

        var (atual, proxima) = evento.AndamentoCronograma(Inicio.AddMinutes(30));
        Assert.Equal(("Recepção", "Entrada dos noivos"), (atual?.Atividade, proxima?.Atividade));

        // Intervalo entre a recepção (até 20h) e a entrada dos noivos (20h30).
        (atual, proxima) = evento.AndamentoCronograma(Inicio.AddMinutes(75));
        Assert.Null(atual);
        Assert.Equal("Entrada dos noivos", proxima?.Atividade);

        // Marco sem duração vale até a próxima etapa começar.
        (atual, _) = evento.AndamentoCronograma(Inicio.AddMinutes(100));
        Assert.Equal("Entrada dos noivos", atual?.Atividade);

        (atual, proxima) = evento.AndamentoCronograma(Inicio.AddMinutes(300));
        Assert.Null(atual);
        Assert.Null(proxima);
        Assert.Equal("Montagem", evento.AndamentoCronograma(Inicio.AddHours(-2)).Atual?.Atividade);
    }

    [Fact]
    public void Modelo_precisa_de_etapas_validas()
    {
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new ModeloCronograma { Nome = "Vazio" }));
        var modelo = Modelo();
        modelo.Itens.Add(new ModeloCronogramaItem { MinutosDoInicio = 20 * 60, Atividade = "Muito tarde" });
        Assert.Throws<ValidacaoException>(() => Validador.Validar(modelo));
        Validador.Validar(Modelo());
    }
}
