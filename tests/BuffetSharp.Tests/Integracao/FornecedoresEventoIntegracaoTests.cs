using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Fornecedores do evento gravados no banco: contas a pagar, chegada no cronograma, pagamento refeito e cancelamento.</summary>
[Collection(ColecaoBanco.Nome)]
public class FornecedoresEventoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> CriarEventoAsync()
    {
        // Salão próprio: o sorteio da data não esbarra nos eventos de outros testes.
        var lookups = banco.Obter<IConsultasLookup>();
        var espaco = await banco.Obter<ICadastroService<Espaco>>().SalvarAsync(new Espaco
        {
            Nome = $"Salão fornecedores {Guid.NewGuid():N}"[..30], CapacidadeMaxima = 100, ValorLocacao = 1000
        }, Ct);
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = "Fornecedores de teste",
            ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espaco.Id,
            Data = DateOnly.FromDateTime(DateTime.Today).AddYears(3).AddDays(Random.Shared.Next(1, 300)),
            HoraInicio = new TimeOnly(12, 0),
            HoraFim = new TimeOnly(15, 0),
            Convidados = 40,
            ValorTotal = 7000m
        }, Ct);
    }

    [Fact]
    public async Task Contratacao_gera_contas_a_pagar_e_chegada_e_pode_ser_refeita_e_cancelada()
    {
        var eventos = banco.Obter<EventoService>();
        var id = await CriarEventoAsync();
        var inicio = (await eventos.ObterDetalheAsync(id, Ct)).Evento.Inicio;
        var fornecedorId = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Fornecedores, ct: Ct)).First().Id;
        var vencimento = DateOnly.FromDateTime(inicio);

        var contratacaoId = await eventos.SalvarFornecedorAsync(id, new ContratacaoFornecedorInput
        {
            FornecedorId = fornecedorId, Servico = "Decoração", Valor = 3000m, Chegada = inicio.AddHours(-3), Responsavel = "Patrícia",
            Plano = new PlanoPagamentoInput { Quantidade = 3, PrimeiroVencimento = vencimento.AddDays(-60), IntervaloDias = 30 }
        }, Ct);

        var detalhe = await eventos.ObterDetalheAsync(id, Ct);
        var decoracao = detalhe.Evento.Fornecedores.Single(f => f.Id == contratacaoId);
        Assert.Equal(3, decoracao.Pagamento!.Parcelas.Count);
        Assert.All(decoracao.Pagamento.Parcelas, p => Assert.Equal((1000m, CategoriaDespesa.Servicos, (int?)id), (p.Valor, p.Categoria, p.EventoId)));
        Assert.Same(detalhe.Evento.Etapas.Single(t => t.Atividade == "Chegada: Decoração"), decoracao.EtapaChegada);
        Assert.Equal(3000m, detalhe.DespesasVinculadas);

        // O parcelamento é do evento: não se desfaz pelo financeiro.
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            banco.Obter<FinanceiroService>().DesfazerParcelamentoAsync(decoracao.Pagamento.Id, Ct));
        Assert.Contains("Refazer pagamento", erro.Message);

        await eventos.SalvarFornecedorAsync(id, new ContratacaoFornecedorInput
        {
            Id = contratacaoId, Servico = "Decoração e flores", Chegada = inicio.AddHours(-4), Telefone = "(11) 98322-7351"
        }, Ct);
        await eventos.RefazerPagamentoFornecedorAsync(id, contratacaoId, 3600m,
            new PlanoPagamentoInput { Quantidade = 1, PrimeiroVencimento = vencimento }, Ct);
        await eventos.ConfirmarFornecedorAsync(id, contratacaoId, Ct);

        decoracao = (await eventos.ObterDetalheAsync(id, Ct)).Evento.Fornecedores.Single();
        Assert.Equal(("Decoração e flores", 3600m, SituacaoFornecedorEvento.Confirmado, inicio.AddHours(-4)),
            (decoracao.Servico, decoracao.Valor, decoracao.Situacao, decoracao.Chegada));
        Assert.Equal(3600m, Assert.Single(decoracao.Pagamento!.Parcelas).Valor);
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            Assert.Equal(3600m, await db.ContasPagar.Where(c => c.EventoId == id && c.Status != StatusTitulo.Cancelado).SumAsync(c => c.Valor, Ct));

        Assert.Equal(0m, await eventos.CancelarFornecedorAsync(id, contratacaoId, Ct));
        detalhe = await eventos.ObterDetalheAsync(id, Ct);
        Assert.Equal(StatusTitulo.Cancelado, detalhe.Evento.Fornecedores.Single().Pagamento!.Parcelas.Single().Status);
        Assert.DoesNotContain(detalhe.Evento.Etapas, t => t.Atividade.StartsWith("Chegada"));
        Assert.Equal(0m, detalhe.DespesasVinculadas);
    }

    [Fact]
    public async Task Base_de_demonstracao_tem_fornecedores_contratados_para_os_proximos_eventos()
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var contratacoes = await db.Eventos.SelectMany(e => e.Fornecedores).Include(f => f.Fornecedor).ToListAsync(Ct);
        Assert.NotEmpty(contratacoes);
        Assert.Contains(contratacoes, f => f.Situacao == SituacaoFornecedorEvento.Confirmado);
        Assert.Empty(await banco.Obter<EventoService>().AvaliacoesDoFornecedorAsync(contratacoes[0].FornecedorId, Ct));
        Assert.Empty(await banco.Obter<EventoService>().MediasAvaliacaoAsync(Ct));
    }
}
