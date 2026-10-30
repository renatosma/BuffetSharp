using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Sobras e desperdício no banco real: registro, retorno de insumos, lembrete, relatório e ajuste da ficha.</summary>
[Collection(ColecaoBanco.Nome)]
public class SobrasIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>O evento encerrado mais recente (a demonstração o deixa sem as sobras registradas).</summary>
    private async Task<(int Id, DateTime Fim)> UltimoEncerradoAsync()
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var evento = await db.Eventos.Where(e => e.Status == StatusEvento.Realizado && e.PacoteId != null)
            .OrderByDescending(e => e.Inicio).Select(e => new { e.Id, e.Fim }).FirstAsync(Ct);
        return (evento.Id, evento.Fim);
    }

    [Fact]
    public async Task Registra_as_sobras_devolve_o_nao_usado_e_sai_do_lembrete()
    {
        var (eventoId, fim) = await UltimoEncerradoAsync();
        var relogio = new FakeTimeProvider(new DateTimeOffset(fim.AddDays(1), TimeSpan.Zero));
        var servico = new SobrasService(banco.Obter<IBuffetDbContextFactory>(), banco.Obter<BuffetOptions>(), new LembretesOptions(),
            banco.Obter<ISessaoUsuario>(), relogio);
        Assert.Contains(await servico.LembretesAsync(Ct), l => l.Tipo == TipoLembrete.SobrasPendentes && l.EventoId == eventoId);

        var input = await servico.PrepararAsync(eventoId, Ct);
        Assert.True(input.Encerrado);
        Assert.Null(input.RegistradasEm);
        Assert.NotEmpty(input.Pratos);
        Assert.All(input.Pratos, p => Assert.Equal(input.Pessoas, p.Porcoes));
        var prato = input.Pratos[0];
        prato.Percentual = 10;
        prato.Destino = DestinoSobra.Descarte;
        var doado = input.Pratos[1];
        doado.Percentual = 20;
        doado.Destino = DestinoSobra.Doacao;
        doado.Instituicao = "Lar São Vicente";
        var insumo = input.Insumos.First(i => !i.Perecivel && i.NaoDevolvido >= 2);
        insumo.Voltar = 2;
        var perecivel = input.Insumos.FirstOrDefault(i => i.Perecivel);
        var estoqueAntes = await EstoqueAsync(insumo.ProdutoId);

        await servico.RegistrarAsync(input, Ct);

        var registrado = await servico.PrepararAsync(eventoId, Ct);
        Assert.NotNull(registrado.RegistradasEm);
        Assert.Equal((10m, DestinoSobra.Descarte), (registrado.Pratos[0].Percentual, registrado.Pratos[0].Destino));
        Assert.Equal("Lar São Vicente", registrado.Pratos[1].Instituicao);
        Assert.Equal(insumo.JaVoltou + 2, registrado.Insumos.Single(i => i.ProdutoId == insumo.ProdutoId).JaVoltou);
        Assert.Equal(estoqueAntes + 2, await EstoqueAsync(insumo.ProdutoId));
        Assert.DoesNotContain(await servico.LembretesAsync(Ct), l => l.EventoId == eventoId);

        if (perecivel is not null)
        {
            perecivel.Voltar = Math.Min(1, perecivel.NaoDevolvido);
            await Assert.ThrowsAsync<RegraNegocioException>(() => servico.RegistrarAsync(new SobrasInput { EventoId = eventoId, Pratos = registrado.Pratos, Insumos = [perecivel] }, Ct));
        }

        var comparativo = await banco.Obter<MargemService>().ComparativoAsync(eventoId, Ct);
        Assert.Equal(Math.Round(prato.Custo, 2), comparativo.Desperdicio);

        var eventos = await servico.EventosAsync(Hoje.AddYears(-1), Hoje, Ct);
        var desteEvento = eventos.Single(e => e.EventoId == eventoId);
        Assert.Equal(Math.Round(prato.Custo, 2), desteEvento.Desperdicio);
        Assert.True(desteEvento.CustoInsumos > 0);
    }

    private async Task<decimal> EstoqueAsync(int produtoId)
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        return await db.Produtos.Where(p => p.Id == produtoId).Select(p => p.EstoqueAtual).SingleAsync(Ct);
    }

    [Fact]
    public async Task Relatorio_agrupa_e_o_prato_que_sobra_muito_vira_sugestao()
    {
        var servico = banco.Obter<SobrasService>();
        var eventos = await servico.EventosAsync(Hoje.AddYears(-1), Hoje, Ct);
        Assert.NotEmpty(eventos);

        var porPrato = SobrasService.Agrupar(eventos, AgrupamentoDesperdicio.Prato);
        Assert.All(porPrato, g => Assert.Null(g.PercentualDesperdicio)); // por prato não há o custo total dos insumos
        var batata = porPrato.SingleOrDefault(g => g.Grupo == "Batata gratinada"); // a demonstração faz a batata sobrar 18% a 26%
        var porMes = SobrasService.Agrupar(eventos, AgrupamentoDesperdicio.Mes);
        Assert.Equal(eventos.Sum(e => e.Desperdicio), porMes.Sum(g => g.Desperdicio));
        Assert.Equal(porMes.Select(g => g.Grupo).Order(), porMes.Select(g => g.Grupo));

        if (batata is { Eventos: >= 3 })
        {
            var sugestao = Assert.Single(await servico.SugestoesAsync(Ct), s => s.Prato == "Batata gratinada");
            Assert.InRange(sugestao.Fator, 0.75m, 0.88m);
            Assert.Contains("Batata", sugestao.Resumo);
        }
    }

    [Fact]
    public async Task Aplicar_reduz_a_ficha_e_recomeca_a_contagem()
    {
        var cardapio = banco.Obter<CardapioService>();
        var lookups = banco.Obter<IConsultasLookup>();
        var produto = (await lookups.ListarAsync(TipoLookup.Produtos, ct: Ct)).Single(p => p.Nome == "Batata");
        var id = await cardapio.SalvarItemAsync(new ItemCardapioInput
        {
            Nome = $"Purê {Guid.NewGuid():N}"[..20],
            CategoriaCardapioId = (await lookups.ListarAsync(TipoLookup.CategoriasCardapio, ct: Ct)).First().Id,
            Ingredientes = [new IngredienteInput { ProdutoId = produto.Id, QuantidadePorPessoa = 0.2m }]
        }, Ct);

        await banco.Obter<SobrasService>().AplicarAsync(id, 0.85m, Ct);

        Assert.Equal(0.17m, (await cardapio.ObterItemAsync(id, Ct)).Ingredientes.Single().QuantidadePorPessoa);
        await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<SobrasService>().AplicarAsync(id, 1.2m, Ct));
        await cardapio.ExcluirItemAsync(id, Ct);
    }
}
