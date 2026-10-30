using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Alçada de desconto: o comercial pede a aprovação ao salvar e o administrador aprova pela fila.</summary>
public class AlcadaDescontoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Comercial_com_desconto_acima_da_alcada_deixa_o_orcamento_aguardando() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar("comercial", "Comercial@123");
            var tela = robo.AbrirMenu<FormOrcamentos>(principal, "Orçamentos");
            var grade = Robo.Grade(tela);
            bool Candidato(OrcamentoResumo o) => o.Status == StatusOrcamento.Rascunho && o.SituacaoDesconto == SituacaoDesconto.DentroDaAlcada
                                                 && o.Inicio > DateTime.Today.AddDays(7) && o.Inicio is not { Month: 12, Day: 31 } && o.Pacote is not null;
            robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<OrcamentoResumo>(grade).Any(Candidato), "orçamento em rascunho");
            var orcamento = Robo.Linhas<OrcamentoResumo>(grade).First(Candidato);
            robo.SelecionarLinha<OrcamentoResumo>(grade, o => o.Id == orcamento.Id);

            robo.Dialogo<FormOrcamentoEdicao>(() => robo.Clicar(tela, "Abrir"), editor =>
            {
                robo.Preencher(editor, "Desconto (%) - máx. 30%", 20m);
                robo.Dialogo<FormEdicao>(() => robo.Clicar(editor, "Salvar"), alcada =>
                {
                    Assert.Equal("Desconto acima da sua alçada", alcada.Text);
                    Assert.Equal("5%", Robo.Campo<Label>(alcada, "Sua alçada").Text);
                    robo.Preencher(alcada, "Motivo do desconto", "Cliente fechando com a concorrência");
                    robo.Clicar(alcada, "Confirmar desconto");
                }, "desconto acima da alçada");
            }, "orçamento com desconto");

            Assert.Contains(robo.Mensagens, m => m.Titulo == "Desconto aguardando aprovação");
            robo.Esperar(() => Robo.Linhas<OrcamentoResumo>(grade).Single(o => o.Id == orcamento.Id).SituacaoDesconto == SituacaoDesconto.Aguardando,
                "orçamento aguardando aprovação na lista");
        });

    [Fact]
    public void Administrador_aprova_o_desconto_pela_fila() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormAprovacoesDesconto>(principal, "Aprovações de desconto");
            var grade = Robo.Grade(tela);
            robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<AprovacaoDescontoResumo>(grade).Any(p => p.PodeAprovar), "pedido na fila");
            var pedido = Robo.Linhas<AprovacaoDescontoResumo>(grade).First(p => p.PodeAprovar);
            robo.SelecionarLinha<AprovacaoDescontoResumo>(grade, p => p.OrcamentoId == pedido.OrcamentoId);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Aprovar"), dialogo =>
            {
                robo.Preencher(dialogo, "Observação (opcional)", "Cliente antigo");
                robo.Clicar(dialogo, "Aprovar desconto");
            }, "aprovar desconto");

            robo.Esperar(() => Robo.Linhas<AprovacaoDescontoResumo>(grade).All(p => p.OrcamentoId != pedido.OrcamentoId), "pedido saiu da fila");
            var orcamento = robo.Rodar(() => robo.Servico<OrcamentoService>().ObterAsync(pedido.OrcamentoId));
            Assert.Equal((SituacaoDesconto.Aprovado, "admin", "Cliente antigo"),
                (orcamento.SituacaoDesconto, orcamento.DescontoDecididoPor, orcamento.RespostaDesconto));
        });
}
