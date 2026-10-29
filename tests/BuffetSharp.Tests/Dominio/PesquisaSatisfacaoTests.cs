using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Pesquisa de satisfação: NPS, notas por área, resposta colada do WhatsApp, detrator e pedido de avaliação.</summary>
public class PesquisaSatisfacaoTests
{
    private static readonly DateTime Agora = new(2026, 7, 1, 10, 0, 0);

    [Fact]
    public void Nps_e_a_diferenca_entre_promotores_e_detratores()
    {
        Assert.Null(PesquisaSatisfacao.CalcularNps([]));
        Assert.Equal(100m, PesquisaSatisfacao.CalcularNps([9, 10]));
        Assert.Equal(-100m, PesquisaSatisfacao.CalcularNps([0, 6]));
        // 3 promotores, 1 neutro e 2 detratores em 6 respostas: (3 - 2) / 6 = 16,7.
        Assert.Equal(16.7m, PesquisaSatisfacao.CalcularNps([10, 9, 9, 8, 6, 3]));
        Assert.Equal([CategoriaNps.Detrator, CategoriaNps.Neutro, CategoriaNps.Neutro, CategoriaNps.Promotor],
            new[] { 6, 7, 8, 9 }.Select(PesquisaSatisfacao.Classificar));
    }

    [Fact]
    public void Resposta_registrada_com_as_notas_validas()
    {
        var pesquisa = new PesquisaSatisfacao(5);
        pesquisa.RegistrarEnvio(Agora);
        pesquisa.RegistrarEnvio(Agora.AddDays(1));
        Assert.Equal(Agora, pesquisa.EnviadaEm);

        Assert.Throws<RegraNegocioException>(() => pesquisa.RegistrarResposta(11, null, null, null, null, null, "ana", Agora));
        Assert.Throws<RegraNegocioException>(() => pesquisa.RegistrarResposta(9, 6, null, null, null, null, "ana", Agora));
        pesquisa.RegistrarResposta(9, 5, 4, null, 5, "  Tudo ótimo  ", "ana", Agora);
        Assert.Equal((9, 5, 4, (int?)null, 5, "Tudo ótimo", "ana", CategoriaNps.Promotor, true),
            (pesquisa.Nps, pesquisa.NotaComida, pesquisa.NotaAtendimento, pesquisa.NotaEspaco, pesquisa.NotaOrganizacao, pesquisa.Comentario,
                pesquisa.RegistradaPor, pesquisa.Categoria, pesquisa.Respondida));

        pesquisa.RegistrarPedidoAvaliacao(Agora);
        Assert.Equal(Agora, pesquisa.AvaliacaoGooglePedidaEm);
        Assert.Throws<RegraNegocioException>(() => pesquisa.TratarDetrator("Ligamos", Agora));
    }

    [Fact]
    public void Detrator_recebe_tratativa_e_nao_o_pedido_de_avaliacao()
    {
        var pesquisa = new PesquisaSatisfacao(5);
        pesquisa.RegistrarResposta(4, 2, 2, 3, 2, "Atrasou", null, Agora);
        Assert.Throws<RegraNegocioException>(() => pesquisa.RegistrarPedidoAvaliacao(Agora));
        Assert.Throws<RegraNegocioException>(() => pesquisa.TratarDetrator(" ", Agora));
        pesquisa.TratarDetrator("Ligamos e oferecemos cortesia", Agora);
        Assert.Equal(("Ligamos e oferecemos cortesia", Agora), (pesquisa.TratativaDetrator, pesquisa.DetratorTratadoEm));
    }

    [Theory]
    [InlineData("10, 5, 5, 4, 5 adoramos a festa", 10, 5, 5, 4, 5, "adoramos a festa")]
    [InlineData("9\n4\n5\n5\n3", 9, 4, 5, 5, 3, null)]
    [InlineData("Nota 8. Comida 5, atendimento 4", 8, 5, 4, null, null, null)]
    [InlineData("10 - Perfeito!", 10, null, null, null, null, "Perfeito!")]
    [InlineData("12, 7, 5", null, null, 5, null, null, null)]
    public void Resposta_colada_do_whatsapp_e_lida(string texto, int? nps, int? comida, int? atendimento, int? espaco, int? organizacao, string? comentario)
    {
        var resposta = RespostaPesquisa.Interpretar(texto);
        Assert.Equal((nps, comida, atendimento, espaco, organizacao), (resposta.Nps, resposta.Comida, resposta.Atendimento, resposta.Espaco, resposta.Organizacao));
        if (comentario is not null)
            Assert.Equal(comentario, resposta.Comentario);
        Assert.Null(RespostaPesquisa.Interpretar("  ").Nps);
    }
}
