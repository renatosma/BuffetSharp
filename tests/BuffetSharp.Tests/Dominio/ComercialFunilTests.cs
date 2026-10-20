using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Dominio;

public class ComercialFunilTests
{
    private static readonly DateTime Agora = new(2026, 10, 20, 10, 0, 0);
    private static DateOnly Hoje => DateOnly.FromDateTime(Agora);

    [Fact]
    public void Lead_precisa_de_telefone_ou_email()
    {
        Assert.Throws<RegraNegocioException>(() => new Lead("Maria", null, null, OrigemLead.Instagram));
        Assert.Throws<RegraNegocioException>(() => new Lead("Maria", null, "email-invalido", OrigemLead.Instagram));
        Assert.Equal(StatusLead.Novo, new Lead("Maria", "(11) 98888-7777", null, OrigemLead.Instagram).Status);
    }

    [Fact]
    public void Contato_registrado_poe_o_lead_em_contato_com_retorno_agendado()
    {
        var lead = new Lead("Maria", "(11) 98888-7777", null, OrigemLead.Indicacao);

        lead.RegistrarContato("Enviei o cardápio.", "comercial", Agora, Hoje.AddDays(2));

        Assert.Equal(StatusLead.EmContato, lead.Status);
        Assert.Equal(Hoje.AddDays(2), lead.ProximoContato);
        Assert.Single(lead.Interacoes);
        Assert.Throws<RegraNegocioException>(() => lead.RegistrarContato("Outro", null, Agora, Hoje.AddDays(-1)));
    }

    [Fact]
    public void Lead_convertido_ou_perdido_sai_da_negociacao()
    {
        var convertido = new Lead("Maria", "(11) 98888-7777", null, OrigemLead.Site);
        convertido.Converter(new Cliente { Nome = "Maria" }, "comercial", Agora);
        Assert.Equal(StatusLead.Convertido, convertido.Status);
        Assert.Throws<RegraNegocioException>(() => convertido.MarcarPerdido("Preço", null, Agora));

        var perdido = new Lead("João", null, "joao@exemplo.com.br", OrigemLead.Google);
        Assert.Throws<RegraNegocioException>(() => perdido.MarcarPerdido(" ", null, Agora));
        perdido.MarcarPerdido("Fechou com outro buffet.", null, Agora);
        Assert.False(perdido.EmAberto);

        perdido.Reabrir(null, Agora);
        Assert.Equal(StatusLead.EmContato, perdido.Status);
        Assert.Null(perdido.MotivoPerda);
    }

    [Theory]
    [InlineData(152.37, 10, ArredondamentoPreco.Centavos, 167.61)]
    [InlineData(152.37, 10, ArredondamentoPreco.Reais, 168)]
    [InlineData(152.37, 10, ArredondamentoPreco.CincoReais, 170)]
    [InlineData(100, -12.5, ArredondamentoPreco.Centavos, 87.5)]
    public void Reajuste_calcula_e_arredonda_o_novo_preco(decimal valor, decimal percentual, ArredondamentoPreco arredondamento, decimal esperado)
    {
        Assert.Equal(esperado, ReajustePreco.Calcular(valor, percentual, arredondamento));
    }

    [Fact]
    public void Reajuste_valida_percentual_e_tabelas()
    {
        Assert.Throws<RegraNegocioException>(() =>
            new ReajustePreco("Tabela", 0, Hoje, AlcanceReajuste.Pacotes, ArredondamentoPreco.Centavos, null));
        Assert.Throws<RegraNegocioException>(() =>
            new ReajustePreco("Tabela", 5, Hoje, AlcanceReajuste.Nenhum, ArredondamentoPreco.Centavos, null));

        var agendado = new ReajustePreco("Tabela", 5, Hoje.AddDays(10), AlcanceReajuste.Pacotes | AlcanceReajuste.Espacos,
            ArredondamentoPreco.Centavos, null);
        Assert.False(agendado.PodeAplicar(Hoje));
        Assert.True(agendado.PodeAplicar(Hoje.AddDays(10)));
        Assert.True(agendado.Alcanca(TipoItemReajuste.Espaco));
        Assert.False(agendado.Alcanca(TipoItemReajuste.ServicoAdicional));
        Assert.Throws<RegraNegocioException>(() => agendado.RegistrarDesfazer(Agora));
    }
}
