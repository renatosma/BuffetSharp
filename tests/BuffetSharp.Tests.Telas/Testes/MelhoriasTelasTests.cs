using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Funil de leads e lembretes do dia operados pelas telas.</summary>
public class MelhoriasTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Lead_cadastrado_pela_tela_e_convertido_em_cliente() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormLeads>(principal, "Leads (funil)");
            const string nome = "Lead da Tela de Teste";

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Novo lead"), dialogo =>
            {
                robo.Preencher(dialogo, "Nome", nome);
                robo.Preencher(dialogo, "Telefone / WhatsApp", "11977776666");
                robo.Escolher(dialogo, "Origem", "Indicação");
                robo.Clicar(dialogo, "Salvar");
            }, "novo lead");
            robo.Esperar(() => Robo.Linhas<LeadResumo>(Robo.Grade(tela)).Any(l => l.Nome == nome && l.Status == StatusLead.Novo), "lead na lista");

            robo.SelecionarLinha<LeadResumo>(Robo.Grade(tela), l => l.Nome == nome);
            robo.RespostaConfirmacao = "Não"; // não abre o orçamento agora
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Converter em cliente"), dialogo => robo.Clicar(dialogo, "Salvar"),
                "converter lead");
            robo.Esperar(() => Robo.Linhas<LeadResumo>(Robo.Grade(tela)).Count(l => l.Nome == nome) == 0, "lead convertido sai da negociação");

            var convertido = robo.Rodar(() => robo.Servico<IConsultasLeads>().ListarAsync(new FiltroLeads { Status = StatusLead.Convertido }))
                .Single(l => l.Nome == nome);
            Assert.Equal(nome, convertido.Cliente);
        });

    [Fact]
    public void Lembrete_resolvido_pela_tela_sai_da_lista() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormLembretes>(principal, "Lembretes");
            robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<Lembrete>(Robo.Grade(tela)).Any(l => l.Tipo == TipoLembrete.RetornoLead),
                "lembretes do dia");

            var lembrete = Robo.Linhas<Lembrete>(Robo.Grade(tela)).First(l => l.Tipo == TipoLembrete.RetornoLead);
            robo.SelecionarLinha<Lembrete>(Robo.Grade(tela), l => l.Chave == lembrete.Chave);
            robo.Clicar(tela, "Resolvido");
            robo.Esperar(() => Robo.Linhas<Lembrete>(Robo.Grade(tela)).All(l => l.Chave != lembrete.Chave), "lembrete resolvido");
        });
}
