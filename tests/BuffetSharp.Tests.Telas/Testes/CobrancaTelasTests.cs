using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Boleto/PIX registrado pelas telas: configuração pedida na primeira emissão, a cobrança pronta e o pagamento simulado.</summary>
public class CobrancaTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Boleto_emitido_em_contas_a_receber_e_pago_no_simulado_baixa_a_parcela() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var conta = robo.Rodar(() => robo.Servico<IConsultasLookup>().ListarAsync(TipoLookup.ContasFinanceiras)).First(c => !c.Marcador).Nome;

            var receber = robo.AbrirMenu<FormContasReceber>(principal, "Contas a receber");
            robo.Esperar(() => Robo.Carregado(receber) && Robo.Linhas<ParcelaResumo>(Robo.Grade(receber)).Any(p => p.Status == StatusTitulo.Aberto),
                "parcelas em aberto");
            var parcela = Robo.Linhas<ParcelaResumo>(Robo.Grade(receber))
                .First(p => p.Status == StatusTitulo.Aberto && p.Valor >= 5 && p.CobrancasPendentes == 0);
            robo.SelecionarLinha<ParcelaResumo>(Robo.Grade(receber), p => p.Id == parcela.Id);

            // Sem configuração: a tela oferece abrir a configuração e segue para a emissão.
            robo.RespostaConfirmacao = "Sim";
            robo.Dialogo<FormEdicao>(() => robo.Clicar(receber, "Boleto / PIX registrado"), dialogo =>
            {
                Assert.Equal("Configuração da cobrança (boleto e PIX)", dialogo.Text);
                robo.Escolher(dialogo, "Conta que recebe os pagamentos", conta);
                robo.Clicar(dialogo, "Salvar");
            }, "configuração da cobrança");
            Assert.Contains(robo.Mensagens, m => m.Texto.Contains("falta a conta bancária"));

            robo.Dialogo<FormEdicao>(() => { }, dialogo =>
            {
                Assert.Equal("Emitir boleto / PIX registrado", dialogo.Text);
                Assert.Equal(Textos.Moeda(parcela.Valor), Robo.Campo<Label>(dialogo, "Valor").Text);
                robo.Clicar(dialogo, "Emitir cobrança");
            }, "emitir cobrança");
            robo.Dialogo<FormBase>(() => { }, janela =>
            {
                Assert.StartsWith("Cobrança - ", janela.Text);
                var campos = Robo.Descendentes(janela).OfType<TextBox>().Select(t => t.Text).ToList();
                Assert.Contains(campos, t => t.Count(char.IsAsciiDigit) == 47);
                Assert.Contains(campos, t => t.StartsWith("000201"));
                robo.Clicar(janela, "Fechar");
            }, "cobrança pronta");
            robo.Esperar(() => Robo.Linhas<ParcelaResumo>(Robo.Grade(receber)).Single(p => p.Id == parcela.Id).Cobranca == "Emitida",
                "cobrança na parcela");

            var cobrancas = robo.AbrirMenu<FormCobrancas>(principal, "Cobranças (boleto e PIX)");
            robo.Esperar(() => Robo.Carregado(cobrancas) && Robo.Linhas<CobrancaResumo>(Robo.Grade(cobrancas)).Any(c => c.ParcelaId == parcela.Id),
                "cobrança na lista");
            var cobranca = Robo.Linhas<CobrancaResumo>(Robo.Grade(cobrancas)).Single(c => c.ParcelaId == parcela.Id);
            Assert.Equal((StatusCobranca.Pendente, AmbienteCobranca.Simulado, parcela.Valor), (cobranca.Status, cobranca.Ambiente, cobranca.Valor));

            robo.SelecionarLinha<CobrancaResumo>(Robo.Grade(cobrancas), c => c.Id == cobranca.Id);
            robo.Clicar(cobrancas, "Simular pagamento");
            robo.Esperar(() => Robo.Linhas<CobrancaResumo>(Robo.Grade(cobrancas)).Single(c => c.Id == cobranca.Id) is
                { Status: StatusCobranca.Paga, Baixada: true }, "cobrança paga e parcela baixada");

            receber = robo.AbrirMenu<FormContasReceber>(principal, "Contas a receber");
            robo.Esperar(() => Robo.Linhas<ParcelaResumo>(Robo.Grade(receber)).Single(p => p.Id == parcela.Id) is
                { Status: StatusTitulo.Pago, Cobranca: "Paga" }, "parcela recebida pelo boleto");
        });
}
