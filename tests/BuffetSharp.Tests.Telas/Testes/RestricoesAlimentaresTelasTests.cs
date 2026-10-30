using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Restrição alimentar de um convidado pela central do evento: aparece na lista e o cardápio mostra a troca.</summary>
public class RestricoesAlimentaresTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Convidado_vegano_aparece_na_lista_e_no_cardapio_com_o_substituto() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var eventoId = robo.Rodar(async () =>
            {
                await using var db = robo.Servico<IBuffetDbContextFactory>().Criar();
                return await db.Eventos.Where(e => e.RestricoesAvulsas.Any()).Select(e => e.Id).FirstAsync();
            });

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;
            robo.SelecionarAba(evento, "Convidados");

            var convidados = Robo.Interno<Grade<Convidado>>(evento, "_convidados");
            robo.Esperar(() => convidados.Itens.Any(c => c.DescricaoRestricao is null), "convidado sem restrição");
            var convidado = convidados.Itens.First(c => c.DescricaoRestricao is null && c.Rsvp != StatusRsvp.Recusado);
            robo.SelecionarLinha<Convidado>(convidados.Controle, c => c.Id == convidado.Id);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Restrição alimentar"), dialogo =>
            {
                Robo.Descendentes(dialogo).OfType<CheckBox>().Single(c => c.Text == "Vegano").Checked = true;
                robo.Preencher(dialogo, "Outra restrição ou alergia", "alergia a kiwi");
                robo.Clicar(dialogo, "Salvar");
            }, "restrição do convidado");

            robo.Esperar(() => convidados.Itens.Single(c => c.Id == convidado.Id).DescricaoRestricao == "Vegano; alergia a kiwi",
                "restrição na lista de convidados");
            Assert.Contains("restrições:", Robo.Interno<Label>(evento, "_resumoConvidados").Text);
            var cardapio = Robo.Interno<ListBox>(evento, "_cardapioLista").Items.Cast<string>().ToList();
            Assert.Contains("1 vegano:", cardapio);
            Assert.Contains(cardapio, l => l.Contains("→ Legumes grelhados"));
        });
}
