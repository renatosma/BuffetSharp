using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Dimensionamento da equipe e das bebidas no banco real.</summary>
[Collection(ColecaoBanco.Nome)]
public class DimensionamentoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Casamento de 40 convidados, sem equipe, numa data livre bem adiante.</summary>
    private async Task<(int Id, DateTime Inicio)> CriarCasamentoAsync()
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var espaco = (await lookups.ListarAsync(TipoLookup.Espacos, ct: Ct)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
        var data = Hoje.AddYears(4).AddDays(Random.Shared.Next(3, 300));
        var id = await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = $"Dimensionamento {Guid.NewGuid():N}"[..30],
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).Single(t => t.Nome == "Casamento").Id,
            EspacoId = espaco.Id,
            Data = data,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(11, 0),
            Convidados = 40,
            ValorTotal = 6000m
        }, Ct);
        return (id, data.ToDateTime(new TimeOnly(8, 0)));
    }

    [Fact]
    public async Task Dimensiona_pela_regra_do_tipo_e_completa_com_quem_esta_livre()
    {
        var servico = banco.Obter<DimensionamentoService>();
        var (id, inicio) = await CriarCasamentoAsync();

        var antes = await servico.EquipeAsync(id, Ct);
        Assert.True(antes.TemRegras);
        var garcons = antes.Equipe.Single(l => l.Cargo == "Garçom");
        Assert.Equal((4, 0, "do tipo: 1 a cada 12 convidados, mínimo 4"), (garcons.Necessarios, garcons.Escalados, garcons.Regra));
        Assert.Equal(1, antes.Equipe.Single(l => l.Cargo == "Barman").Necessarios); // só no casamento
        Assert.Equal(1, antes.Equipe.Single(l => l.Cargo == "Maître").Necessarios); // regra geral
        Assert.Equal(antes.Equipe.Sum(l => l.Necessarios), antes.Faltam);
        Assert.Equal(antes.Faltam, (await servico.FaltasAsync(inicio.Date, inicio.Date.AddDays(1), Ct))[id]);

        var resultado = await servico.CompletarAsync(id, Ct);

        Assert.NotEmpty(resultado.Escalados);
        var depois = await servico.EquipeAsync(id, Ct);
        Assert.Equal(antes.Faltam - resultado.Escalados.Count, depois.Faltam);
        Assert.Equal(depois.Faltam, resultado.Faltando.Sum(f => int.Parse(f.Split(' ')[0])));
        Assert.All(depois.Equipe, l => Assert.Equal(l.Escalados, l.Nomes.Count));
    }

    [Fact]
    public async Task Lembra_do_evento_proximo_com_equipe_incompleta()
    {
        var (id, inicio) = await CriarCasamentoAsync();
        var relogio = new FakeTimeProvider(new DateTimeOffset(inicio.AddDays(-2), TimeSpan.Zero));
        var servico = new DimensionamentoService(banco.Obter<IBuffetDbContextFactory>(), banco.Obter<EventoService>(), banco.Obter<EscalaService>(),
            new LembretesOptions { DiasEquipe = 7 }, relogio);

        var lembrete = Assert.Single(await servico.LembretesAsync(Ct), l => l.EventoId == id);
        Assert.Equal(TipoLembrete.EquipeIncompleta, lembrete.Tipo);
        Assert.Contains("faltam", lembrete.Titulo);
        Assert.Contains("Garçom", lembrete.Detalhe);

        Assert.Empty(await new DimensionamentoService(banco.Obter<IBuffetDbContextFactory>(), banco.Obter<EventoService>(),
            banco.Obter<EscalaService>(), new LembretesOptions { DiasEquipe = 0 }, relogio).LembretesAsync(Ct)); // desligado
    }

    [Fact]
    public async Task Plano_traz_as_bebidas_pela_duracao_e_a_equipe()
    {
        int eventoId;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            eventoId = await db.Eventos.Where(e => e.RestricoesAvulsas.Any()).Select(e => e.Id).FirstAsync(Ct);

        var plano = await banco.Obter<DimensionamentoService>().PlanoAsync(eventoId, Ct);

        var refrigerante = plano.Bebidas.Single(b => b.Produto == "Refrigerante 2L");
        Assert.True(refrigerante.PorHora);
        Assert.Equal(Math.Round(0.07m * plano.Contexto.Pessoas * plano.Contexto.Horas, 3), refrigerante.Quantidade);
        Assert.NotNull(refrigerante.Embalagem); // fardo com 6
        Assert.DoesNotContain(plano.Bebidas, b => b.Produto == "Coxinha de frango");
        Assert.True(plano.Equipe.TemRegras);
        Assert.NotEmpty(plano.Equipe.Equipe);
    }
}
