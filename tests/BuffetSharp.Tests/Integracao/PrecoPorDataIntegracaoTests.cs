using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Preço por data no banco: o orçamento sem valor negociado sai com o ajuste da data, e a data especial exige o mínimo.</summary>
[Collection(ColecaoBanco.Nome)]
public class PrecoPorDataIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Orcamento_sai_com_o_preco_da_data_e_o_minimo_de_convidados()
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var precos = banco.Obter<TabelaPrecoService>();
        var orcamentos = banco.Obter<OrcamentoService>();

        // As regras do teste valem só num salão próprio, para não mexer nos preços dos outros testes.
        var espaco = await banco.Obter<ICadastroService<Espaco>>().SalvarAsync(new Espaco
        {
            Nome = $"Salão preços {Guid.NewGuid():N}"[..30], CapacidadeMaxima = 300, ValorLocacao = 2000
        }, Ct);
        var sabado = DateOnly.FromDateTime(DateTime.Today).AddYears(4);
        // Longe do fim de ano: o réveillon da base de demonstração é exclusivo e tiraria as regras do teste do dia.
        while (sabado.DayOfWeek != DayOfWeek.Saturday || (sabado.Month == 12 && sabado.Day >= 24))
            sabado = sabado.AddDays(1);
        var regras = banco.Obter<ICadastroService<RegraPreco>>();
        var regraSabado = new RegraPreco { Nome = "Sábado no salão", Tipo = TipoRegraPreco.DiaSemana, Percentual = 20, AjustaLocacao = true, EspacoId = espaco.Id };
        regraSabado.DefinirDia(DayOfWeek.Saturday, true);
        await regras.SalvarAsync(regraSabado, Ct);
        await regras.SalvarAsync(new RegraPreco
        {
            Nome = "Formatura", Tipo = TipoRegraPreco.DataEspecial, Percentual = 30, Data = sabado.AddDays(7), MinimoConvidados = 150,
            Exclusiva = true, EspacoId = espaco.Id
        }, Ct);

        var pacote = (await lookups.ListarAsync(TipoLookup.Pacotes, ct: Ct)).OrderBy(p => p.Numero).First();
        var ajuste = await precos.AjusteAsync(sabado, pacote.Id, espaco.Id, Ct);
        Assert.Contains("Sábado no salão +20%", ajuste.Descricao);

        var input = orcamentos.Novo();
        input.ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id;
        input.TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id;
        input.PacoteId = pacote.Id;
        input.EspacoId = espaco.Id;
        input.Data = sabado;
        input.Convidados = Math.Max(pacote.Numero, 100);
        var id = await orcamentos.SalvarAsync(input, Ct);

        var salvo = await orcamentos.ObterAsync(id, Ct);
        Assert.Equal((ajuste.Pessoa(pacote.Valor), ajuste.Locacao(2000m)), (salvo.ValorPorPessoa, salvo.ValorEspaco));
        Assert.Equal((ajuste.PercentualPessoa, ajuste.Descricao), (salvo.AjustePessoaPercentual, salvo.AjusteDescricao));

        // Na formatura (data especial exclusiva) o mínimo é de 150 pagantes.
        salvo.Data = sabado.AddDays(7);
        salvo.ValorPorPessoa = null;
        salvo.ValorEspaco = null;
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.SalvarAsync(salvo, Ct));
        Assert.Contains("mínimo é de 150", erro.Message);
        salvo.Convidados = 160;
        await orcamentos.SalvarAsync(salvo, Ct);
        salvo = await orcamentos.ObterAsync(id, Ct);
        Assert.Equal((Math.Round(pacote.Valor * 1.3m, 2), "Formatura +30% (por pessoa)"), (salvo.ValorPorPessoa, salvo.AjusteDescricao));

        var calendario = await precos.CalendarioAsync(sabado.AddDays(7), pacote.Id, espaco.Id, Ct);
        Assert.Equal(150, calendario.Single(d => d.Data == sabado.AddDays(7)).Ajuste.MinimoConvidados);
        Assert.Equal(DateTime.DaysInMonth(sabado.AddDays(7).Year, sabado.AddDays(7).Month), calendario.Count);
    }
}
