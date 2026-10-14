using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Executa todas as consultas Dapper contra a base de demonstração.</summary>
[Collection(ColecaoBanco.Nome)]
public class ConsultasDapperTests(BancoFixture banco)
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(TipoLookup.Clientes)]
    [InlineData(TipoLookup.TiposEvento)]
    [InlineData(TipoLookup.Espacos)]
    [InlineData(TipoLookup.Pacotes)]
    [InlineData(TipoLookup.ServicosAdicionais)]
    [InlineData(TipoLookup.FormasPagamento)]
    [InlineData(TipoLookup.Fornecedores)]
    [InlineData(TipoLookup.Funcionarios)]
    [InlineData(TipoLookup.Cargos)]
    [InlineData(TipoLookup.CategoriasProduto)]
    [InlineData(TipoLookup.CategoriasCardapio)]
    [InlineData(TipoLookup.UnidadesMedida)]
    [InlineData(TipoLookup.Produtos)]
    [InlineData(TipoLookup.ItensCardapio)]
    [InlineData(TipoLookup.EventosConfirmados)]
    public async Task Lookups_retornam_dados(TipoLookup tipo)
    {
        var itens = await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct);
        Assert.NotEmpty(itens);
        Assert.All(itens, i => Assert.False(string.IsNullOrWhiteSpace(i.Nome)));
    }

    [Fact]
    public async Task Dashboard_traz_indicadores_e_series()
    {
        var resumo = await banco.Obter<IConsultasDashboard>().ObterAsync(Hoje, Ct);

        Assert.Equal(12, resumo.FaturamentoMensal.Count);
        Assert.True(resumo.FaturamentoMensal.Sum(m => m.Valor) > 0);
        Assert.True(resumo.FaturamentoMensal.Sum(m => m.Valor2) > 0);
        Assert.NotEmpty(resumo.ProximosEventos);
        Assert.All(resumo.ProximosEventos, e => Assert.True(e.Inicio >= Hoje.ToDateTime(TimeOnly.MinValue)));
        Assert.InRange(resumo.TaxaConversao90Dias, 0, 100);
    }

    [Fact]
    public async Task Agenda_lista_eventos_do_periodo()
    {
        var inicio = Hoje.AddDays(-60).ToDateTime(TimeOnly.MinValue);
        var itens = await banco.Obter<IConsultasAgenda>().ListarAsync(inicio, inicio.AddDays(120), ct: Ct);

        Assert.NotEmpty(itens);
        Assert.DoesNotContain(itens, i => i.Status == StatusEvento.Cancelado);
    }

    [Fact]
    public async Task Listagens_operacionais_funcionam_com_e_sem_filtros()
    {
        var comercial = banco.Obter<IConsultasComercial>();
        var orcamentos = await comercial.ListarOrcamentosAsync(new FiltroOrcamentos(), Ct);
        Assert.NotEmpty(orcamentos);
        var filtrados = await comercial.ListarOrcamentosAsync(new FiltroOrcamentos { Texto = orcamentos[0].Cliente[..4] }, Ct);
        Assert.NotEmpty(filtrados);

        var historico = await comercial.HistoricoClienteAsync(1, Ct);
        Assert.NotNull(historico);

        var eventos = await banco.Obter<IConsultasEventos>().ListarAsync(new FiltroEventos { Status = StatusEvento.Realizado }, Ct);
        Assert.NotEmpty(eventos);
        Assert.All(eventos, e => Assert.Equal(StatusEvento.Realizado, e.Status));

        var financeiro = banco.Obter<IConsultasFinanceiro>();
        var parcelas = await financeiro.ListarParcelasAsync(new FiltroTitulos(), Hoje, Ct);
        Assert.Contains(parcelas, p => p.Status == StatusTitulo.Pago);
        var vencidas = await financeiro.ListarParcelasAsync(new FiltroTitulos { SomenteVencidos = true }, Hoje, Ct);
        Assert.All(vencidas, p => Assert.True(p.DiasAtraso > 0));
        Assert.NotEmpty(await financeiro.ListarContasPagarAsync(new FiltroTitulos { Status = StatusTitulo.Aberto }, Hoje, Ct));

        var estoque = banco.Obter<IConsultasEstoqueCardapio>();
        Assert.NotEmpty(await estoque.ListarMovimentacoesAsync(DateTime.Today.AddYears(-1), DateTime.Today, ct: Ct));
        Assert.NotEmpty(await estoque.ListarItensCardapioAsync(Ct));
        var pacotes = await estoque.ListarPacotesAsync(Ct);
        Assert.All(pacotes, p => Assert.True(p.CustoPorPessoa > 0 && p.CustoPorPessoa < p.PrecoPorPessoa));
        Assert.NotEmpty(await estoque.ComposicaoPacoteAsync(pacotes[0].Id, Ct));
    }

    [Fact]
    public async Task Relatorios_gerenciais_funcionam()
    {
        var relatorios = banco.Obter<IConsultasRelatorios>();
        var de = DateTime.Today.AddYears(-1);
        var ate = DateTime.Today.AddMonths(6);

        Assert.NotEmpty(await relatorios.FaturamentoPorTipoAsync(de, ate, Ct));
        Assert.NotEmpty(await relatorios.RankingPacotesAsync(de, ate, Ct));
        var lucro = await relatorios.LucratividadeAsync(de, ate, Ct);
        Assert.NotEmpty(lucro);
        Assert.Contains(lucro, l => l.CustoInsumos > 0 && l.CustoEquipe > 0);
        var fluxo = await relatorios.FluxoCaixaAsync(de, ate, Ct);
        Assert.Equal(19, fluxo.Count);
        Assert.NotEmpty(await relatorios.ConversaoOrcamentosAsync(de.AddYears(-1), ate, Ct));
    }
}
