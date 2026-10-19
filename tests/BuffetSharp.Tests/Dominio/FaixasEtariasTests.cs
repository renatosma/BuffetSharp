using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Dominio;

public class FaixasEtariasTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly DataEvento = new(2026, 6, 20);

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static Pacote Pacote(decimal preco = 100m, int minimo = 50) =>
        ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = preco, MinimoConvidados = minimo }, 1);

    private static Espaco Espaco() => ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 5000m }, 1);

    private static Orcamento NovoOrcamento(QuantidadesConvidados convidados) =>
        new(clienteId: 1, tipoEventoId: 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), convidados,
            validoAte: DataEvento.AddDays(-30));

    [Fact]
    public void Alimentacao_cobra_inteira_meia_e_nada_dos_isentos()
    {
        var orcamento = NovoOrcamento(new QuantidadesConvidados(100, Meia: 20, Isentos: 10));
        orcamento.DefinirPacote(Pacote(100m));
        orcamento.DefinirEspaco(Espaco());

        // 70 × 100 + 20 × 50 (meia de 50%) + 10 × 0 = 8.000 + 5.000 de locação
        Assert.Equal(8000m, orcamento.Resumo.Alimentacao);
        Assert.Equal(13000m, orcamento.ValorTotal);
        Assert.Equal(70, orcamento.QuantidadesPorFaixa.Inteira);
        Assert.Equal(90, orcamento.QuantidadesPorFaixa.Pagantes);
        Assert.Equal(100, orcamento.QuantidadeConvidados);
    }

    [Fact]
    public void Valor_da_meia_e_arredondado_por_crianca()
    {
        var regra = RegraFaixasEtarias.Padrao with { PercentualMeia = 33m };
        var resumo = CalculadoraValores.Calcular(99.99m, new QuantidadesConvidados(10, Meia: 3), 0, 0, 0, regra);

        Assert.Equal(33.00m, regra.ValorMeia(99.99m));
        Assert.Equal(7 * 99.99m + 3 * 33.00m, resumo.Alimentacao);
    }

    [Fact]
    public void Inteiro_vira_todos_pagando_inteira()
    {
        QuantidadesConvidados convidados = 80;

        Assert.Equal(new QuantidadesConvidados(80, 0, 0), convidados);
        Assert.False(convidados.TemCriancas);
        Assert.Equal(8000m, CalculadoraValores.Calcular(100m, convidados, 0, 0, 0).Alimentacao);
    }

    [Fact]
    public void Minimo_do_pacote_conta_somente_os_pagantes()
    {
        var poucosPagantes = NovoOrcamento(new QuantidadesConvidados(55, Meia: 10, Isentos: 10));
        var erro = Assert.Throws<RegraNegocioException>(() => poucosPagantes.DefinirPacote(Pacote(minimo: 50)));
        Assert.Contains("pagantes", erro.Message);

        // A meia conta como pagante: 55 - 5 isentos = 50.
        var noLimite = NovoOrcamento(new QuantidadesConvidados(55, Meia: 20, Isentos: 5));
        noLimite.DefinirPacote(Pacote(minimo: 50));
        Assert.Equal(1, noLimite.PacoteId);
    }

    [Theory]
    [InlineData(100, 60, 50)] // crianças acima do total
    [InlineData(100, -1, 0)]
    [InlineData(100, 0, -1)]
    [InlineData(10, 0, 10)] // ninguém pagante
    [InlineData(0, 0, 0)]
    public void Quantidades_invalidas_sao_rejeitadas(int total, int meia, int isentos)
    {
        Assert.Throws<RegraNegocioException>(() => NovoOrcamento(new QuantidadesConvidados(total, meia, isentos)));
    }

    [Fact]
    public void Regra_do_pacote_fica_congelada_no_orcamento_e_passa_para_o_evento()
    {
        var pacote = Pacote();
        pacote.IdadeMaximaIsento = 3;
        pacote.IdadeMaximaMeia = 7;
        var orcamento = NovoOrcamento(new QuantidadesConvidados(100, Meia: 20, Isentos: 10));
        orcamento.DefinirPacote(pacote, percentualMeiaNegociado: 40m);
        orcamento.DefinirEspaco(Espaco());

        pacote.PercentualMeia = 70m; // mudança posterior no cadastro não afeta a proposta
        Assert.Equal(new RegraFaixasEtarias(3, 7, 40m), orcamento.RegraFaixas);
        Assert.Equal(7000m + 20 * 40m, orcamento.Resumo.Alimentacao);

        var evento = orcamento.Aprovar("Festa da Clara", Agora);
        Assert.Equal(orcamento.QuantidadesPorFaixa, evento.QuantidadesPorFaixa);
        Assert.Equal(orcamento.RegraFaixas, evento.RegraFaixas);

        var copia = orcamento.Duplicar(DateOnly.FromDateTime(Agora), 15);
        Assert.Equal(orcamento.QuantidadesPorFaixa, copia.QuantidadesPorFaixa);
        Assert.Equal(orcamento.RegraFaixas, copia.RegraFaixas);
    }

    [Fact]
    public void Meia_negociada_fora_dos_limites_e_rejeitada()
    {
        var orcamento = NovoOrcamento(100);
        Assert.Throws<RegraNegocioException>(() => orcamento.DefinirPacote(Pacote(), percentualMeiaNegociado: 120m));
    }

    [Fact]
    public void Pacote_nao_aceita_idade_da_meia_menor_que_a_dos_isentos()
    {
        var pacote = Pacote();
        pacote.IdadeMaximaIsento = 8;
        pacote.IdadeMaximaMeia = 5;

        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(pacote));
        Assert.Contains(erro.Erros, e => e.MemberNames.Contains(nameof(Domain.Cardapio.Pacote.IdadeMaximaMeia)));
    }

    [Fact]
    public void Textos_das_faixas_para_proposta_e_contrato()
    {
        var regra = RegraFaixasEtarias.Padrao;
        var convidados = new QuantidadesConvidados(100, Meia: 20, Isentos: 10);

        Assert.Equal("Crianças até 5 anos não pagam; de 6 a 10 anos pagam 50% do valor por pessoa.", regra.Descricao);
        Assert.Equal("Crianças até 3 anos não pagam.", new RegraFaixasEtarias(3, 3, 50m).Descricao);
        Assert.Equal("100 convidados (70 inteira, 20 meia e 10 isentos)", TextosFaixas.Resumo(convidados));
        Assert.Equal("80 convidados", TextosFaixas.Resumo(80));
        Assert.Equal(", sendo 70 com valor integral, 20 crianças de 6 a 10 anos com meia (50%) e 10 crianças até 5 anos isentas",
            TextosFaixas.DetalheContrato(convidados, regra));
        Assert.Equal("", TextosFaixas.DetalheContrato(80, regra));

        var linhas = TextosFaixas.LinhasAlimentacao(100m, convidados, regra);
        Assert.Equal(3, linhas.Count);
        Assert.Equal([7000m, 1000m, 0m], linhas.Select(l => l.Valor));
        Assert.Single(TextosFaixas.LinhasAlimentacao(100m, 80, regra));
    }

    [Fact]
    public void Novos_campos_do_contrato_sao_conhecidos()
    {
        Assert.Empty(CamposContrato.Desconhecidos(
            "{{CONVIDADOS_DETALHE}} {{CONVIDADOS_INTEIRA}} {{CONVIDADOS_MEIA}} {{CONVIDADOS_ISENTOS}} {{FAIXAS_ETARIAS}} {{VALOR_MEIA}}"));
    }
}
