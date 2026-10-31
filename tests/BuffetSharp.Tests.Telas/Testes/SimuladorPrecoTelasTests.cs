using BuffetSharp.Domain.Comercial;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Cardapio;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Simulador de preço aberto pelos pacotes: mostra o custo, o preço mínimo e o sugerido do pacote escolhido.</summary>
public class SimuladorPrecoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Simulador_mostra_os_precos_do_pacote_por_numero_de_convidados() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var pacotes = robo.AbrirMenu<FormPacotes>(principal, "Pacotes");
            var grade = Robo.Grade(pacotes);
            robo.Esperar(() => Robo.Carregado(pacotes) && Robo.Linhas<Application.Consultas.PacoteResumo>(grade).Any(p => p.Nome == "Prata"), "pacotes");
            robo.SelecionarLinha<Application.Consultas.PacoteResumo>(grade, p => p.Nome == "Prata");
            robo.Clicar(pacotes, "Simular preço");

            robo.Esperar(() => Robo.AbaAtual(principal) is FormSimuladorPreco tela && Robo.Carregado(tela), "simulador");
            var simulador = (FormSimuladorPreco)Robo.AbaAtual(principal)!;
            var resultado = Robo.Interno<Label>(simulador, "_resultado");
            robo.Esperar(() => resultado.Text.Contains("Preço mínimo"), "preços do pacote");
            Assert.Contains("Preço atual do pacote", resultado.Text);

            var tabela = Robo.Interno<Grade<ResultadoSimulacao>>(simulador, "_tabela");
            Assert.Contains(tabela.Itens, r => r.Convidados == 100);
            Assert.All(tabela.Itens, r => Assert.True(r.PrecoSugerido > r.PrecoMinimo));

            Robo.Interno<NumericUpDown>(simulador, "_convidados").Value = 180;
            robo.Esperar(() => tabela.Itens.Any(r => r.Convidados == 180) && resultado.Text.Contains("Para 180 convidados"), "cenário com 180 convidados");
        });
}
