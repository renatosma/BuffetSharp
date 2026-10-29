using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Mapa de mesas gravado no banco: o padrão do espaço no evento novo, convidados sentados, importação e o padrão salvo do evento.</summary>
[Collection(ColecaoBanco.Nome)]
public class MapaMesasIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Espaco> CriarEspacoAsync() =>
        await banco.Obter<ICadastroService<Espaco>>().SalvarAsync(new Espaco
        {
            Nome = $"Salão de teste {Guid.NewGuid():N}"[..30], CapacidadeMaxima = 120, ValorLocacao = 1000
        }, Ct);

    private async Task<int> CriarEventoAsync(int espacoId)
    {
        var lookups = banco.Obter<IConsultasLookup>();
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = "Mapa de mesas de teste",
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espacoId,
            Data = DateOnly.FromDateTime(DateTime.Today).AddYears(3).AddDays(Random.Shared.Next(1, 300)),
            HoraInicio = new TimeOnly(19, 0),
            HoraFim = new TimeOnly(23, 0),
            Convidados = 60,
            ValorTotal = 9000m
        }, Ct);
    }

    [Fact]
    public async Task Evento_novo_copia_o_mapa_do_espaco_e_os_convidados_sentam_nas_mesas()
    {
        var mapas = banco.Obter<MapaMesasService>();
        var eventos = banco.Obter<EventoService>();
        var espaco = await CriarEspacoAsync();
        await mapas.SalvarDoEspacoAsync(espaco.Id, [
            new MesaInput { Numero = 1, Nome = "Noivos", Lugares = 6, Formato = FormatoMesa.Retangular, X = 600, Y = 90 },
            new MesaInput { Numero = 2, Lugares = 4 },
            new MesaInput { Numero = 3, Lugares = 8 }
        ], Ct);
        var planta = await mapas.ObterDoEspacoAsync(espaco.Id, Ct);
        Assert.Equal([1, 2, 3], planta.Select(m => m.Numero));
        Assert.All(planta.Where(m => m.Numero > 1), m => Assert.NotEqual((600, 90), (m.X!.Value, m.Y!.Value)));
        await Assert.ThrowsAsync<RegraNegocioException>(() => mapas.SalvarDoEspacoAsync(espaco.Id,
            [new MesaInput { Numero = 1 }, new MesaInput { Numero = 1 }], Ct));

        var id = await CriarEventoAsync(espaco.Id);
        var evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;
        Assert.Equal(["Mesa 1 - Noivos", "Mesa 2", "Mesa 3"], evento.Mesas.OrderBy(m => m.Numero).Select(m => m.Descricao));
        var noivos = evento.Mesas.Single(m => m.Numero == 1);
        var dois = evento.Mesas.Single(m => m.Numero == 2);

        await eventos.AdicionarConvidadoAsync(id, "Ana Noiva", null, null, 1, noivos.Id, Ct);
        await eventos.ImportarConvidadosAsync(id, "Bruno;;3;2\nCarla;;0;Mesa 7\nDavi;;9;2\nEva", Ct);
        await eventos.DefinirRsvpAsync(id, (await eventos.ObterDetalheAsync(id, Ct)).Evento.Convidados.Single(c => c.Nome == "Eva").Id,
            StatusRsvp.Recusado, Ct);

        evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;
        var porNome = evento.Convidados.ToDictionary(c => c.Nome);
        Assert.Same(evento.Mesas.Single(m => m.Numero == 1), porNome["Ana Noiva"].Mesa);
        Assert.Equal(2, porNome["Bruno"].Mesa?.Numero);
        Assert.Equal((7, PlantaSalao.LugaresPadrao), (porNome["Carla"].Mesa!.Numero, porNome["Carla"].Mesa!.Lugares));
        Assert.Null(porNome["Davi"].Mesa);
        Assert.Equal(4, evento.LugaresOcupados(evento.Mesas.Single(m => m.Numero == 2)));

        // Davi vem com 9: não cabe junto em nenhuma mesa; Eva não vai e fica de fora da distribuição.
        Assert.Equal(0, await eventos.DistribuirConvidadosAsync(id, Ct));
        await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.SentarConvidadoAsync(id, porNome["Davi"].Id, dois.Id, Ct));
        await eventos.SalvarMesaAsync(id, new MesaInput { Id = noivos.Id, Numero = 1, Nome = "Noivos", Lugares = 12, Formato = FormatoMesa.Retangular }, Ct);
        Assert.Equal(1, await eventos.DistribuirConvidadosAsync(id, Ct));

        await eventos.MoverMesaAsync(id, dois.Id, 300, 450, Ct);
        await eventos.RemoverMesaAsync(id, dois.Id, Ct);
        evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;
        Assert.Equal([1, 3, 7], evento.Mesas.Select(m => m.Numero).Order());
        Assert.Null(evento.Convidados.Single(c => c.Nome == "Bruno").Mesa);
        Assert.Equal(1, evento.Convidados.Single(c => c.Nome == "Davi").Mesa?.Numero);

        // O mapa do evento vira o padrão do salão: a mesa 7 entra e a 2 sai.
        Assert.Equal(espaco.Nome, await mapas.SalvarDoEventoComoPadraoAsync(id, Ct));
        planta = await mapas.ObterDoEspacoAsync(espaco.Id, Ct);
        Assert.Equal([1, 3, 7], planta.Select(m => m.Numero));
        Assert.Equal(12, planta[0].Lugares);
    }

    [Fact]
    public async Task Usar_o_mapa_do_espaco_troca_as_mesas_do_evento()
    {
        var mapas = banco.Obter<MapaMesasService>();
        var eventos = banco.Obter<EventoService>();
        var espaco = await CriarEspacoAsync();
        var id = await CriarEventoAsync(espaco.Id);
        await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.UsarMapaDoEspacoAsync(id, Ct));

        await eventos.SalvarMesaAsync(id, new MesaInput { Numero = 5, Lugares = 10 }, Ct);
        await eventos.AdicionarConvidadoAsync(id, "Fábio", null, null, 0,
            (await eventos.ObterDetalheAsync(id, Ct)).Evento.Mesas.Single().Id, Ct);
        await mapas.SalvarDoEspacoAsync(espaco.Id, [new MesaInput { Numero = 1 }, new MesaInput { Numero = 2 }], Ct);

        Assert.Equal(1, await eventos.UsarMapaDoEspacoAsync(id, Ct));
        var evento = (await eventos.ObterDetalheAsync(id, Ct)).Evento;
        Assert.Equal([1, 2], evento.Mesas.Select(m => m.Numero).Order());
        Assert.Null(evento.Convidados.Single().Mesa);
    }
}
