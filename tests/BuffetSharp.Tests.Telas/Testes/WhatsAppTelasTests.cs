using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>WhatsApp com envio direto (modo pasta): o lembrete sai pelo número do buffet, sem abrir o aplicativo.</summary>
public class WhatsAppTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Lembrete_de_parcela_vencida_sai_direto_pelo_whatsapp_e_deixa_a_lista() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormLembretes>(principal, "Lembretes");
            robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<Lembrete>(Robo.Grade(tela)).Any(l => l.Tipo == TipoLembrete.ParcelaVencida),
                "parcela vencida nos lembretes");
            var lembrete = Robo.Linhas<Lembrete>(Robo.Grade(tela)).First(l => l.Tipo == TipoLembrete.ParcelaVencida);
            robo.SelecionarLinha<Lembrete>(Robo.Grade(tela), l => l.Chave == lembrete.Chave);
            var antes = Directory.Exists(ambiente.PastaWhatsApp) ? Directory.GetFiles(ambiente.PastaWhatsApp, "*.txt").Length : 0;

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "WhatsApp"), dialogo =>
            {
                Assert.Contains("Consta em aberto", Robo.Campo<TextBox>(dialogo, "Mensagem").Text);
                robo.Preencher(dialogo, "Celular do cliente", "11977776666");
                robo.Clicar(dialogo, "Enviar");
            }, "lembrete por WhatsApp");

            robo.Esperar(() => Robo.Linhas<Lembrete>(Robo.Grade(tela)).All(l => l.Chave != lembrete.Chave), "lembrete tratado");
            var enviados = Directory.GetFiles(ambiente.PastaWhatsApp, "*5511977776666.txt");
            Assert.Equal(antes + 1, Directory.GetFiles(ambiente.PastaWhatsApp, "*.txt").Length);
            Assert.Contains("Consta em aberto", File.ReadAllText(Assert.Single(enviados)));
        });
}
