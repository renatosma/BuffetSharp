using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cardapio;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Restrições alimentares: cardápio com selos e substitutos, convidados, importação e produção no banco real.</summary>
[Collection(ColecaoBanco.Nome)]
public class RestricoesAlimentaresIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>O evento da demonstração com restrições (3 convidados e 4 vegetarianos sem nome).</summary>
    private async Task<int> EventoComRestricoesAsync()
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        return await db.Eventos.Where(e => e.RestricoesAvulsas.Any()).Select(e => e.Id).FirstAsync(Ct);
    }

    [Fact]
    public async Task Cardapio_mostra_para_quem_cada_prato_serve_e_o_substituto()
    {
        var consultas = banco.Obter<IConsultasEstoqueCardapio>();
        var itens = await consultas.ListarItensCardapioAsync(Ct);
        var file = itens.Single(i => i.Nome == "Filé ao molho madeira");
        Assert.Equal((ConteudoAlimentar.Carne | ConteudoAlimentar.Lactose, "Risoto de cogumelos"), (file.Contem, file.Substituto));
        Assert.Equal("Legumes grelhados", itens.Single(i => i.Nome == "Risoto de cogumelos").Substituto);

        var prata = (await consultas.ListarPacotesAsync(Ct)).Single(p => p.Nome == "Prata");
        var composicao = await consultas.ComposicaoPacoteAsync(prata.Id, Ct);
        Assert.Equal("vegano • sem glúten • sem lactose", composicao.Single(i => i.Item == "Arroz branco").Selos);
        Assert.Equal("vegetariano • sem glúten • contém: lactose, soja", composicao.Single(i => i.Item == "Salada caprese").Selos);
        Assert.Contains("contém: glúten", composicao.Single(i => i.Item == "Coxinha de frango").Selos);
    }

    [Fact]
    public async Task Prato_com_substituto_e_o_que_contem_alem_dos_insumos_e_exclusao_do_substituto()
    {
        var servico = banco.Obter<CardapioService>();
        var lookups = banco.Obter<IConsultasLookup>();
        var secao = (await lookups.ListarAsync(TipoLookup.CategoriasCardapio, ct: Ct)).First().Id;
        var arroz = (await lookups.ListarAsync(TipoLookup.Produtos, ct: Ct)).Single(p => p.Nome == "Arroz agulhinha");
        var marca = Guid.NewGuid().ToString("N")[..6];

        var substitutoId = await servico.SalvarItemAsync(new ItemCardapioInput
        {
            Nome = $"Substituto {marca}", CategoriaCardapioId = secao,
            Ingredientes = [new IngredienteInput { ProdutoId = arroz.Id, QuantidadePorPessoa = 0.1m }]
        }, Ct);
        var pratoId = await servico.SalvarItemAsync(new ItemCardapioInput
        {
            Nome = $"Prato {marca}", CategoriaCardapioId = secao, ContemInformado = ConteudoAlimentar.Gluten, SubstitutoId = substitutoId,
            Ingredientes = [new IngredienteInput { ProdutoId = arroz.Id, QuantidadePorPessoa = 0.1m }]
        }, Ct);

        var prato = await servico.ObterItemAsync(pratoId, Ct);
        Assert.Equal((ConteudoAlimentar.Gluten, substitutoId), (prato.ContemInformado, prato.SubstitutoId));
        var resumo = (await banco.Obter<IConsultasEstoqueCardapio>().ListarItensCardapioAsync(Ct)).Single(i => i.Id == pratoId);
        Assert.Equal((ConteudoAlimentar.Gluten, $"Substituto {marca}"), (resumo.Contem, resumo.Substituto));

        prato.SubstitutoId = pratoId;
        await Assert.ThrowsAsync<Domain.Comum.RegraNegocioException>(() => servico.SalvarItemAsync(prato, Ct));

        await servico.ExcluirItemAsync(substitutoId, Ct); // quem o tinha de substituto fica sem
        Assert.Null((await servico.ObterItemAsync(pratoId, Ct)).SubstitutoId);
        await servico.ExcluirItemAsync(pratoId, Ct);
    }

    [Fact]
    public async Task Ordem_de_producao_soma_os_substitutos_e_diz_quem_tem_restricao()
    {
        var eventoId = await EventoComRestricoesAsync();
        var ordem = await banco.Obter<PlanejamentoService>().OrdemDoEventoAsync(eventoId, Ct);

        var vegetarianos = ordem.Restricoes.Single(r => r.Restricao == "Vegetariano");
        Assert.Equal(5, vegetarianos.Pessoas); // 1 da lista e 4 sem nome
        Assert.Contains(vegetarianos.Quem, q => q.StartsWith("4 sem nome na lista (mesa dos amigos da faculdade)"));
        Assert.Contains(vegetarianos.Trocas, t => t.EndsWith("→ Risoto de cogumelos"));
        var vegano = ordem.Restricoes.Single(r => r.Sigla == "VGN+SG");
        Assert.Equal(1, vegano.Pessoas);
        Assert.Contains(vegano.Trocas, t => t.EndsWith("→ Legumes grelhados"));
        Assert.Contains(ordem.ObservacoesRestricao, o => o.EndsWith("alergia a kiwi"));

        var risoto = ordem.Pratos.Single(p => p.Prato == "Risoto de cogumelos");
        Assert.True(risoto.Substituto);
        Assert.True(risoto.Porcoes >= 5 && risoto.Porcoes % 5 == 0); // 5 vegetarianos para cada prato de carne do pacote
        Assert.True(ordem.Pratos.Single(p => p.Prato == "Legumes grelhados").Porcoes >= 1);
        Assert.All(ordem.Pratos.Where(p => !p.Substituto), p => Assert.True(p.Porcoes <= ordem.Pessoas));
    }

    [Fact]
    public async Task Importacao_reconhece_as_restricoes_e_o_resto_vira_observacao()
    {
        var eventoId = await EventoComRestricoesAsync();
        var eventos = banco.Obter<EventoService>();
        var marca = Guid.NewGuid().ToString("N")[..6];

        var importados = await eventos.ImportarConvidadosAsync(eventoId,
            $"Zeca {marca};;0;;vegano, sem glúten\nLia {marca};;;;celíaca, alergia a kiwi\nTeo {marca}", Ct);

        Assert.Equal(3, importados);
        var convidados = (await eventos.ObterDetalheAsync(eventoId, Ct)).Evento.Convidados.Where(c => c.Nome.EndsWith(marca)).ToList();
        Assert.Equal(RestricaoAlimentar.Vegano | RestricaoAlimentar.SemGluten, convidados.Single(c => c.Nome.StartsWith("Zeca")).Restricoes);
        var lia = convidados.Single(c => c.Nome.StartsWith("Lia"));
        Assert.Equal((RestricaoAlimentar.SemGluten, "alergia a kiwi"), (lia.Restricoes, lia.ObservacaoRestricao));
        Assert.Null(convidados.Single(c => c.Nome.StartsWith("Teo")).DescricaoRestricao);

        foreach (var convidado in convidados)
            await eventos.RemoverConvidadoAsync(eventoId, convidado.Id, Ct);
    }

    [Fact]
    public async Task Restricoes_avulsas_e_do_convidado_gravadas_e_consumo_com_substitutos()
    {
        var eventoId = await EventoComRestricoesAsync();
        var eventos = banco.Obter<EventoService>();
        var antes = (await eventos.ObterDetalheAsync(eventoId, Ct)).Evento;
        var convidado = antes.Convidados.First(c => c.Restricoes == RestricaoAlimentar.Nenhuma && c.ObservacaoRestricao is null);

        await eventos.DefinirRestricoesConvidadoAsync(eventoId, convidado.Id, RestricaoAlimentar.SemLactose, "sem pimenta", Ct);
        await eventos.DefinirRestricoesAvulsasAsync(eventoId,
            [(RestricaoAlimentar.Vegetariano, 4, "mesa dos amigos da faculdade"), (RestricaoAlimentar.SemFrutosDoMar, 2, null)], Ct);

        var evento = (await eventos.ObterDetalheAsync(eventoId, Ct)).Evento;
        Assert.Equal("Sem lactose; sem pimenta", evento.Convidados.Single(c => c.Id == convidado.Id).DescricaoRestricao);
        Assert.Equal(2, evento.RestricoesAvulsas.Count);
        var plano = evento.PlanejarCardapio(evento.QuantidadeConvidados);
        var semRestricao = evento.Pacote!.CalcularConsumo(evento.QuantidadeConvidados);
        Assert.NotEqual(semRestricao.OrderBy(c => c.Key), plano.Consumo().OrderBy(c => c.Key)); // os substitutos mudam os insumos

        // Volta como a demonstração deixou.
        await eventos.DefinirRestricoesConvidadoAsync(eventoId, convidado.Id, RestricaoAlimentar.Nenhuma, null, Ct);
        await eventos.DefinirRestricoesAvulsasAsync(eventoId, [(RestricaoAlimentar.Vegetariano, 4, "mesa dos amigos da faculdade")], Ct);
    }
}
