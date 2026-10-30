using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Sobras pela central do evento encerrado: o prato com sobra e o destino ficam registrados.</summary>
public class SobrasTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Registra_a_sobra_de_um_prato_do_evento_encerrado() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var eventoId = robo.Rodar(async () =>
            {
                await using var db = robo.Servico<IBuffetDbContextFactory>().Criar();
                return await db.Eventos.Where(e => e.Status == StatusEvento.Realizado && e.PacoteId != null)
                    .OrderByDescending(e => e.Inicio).Select(e => e.Id).FirstAsync();
            });

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            string prato = "";
            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Sobras"), dialogo =>
            {
                var pratos = Robo.Descendentes(dialogo).OfType<DataGridView>().First();
                var linha = pratos.Rows[0];
                prato = (string)linha.Cells[1].Value!;
                linha.Cells[3].Value = 12m;
                robo.Clicar(dialogo, "Registrar sobras");
            }, "sobras do evento");

            var registradas = robo.Rodar(() => robo.Servico<SobrasService>().PrepararAsync(eventoId));
            Assert.NotNull(registradas.RegistradasEm);
            Assert.Equal((12m, DestinoSobra.Descarte), (registradas.Pratos.Single(p => p.Prato == prato).Percentual,
                registradas.Pratos.Single(p => p.Prato == prato).Destino));
        });
}
