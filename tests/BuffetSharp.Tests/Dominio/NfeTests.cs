using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.Tests.Dominio;

public class NfeTests
{
    private static readonly DateOnly Emissao = new(2026, 10, 20);

    [Fact]
    public void Le_a_nota_autorizada_com_itens_frete_desconto_e_boletos()
    {
        var xml = NfeExemplo.Xml("45871", "12345678000195", "Atacadão",
        [
            new("A10", "ARROZ AGULHINHA TIPO 1 5KG", "FD", 4, 31.90m),
            new("B22", "Açúcar Refinado 1kg", "UN", 10, 4.79m, Desconto: 2.90m)
        ], frete: 30m, duplicatas: [(Emissao.AddDays(42), 100m), (Emissao.AddDays(28), 102.60m)], emissao: Emissao);

        var nota = LeitorNfe.Ler(xml);

        Assert.Equal(("45871", "1", Emissao, "55"), (nota.Numero, nota.Serie, nota.DataEmissao, nota.Modelo));
        Assert.Equal(44, nota.ChaveAcesso.Length);
        Assert.Equal(("12345678000195", "Atacadão Ltda", "Atacadão"), (nota.Emitente.Documento, nota.Emitente.RazaoSocial, nota.Emitente.NomeFantasia));
        Assert.Equal(("São Paulo", "SP", "01001-000"), (nota.Emitente.Endereco.Cidade, nota.Emitente.Endereco.Uf, nota.Emitente.Endereco.Cep));
        Assert.Equal((175.50m, 30m, 2.90m, 202.60m), (nota.ValorProdutos, nota.ValorFrete, nota.ValorDesconto, nota.ValorTotal));
        Assert.Equal("11222333000181", nota.DestinatarioDocumento);
        Assert.Equal(TipoFormaPagamento.Boleto, nota.MeioPagamento); // tPag 15

        Assert.Equal(2, nota.Itens.Count);
        var acucar = nota.Itens[1];
        Assert.Equal((2, "B22", "UN", 10m, 4.79m), (acucar.Numero, acucar.Codigo, acucar.Unidade, acucar.Quantidade, acucar.ValorUnitario));
        Assert.Equal(4.5m, acucar.CustoUnitario); // (47,90 - 2,90) / 10

        // Os boletos vêm em ordem de vencimento.
        Assert.Equal([Emissao.AddDays(28), Emissao.AddDays(42)], nota.Duplicatas.Select(d => d.Vencimento));
        Assert.Equal(202.60m, nota.Duplicatas.Sum(d => d.Valor));
    }

    [Fact]
    public void Nota_sem_protocolo_usa_a_chave_do_Id_e_aceita_a_data_antiga()
    {
        const string xml = """
            <NFe xmlns="http://www.portalfiscal.inf.br/nfe"><infNFe Id="NFe35261012345678000195550010000001231000000010">
              <ide><mod>55</mod><serie>2</serie><nNF>123</nNF><dEmi>2026-09-30</dEmi></ide>
              <emit><CPF>12345678909</CPF><xNome>João Hortifruti</xNome></emit>
              <det nItem="1"><prod><cProd>1</cProd><xProd>Alface</xProd><uCom>un</uCom><qCom>30</qCom><vUnCom>1.5</vUnCom><vProd>45</vProd></prod></det>
            </infNFe></NFe>
            """;

        var nota = LeitorNfe.Ler(xml);

        Assert.Equal("35261012345678000195550010000001231000000010", nota.ChaveAcesso);
        Assert.Equal((new DateOnly(2026, 9, 30), "12345678909", "UN"), (nota.DataEmissao, nota.Emitente.Documento, nota.Itens[0].Unidade));
        Assert.Equal(45m, nota.ValorTotal); // sem o grupo de totais, soma os itens
        Assert.Empty(nota.Duplicatas);
        Assert.Null(nota.MeioPagamento); // sem o grupo de pagamento
    }

    [Theory]
    [InlineData("isto não é xml")]
    [InlineData("<pedido><numero>1</numero></pedido>")]
    public void Arquivo_que_nao_e_NFe_e_recusado(string conteudo) =>
        Assert.Throws<RegraNegocioException>(() => LeitorNfe.Ler(conteudo));

    [Fact]
    public void Chave_de_acesso_precisa_ter_44_digitos()
    {
        var nota = NovaNota();
        Assert.Throws<RegraNegocioException>(() => nota.DefinirChaveAcesso("123"));
        nota.DefinirChaveAcesso("3526 1012 3456 7800 0195 5500 1000 0001 2310 0000 0010");
        Assert.Equal("35261012345678000195550010000001231000000010", nota.ChaveAcesso);
    }

    [Fact]
    public void Conferencia_aponta_o_que_difere_entre_a_nota_e_os_recebimentos()
    {
        var analise = new AnaliseNfe
        {
            Itens =
            [
                new ItemNfeAnalisado { Item = new ItemNfe(1, "A", "Arroz 5kg", "FD", 4, 30, 120, 0), ProdutoId = 1, Produto = "Arroz", Fator = 5, UnidadeEstoque = "KG" },
                new ItemNfeAnalisado { Item = new ItemNfe(2, "B", "Refrigerante", "PCT", 2, 50, 100, 0), ProdutoId = 2, Produto = "Refrigerante", Fator = 6, UnidadeEstoque = "UN" },
                new ItemNfeAnalisado { Item = new ItemNfe(3, "C", "Sacola", "UN", 1, 1, 1, 0) }
            ],
            Recebido = [new(10, 1, "Arroz", "KG", 20), new(10, 2, "Refrigerante", "UN", 6), new(11, 2, "Refrigerante", "UN", 6)]
        };

        Assert.Equal(["Item 3 da nota não reconhecido: Sacola"], analise.Divergencias([10, 11]));
        var faltando = analise.Divergencias([10]);
        Assert.Contains("Refrigerante: nota 12 UN, recebido 6 UN", faltando);
    }

    [Fact]
    public void Vinculo_guarda_o_produto_e_a_unidade_do_codigo_do_fornecedor()
    {
        var vinculo = new CodigoProdutoFornecedor(7, " A10 ", "ARROZ 5KG", 3, 9);
        Assert.Equal(("A10", 3, 9), (vinculo.Codigo, vinculo.ProdutoId, vinculo.ProdutoEmbalagemId));

        vinculo.Vincular("ARROZ 5KG", 4, 0);
        Assert.Equal((4, (int?)null), (vinculo.ProdutoId, vinculo.ProdutoEmbalagemId));
        Assert.Throws<RegraNegocioException>(() => vinculo.Vincular(null, 0, null));
    }

    private static NotaFiscalEntrada NovaNota()
    {
        var pedido = new PedidoCompra(7, Emissao);
        pedido.DefinirItens([(new Produto { Nome = "Arroz" }, null, 1m, 10m)]);
        var recebimento = pedido.Receber(Emissao, pedido.Itens.ToDictionary(i => i, i => i.Quantidade), null, null, Emissao.ToDateTime(TimeOnly.MinValue));
        return new NotaFiscalEntrada(7, "1", null, Emissao, Domain.Financeiro.CategoriaDespesa.Insumos, [recebimento], 10m, null, Emissao);
    }
}
