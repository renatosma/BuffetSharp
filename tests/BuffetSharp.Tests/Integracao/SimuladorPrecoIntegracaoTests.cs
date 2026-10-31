using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Simulador de preço do pacote no banco real.</summary>
[Collection(ColecaoBanco.Nome)]
public class SimuladorPrecoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Simula_o_pacote_e_aplica_o_preco_sugerido()
    {
        var servico = banco.Obter<SimuladorPrecoService>();
        var dados = await servico.DadosAsync(Ct);
        var prata = dados.Pacotes.Single(p => p.Nome == "Prata");
        var casamento = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.TiposEvento, ct: Ct)).Single(t => t.Nome == "Casamento").Id;
        var cenario = new SimulacaoInput
        {
            Itens = prata.Itens.ToList(), TipoEventoId = casamento, Convidados = 120, Horas = 5, PercentualAdultos = 90,
            Comissao = dados.ComissaoMedia, MargemAlvo = dados.Parametros.MargemMeta, PrecoAtual = prata.Preco
        };

        var resultado = SimuladorPrecoService.Simular(dados, cenario);

        Assert.Equal(120, resultado.Convidados);
        Assert.True(resultado.InsumosPorPessoa > 0 && resultado.EquipePorPessoa > 0);
        Assert.True(resultado.PrecoSugerido > resultado.PrecoMinimo);
        Assert.NotNull(resultado.MargemAtual);
        var equipe = dados.Parametros.CustoEquipe(casamento, 120);
        Assert.Equal(Math.Round(equipe / 120, 2), resultado.EquipePorPessoa); // dimensionamento do casamento
        var tabela = SimuladorPrecoService.Tabela(dados, cenario);
        Assert.Contains(tabela, r => r.Convidados == 120);
        Assert.True(tabela.First().CustoDiretoPorPessoa >= tabela.Last().CustoDiretoPorPessoa); // a equipe se dilui com mais gente

        // Cria um pacote com a composição e muda o preço dele pelo simulador.
        var id = await servico.CriarPacoteAsync($"Simulado {Guid.NewGuid():N}"[..20], cenario.Itens, resultado.PrecoMinimo!.Value, Ct);
        await servico.AplicarPrecoAsync(id, resultado.PrecoSugerido!.Value, Ct);
        var criado = (await servico.DadosAsync(Ct)).Pacotes.Single(p => p.Id == id);
        Assert.Equal((resultado.PrecoSugerido.Value, cenario.Itens.Count), (criado.Preco, criado.Itens.Count));
        await banco.Obter<CardapioService>().ExcluirPacoteAsync(id, Ct);
    }

    [Fact]
    public async Task So_administrador_e_financeiro_usam_o_simulador()
    {
        var sessao = banco.Obter<ISessaoUsuario>();
        var comercial = (await banco.Obter<UsuarioService>().ListarAsync(Ct)).Single(u => u.Login == "comercial");
        sessao.Iniciar(new UsuarioLogado(comercial.Id, comercial.Nome, comercial.Login, comercial.Perfil));
        try
        {
            await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<SimuladorPrecoService>().DadosAsync(Ct));
        }
        finally
        {
            sessao.Iniciar(new UsuarioLogado(1, "Administrador", "admin", PerfilUsuario.Administrador));
        }
    }
}
