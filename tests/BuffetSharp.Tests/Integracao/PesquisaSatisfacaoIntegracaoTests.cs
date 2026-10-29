using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Pesquisa de satisfação no banco: envio, resposta, alerta do detrator, pedido de avaliação e o painel com o NPS.</summary>
[Collection(ColecaoBanco.Nome)]
public class PesquisaSatisfacaoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public async Task Pesquisa_respondida_alimenta_o_painel_e_o_detrator_vira_alerta()
    {
        var pesquisas = banco.Obter<PesquisaService>();
        var painel = await pesquisas.PainelAsync(Hoje.AddDays(-150), Hoje, Ct);
        Assert.True(painel.Respondidas > 0);
        Assert.NotNull(painel.Nps);
        Assert.Equal(painel.Respondidas, painel.Promotores + painel.Neutros + painel.Detratores);
        Assert.NotEmpty(painel.PorTipo);
        Assert.All(painel.Comentarios, c => Assert.NotNull(c.Comentario));

        // Os eventos realizados mais recentes da base ainda não têm resposta.
        var pendentes = (await pesquisas.ListarAsync(Hoje.AddDays(-80), Hoje, Ct)).Where(p => p.RespondidaEm is null).ToList();
        Assert.True(pendentes.Count >= 2);
        var (insatisfeito, fa) = (pendentes[0], pendentes[1]);

        await pesquisas.RegistrarEnvioAsync(insatisfeito.EventoId, Ct);
        await pesquisas.RegistrarRespostaAsync(insatisfeito.EventoId, new RespostaPesquisaInput
        {
            Nps = 3, Comida = 2, Atendimento = 3, Espaco = 4, Organizacao = 2, Comentario = "O jantar atrasou muito."
        }, Ct);
        var alerta = (await pesquisas.LembretesAsync(Ct)).Single(l => l.EventoId == insatisfeito.EventoId);
        Assert.Equal((TipoLembrete.Detrator, "O jantar atrasou muito."), (alerta.Tipo, alerta.Detalhe));
        Assert.Contains(await banco.Obter<LembreteService>().ListarAsync(Ct), l => l.Chave == alerta.Chave);

        await banco.Obter<LembreteService>().MarcarTratadoAsync(alerta, CanalLembrete.Manual, Ct);
        Assert.DoesNotContain(await pesquisas.LembretesAsync(Ct), l => l.EventoId == insatisfeito.EventoId);
        var tratado = (await pesquisas.ListarAsync(Hoje.AddDays(-80), Hoje, Ct)).Single(p => p.EventoId == insatisfeito.EventoId);
        Assert.Equal((3, CategoriaNps.Detrator, false), (tratado.Nps, tratado.Categoria, tratado.DetratorPendente));
        Assert.StartsWith("Resolvido nos lembretes", tratado.TratativaDetrator);

        await pesquisas.RegistrarRespostaAsync(fa.EventoId, new RespostaPesquisaInput { Nps = 10, Comida = 5 }, Ct);
        await pesquisas.RegistrarPedidoAvaliacaoAsync(fa.EventoId, Ct);
        var promotor = (await pesquisas.ListarAsync(Hoje.AddDays(-80), Hoje, Ct)).Single(p => p.EventoId == fa.EventoId);
        Assert.NotNull(promotor.AvaliacaoGooglePedidaEm);
        Assert.NotNull(promotor.RespondidaEm);

        // Só depois de realizado.
        var confirmado = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.EventosConfirmados, ct: Ct)).First();
        Assert.Contains("realizado", (await Assert.ThrowsAsync<RegraNegocioException>(() =>
            pesquisas.RegistrarRespostaAsync(confirmado.Id, new RespostaPesquisaInput { Nps = 9 }, Ct))).Message);
    }
}
