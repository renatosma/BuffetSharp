using System.Text;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class FinanceiroCobrancaTests
{
    [Fact]
    public void Crc_do_br_code_confere_com_o_exemplo_do_banco_central()
    {
        const string exemplo = "00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-4266554400005204000053039865802BR" +
                               "5913Fulano de Tal6008BRASILIA62070503***6304";
        Assert.Equal("1D3D", BrCodePix.Crc16(exemplo));
    }

    [Fact]
    public void Br_code_tem_chave_valor_recebedor_identificador_e_crc_valido()
    {
        var codigo = BrCodePix.Montar("contato@buffet.com.br", "Buffet São João Eventos Ltda", "São Paulo", 1500.5m, "PARC-000042", "Parcela 1/3");

        Assert.StartsWith("000201", codigo);
        Assert.Contains("0014br.gov.bcb.pix0121contato@buffet.com.br", codigo);
        Assert.Contains("54071500.50", codigo);
        Assert.Contains("5925BUFFET SAO JOAO EVENTOS", codigo); // sem acento e no máximo 25 caracteres
        Assert.Contains("6009SAO PAULO", codigo);
        Assert.Contains("62140510PARC000042", codigo);
        Assert.Equal(BrCodePix.Crc16(codigo[..^4]), codigo[^4..]);
    }

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("(11) 98888-7777", "+5511988887777")]
    [InlineData("+55 11 98888-7777", "+5511988887777")]
    [InlineData("Financeiro@Buffet.com.br", "financeiro@buffet.com.br")]
    [InlineData("123E4567-E12B-12D1-A456-426655440000", "123e4567-e12b-12d1-a456-426655440000")]
    public void Chave_pix_e_normalizada(string chave, string esperada) =>
        Assert.Equal(esperada, BrCodePix.NormalizarChave(chave));

    [Fact]
    public void Chave_pix_invalida_e_recusada() =>
        Assert.Throws<RegraNegocioException>(() => BrCodePix.NormalizarChave("123"));

    private const string OfxSgml = """
        OFXHEADER:100
        DATA:OFXSGML
        VERSION:102
        CHARSET:1252

        <OFX>
        <BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <BANKACCTFROM><BANKID>001<ACCTID>12345-6</BANKACCTFROM>
        <BANKTRANLIST>
        <DTSTART>20261001<DTEND>20261031
        <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20261005120000[-3:BRT]<TRNAMT>2500.00<FITID>A1<MEMO>PIX RECEBIDO MARIA</STMTTRN>
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20261010<TRNAMT>-35,90<FITID>A2<MEMO>TARIFA PACOTE</STMTTRN>
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20261012<TRNAMT>-1.234,56<NAME>BOLETO<MEMO>ATACADAO</STMTTRN>
        </BANKTRANLIST>
        <LEDGERBAL><BALAMT>10229.54<DTASOF>20261031</LEDGERBAL>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1>
        </OFX>
        """;

    [Fact]
    public void Leitor_ofx_le_extrato_sgml_com_virgula_decimal_e_sem_fitid()
    {
        var extrato = LeitorOfx.Ler(Encoding.Latin1.GetBytes(OfxSgml));

        Assert.Equal("001", extrato.Banco);
        Assert.Equal("12345-6", extrato.Conta);
        Assert.Equal(new DateOnly(2026, 10, 1), extrato.Inicio);
        Assert.Equal(10229.54m, extrato.Saldo);
        Assert.Equal(new DateOnly(2026, 10, 31), extrato.DataSaldo);
        Assert.Equal(3, extrato.Lancamentos.Count);
        Assert.Equal(new LancamentoExtrato("A1", new DateOnly(2026, 10, 5), 2500m, "PIX RECEBIDO MARIA", "CREDIT"), extrato.Lancamentos[0]);
        Assert.Equal(-35.90m, extrato.Lancamentos[1].Valor);
        Assert.Equal(-1234.56m, extrato.Lancamentos[2].Valor);
        Assert.Equal("BOLETO - ATACADAO", extrato.Lancamentos[2].Descricao);
        Assert.StartsWith("20261012-", extrato.Lancamentos[2].Id);
    }

    [Fact]
    public void Leitor_ofx_le_extrato_xml()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?><?OFX OFXHEADER="200" VERSION="220"?>
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
            <STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261103</DTPOSTED><TRNAMT>99.90</TRNAMT><FITID>X9</FITID><MEMO>Rendimento &amp; juros</MEMO></STMTTRN>
            </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;
        var lancamento = Assert.Single(LeitorOfx.Ler(Encoding.UTF8.GetBytes(xml)).Lancamentos);
        Assert.Equal("X9", lancamento.Id);
        Assert.Equal(99.90m, lancamento.Valor);
        Assert.Equal("Rendimento & juros", lancamento.Descricao);
    }

    [Fact]
    public void Arquivo_que_nao_e_ofx_e_recusado() =>
        Assert.Throws<RegraNegocioException>(() => LeitorOfx.Ler(Encoding.UTF8.GetBytes("data;valor\n01/10;10")));

    [Fact]
    public void Movimento_so_concilia_com_o_mesmo_valor_e_uma_vez()
    {
        var conta = new ContaFinanceira { Nome = "Banco", DataSaldoInicial = new DateOnly(2026, 1, 1) };
        var movimento = new MovimentoConta(conta, new DateOnly(2026, 10, 5), 2500m, "Recebimento", OrigemMovimento.Lancamento);

        Assert.Throws<RegraNegocioException>(() => movimento.Conciliar("A1", 2400m, DateTime.Now));
        movimento.Conciliar("A1", 2500m, DateTime.Now);
        Assert.True(movimento.Conciliado);
        Assert.Throws<RegraNegocioException>(() => movimento.Conciliar("A1", 2500m, DateTime.Now));

        movimento.DesfazerConciliacao();
        Assert.False(movimento.Conciliado);
        Assert.Null(movimento.IdExtrato);
    }
}
