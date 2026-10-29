using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Convites de agenda: a central do evento abre a lista com o convite do cliente pendente.</summary>
public class ConvitesAgendaTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Convites_de_agenda_do_evento() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
            var eventoId = robo.Rodar(() => robo.Servico<EventoService>().SalvarAsync(new EventoInput
            {
                Titulo = "Convites do teste de tela",
                ClienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id,
                TipoEventoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First().Id,
                EspacoId = espaco.Id,
                Data = DateOnly.FromDateTime(DateTime.Today).AddYears(4).AddDays(160),
                HoraInicio = new TimeOnly(9, 0),
                HoraFim = new TimeOnly(11, 0),
                Convidados = 40,
                ValorTotal = 5000m
            }));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            robo.Dialogo<FormConvitesEvento>(() => robo.Clicar(evento, "Convites de agenda"), dialogo =>
            {
                var grade = Robo.Interno<Grade<ConviteAgenda>>(dialogo, "_grade");
                robo.Esperar(() => grade.Itens.Count > 0, "convites do evento");
                var cliente = Assert.Single(grade.Itens);
                Assert.Equal((DestinoConvite.Cliente, "Não enviado"), (cliente.Destino, cliente.Situacao));
                Assert.Contains("1 convite(s)", Robo.Interno<Label>(dialogo, "_resumo").Text);
                robo.Clicar(dialogo, "Fechar");
            }, "convites de agenda");
        });
}
