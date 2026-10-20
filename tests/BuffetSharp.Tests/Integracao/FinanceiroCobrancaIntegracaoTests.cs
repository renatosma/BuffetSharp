using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Cobrança por PIX, conciliação bancária com extrato OFX e DRE.</summary>
[Collection(ColecaoBanco.Nome)]
public class FinanceiroCobrancaIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Pix_gera_codigo_e_qr_code_png()
    {
        var pix = new PixService(new PixOptions { Chave = "financeiro@buffet.exemplo.com.br", Cidade = "Campinas" }, new BuffetOptions(),
            banco.Obter<IGeradorQrCode>());

        var cobranca = pix.Parcela(42, 1234.56m, "Parcela 2/5");

        Assert.Equal("PARC000042", cobranca.Identificador);
        Assert.Equal(BrCodePix.Crc16(cobranca.CopiaECola[..^4]), cobranca.CopiaECola[^4..]);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], cobranca.QrCodePng.Take(4));
        Assert.Contains(pix.Parcela(42, 1234.56m).CopiaECola, pix.TextoCobranca(42, 1234.56m));

        var semChave = new PixService(new PixOptions(), new BuffetOptions(), banco.Obter<IGeradorQrCode>());
        Assert.False(semChave.Configurado);
        Assert.Null(semChave.TextoCobranca(42, 10));
        Assert.Throws<RegraNegocioException>(() => semChave.Parcela(42, 10));
    }

    [Fact]
    public async Task Extrato_ofx_e_conciliado_com_os_movimentos_da_conta()
    {
        var contaId = (await banco.Obter<IConsultasContas>().ListarContasAsync(ct: Ct))
            .First(c => c.Tipo == Domain.Financeiro.TipoContaFinanceira.ContaCorrente).Id;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var contas = banco.Obter<ContaFinanceiraService>();
        var deposito = await contas.LancarAsync(new MovimentoInput
        {
            ContaFinanceiraId = contaId, Data = hoje.AddDays(-2), Entrada = true, Valor = 777.77m, Descricao = "Depósito para conciliação"
        }, Ct);

        // O banco mostra o depósito um dia depois e cobra uma tarifa que o sistema não tem.
        var extrato = new ExtratoOfx("001", "12345-6", hoje.AddDays(-3), hoje, null, null,
        [
            new LancamentoExtrato($"T-{Guid.NewGuid():N}"[..20], hoje.AddDays(-1), 777.77m, "DEPOSITO", "CREDIT"),
            new LancamentoExtrato($"T-{Guid.NewGuid():N}"[..20], hoje, -12.34m, "TARIFA AVULSA", "DEBIT")
        ]);
        var conciliacao = banco.Obter<ConciliacaoService>();
        var analise = await conciliacao.AnalisarAsync(contaId, extrato, Ct);

        var sugerido = analise.Itens.Single(i => i.Extrato.Valor == 777.77m);
        Assert.Equal(SituacaoConciliacao.Sugerido, sugerido.Situacao);
        Assert.Equal(deposito, sugerido.MovimentoId);
        Assert.Equal(1, sugerido.DiferencaDias);
        var tarifa = analise.Itens.Single(i => i.Extrato.Valor == -12.34m);
        Assert.Equal(SituacaoConciliacao.SemCorrespondencia, tarifa.Situacao);

        Assert.Equal(1, await conciliacao.ConciliarAsync(contaId, [sugerido], Ct));
        await conciliacao.LancarAsync(contaId, tarifa.Extrato, ct: Ct);
        await Assert.ThrowsAsync<RegraNegocioException>(() => conciliacao.LancarAsync(contaId, tarifa.Extrato, ct: Ct));

        var depois = await conciliacao.AnalisarAsync(contaId, extrato, Ct);
        Assert.All(depois.Itens, i => Assert.Equal(SituacaoConciliacao.Conciliado, i.Situacao));
        var extratoSistema = await banco.Obter<IConsultasContas>().ExtratoAsync(contaId, hoje.AddDays(-3), hoje, Ct);
        Assert.True(extratoSistema.Movimentos.Single(m => m.Id == deposito).Conciliado);

        await conciliacao.DesfazerAsync(deposito, Ct);
        Assert.Equal(SituacaoConciliacao.Sugerido,
            (await conciliacao.AnalisarAsync(contaId, extrato, Ct)).Itens.Single(i => i.Extrato.Valor == 777.77m).Situacao);
    }

    [Theory]
    [InlineData(RegimeDre.Caixa)]
    [InlineData(RegimeDre.Competencia)]
    public async Task Dre_fecha_receita_menos_custos_e_despesas(RegimeDre regime)
    {
        var ano = DateTime.Today.Year;
        var dre = await banco.Obter<IConsultasRelatorios>().DreAsync(ano, regime, Ct);

        var receita = dre.Linhas.Single(l => l.Descricao == "Receita bruta de eventos");
        var custos = dre.Linhas.Single(l => l.Descricao.Contains("Custos dos eventos"));
        var margem = dre.Linhas.Single(l => l.Descricao.Contains("Margem de contribuição"));
        var despesas = dre.Linhas.Single(l => l.Descricao.Contains("Despesas operacionais"));
        var impostos = dre.Linhas.Single(l => l.Descricao.Contains("Impostos"));
        var resultado = dre.Linhas.Single(l => l.Descricao.Contains("Resultado do período"));

        Assert.True(receita.Total > 0);
        Assert.True(custos.Total < 0);
        Assert.Equal(receita.Total + custos.Total, margem.Total);
        Assert.Equal(margem.Total + despesas.Total + impostos.Total, resultado.Total);
        Assert.Equal(receita.Total, dre.Linhas.TakeWhile(l => !ReferenceEquals(l, custos)).Where(l => l.Nivel == 2).Sum(l => l.Total));
        Assert.Equal(100m, receita.PercentualReceita);

        if (regime == RegimeDre.Caixa)
        {
            // Recebido no ano = o mesmo valor do fluxo de caixa.
            var fluxo = await banco.Obter<IConsultasRelatorios>().FluxoCaixaAsync(new DateTime(ano, 1, 1), new DateTime(ano, 12, 31), Ct);
            Assert.Equal(fluxo.Sum(f => f.Recebido), receita.Total);
        }
    }
}
