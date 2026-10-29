using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Aba Fornecedores: a contratação feita pelo diálogo entra na grade com a chegada no cronograma.</summary>
public class FornecedoresEventoTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Fornecedor_contratado_na_aba_fornecedores() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
            var eventoId = robo.Rodar(() => robo.Servico<EventoService>().SalvarAsync(new EventoInput
            {
                Titulo = "Fornecedores do teste de tela",
                ClienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id,
                TipoEventoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First().Id,
                EspacoId = espaco.Id,
                Data = DateOnly.FromDateTime(DateTime.Today).AddYears(4).AddDays(100),
                HoraInicio = new TimeOnly(9, 0),
                HoraFim = new TimeOnly(11, 0),
                Convidados = 40,
                ValorTotal = 5000m
            }));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            robo.SelecionarAba(evento, "Fornecedores");
            var contratacoes = Robo.Interno<Grade<EventoFornecedor>>(evento, "_contratacoes");
            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Contratar"), dialogo =>
            {
                robo.Escolher(dialogo, "Fornecedor", "Som & Luz");
                robo.Preencher(dialogo, "Serviço", "Bolo de três andares");
                robo.Preencher(dialogo, "Valor combinado (R$)", 850m);
                Robo.Encontrar<CheckBox>(dialogo, c => c.Text.StartsWith("Tem horário de chegada")).Checked = true;
                robo.Preencher(dialogo, "Responsável no dia", "Juliana");
                robo.Clicar(dialogo, "Salvar");
            }, "contratar fornecedor");
            robo.Esperar(() => contratacoes.Itens.Any(f => f.Servico == "Bolo de três andares"), "contratação na grade");

            var bolo = contratacoes.Itens.Single();
            Assert.Equal(("Som & Luz", 850m, "Juliana"), (bolo.NomeFornecedor, bolo.Valor, bolo.Responsavel));
            Assert.Single(bolo.Pagamento!.Parcelas);
            Assert.NotNull(bolo.Chegada);
            Assert.Contains("Custo dos fornecedores", Robo.Interno<Label>(evento, "_resumoFornecedores").Text);
        });
}
