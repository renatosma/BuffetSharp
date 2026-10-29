using BuffetSharp.Application.Servicos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Pesquisa de satisfação: a resposta colada do WhatsApp é lida e registrada no evento.</summary>
public class PesquisaSatisfacaoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Resposta_colada_e_lida_e_registrada() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormPesquisasSatisfacao>(principal, "Pesquisa de satisfação");
            var grade = Robo.Grade(tela);
            robo.Esperar(() => Robo.Linhas<PesquisaResumo>(grade).Any(p => p.RespondidaEm is null), "evento sem resposta");
            var alvo = Robo.Linhas<PesquisaResumo>(grade).First(p => p.RespondidaEm is null);
            robo.SelecionarLinha<PesquisaResumo>(grade, p => p.EventoId == alvo.EventoId);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Registrar resposta"), dialogo =>
            {
                robo.Preencher(dialogo, "Resposta do cliente (cole a mensagem)", "7, 4, 3, 5, 4 o salão é lindo, mas a comida chegou fria");
                robo.Clicar(dialogo, "Ler a resposta");
                Assert.Equal(7, Robo.Campo<NumericUpDown>(dialogo, "De 0 a 10, indicaria o buffet?").Value);
                Assert.Equal(3, Robo.Campo<NumericUpDown>(dialogo, "Atendimento (1 a 5)").Value);
                Assert.Equal("o salão é lindo, mas a comida chegou fria", Robo.Campo<TextBoxBase>(dialogo, "Comentário").Text);
                robo.Clicar(dialogo, "Salvar");
            }, "registrar resposta");

            robo.Esperar(() => Robo.Linhas<PesquisaResumo>(grade).Any(p => p.EventoId == alvo.EventoId && p.Nps == 7), "resposta na lista");
            var registrada = Robo.Linhas<PesquisaResumo>(grade).Single(p => p.EventoId == alvo.EventoId);
            Assert.Equal((4, 3, 5, 4), (registrada.Comida, registrada.Atendimento, registrada.Espaco, registrada.Organizacao));
        });
}
