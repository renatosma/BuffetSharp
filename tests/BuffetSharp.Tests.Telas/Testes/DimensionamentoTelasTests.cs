using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Dimensionamento na aba Equipe: mostra quanto falta e completa a equipe com quem está livre.</summary>
public class DimensionamentoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Aba_equipe_mostra_o_dimensionado_e_completa_a_equipe() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
            var eventoId = robo.Rodar(() => robo.Servico<EventoService>().SalvarAsync(new EventoInput
            {
                Titulo = "Equipe do teste de tela",
                ClienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id,
                TipoEventoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).Single(t => t.Nome == "Casamento").Id,
                EspacoId = espaco.Id,
                Data = DateOnly.FromDateTime(DateTime.Today).AddYears(4).AddDays(200),
                HoraInicio = new TimeOnly(9, 0),
                HoraFim = new TimeOnly(11, 0),
                Convidados = 40,
                ValorTotal = 5000m
            }));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;
            robo.SelecionarAba(evento, "Equipe");

            var dimensionado = Robo.Interno<Label>(evento, "_dimensionamento");
            robo.Esperar(() => dimensionado.Text.StartsWith("Dimensionado"), "dimensionamento da equipe");
            Assert.Contains("Garçom 0/4", dimensionado.Text);
            Assert.Contains("faltam", dimensionado.Text);

            robo.Clicar(evento, "Completar equipe");
            robo.Esperar(() => robo.Mensagens.Any(m => m.Titulo == "Completar equipe"), "resultado de completar a equipe");
            var equipe = Robo.Interno<Grade<EventoFuncionario>>(evento, "_equipe");
            robo.Esperar(() => equipe.Itens.Count > 0, "equipe escalada");
            Assert.DoesNotContain("Garçom 0/4", dimensionado.Text);
        });
}
