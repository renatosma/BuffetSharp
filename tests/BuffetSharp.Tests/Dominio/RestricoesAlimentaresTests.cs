using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

public class RestricoesAlimentaresTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static int _proximoId = 100;

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static Produto Produto(string nome, ConteudoAlimentar contem) => ComId(new Produto { Nome = nome, Contem = contem }, _proximoId++);

    /// <summary>Prato com a ficha técnica (0,1 de cada insumo por pessoa) e os produtos carregados, como o EF entrega.</summary>
    private static ItemCardapio Prato(string nome, params Produto[] insumos)
    {
        var prato = ComId(new ItemCardapio { Nome = nome }, _proximoId++);
        prato.DefinirFichaTecnica(insumos.Select(p => (p.Id, 0.1m)));
        foreach (var ficha in prato.Ingredientes)
            typeof(FichaTecnicaItem).GetProperty(nameof(FichaTecnicaItem.Produto))!.SetValue(ficha, insumos.Single(p => p.Id == ficha.ProdutoId));
        return prato;
    }

    private static readonly Produto File = Produto("Filé", ConteudoAlimentar.Carne);
    private static readonly Produto Creme = Produto("Creme de leite", ConteudoAlimentar.Lactose);
    private static readonly Produto Massa = Produto("Massa", ConteudoAlimentar.Gluten);
    private static readonly Produto Arroz = Produto("Arroz", ConteudoAlimentar.Nenhum);
    private static readonly Produto Batata = Produto("Batata", ConteudoAlimentar.Nenhum);

    [Fact]
    public void Cada_restricao_proibe_o_que_contem()
    {
        Assert.True(RestricoesAlimentares.Serve(ConteudoAlimentar.Lactose | ConteudoAlimentar.Ovo, RestricaoAlimentar.Vegetariano));
        Assert.False(RestricoesAlimentares.Serve(ConteudoAlimentar.Lactose, RestricaoAlimentar.Vegano));
        Assert.False(RestricoesAlimentares.Serve(ConteudoAlimentar.FrutosDoMar, RestricaoAlimentar.Vegetariano));
        Assert.False(RestricoesAlimentares.Serve(ConteudoAlimentar.Gluten, RestricaoAlimentar.Vegetariano | RestricaoAlimentar.SemGluten));
        Assert.True(RestricoesAlimentares.Serve(ConteudoAlimentar.Carne, RestricaoAlimentar.SemLactose));
        Assert.Equal("VGN+SG", RestricoesAlimentares.Sigla(RestricaoAlimentar.Vegano | RestricaoAlimentar.SemGluten));
        Assert.Equal("Vegano + Sem glúten", RestricoesAlimentares.Nome(RestricaoAlimentar.Vegano | RestricaoAlimentar.SemGluten));
        Assert.Equal("vegano • sem lactose • contém: glúten", RestricoesAlimentares.Selos(ConteudoAlimentar.Gluten));
        Assert.Equal("vegetariano • sem glúten • contém: lactose", RestricoesAlimentares.Selos(ConteudoAlimentar.Lactose));
        Assert.Equal("contém: glúten, lactose, peixe/frutos do mar",
            RestricoesAlimentares.Selos(ConteudoAlimentar.Lactose | ConteudoAlimentar.FrutosDoMar | ConteudoAlimentar.Gluten));
    }

    [Theory]
    [InlineData("Vegano; sem lactose", RestricaoAlimentar.Vegano | RestricaoAlimentar.SemLactose, null)]
    [InlineData("celíaco, alergia a kiwi", RestricaoAlimentar.SemGluten, "alergia a kiwi")]
    [InlineData("vegetariana / alergia a camarão", RestricaoAlimentar.Vegetariano | RestricaoAlimentar.SemFrutosDoMar, null)]
    [InlineData("intolerante a leite + castanhas", RestricaoAlimentar.SemLactose | RestricaoAlimentar.SemOleaginosas, null)]
    [InlineData("  ", RestricaoAlimentar.Nenhuma, null)]
    public void Le_restricoes_escritas_a_mao(string texto, RestricaoAlimentar esperadas, string? observacao)
    {
        Assert.Equal((esperadas, observacao), RestricoesAlimentares.Ler(texto));
    }

    [Fact]
    public void Prato_herda_dos_insumos_e_soma_o_informado()
    {
        var prato = Prato("Risoto", Arroz, Creme);
        Assert.Equal(ConteudoAlimentar.Lactose, prato.Contem);
        Assert.True(prato.Serve(RestricaoAlimentar.Vegetariano | RestricaoAlimentar.SemGluten));
        Assert.False(prato.Serve(RestricaoAlimentar.Vegano));

        prato.ContemInformado = ConteudoAlimentar.Gluten; // o caldo leva trigo e não está na ficha
        Assert.False(prato.Serve(RestricaoAlimentar.SemGluten));
        Assert.Throws<RegraNegocioException>(() => prato.DefinirSubstituto(prato));
    }

    /// <summary>Filé (→ risoto → legumes), massa (→ legumes) e salada, para 100 pessoas.</summary>
    private static (ItemCardapio File, ItemCardapio Risoto, ItemCardapio Legumes, ItemCardapio Massa, ItemCardapio Salada) Cardapio()
    {
        var legumes = Prato("Legumes grelhados", Batata);
        var risoto = Prato("Risoto", Arroz, Creme);
        risoto.DefinirSubstituto(legumes);
        var file = Prato("Filé ao molho", File, Creme);
        file.DefinirSubstituto(risoto);
        var massa = Prato("Massa ao sugo", Massa);
        massa.DefinirSubstituto(legumes);
        return (file, risoto, legumes, massa, Prato("Salada", Batata));
    }

    [Fact]
    public void Quem_tem_restricao_recebe_o_substituto_que_serve()
    {
        var (file, risoto, legumes, massa, salada) = Cardapio();

        var plano = PlanoCardapio.Calcular([file, massa, salada], 100,
        [
            new GrupoRestricao(RestricaoAlimentar.Vegetariano, 3),
            new GrupoRestricao(RestricaoAlimentar.Vegano | RestricaoAlimentar.SemGluten, 1),
            new GrupoRestricao(RestricaoAlimentar.SemGluten, 2),
            new GrupoRestricao(RestricaoAlimentar.Vegetariano, 1) // mesma restrição: soma no grupo
        ]);

        Assert.Equal(95, plano.Porcoes[file]); // 4 vegetarianos e 1 vegano
        Assert.Equal(4, plano.Porcoes[risoto]);
        Assert.Equal(97, plano.Porcoes[massa]); // 1 vegano e 2 sem glúten
        Assert.Equal(4, plano.Porcoes[legumes]); // o vegano no lugar do filé (o risoto tem lactose) e da massa, e os 2 sem glúten
        Assert.Equal(100, plano.Porcoes[salada]);
        Assert.Contains(plano.Trocas, t => t.Grupo.Restricoes == (RestricaoAlimentar.Vegano | RestricaoAlimentar.SemGluten) &&
                                           t.Prato == file && t.Substituto == legumes);
        Assert.Equal(4, plano.Trocas.Count);

        var consumo = plano.Consumo();
        Assert.Equal(9.5m, consumo[File.Id]);
        Assert.Equal(9.9m, consumo[Creme.Id]); // 95 do filé + 4 do risoto
        Assert.Equal(10.4m, consumo[Batata.Id]); // 100 da salada + 4 dos legumes
    }

    [Fact]
    public void Sem_substituto_que_sirva_a_porcao_sai_e_a_cozinha_e_avisada()
    {
        var (file, _, _, massa, _) = Cardapio();
        massa.DefinirSubstituto(null);

        var plano = PlanoCardapio.Calcular([file, massa], 10, [new GrupoRestricao(RestricaoAlimentar.SemGluten, 30)]); // mais que o evento

        Assert.Equal(0, plano.Porcoes[massa]);
        Assert.Equal(10, plano.Porcoes[file]);
        var troca = Assert.Single(plano.Trocas);
        Assert.Equal((massa, (ItemCardapio?)null, 10), (troca.Prato, troca.Substituto, troca.Grupo.Pessoas));
    }

    private static Evento Evento(int convidados = 100)
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(convidados), data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 1 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(Agora);
        return orcamento.Aprovar("Casamento", Agora);
    }

    [Fact]
    public void Evento_soma_os_convidados_com_restricao_e_as_avulsas()
    {
        var evento = Evento();
        var ana = evento.AdicionarConvidado("Ana");
        var bruno = evento.AdicionarConvidado("Bruno");
        var carla = evento.AdicionarConvidado("Carla");
        evento.DefinirRestricoesConvidado(ana, RestricaoAlimentar.Vegetariano, null);
        evento.DefinirRestricoesConvidado(bruno, RestricaoAlimentar.Nenhuma, "  alergia a kiwi ");
        evento.DefinirRestricoesConvidado(carla, RestricaoAlimentar.Vegetariano, null);
        typeof(Convidado).GetProperty(nameof(Convidado.Id))!.SetValue(carla, 3);
        evento.DefinirRsvp(3, StatusRsvp.Recusado); // quem não vai não conta
        evento.DefinirRestricoesAvulsas([(RestricaoAlimentar.Vegetariano, 4, "mesa da faculdade"), (RestricaoAlimentar.SemGluten, 2, null)]);

        Assert.Equal([new GrupoRestricao(RestricaoAlimentar.Vegetariano, 5), new GrupoRestricao(RestricaoAlimentar.SemGluten, 2)],
            evento.GruposRestricao());
        Assert.Equal(("alergia a kiwi", "alergia a kiwi"), (bruno.ObservacaoRestricao, bruno.DescricaoRestricao));
        Assert.Equal("Vegetariano", ana.DescricaoRestricao);

        Assert.Throws<RegraNegocioException>(() => evento.DefinirRestricoesAvulsas([(RestricaoAlimentar.Vegano, 101, null)]));
        Assert.Throws<RegraNegocioException>(() => evento.DefinirRestricoesAvulsas([(RestricaoAlimentar.Nenhuma, 1, null)]));
        Assert.Throws<RegraNegocioException>(() => evento.DefinirRestricoesAvulsas([(RestricaoAlimentar.Vegano, 0, null)]));
    }
}
