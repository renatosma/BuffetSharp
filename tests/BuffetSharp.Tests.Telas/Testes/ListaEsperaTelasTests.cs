using BuffetSharp.Application.Servicos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Lista de espera: a base traz a fila e a data liberada; uma espera nova entra pelo diálogo.</summary>
public class ListaEsperaTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Nova_espera_entra_na_lista() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormListaEspera>(principal, "Lista de espera");
            var grade = Robo.Grade(tela);
            robo.Esperar(() => Robo.Linhas<EsperaResumo>(grade).Count > 0, "esperas da base");
            var antes = Robo.Linhas<EsperaResumo>(grade);
            Assert.Contains(antes, e => e.SuaVez && e.Lead);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Nova espera"), dialogo =>
            {
                robo.Escolher(dialogo, "Cliente");
                robo.Preencher(dialogo, "Convidados", 45m);
                robo.Preencher(dialogo, "Observação", "Espera do teste de tela");
                robo.Clicar(dialogo, "Entrar na espera");
            }, "nova espera");
            robo.Esperar(() => Robo.Linhas<EsperaResumo>(grade).Any(e => e.Observacao == "Espera do teste de tela"), "espera na lista");
            Assert.Equal(antes.Count + 1, Robo.Linhas<EsperaResumo>(grade).Count);
        });
}
