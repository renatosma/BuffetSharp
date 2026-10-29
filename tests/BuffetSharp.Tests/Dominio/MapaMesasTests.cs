using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Mapa de mesas: mesas na planta, lugares com os acompanhantes, distribuição automática e o mapa do salão.</summary>
public class MapaMesasTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly Data = new(2026, 6, 20);

    private sealed record Planta(int Numero, string? Nome, int Lugares, FormatoMesa Formato, int X, int Y) : IMesaPlanta;

    private static Evento Evento()
    {
        var orcamento = new Orcamento(1, 1, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), validoAte: Data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(Agora);
        return orcamento.Aprovar("Casamento Ana e Bruno", Agora);
    }

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static EventoMesa ComId(EventoMesa mesa, int id)
    {
        typeof(EventoMesa).GetProperty(nameof(EventoMesa.Id))!.SetValue(mesa, id);
        return mesa;
    }

    [Fact]
    public void Mesa_nova_vai_para_o_primeiro_lugar_livre_e_o_numero_nao_se_repete()
    {
        var evento = Evento();
        var primeira = evento.AdicionarMesa(1);
        var segunda = evento.AdicionarMesa(2, "Noivos", 8, FormatoMesa.Retangular);
        Assert.Equal((100, 100), (primeira.X, primeira.Y));
        Assert.Equal((250, 100), (segunda.X, segunda.Y));
        Assert.Equal(("Mesa 2 - Noivos", 3), (segunda.Descricao, evento.ProximoNumeroMesa));

        Assert.Contains("Já existe a mesa 1", Assert.Throws<RegraNegocioException>(() => evento.AdicionarMesa(1)).Message);
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarMesa(3, lugares: PlantaSalao.LugaresMaximo + 1));
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarMesa(0));

        var fora = evento.AdicionarMesa(3, x: 5000, y: -10);
        Assert.Equal((PlantaSalao.Largura, 0), (fora.X, fora.Y));
    }

    [Fact]
    public void Convidado_senta_com_os_acompanhantes_enquanto_houver_lugar()
    {
        var evento = Evento();
        var mesa = evento.AdicionarMesa(1, lugares: 4);
        var ana = evento.AdicionarConvidado("Ana", acompanhantes: 2);
        var bruno = evento.AdicionarConvidado("Bruno", acompanhantes: 1);

        evento.SentarConvidado(ana, mesa);
        Assert.Equal((3, 1), (evento.LugaresOcupados(mesa), evento.LugaresLivres(mesa)));
        var erro = Assert.Throws<RegraNegocioException>(() => evento.SentarConvidado(bruno, mesa));
        Assert.Contains("1 lugar(es) livre(s)", erro.Message);
        ComId(mesa, 7);
        Assert.Contains("já tem 3 pessoa(s)", Assert.Throws<RegraNegocioException>(() => evento.AlterarMesa(7, 1, null, 2, FormatoMesa.Redonda)).Message);

        // Quem avisa que não vai libera o lugar e não pode ser sentado.
        typeof(Convidado).GetProperty(nameof(Convidado.Id))!.SetValue(ana, 11);
        evento.DefinirRsvp(ana.Id, StatusRsvp.Recusado);
        Assert.Null(ana.Mesa);
        Assert.Contains("não vai", Assert.Throws<RegraNegocioException>(() => evento.SentarConvidado(ana, mesa)).Message);
        evento.SentarConvidado(bruno, mesa);
        Assert.Equal([bruno], evento.ConvidadosDaMesa(mesa));
        Assert.Empty(evento.ConvidadosSemMesa());
    }

    [Fact]
    public void Distribuicao_senta_os_grupos_maiores_primeiro_sem_separar_acompanhantes()
    {
        var evento = Evento();
        var mesa1 = evento.AdicionarMesa(1, lugares: 5);
        var mesa2 = evento.AdicionarMesa(2, lugares: 6);
        var ana = evento.AdicionarConvidado("Ana", acompanhantes: 3);
        var bia = evento.AdicionarConvidado("Bia", acompanhantes: 2);
        var caio = evento.AdicionarConvidado("Caio", acompanhantes: 1);
        var davi = evento.AdicionarConvidado("Davi");
        var edu = evento.AdicionarConvidado("Edu", acompanhantes: 5);

        Assert.Equal(3, evento.DistribuirConvidados());
        Assert.Same(mesa2, edu.Mesa);
        Assert.Same(mesa1, ana.Mesa);
        Assert.Same(mesa1, davi.Mesa);
        Assert.Equal([bia, caio], evento.ConvidadosSemMesa().OrderBy(c => c.Nome));
        Assert.Equal(11, evento.PessoasSentadas);
        Assert.Equal(0, evento.DistribuirConvidados());
    }

    [Fact]
    public void Distribuir_sem_mesas_avisa_para_montar_o_mapa() =>
        Assert.Contains("Monte o mapa", Assert.Throws<RegraNegocioException>(() => Evento().DistribuirConvidados()).Message);

    [Fact]
    public void Mapa_do_salao_mantem_as_mesas_de_mesmo_numero_e_libera_quem_nao_cabe()
    {
        var evento = Evento();
        var mesa1 = evento.AdicionarMesa(1);
        var mesa3 = evento.AdicionarMesa(3);
        var ana = evento.AdicionarConvidado("Ana", acompanhantes: 3);
        var bruno = evento.AdicionarConvidado("Bruno", acompanhantes: 1);
        var carla = evento.AdicionarConvidado("Carla");
        evento.SentarConvidado(ana, mesa1);
        evento.SentarConvidado(bruno, mesa1);
        evento.SentarConvidado(carla, mesa3);

        var semMesa = evento.UsarPlanta([
            new Planta(1, "Família", 4, FormatoMesa.Quadrada, 500, 500),
            new Planta(2, null, 8, FormatoMesa.Redonda, 700, 500)
        ]);

        Assert.Equal(2, semMesa);
        Assert.Equal([1, 2], evento.Mesas.Select(m => m.Numero).Order());
        Assert.Equal(("Família", 4, FormatoMesa.Quadrada, 500, 500), (mesa1.Nome, mesa1.Lugares, mesa1.Formato, mesa1.X, mesa1.Y));
        Assert.Same(mesa1, ana.Mesa);
        Assert.Null(bruno.Mesa);
        Assert.Null(carla.Mesa);
        Assert.Throws<RegraNegocioException>(() => evento.UsarPlanta([new Planta(1, null, 8, 0, 0, 0), new Planta(1, null, 8, 0, 0, 0)]));
    }

    [Fact]
    public void Mesa_da_lista_importada_e_achada_pelo_numero_ou_pelo_nome()
    {
        var evento = Evento();
        var cinco = evento.ObterOuCriarMesa("Mesa 5");
        Assert.Equal((5, (string?)null, PlantaSalao.LugaresPadrao), (cinco.Numero, cinco.Nome, cinco.Lugares));
        Assert.Same(cinco, evento.ObterOuCriarMesa(" 5 "));

        var noivos = evento.ObterOuCriarMesa("Noivos");
        Assert.Equal((6, "Noivos"), (noivos.Numero, noivos.Nome));
        Assert.Same(noivos, evento.ObterOuCriarMesa("noivos"));
        Assert.Equal(2, evento.Mesas.Count);
    }

    [Fact]
    public void Remover_a_mesa_deixa_os_convidados_sem_mesa()
    {
        var evento = Evento();
        var mesa = ComId(evento.AdicionarMesa(1), 9);
        var ana = evento.AdicionarConvidado("Ana");
        evento.SentarConvidado(ana, mesa);

        evento.MoverMesa(9, 640, 380);
        Assert.Equal((640, 380), (mesa.X, mesa.Y));
        evento.RemoverMesa(9);
        Assert.Empty(evento.Mesas);
        Assert.Null(ana.Mesa);
    }
}
