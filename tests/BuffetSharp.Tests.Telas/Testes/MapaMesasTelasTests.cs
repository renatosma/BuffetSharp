using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Aba Mesas: o evento nasce com o mapa do salão, a mesa nova entra no mapa e o convidado é sentado na mesa escolhida.</summary>
public class MapaMesasTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Mesa_nova_e_convidado_sentado_na_aba_mesas() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var eventos = robo.Servico<EventoService>();
            var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
            var planta = robo.Rodar(() => robo.Servico<MapaMesasService>().ObterDoEspacoAsync(espaco.Id));
            var eventoId = robo.Rodar(() => eventos.SalvarAsync(new EventoInput
            {
                Titulo = "Mapa de mesas do teste de tela",
                ClienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id,
                TipoEventoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First().Id,
                EspacoId = espaco.Id,
                Data = DateOnly.FromDateTime(DateTime.Today).AddYears(4).AddDays(70),
                HoraInicio = new TimeOnly(9, 0),
                HoraFim = new TimeOnly(11, 0),
                Convidados = 40,
                ValorTotal = 5000m
            }));
            robo.Rodar(() => eventos.AdicionarConvidadoAsync(eventoId, "Helena Teste", null, null, 2));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            robo.SelecionarAba(evento, "Mesas");
            var mapa = Robo.Interno<MapaMesas>(evento, "_mapa");
            Assert.NotEmpty(planta);
            Assert.Equal(planta.Select(m => m.Numero), mapa.Mesas.Select(m => m.Numero));

            var proximo = planta.Max(m => m.Numero) + 1;
            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Nova mesa"), dialogo =>
            {
                robo.Preencher(dialogo, "Lugares", 6);
                robo.Preencher(dialogo, "Nome (opcional)", "Noivos");
                robo.Clicar(dialogo, "Salvar");
            }, "nova mesa");
            robo.Esperar(() => mapa.Mesas.Any(m => m.Nome == "Noivos"), "mesa nova no mapa");
            var noivos = mapa.Mesas.Single(m => m.Nome == "Noivos");
            Assert.Equal((proximo, 6), (noivos.Numero, noivos.Lugares));

            var semMesa = Robo.Interno<Grade<Convidado>>(evento, "_semMesa");
            robo.SelecionarLinha<Convidado>(semMesa.Controle, c => c.Nome == "Helena Teste");
            mapa.Selecionar(noivos.Id);
            robo.Clicar(evento, "Sentar na mesa");
            robo.Esperar(() => mapa.Mesas.Single(m => m.Id == noivos.Id).Ocupados == 3, "convidado sentado na mesa");

            var naMesa = Robo.Interno<Grade<Convidado>>(evento, "_naMesa");
            Assert.Equal(["Helena Teste"], naMesa.Itens.Select(c => c.Nome));
            Assert.DoesNotContain(semMesa.Itens, c => c.Nome == "Helena Teste");
        });
}
