using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

public class SobrasTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static Evento Evento(bool realizado = true)
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 1 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200 }, 1));
        orcamento.Enviar(Agora);
        var evento = orcamento.Aprovar("Casamento", Agora);
        if (realizado)
            evento.Realizar(new DateTime(2026, 6, 21, 1, 0, 0));
        return evento;
    }

    [Fact]
    public void Sobra_por_prato_com_destino_e_so_o_descarte_e_desperdicio()
    {
        var evento = Evento();

        evento.RegistrarSobras(
        [
            new SobraInformada(1, "Filé ao molho", 100, 18m, 10, DestinoSobra.Descarte, null),
            new SobraInformada(2, "Arroz", 100, 0.5m, 20, DestinoSobra.Doacao, " Lar São Vicente "),
            new SobraInformada(3, "Mousse", 100, 2.4m, 5, DestinoSobra.RefeicaoEquipe, "ignorado")
        ], "operacional", Agora.AddMonths(4));

        var file = evento.Sobras.Single(s => s.Prato == "Filé ao molho");
        Assert.Equal((180m, 10m, true), (file.Custo, file.PorcoesSobra, file.Desperdicio)); // 10% de 100 porções a R$ 18
        Assert.Equal("Lar São Vicente", evento.Sobras.Single(s => s.Prato == "Arroz").Instituicao);
        Assert.Null(evento.Sobras.Single(s => s.Prato == "Mousse").Instituicao); // só a doação guarda a instituição
        Assert.Equal(180m, evento.CustoDesperdicio); // doação e refeição da equipe não são desperdício
        Assert.Equal("operacional", evento.SobrasRegistradasPor);

        evento.RegistrarSobras([new SobraInformada(1, "Filé ao molho", 100, 18m, 5, DestinoSobra.Descarte, null)], "operacional", Agora.AddMonths(4));
        Assert.Single(evento.Sobras); // registrar de novo substitui
    }

    [Fact]
    public void Sobras_so_depois_do_encerramento_e_doacao_pede_a_instituicao()
    {
        Assert.Throws<RegraNegocioException>(() => Evento(realizado: false).RegistrarSobras([], null, Agora));
        var evento = Evento();
        Assert.Throws<RegraNegocioException>(() => evento.RegistrarSobras([new SobraInformada(1, "Arroz", 100, 1, 10, DestinoSobra.Doacao, " ")], null, Agora));
        Assert.Throws<RegraNegocioException>(() => evento.RegistrarSobras([new SobraInformada(1, "Arroz", 100, 1, 101, DestinoSobra.Descarte, null)], null, Agora));
        evento.RegistrarSobras([new SobraInformada(1, "Arroz", 100, 1, 0, DestinoSobra.Doacao, null)], null, Agora); // sem sobra, sem instituição
    }

    [Theory]
    [InlineData(20, 5, 0.85)]
    [InlineData(16, 5, 0.89)]
    [InlineData(80, 5, 0.5)] // nunca reduz mais da metade
    [InlineData(3, 5, 1)] // abaixo da folga não muda
    public void Fator_de_ajuste_tira_a_sobra_media_e_deixa_a_folga(decimal sobraMedia, decimal folga, decimal esperado)
    {
        Assert.Equal(esperado, AjusteFicha.Fator(sobraMedia, folga));
    }

    [Fact]
    public void Insumo_nao_usado_volta_ao_estoque_pelo_custo_da_baixa_menos_o_perecivel()
    {
        var refrigerante = new Produto { Nome = "Refrigerante", CustoMedio = 8m };
        refrigerante.RegistrarEntrada(10, 8m, Agora, null);
        refrigerante.RegistrarSaida(10, Agora, "Evento", 7);
        refrigerante.RegistrarEntrada(10, 10m, Agora, null); // comprou mais caro depois

        var retorno = refrigerante.RetornarDoEvento(4, 8m, naoDevolvido: 10, Agora, 7, "Não usado no evento EVT-00007");

        Assert.Equal((TipoMovimentacao.RetornoEvento, 4m, 8m, 7), (retorno.Tipo, retorno.Quantidade, retorno.CustoUnitario, retorno.EventoId!.Value));
        Assert.Equal((14m, 9.4286m), (refrigerante.EstoqueAtual, refrigerante.CustoMedio)); // (10 × 10 + 4 × 8) / 14
        Assert.Throws<RegraNegocioException>(() => refrigerante.RetornarDoEvento(7, 8m, naoDevolvido: 6, Agora, 7, "x"));

        var morango = new Produto { Nome = "Morango", ControlaValidade = true };
        Assert.Contains("perecível", Assert.Throws<RegraNegocioException>(() => morango.RetornarDoEvento(1, 20m, 5, Agora, 7, "x")).Message);
    }
}
