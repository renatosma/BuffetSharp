using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Condição de pagamento, modelos de contrato, valor por extenso, conversão de unidades e contatos adicionais.</summary>
public class CondicoesContratosConversoesTests
{
    private static readonly DateOnly Hoje = new(2026, 1, 1);

    // ------------------------- Condição de pagamento -------------------------

    private static CondicaoPagamento TrintaMaisCinco(int? quitarAntes = null) => new()
    {
        Nome = "30% + 5x",
        PercentualEntrada = 30,
        DiasEntrada = 2,
        QuantidadeParcelas = 5,
        DiasPrimeiraParcela = 30,
        QuitarDiasAntesEvento = quitarAntes
    };

    [Fact]
    public void Condicao_calcula_entrada_e_parcelas_mensais()
    {
        var plano = TrintaMaisCinco().Simular(10_000m, Hoje, new DateOnly(2026, 12, 31));

        Assert.Equal(3_000m, plano.ValorEntrada);
        Assert.Equal(Hoje.AddDays(2), plano.VencimentoEntrada);
        Assert.Equal(5, plano.Quantidade);
        Assert.Equal(new DateOnly(2026, 1, 31), plano.PrimeiroVencimento);
        Assert.Null(plano.IntervaloDias);
        Assert.Equal(1_400m, plano.ValorParcela);
        Assert.Equal("30% de entrada + 5x mensais", TrintaMaisCinco().Resumo);
    }

    [Fact]
    public void Condicao_com_quitacao_antes_do_evento_reduz_as_parcelas()
    {
        // Evento em 15/04: quitação até 05/04. Parcelas em 31/01, 28/02 e 31/03 cabem; 30/04 não.
        var plano = TrintaMaisCinco(quitarAntes: 10).Simular(10_000m, Hoje, new DateOnly(2026, 4, 15));

        Assert.Equal(3, plano.Quantidade);
        Assert.Equal(new DateOnly(2026, 3, 31), plano.UltimoVencimento);
        Assert.Equal(Math.Round(7_000m / 3, 2, MidpointRounding.ToZero), plano.ValorParcela);
    }

    [Fact]
    public void Condicao_a_vista_gera_pagamento_unico()
    {
        var avista = new CondicaoPagamento { Nome = "À vista", PercentualEntrada = 100, QuantidadeParcelas = 0, DiasEntrada = 3 };
        Validador.Validar(avista);

        var plano = avista.Simular(5_000m, Hoje, new DateOnly(2026, 6, 1));

        Assert.Equal(0, plano.ValorEntrada);
        Assert.Equal(1, plano.Quantidade);
        Assert.Equal(Hoje.AddDays(3), plano.PrimeiroVencimento);
        Assert.Equal("À vista", avista.Texto);
        Assert.StartsWith("Pagamento único de R$", TextosPagamento.Simulacao(avista, 5_000m, Hoje, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void Condicao_sem_parcelas_exige_entrada_de_cem_por_cento()
    {
        var erro = Assert.Throws<ValidacaoException>(() =>
            Validador.Validar(new CondicaoPagamento { Nome = "Inválida", PercentualEntrada = 30, QuantidadeParcelas = 0 }));
        Assert.Contains(erro.Erros, e => e.MemberNames.Contains(nameof(CondicaoPagamento.PercentualEntrada)));
    }

    private static CondicaoPagamento Definida(params (int Dias, decimal Percentual)[] parcelas) => new()
    {
        Nome = "Definida",
        Parcelas = parcelas.Select(p => new CondicaoPagamentoParcela { Dias = p.Dias, Percentual = p.Percentual }).ToList()
    };

    [Fact]
    public void Parcelas_definidas_dao_o_prazo_e_o_percentual_de_cada_uma()
    {
        // Fora de ordem no cadastro: os vencimentos saem por prazo; os centavos ficam na última.
        var condicao = Definida((60, 35), (0, 30), (30, 35));
        Validador.Validar(condicao);

        var parcelas = condicao.Vencimentos(1_000.01m, Hoje);

        Assert.Equal([Hoje, Hoje.AddDays(30), Hoje.AddDays(60)], parcelas.Select(p => p.Vencimento));
        Assert.Equal([300m, 350m, 350.01m], parcelas.Select(p => p.Valor));
        Assert.Equal("30% à vista, 35% em 30 dias e 35% em 60 dias", condicao.Resumo);
        Assert.Equal("28/42 dias", Definida((28, 50), (42, 50)).Resumo);
        Assert.Equal("30/60/90 dias", Definida((30, 33.33m), (60, 33.33m), (90, 33.34m)).Resumo); // centavos na última
        Assert.Equal("40% em 30 dias e 60% em 60 dias", Definida((30, 40), (60, 60)).Resumo);
        Assert.Equal("À vista", Definida((0, 100)).Resumo);
        Assert.True(Definida((0, 100)).AVista);
    }

    [Fact]
    public void Parcelas_definidas_respeitam_a_quitacao_antes_do_evento()
    {
        var condicao = Definida((0, 50), (90, 50));
        condicao.QuitarDiasAntesEvento = 10;

        var parcelas = condicao.Vencimentos(2_000m, Hoje, dataEvento: Hoje.AddDays(40));

        Assert.Equal([Hoje, Hoje.AddDays(30)], parcelas.Select(p => p.Vencimento)); // 90 dias passaria do limite
        Assert.Equal([Hoje.AddDays(90)], Definida((90, 100)).Vencimentos(10m, Hoje).Select(p => p.Vencimento)); // sem evento, sem limite
    }

    [Theory]
    [InlineData(new[] { 30, 60 }, new[] { 50.0, 40.0 }, "somam 90")]
    [InlineData(new[] { 30, 30 }, new[] { 50.0, 50.0 }, "mesmo prazo")]
    [InlineData(new[] { 30, 60 }, new[] { 100.0, 0.0 }, "maior que zero")]
    public void Parcelas_definidas_invalidas_sao_recusadas(int[] dias, double[] percentuais, string mensagem)
    {
        var condicao = Definida(dias.Zip(percentuais, (d, p) => (d, (decimal)p)).ToArray());
        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(condicao));
        Assert.Contains(erro.Erros, e => e.ErrorMessage!.Contains(mensagem));
    }

    [Fact]
    public void Cada_parcela_leva_a_sua_forma_e_as_demais_a_forma_geral()
    {
        var condicao = Definida((0, 30), (30, 70));
        condicao.Parcelas.Single(p => p.Dias == 0).FormaPagamentoId = 1; // entrada no PIX

        var parcelas = condicao.Vencimentos(1_000m, Hoje);
        Assert.Equal([1, (int?)null], parcelas.Select(p => p.FormaPagamentoId));

        var evento = new Evento("Teste", 1, 1, 1, new DateOnly(2026, 6, 1), new TimeOnly(19, 0), new TimeOnly(23, 0), 50, 1_000m);
        var geradas = evento.GerarParcelas(parcelas.Select(p => new VencimentoPlanejado(p.Vencimento, p.Valor, p.FormaPagamentoId)).ToList(),
            formaPagamentoId: 5); // demais no cartão
        Assert.Equal([1, 5], geradas.Select(p => p.FormaPagamentoId!.Value));
    }

    [Fact]
    public void Plano_do_evento_aceita_parcelas_de_valores_diferentes()
    {
        var evento = new Evento("Teste", 1, 1, 1, new DateOnly(2026, 6, 1), new TimeOnly(19, 0), new TimeOnly(23, 0), 50, 9_000m);

        Assert.Throws<RegraNegocioException>(() => evento.GerarParcelas([(new DateOnly(2026, 2, 1), 1_000m)]));
        var parcelas = evento.GerarParcelas([(new DateOnly(2026, 3, 1), 6_300m), (new DateOnly(2026, 2, 1), 2_700m)], formaPagamentoId: 3);

        Assert.Equal([2_700m, 6_300m], parcelas.Select(p => p.Valor));
        Assert.Equal(["Parcela 1/2", "Parcela 2/2"], parcelas.Select(p => p.Descricao));
        Assert.All(parcelas, p => Assert.Equal(3, p.FormaPagamentoId));
    }

    [Fact]
    public void Plano_de_pagamento_aceita_intervalo_em_dias()
    {
        var evento = new Evento("Teste", 1, 1, 1, new DateOnly(2026, 6, 1), new TimeOnly(19, 0), new TimeOnly(23, 0), 50, 9_000m);

        var parcelas = evento.GerarParcelas(3, new DateOnly(2026, 2, 1), intervaloDias: 15);

        Assert.Equal(new[] { new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 16), new DateOnly(2026, 3, 3) }, parcelas.Select(p => p.Vencimento));
        Assert.Equal(9_000m, parcelas.Sum(p => p.Valor));
    }

    // ------------------------- Valor por extenso -------------------------

    [Theory]
    [InlineData(1, "um real")]
    [InlineData(100, "cem reais")]
    [InlineData(101, "cento e um reais")]
    [InlineData(1_000, "mil reais")]
    [InlineData(1_500.50, "mil e quinhentos reais e cinquenta centavos")]
    [InlineData(1_234.56, "mil duzentos e trinta e quatro reais e cinquenta e seis centavos")]
    [InlineData(2_030, "dois mil e trinta reais")]
    [InlineData(30_000, "trinta mil reais")]
    [InlineData(1_000_000, "um milhão de reais")]
    [InlineData(1_200_000, "um milhão e duzentos mil reais")]
    [InlineData(0.01, "um centavo")]
    public void Valor_por_extenso(double valor, string esperado) => Assert.Equal(esperado, Extenso.Moeda((decimal)valor));

    // ------------------------- Modelos de contrato -------------------------

    [Fact]
    public void Modelos_do_sistema_usam_somente_campos_conhecidos()
    {
        Assert.Empty(CamposContrato.Desconhecidos(GeradorContrato.ModeloPadrao));
        Assert.Empty(CamposContrato.Desconhecidos(GeradorContrato.ModeloEmBranco));
        Validador.Validar(new ModeloContrato { Nome = "Padrão", Conteudo = GeradorContrato.ModeloPadrao, Padrao = true });
    }

    [Fact]
    public void Modelo_com_campo_desconhecido_e_rejeitado()
    {
        var modelo = new ModeloContrato { Nome = "Errado", Conteudo = GeradorContrato.ModeloPadrao + "\n{{NOME_DO_NOIVO}}" };

        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(modelo));

        Assert.Contains("{{NOME_DO_NOIVO}}", erro.Message);
    }

    [Fact]
    public void Campos_sao_trocados_e_clausulas_numeradas_em_ordem()
    {
        const string modelo = "CLÁUSULA {{N}}ª - A\nCliente: {{ cliente_nome }}\n\n\n\nCLÁUSULA {{N}}ª - B\n{{DESCONHECIDO}}";

        var texto = CamposContrato.Substituir(modelo, new Dictionary<string, string> { ["CLIENTE_NOME"] = "Ana" });

        Assert.Equal("CLÁUSULA 1ª - A\nCliente: Ana\n\nCLÁUSULA 2ª - B\n{{DESCONHECIDO}}", texto);
    }

    // ------------------------- Conversão de unidades -------------------------

    [Fact]
    public void Compra_em_embalagem_converte_quantidade_e_custo_para_a_unidade_do_estoque()
    {
        var produto = new Produto { Nome = "Refrigerante lata" };
        var caixa = new ProdutoEmbalagem { FornecedorId = 7, Fator = 12, UnidadeMedida = new UnidadeMedida { Sigla = "CX", Descricao = "Caixa" } };

        var movimentacao = produto.RegistrarCompra(2, 45m, DateTime.Now, "NF 123", 7, caixa);

        Assert.Equal(24, produto.EstoqueAtual);
        Assert.Equal(3.75m, produto.CustoMedio);
        Assert.Equal(24, movimentacao.Quantidade);
        Assert.Equal(3.75m, movimentacao.CustoUnitario);
        Assert.Equal("CX", movimentacao.UnidadeCompra);
        Assert.Equal(2, movimentacao.QuantidadeCompra);
        Assert.Equal(12, movimentacao.FatorConversao);
        Assert.Equal(45m, movimentacao.CustoUnitarioCompra);
        Assert.Equal(7, movimentacao.FornecedorId);
    }

    [Fact]
    public void Embalagem_de_outro_fornecedor_e_recusada()
    {
        var produto = new Produto { Nome = "Água" };
        var fardo = new ProdutoEmbalagem { FornecedorId = 1, Fator = 6 };

        Assert.Throws<RegraNegocioException>(() => produto.RegistrarCompra(1, 10m, DateTime.Now, null, 2, fardo));
    }

    [Fact]
    public void Produto_nao_aceita_embalagens_repetidas_ou_sem_unidade()
    {
        var produto = new Produto { Nome = "Arroz", CategoriaProdutoId = 1, UnidadeMedidaId = 1 };
        produto.Embalagens.Add(new ProdutoEmbalagem { UnidadeMedidaId = 2, Fator = 5 });
        produto.Embalagens.Add(new ProdutoEmbalagem { UnidadeMedidaId = 2, Fator = 10 });
        produto.Embalagens.Add(new ProdutoEmbalagem { UnidadeMedidaId = 0, Fator = 1 });

        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(produto));

        Assert.Contains("repetidas", erro.Message);
        Assert.Contains("unidade de compra", erro.Message);
    }

    // ------------------------- Contatos adicionais -------------------------

    [Fact]
    public void Contato_adicional_basta_como_contato_e_telefone_e_formatado()
    {
        var cliente = new Cliente { Nome = "Só WhatsApp" };
        cliente.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.WhatsApp, Valor = "11987654321" });

        Validador.Validar(cliente);

        Assert.Equal("(11) 98765-4321", cliente.Contatos[0].Valor);
    }

    [Fact]
    public void Contatos_adicionais_invalidos_ou_repetidos_sao_rejeitados()
    {
        var fornecedor = new Fornecedor { RazaoSocial = "Fornecedor" };
        fornecedor.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Email, Valor = "sem-arroba" });
        fornecedor.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Celular, Valor = "(11) 99999-0000" });
        fornecedor.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Celular, Valor = "11999990000" });
        fornecedor.Enderecos.Add(new EnderecoAdicional { Identificacao = "Depósito" });

        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(fornecedor));

        Assert.Contains("E-mail adicional inválido", erro.Message);
        Assert.Contains("repetido", erro.Message);
        Assert.Contains("logradouro e a cidade", erro.Message);
    }
}
