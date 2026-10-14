using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios;
using Microsoft.Extensions.DependencyInjection;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Login, menu por perfil e abertura de todas as telas do menu.</summary>
public class NavegacaoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Login_com_senha_errada_mostra_aviso_e_com_senha_certa_inicia_a_sessao() =>
        Robo.Executar(ambiente, robo =>
        {
            using var login = ActivatorUtilities.CreateInstance<FormLogin>(robo.Servicos);
            login.Show();
            robo.Esperar(() => login.Visible, "tela de login");

            robo.Digitar(Robo.Encontrar<TextBox>(login, t => !t.UseSystemPasswordChar), "admin");
            robo.Digitar(Robo.Encontrar<TextBox>(login, t => t.UseSystemPasswordChar), "senha-errada");
            robo.Clicar(login, "Entrar");
            robo.Esperar(() => Robo.Descendentes(login).OfType<Label>().Any(l => l.Text == "Usuário ou senha inválidos."), "aviso de login inválido");
            Assert.False(robo.Servico<ISessaoUsuario>().Autenticado);

            robo.Digitar(Robo.Encontrar<TextBox>(login, t => t.UseSystemPasswordChar), "Admin@123");
            robo.Clicar(login, "Entrar");
            robo.Esperar(() => login.DialogResult == DialogResult.OK, "login aceito");
            Assert.Equal("admin", robo.Servico<ISessaoUsuario>().Usuario!.Login);
        });

    [Theory]
    [InlineData("admin", "Admin@123", new[] { "Orçamentos", "Degustações", "Contratos", "Materiais e enxoval", "Comissões", "Auditoria" }, new string[0])]
    [InlineData("comercial", "Comercial@123", new[] { "Orçamentos", "Degustações", "Contratos", "Eventos" }, new[] { "Comissões", "Auditoria", "Materiais e enxoval" })]
    [InlineData("financeiro", "Financeiro@123", new[] { "Contas a receber", "Comissões", "Contratos" }, new[] { "Orçamentos", "Degustações", "Auditoria" })]
    [InlineData("operacional", "Operacional@123", new[] { "Materiais e enxoval", "Eventos", "Produtos" }, new[] { "Orçamentos", "Comissões", "Contratos", "Auditoria" })]
    public void Menu_lateral_e_filtrado_pelo_perfil(string login, string senha, string[] visiveis, string[] ocultos) =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar(login, senha);
            var menu = Robo.Descendentes(principal).OfType<Button>().Select(b => b.Text.Trim()).ToHashSet();

            Assert.All(visiveis, item => Assert.Contains(item, menu));
            Assert.All(ocultos, item => Assert.DoesNotContain(item, menu));
        });

    [Fact]
    public void Todas_as_telas_do_menu_abrem_e_carregam_sem_erro() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var itens = Robo.Descendentes(principal).OfType<Button>()
                .Where(b => b.Parent is FlowLayoutPanel && b.Text.StartsWith("   "))
                .ToList();
            Assert.True(itens.Count >= 25, $"Menu com {itens.Count} itens.");

            foreach (var item in itens)
            {
                item.PerformClick();
                robo.Esperar(() => Robo.AbaAtual(principal) is { } tela && Robo.Carregado(tela), $"tela \"{item.Text.Trim()}\"");
                robo.Aguardar(150);
                var tela = Robo.AbaAtual(principal)!;
                Assert.True(Robo.Carregado(tela), $"\"{item.Text.Trim()}\" não terminou de carregar.");
            }

            var abas = Robo.Encontrar<TabControl>(principal).TabPages.Count;
            Assert.Equal(itens.Count, abas);
            Assert.DoesNotContain(robo.Mensagens, m => m.Erro);
        });
}
