using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Comercial;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Margem no orçamento: o administrador vê os custos; o comercial, só o semáforo.</summary>
public class MargemTelasTests(AmbienteTelas ambiente)
{
    private static void AbrirOrcamento(Robo robo, string login, string senha, Action<FormOrcamentoEdicao> conferir)
    {
        var principal = robo.Entrar(login, senha);
        var tela = robo.AbrirMenu<FormOrcamentos>(principal, "Orçamentos");
        var grade = Robo.Grade(tela);
        robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<OrcamentoResumo>(grade).Any(o => o.MargemPercentual is not null), "orçamento com margem");
        var orcamento = Robo.Linhas<OrcamentoResumo>(grade).First(o => o.MargemPercentual is not null && o.Pacote is not null);
        robo.SelecionarLinha<OrcamentoResumo>(grade, o => o.Id == orcamento.Id);

        robo.Dialogo<FormOrcamentoEdicao>(() => robo.Clicar(tela, "Abrir"), editor =>
        {
            var margem = Robo.Interno<Label>(editor, "_margem");
            robo.Esperar(() => margem.Text.Length > 0, "margem no resumo");
            conferir(editor);
            robo.Clicar(editor, "Fechar");
        }, "orçamento");
    }

    [Fact]
    public void Administrador_ve_os_custos_e_a_margem() =>
        Robo.Executar(ambiente, robo => AbrirOrcamento(robo, "admin", "Admin@123", editor =>
        {
            var texto = Robo.Interno<Label>(editor, "_margem").Text;
            Assert.StartsWith("Custos previstos:", texto);
            Assert.Contains("Desconto máximo para a margem mínima", texto);
            Assert.True(Robo.Interno<Button>(editor, "_custos").Visible);
        }));

    [Fact]
    public void Comercial_ve_so_o_semaforo() =>
        Robo.Executar(ambiente, robo => AbrirOrcamento(robo, "comercial", "Comercial@123", editor =>
        {
            var texto = Robo.Interno<Label>(editor, "_margem").Text;
            Assert.StartsWith("Margem: ●", texto);
            Assert.DoesNotContain("R$", texto);
            Assert.False(Robo.Interno<Button>(editor, "_custos").Visible);
        }));
}
