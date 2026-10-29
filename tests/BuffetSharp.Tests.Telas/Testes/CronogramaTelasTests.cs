using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Cronograma na tela do evento: o padrão já vem no evento novo e a etapa incluída entra na grade.</summary>
public class CronogramaTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Etapa_incluida_na_aba_cronograma_do_evento() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
            var eventoId = robo.Rodar(() => robo.Servico<EventoService>().SalvarAsync(new EventoInput
            {
                Titulo = "Cronograma do teste de tela",
                ClienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id,
                TipoEventoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First().Id,
                EspacoId = espaco.Id,
                Data = DateOnly.FromDateTime(DateTime.Today).AddYears(4).AddDays(40),
                HoraInicio = new TimeOnly(9, 0),
                HoraFim = new TimeOnly(11, 0),
                Convidados = 40,
                ValorTotal = 5000m
            }));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            robo.SelecionarAba(evento, "Cronograma");
            var etapas = Robo.Interno<Grade<EventoEtapa>>(evento, "_etapas");
            Assert.Equal(11, etapas.Itens.Count);
            Assert.Contains("11 etapa(s)", Robo.Interno<Label>(evento, "_agora").Text);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Nova etapa"), dialogo =>
            {
                robo.Preencher(dialogo, "Atividade", "Sorteio de brindes");
                robo.Preencher(dialogo, "Local", "Pista");
                robo.Preencher(dialogo, "Ou responsável (texto)", "Cerimonial");
                robo.Clicar(dialogo, "Salvar");
            }, "nova etapa");
            robo.Esperar(() => etapas.Itens.Any(t => t.Atividade == "Sorteio de brindes"), "etapa na grade");
            var sorteio = etapas.Itens.Single(t => t.Atividade == "Sorteio de brindes");
            Assert.Equal(("Pista", "Cerimonial"), (sorteio.Local, sorteio.Responsavel));
            Assert.Equal(etapas.Itens.Where(t => t.Id != sorteio.Id).Max(t => t.Fim), sorteio.Horario);
        });
}
