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

/// <summary>Boleto/PIX registrado gravado no banco: simulado com a baixa automática e o fluxo do Asaas (provedor falso).</summary>
[Collection(ColecaoBanco.Nome)]
public class CobrancaIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private sealed class AsaasFalso : IProvedorCobranca
    {
        public Queue<SituacaoCobranca> Situacoes { get; } = new();
        public List<string> Chamadas { get; } = [];

        public Task<CobrancaEmitida> EmitirAsync(PedidoCobranca pedido, CancellationToken ct = default)
        {
            Chamadas.Add($"emitir {pedido.Tipo} {pedido.Valor} {pedido.Vencimento:yyyy-MM-dd} {pedido.Pagador.Documento}");
            var id = $"pay_{Chamadas.Count}{Guid.NewGuid():N}"[..14];
            return Task.FromResult(new CobrancaEmitida(id, "cus_1", "00190.00009 01234.567890 12345.678901 1 99990000500000", "1234",
                $"https://sandbox.asaas.com/b/pdf/{id}", $"https://sandbox.asaas.com/i/{id}", "000201pix"));
        }

        public Task<SituacaoCobranca> ConsultarAsync(AmbienteCobranca ambiente, string id, CancellationToken ct = default)
        {
            Chamadas.Add($"consultar {id}");
            return Task.FromResult(Situacoes.Count > 0 ? Situacoes.Dequeue() : new SituacaoCobranca(StatusCobranca.Pendente));
        }

        public Task CancelarAsync(AmbienteCobranca ambiente, string id, CancellationToken ct = default)
        {
            Chamadas.Add($"cancelar {id}");
            return Task.CompletedTask;
        }
    }

    private CobrancaService Servico(IProvedorCobranca provedor) =>
        new(banco.Obter<IBuffetDbContextFactory>(), provedor, new AsaasOptions { ChaveSandbox = "chave" }, banco.Obter<BaixaService>(),
            banco.Obter<ISessaoUsuario>(), TimeProvider.System);

    private async Task<int> ConfigurarAsync(AmbienteCobranca ambiente)
    {
        var banco_ = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.ContasFinanceiras, ct: Ct)).First(c => !c.Marcador).Id;
        var servico = banco.Obter<CobrancaService>();
        var configuracao = await servico.ObterConfiguracaoAsync(Ct);
        configuracao.Ambiente = ambiente;
        configuracao.ContaFinanceiraId = banco_;
        await servico.SalvarConfiguracaoAsync(configuracao, Ct);
        return banco_;
    }

    /// <summary>Parcela em aberto de um evento confirmado, sem cobrança (cada chamada pega a de outro evento).</summary>
    private async Task<Parcela> ParcelaAsync()
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var usados = await db.CobrancasRegistradas.Select(c => c.EventoId).Distinct().ToListAsync(Ct);
        return await db.Parcelas.AsNoTracking().Include(p => p.Evento)
            .Where(p => p.Status == StatusTitulo.Aberto && !p.Excedentes && p.Valor >= 5 && p.Evento!.Status == StatusEvento.Confirmado &&
                        !usados.Contains(p.EventoId))
            .OrderBy(p => p.Id).FirstAsync(Ct);
    }

    private async Task<Parcela> ParcelaPorIdAsync(int id)
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        return await db.Parcelas.AsNoTracking().Include(p => p.FormaPagamento).FirstAsync(p => p.Id == id, Ct);
    }

    [Fact]
    public async Task Simulado_emite_bloqueia_o_plano_e_o_pagamento_baixa_a_parcela()
    {
        var contaId = await ConfigurarAsync(AmbienteCobranca.Simulado);
        var servico = banco.Obter<CobrancaService>();
        var parcela = await ParcelaAsync();

        var preparo = await servico.PrepararAsync(parcela.Id, Ct);
        Assert.Empty(preparo.Pendencias);
        Assert.Null(preparo.CobrancaPendenteId);
        Assert.Equal(parcela.Valor, preparo.Valor);
        Assert.True(preparo.VencimentoSugerido >= Hoje);

        var id = await servico.EmitirAsync(new EmissaoCobrancaInput { ParcelaId = parcela.Id, Tipo = TipoCobranca.Boleto, Vencimento = preparo.VencimentoSugerido }, Ct);
        var cobranca = await servico.ObterAsync(id, Ct);
        Assert.Equal((StatusCobranca.Pendente, parcela.Valor, AmbienteCobranca.Simulado), (cobranca.Status, cobranca.Valor, cobranca.Ambiente));
        Assert.Equal(47, cobranca.LinhaDigitavel!.Count(char.IsAsciiDigit));
        Assert.StartsWith("000201", cobranca.PixCopiaECola);
        Assert.Equal(id, (await servico.PrepararAsync(parcela.Id, Ct)).CobrancaPendenteId);
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.EmitirAsync(
            new EmissaoCobrancaInput { ParcelaId = parcela.Id, Tipo = TipoCobranca.Pix, Vencimento = Hoje }, Ct));

        var lista = await banco.Obter<IConsultasFinanceiro>().ListarParcelasAsync(new FiltroTitulos(), Hoje, Ct);
        Assert.Equal("Emitida", lista.Single(p => p.Id == parcela.Id).Cobranca);

        // Com boleto pendente, o plano de pagamento não pode ser refeito nem o evento cancelado.
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<EventoService>().GerarParcelasAsync(parcela.EventoId,
            new PlanoPagamentoInput { Quantidade = 1, PrimeiroVencimento = Hoje.AddDays(10) }, Ct));
        Assert.Contains("Cancele a cobrança", erro.Message);
        await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<EventoService>().CancelarAsync(parcela.EventoId, "Teste", Ct));

        await servico.SimularPagamentoAsync(id, ct: Ct);

        var paga = await ParcelaPorIdAsync(parcela.Id);
        Assert.Equal((StatusTitulo.Pago, parcela.Valor, Hoje), (paga.Status, paga.ValorPago, paga.DataPagamento));
        Assert.Equal(TipoFormaPagamento.Boleto, paga.FormaPagamento!.Tipo);
        var detalhe = await banco.Obter<BaixaService>().DetalharDoTituloAsync(TipoBaixa.Recebimento, parcela.Id, Ct);
        var conta = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.ContasFinanceiras, ct: Ct)).Single(c => c.Id == contaId).Nome;
        var forma = Assert.Single(detalhe!.Formas);
        Assert.Equal((TipoFormaPagamento.Boleto, parcela.Valor), (forma.Tipo, forma.Valor));
        Assert.Contains(conta, forma.Onde);

        var resumo = (await banco.Obter<IConsultasCobrancas>().ListarAsync(new FiltroCobrancas(), Ct)).Single(c => c.Id == id);
        Assert.Equal((StatusCobranca.Paga, true, CobrancaService.TaxaSimulada, (string?)null), (resumo.Status, resumo.Baixada, resumo.Taxa, resumo.Mensagem));
        Assert.Equal("Paga", (await banco.Obter<IConsultasFinanceiro>().ListarParcelasAsync(new FiltroTitulos(), Hoje, Ct))
            .Single(p => p.Id == parcela.Id).Cobranca);
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.CancelarAsync(id, Ct));
    }

    [Fact]
    public async Task Asaas_pago_com_juros_baixa_pelo_pix_e_parcela_ja_recebida_fica_para_conferir()
    {
        await ConfigurarAsync(AmbienteCobranca.Sandbox);
        try
        {
            var asaas = new AsaasFalso();
            var servico = Servico(asaas);
            var parcela = await ParcelaAsync();

            var id = await servico.EmitirAsync(new EmissaoCobrancaInput { ParcelaId = parcela.Id, Tipo = TipoCobranca.Boleto, Vencimento = Hoje.AddDays(3) }, Ct);
            Assert.StartsWith($"emitir Boleto {parcela.Valor} {Hoje.AddDays(3):yyyy-MM-dd} ", asaas.Chamadas[0]);
            var emitida = await servico.ObterAsync(id, Ct);
            Assert.StartsWith("https://sandbox.asaas.com/i/", emitida.UrlFatura);

            // Ainda não pago: nada muda.
            var resultado = await servico.VerificarPagamentosAsync(Ct);
            Assert.True(resultado.Consultadas >= 1);
            Assert.Equal(StatusTitulo.Aberto, (await ParcelaPorIdAsync(parcela.Id)).Status);

            // Pago com atraso pelo PIX: os juros entram na baixa.
            asaas.Situacoes.Enqueue(new SituacaoCobranca(StatusCobranca.Paga, Hoje, parcela.Valor + 12.5m, parcela.Valor + 10.51m, TipoFormaPagamento.Pix));
            resultado = await servico.VerificarPagamentosAsync(Ct);
            Assert.Equal((1, 1), (resultado.Pagas, resultado.Baixadas));
            var paga = await ParcelaPorIdAsync(parcela.Id);
            Assert.Equal((StatusTitulo.Pago, parcela.Valor + 12.5m, TipoFormaPagamento.Pix), (paga.Status, paga.ValorPago, paga.FormaPagamento!.Tipo));
            var pagamento = Assert.Single((await banco.Obter<BaixaService>().DetalharDoTituloAsync(TipoBaixa.Recebimento, parcela.Id, Ct))!.Titulos);
            Assert.Equal(12.5m, pagamento.Juros);

            // Outra parcela, recebida no balcão antes de o Asaas confirmar: a cobrança paga fica para conferir.
            var outra = await ParcelaAsync();
            var outraId = await servico.EmitirAsync(new EmissaoCobrancaInput { ParcelaId = outra.Id, Tipo = TipoCobranca.Pix, Vencimento = Hoje }, Ct);
            var dinheiro = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.FormasPagamento, ct: Ct)).First(f => f.Info == "Dinheiro").Id;
            var caixa = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.ContasFinanceiras, ct: Ct)).First(c => c.Marcador).Id;
            await banco.Obter<BaixaService>().RegistrarAsync(new BaixaInput
            {
                Tipo = TipoBaixa.Recebimento, Data = Hoje,
                Titulos = [new BaixaTituloInput { TituloId = outra.Id, Valor = outra.Valor }],
                Formas = [new BaixaFormaInput { FormaPagamentoId = dinheiro, Valor = outra.Valor, ContaFinanceiraId = caixa }]
            }, Ct);
            asaas.Situacoes.Enqueue(new SituacaoCobranca(StatusCobranca.Paga, Hoje, outra.Valor, outra.Valor - 1, TipoFormaPagamento.Pix));
            resultado = await servico.VerificarPagamentosAsync(Ct);
            Assert.Equal((1, 0), (resultado.Pagas, resultado.Baixadas));
            var conferir = (await banco.Obter<IConsultasCobrancas>().ListarAsync(new FiltroCobrancas(), Ct)).Single(c => c.Id == outraId);
            Assert.Equal((StatusCobranca.Paga, false), (conferir.Status, conferir.Baixada));
            Assert.Contains("já estava recebida", conferir.Mensagem);
        }
        finally
        {
            await ConfigurarAsync(AmbienteCobranca.Simulado);
        }
    }

    [Fact]
    public async Task Asaas_cancela_a_pendente_e_recusa_cancelar_a_que_foi_paga()
    {
        await ConfigurarAsync(AmbienteCobranca.Sandbox);
        try
        {
            var asaas = new AsaasFalso();
            var servico = Servico(asaas);
            var parcela = await ParcelaAsync();

            var id = await servico.EmitirAsync(new EmissaoCobrancaInput { ParcelaId = parcela.Id, Tipo = TipoCobranca.ClienteEscolhe, Vencimento = Hoje.AddDays(7) }, Ct);
            await servico.CancelarAsync(id, Ct);
            Assert.Contains(asaas.Chamadas, c => c.StartsWith("cancelar pay_"));
            Assert.Null((await servico.PrepararAsync(parcela.Id, Ct)).CobrancaPendenteId);

            // Pago entre a consulta e o cancelamento: não cancela.
            var segunda = await servico.EmitirAsync(new EmissaoCobrancaInput { ParcelaId = parcela.Id, Tipo = TipoCobranca.Pix, Vencimento = Hoje.AddDays(7) }, Ct);
            asaas.Situacoes.Enqueue(new SituacaoCobranca(StatusCobranca.Paga, Hoje, parcela.Valor, parcela.Valor, TipoFormaPagamento.Pix));
            var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => servico.CancelarAsync(segunda, Ct));
            Assert.Contains("já pagou", erro.Message);
            await servico.CancelarAsync(segunda, Ct);

            // Sem a chave da API não emite.
            var semChave = new CobrancaService(banco.Obter<IBuffetDbContextFactory>(), asaas, new AsaasOptions(), banco.Obter<BaixaService>(),
                banco.Obter<ISessaoUsuario>(), TimeProvider.System);
            var outra = await ParcelaAsync();
            Assert.Contains((await semChave.PrepararAsync(outra.Id, Ct)).Pendencias, p => p.Contains("Chave da API do Asaas"));
            await Assert.ThrowsAsync<RegraNegocioException>(() => semChave.EmitirAsync(
                new EmissaoCobrancaInput { ParcelaId = outra.Id, Tipo = TipoCobranca.Pix, Vencimento = Hoje }, Ct));
        }
        finally
        {
            await ConfigurarAsync(AmbienteCobranca.Simulado);
        }
    }
}
