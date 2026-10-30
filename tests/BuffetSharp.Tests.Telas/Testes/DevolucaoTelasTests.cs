using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Devolução parcial: pela tela de recebimentos, só parte de um item volta ao fornecedor e aparece nas devoluções.</summary>
public class DevolucaoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Devolve_parte_de_um_item_do_recebimento_e_aparece_nas_devolucoes() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormRecebimentos>(principal, "Recebimentos");
            var grade = Robo.Grade(tela);
            robo.Esperar(() => Robo.Linhas<RecebimentoResumo>(grade).Any(r => r.Fornecedor == "Vale Verde" && r.Status == StatusRecebimento.AFaturar),
                "recebimento do hortifrúti");
            var recebimento = Robo.Linhas<RecebimentoResumo>(grade).First(r => r.Fornecedor == "Vale Verde" && r.Status == StatusRecebimento.AFaturar);
            robo.SelecionarLinha<RecebimentoResumo>(grade, r => r.Id == recebimento.Id);
            decimal valor = 0;

            robo.RespostaConfirmacao = "Não"; // não abre o comprovante agora
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Devolver itens"), dialogo =>
            {
                var itens = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                var batata = itens.Rows.Cast<DataGridViewRow>().Single(r => (string?)r.Cells[0].Value == "Batata");
                batata.Cells[5].Value = 3m;
                valor = Math.Round(3 * (decimal)batata.Cells[4].Value!, 2);
                var destino = Robo.Descendentes(dialogo).OfType<Label>().Single(l => l.Text.StartsWith("Devolução de"));
                Assert.Contains("ainda não tem nota", destino.Text);
                robo.Escolher(dialogo, "Motivo", "Qualidade");
                robo.Clicar(dialogo, "Registrar devolução");
            }, "devolução de batata");
            Assert.Contains(robo.Mensagens, m => m.Titulo == "Devolução registrada");
            robo.Esperar(() => Robo.Linhas<RecebimentoResumo>(grade).Single(r => r.Id == recebimento.Id).Total == recebimento.Total - valor,
                "recebimento sem o devolvido");

            var devolucoes = robo.AbrirMenu<FormDevolucoesCompra>(principal, "Devoluções ao fornecedor");
            robo.Esperar(() => Robo.Linhas<DevolucaoResumo>(Robo.Grade(devolucoes))
                .Any(d => d.RecebimentoCompraId == recebimento.Id && d.Motivo == MotivoDevolucao.Qualidade && d.Valor == valor), "devolução na lista");
        });
}
