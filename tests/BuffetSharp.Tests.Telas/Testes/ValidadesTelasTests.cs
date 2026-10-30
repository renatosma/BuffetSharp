using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Validades: o lote vencido da demonstração é descartado pela tela e aparece nas perdas do período.</summary>
public class ValidadesTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Lote_vencido_e_descartado_e_vai_para_as_perdas() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormValidades>(principal, "Validades");
            var grade = Robo.Grade(tela);
            robo.Esperar(() => Robo.Linhas<LoteResumo>(grade).Any(l => l.Situacao == SituacaoLote.Vencido), "lote vencido na lista");
            var vencido = Robo.Linhas<LoteResumo>(grade).First(l => l.Situacao == SituacaoLote.Vencido);
            Assert.Contains(Robo.Linhas<LoteResumo>(grade), l => l.Situacao == SituacaoLote.Vencendo);
            robo.SelecionarLinha<LoteResumo>(grade, l => l.Id == vencido.Id);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Descartar (perda)"), dialogo =>
            {
                // O lote escolhido já vem com todo o saldo e o motivo.
                Assert.Equal(vencido.Saldo, Robo.Campo<NumericUpDown>(dialogo, "Quantidade descartada").Value);
                Assert.Equal("Produto vencido", Robo.Campo<TextBoxBase>(dialogo, "Motivo").Text);
                robo.Clicar(dialogo, "Descartar");
            }, "descarte do lote vencido");

            robo.Esperar(() => Robo.Linhas<LoteResumo>(grade).All(l => l.Id != vencido.Id), "lote descartado sai da lista");

            robo.Clicar(tela, "Perdas do período");
            robo.Esperar(() => Robo.AbaAtual(principal) is FormPerdasEstoque perdas && Robo.Carregado(perdas), "tela de perdas");
            var perdas = Robo.Grade(Robo.AbaAtual(principal)!);
            robo.Esperar(() => Robo.Linhas<PerdaResumo>(perdas).Any(p => p.Produto == vencido.Produto && p.Quantidade == vencido.Saldo &&
                                                                        p.Observacao!.Contains("Produto vencido")), "descarte nas perdas");
        });
}
