using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;

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
