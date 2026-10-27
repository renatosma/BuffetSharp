using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Fiscal;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Formularios.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>NFS-e pelas telas: configuração fiscal pedida na primeira emissão, nota simulada e a lista de notas.</summary>
public class NfseTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Primeira_nfse_pede_a_configuracao_fiscal_e_sai_autorizada_no_simulado() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var alvo = robo.Rodar(() => robo.Servico<IConsultasEventos>().ListarAsync(new FiltroEventos { Status = StatusEvento.Confirmado }))
                .Where(e => e.ValorTotal > 0).OrderBy(e => e.Id).First();

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{alvo.Id}", () => navegador.Criar<FormEventoDetalhe>(alvo.Id));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            // Sem configuração fiscal: a tela oferece abrir a configuração e segue para a emissão.
            robo.RespostaConfirmacao = "Sim";
            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "NFS-e"), dialogo =>
            {
                Assert.Equal("Configuração fiscal (NFS-e)", dialogo.Text);
                robo.Preencher(dialogo, "CNPJ", "11222333000181");
                robo.Preencher(dialogo, "Inscrição municipal", "1234567");
                robo.Preencher(dialogo, "Código IBGE do município", "3550308");
                robo.Clicar(dialogo, "Salvar");
            }, "configuração fiscal");
            Assert.Contains(robo.Mensagens, m => m.Texto.Contains("Configuração fiscal: falta CNPJ do buffet"));

            robo.Dialogo<FormEdicao>(() => { }, dialogo =>
            {
                Assert.Equal("Emitir NFS-e", dialogo.Text);
                Assert.Equal(Textos.Moeda(alvo.ValorTotal), Textos.Moeda(Robo.Campo<NumericUpDown>(dialogo, "Valor dos serviços").Value));
                Assert.Contains(alvo.Titulo, Robo.Campo<TextBox>(dialogo, "Descrição do serviço").Text);
                Assert.StartsWith("5% = ", Robo.Campo<Label>(dialogo, "ISS").Text);
                robo.Clicar(dialogo, "Emitir nota");
            }, "emitir NFS-e");
            robo.Esperar(() => robo.Mensagens.Any(m => m.Texto.StartsWith("NFS-e autorizada")), "nota autorizada");

            // Segunda vez: o evento já tem nota.
            robo.Clicar(evento, "NFS-e");
            robo.Esperar(() => robo.Mensagens.Any(m => m.Texto.Contains("autorizada em")), "aviso de nota existente");

            var lista = robo.AbrirMenu<FormNotasServico>(principal, "Notas fiscais de serviço");
            robo.Esperar(() => Robo.Carregado(lista) && Robo.Linhas<NotaServicoResumo>(Robo.Grade(lista)).Any(n => n.EventoId == alvo.Id),
                "nota na lista");
            var nota = Robo.Linhas<NotaServicoResumo>(Robo.Grade(lista)).Single(n => n.EventoId == alvo.Id);
            Assert.Equal((StatusNfse.Autorizada, AmbienteNfse.Simulado, alvo.ValorTotal), (nota.Status, nota.Ambiente, nota.ValorServicos));

            robo.SelecionarLinha<NotaServicoResumo>(Robo.Grade(lista), n => n.Id == nota.Id);
            robo.Dialogo<FormEdicao>(() => robo.Clicar(lista, "Cancelar nota"), dialogo =>
            {
                robo.Preencher(dialogo, "Motivo (mínimo de 15 caracteres)", "Nota emitida para teste da tela");
                robo.Clicar(dialogo, "Salvar");
            }, "cancelar NFS-e");
            robo.Esperar(() => Robo.Linhas<NotaServicoResumo>(Robo.Grade(lista)).Single(n => n.Id == nota.Id).Status == StatusNfse.Cancelada,
                "nota cancelada");
        });
}
