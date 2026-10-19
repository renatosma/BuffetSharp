using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Recebimentos e pagamentos (baixas), caixa e contas bancárias no banco real.</summary>
[Collection(ColecaoBanco.Nome)]
public class FinanceiroIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

    private Task<ItemLookup> Forma(string tipo) => Lookup(TipoLookup.FormasPagamento, f => f.Info == tipo);
    private Task<ItemLookup> Caixa() => Lookup(TipoLookup.ContasFinanceiras, c => c.Marcador);
    private Task<ItemLookup> Banco() => Lookup(TipoLookup.ContasFinanceiras, c => !c.Marcador);

    private async Task<decimal> Saldo(int contaId) =>
        (await banco.Obter<IConsultasContas>().ListarContasAsync(ct: Ct)).Single(c => c.Id == contaId).Saldo;

    /// <summary>Cria contas a pagar com descrição única e devolve os Ids.</summary>
    private async Task<List<ContaPagarResumo>> NovasContas(int? fornecedorId, params decimal[] valores)
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        foreach (var (valor, i) in valores.Select((v, i) => (v, i)))
        {
            await banco.Obter<FinanceiroService>().SalvarContaAsync(new ContaPagarInput
            {
                Descricao = $"Teste {marca} {i}",
                Categoria = CategoriaDespesa.Insumos,
                Valor = valor,
                Vencimento = Hoje,
                FornecedorId = fornecedorId
            }, Ct);
        }
        return (await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct))
            .OrderBy(c => c.Descricao).ToList();
    }

    [Fact]
    public async Task Pagamento_agrupado_com_dinheiro_pix_e_cartao_e_estorno()
    {
        var fornecedor = await Lookup(TipoLookup.Fornecedores);
        var contas = await NovasContas(fornecedor.Id, 700m, 500m);
        var (caixa, contaBanco) = (await Caixa(), await Banco());
        var cartao = await Lookup(TipoLookup.CartoesCredito);
        var (saldoCaixa, saldoBanco) = (await Saldo(caixa.Id), await Saldo(contaBanco.Id));
        var fluxoAntes = (await banco.Obter<IConsultasRelatorios>().FluxoCaixaAsync(DateTime.Today, DateTime.Today, Ct)).Single();

        var baixas = banco.Obter<BaixaService>();
        var baixaId = await baixas.RegistrarAsync(new BaixaInput
        {
            Tipo = TipoBaixa.Pagamento,
            Data = Hoje,
            Titulos =
            [
                new BaixaTituloInput { TituloId = contas[0].Id, Valor = 700m, Juros = 20m },
                new BaixaTituloInput { TituloId = contas[1].Id, Valor = 500m }
            ],
            Formas =
            [
                new BaixaFormaInput { FormaPagamentoId = (await Forma("Dinheiro")).Id, Valor = 120m, ContaFinanceiraId = caixa.Id },
                new BaixaFormaInput { FormaPagamentoId = (await Forma("Pix")).Id, Valor = 400m, ContaFinanceiraId = contaBanco.Id },
                new BaixaFormaInput { FormaPagamentoId = (await Forma("CartaoCredito")).Id, Valor = 700m, CartaoCreditoId = cartao.Id, Parcelas = 2 }
            ]
        }, Ct);

        Assert.Equal(saldoCaixa - 120m, await Saldo(caixa.Id));
        Assert.Equal(saldoBanco - 400m, await Saldo(contaBanco.Id));

        var financeiro = banco.Obter<IConsultasFinanceiro>();
        var todas = await financeiro.ListarContasPagarAsync(new FiltroTitulos(), Hoje, Ct);
        Assert.All(contas, c => Assert.Equal(StatusTitulo.Pago, todas.Single(t => t.Id == c.Id).Status));
        Assert.Equal(720m, todas.Single(t => t.Id == contas[0].Id).ValorPago);
        var faturas = (await financeiro.ListarContasPagarAsync(new FiltroTitulos { VencimentoAte = DateTime.Today.AddMonths(4) }, Hoje, Ct))
            .Where(c => c.Categoria == CategoriaDespesa.FaturaCartao && c.Status == StatusTitulo.Aberto && c.Descricao.Contains($"{fornecedor.Nome} - 2 contas"))
            .ToList();
        Assert.Equal(2, faturas.Count);
        Assert.Equal(700m, faturas.Sum(f => f.Valor));
        Assert.All(faturas, f => Assert.True(f.Vencimento > DateTime.Today));

        // Fluxo de caixa: o cartão não é saída de dinheiro no dia (sai quando a fatura for paga).
        var fluxo = (await banco.Obter<IConsultasRelatorios>().FluxoCaixaAsync(DateTime.Today, DateTime.Today, Ct)).Single();
        Assert.Equal(520m, fluxo.Pago - fluxoAntes.Pago);
        Assert.Equal(-700m, fluxo.PagarPrevisto - fluxoAntes.PagarPrevisto); // as contas já estavam no previsto

        var resumo = await baixas.ObterDoTituloAsync(TipoBaixa.Pagamento, contas[1].Id, Ct);
        Assert.Equal((baixaId, 1220m, 2, 2), (resumo!.Id, resumo.Total, resumo.Titulos.Count, resumo.FaturasGeradas));

        // Estorno pela segunda conta desfaz o pagamento inteiro.
        await baixas.EstornarTituloAsync(TipoBaixa.Pagamento, contas[1].Id, Ct);
        todas = await financeiro.ListarContasPagarAsync(new FiltroTitulos(), Hoje, Ct);
        Assert.All(contas, c => Assert.Equal(StatusTitulo.Aberto, todas.Single(t => t.Id == c.Id).Status));
        Assert.DoesNotContain(todas, t => faturas.Any(f => f.Id == t.Id));
        Assert.Equal(saldoCaixa, await Saldo(caixa.Id));
        Assert.Equal(saldoBanco, await Saldo(contaBanco.Id));
        Assert.Null(await baixas.ObterDoTituloAsync(TipoBaixa.Pagamento, contas[1].Id, Ct));
    }

    [Fact]
    public async Task Contas_de_fornecedores_diferentes_nao_sao_pagas_juntas()
    {
        var fornecedores = await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Fornecedores, ct: Ct);
        var a = await NovasContas(fornecedores[0].Id, 100m);
        var b = await NovasContas(fornecedores[1].Id, 100m);

        var (pix, contaBanco) = ((await Forma("Pix")).Id, (await Banco()).Id);
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<BaixaService>().RegistrarAsync(new BaixaInput
        {
            Tipo = TipoBaixa.Pagamento,
            Data = Hoje,
            Titulos = [new BaixaTituloInput { TituloId = a[0].Id, Valor = 100m }, new BaixaTituloInput { TituloId = b[0].Id, Valor = 100m }],
            Formas = [new BaixaFormaInput { FormaPagamentoId = pix, Valor = 200m, ContaFinanceiraId = contaBanco }]
        }, Ct));
        Assert.Contains("mesmo fornecedor", erro.Message);
    }

    [Fact]
    public async Task Recebimento_parcial_cria_o_saldo_e_o_estorno_junta_de_volta()
    {
        var consultas = banco.Obter<IConsultasFinanceiro>();
        var parcela = (await consultas.ListarParcelasAsync(new FiltroTitulos { Status = StatusTitulo.Aberto }, Hoje, Ct))
            .First(p => p.Valor > 100m && p.Descricao.StartsWith("Parcela", StringComparison.Ordinal));
        var emAberto = await banco.Obter<IConsultasContas>().TitulosEmAbertoAsync(TipoBaixa.Recebimento, parcela.ClienteId, Ct);
        Assert.Contains(emAberto, t => t.Id == parcela.Id && t.Referencia!.StartsWith("EVT-", StringComparison.Ordinal));

        var baixas = banco.Obter<BaixaService>();
        await baixas.RegistrarAsync(new BaixaInput
        {
            Tipo = TipoBaixa.Recebimento,
            Data = Hoje,
            Titulos = [new BaixaTituloInput { TituloId = parcela.Id, Valor = 100m }],
            Formas = [new BaixaFormaInput { FormaPagamentoId = (await Forma("Dinheiro")).Id, Valor = 100m, ContaFinanceiraId = (await Caixa()).Id }]
        }, Ct);

        var doEvento = (await consultas.ListarParcelasAsync(new FiltroTitulos(), Hoje, Ct)).Where(p => p.EventoId == parcela.EventoId).ToList();
        Assert.Equal((100m, StatusTitulo.Pago), (doEvento.Single(p => p.Id == parcela.Id).Valor, doEvento.Single(p => p.Id == parcela.Id).Status));
        var saldo = doEvento.Single(p => p.Descricao == parcela.Descricao + TituloFinanceiro.SufixoSaldo);
        Assert.Equal((parcela.Valor - 100m, StatusTitulo.Aberto, parcela.Numero), (saldo.Valor, saldo.Status, saldo.Numero));

        await baixas.EstornarTituloAsync(TipoBaixa.Recebimento, parcela.Id, Ct);
        doEvento = (await consultas.ListarParcelasAsync(new FiltroTitulos(), Hoje, Ct)).Where(p => p.EventoId == parcela.EventoId).ToList();
        Assert.Equal((parcela.Valor, StatusTitulo.Aberto), (doEvento.Single(p => p.Id == parcela.Id).Valor, doEvento.Single(p => p.Id == parcela.Id).Status));
        Assert.DoesNotContain(doEvento, p => p.Id == saldo.Id);
    }

    [Fact]
    public async Task Transferencia_extrato_e_fechamento_da_conta()
    {
        var servico = banco.Obter<ContaFinanceiraService>();
        var consultas = banco.Obter<IConsultasContas>();
        var cadastro = banco.Obter<ICadastroService<ContaFinanceira>>();
        var nova = await cadastro.SalvarAsync(new ContaFinanceira
        {
            Nome = $"Poupança {Guid.NewGuid():N}"[..30],
            Tipo = TipoContaFinanceira.Poupanca,
            SaldoInicial = 1000m,
            DataSaldoInicial = Hoje.AddDays(-5)
        }, Ct);
        var contaBanco = await Banco();

        await servico.TransferirAsync(new TransferenciaInput { OrigemId = nova.Id, DestinoId = contaBanco.Id, Data = Hoje.AddDays(-2), Valor = 300m }, Ct);
        var tarifa = await servico.LancarAsync(new MovimentoInput { ContaFinanceiraId = nova.Id, Data = Hoje.AddDays(-1), Valor = 15m, Descricao = "Tarifa" }, Ct);

        var extrato = await consultas.ExtratoAsync(nova.Id, Hoje.AddDays(-5), Hoje, Ct);
        Assert.Equal(1000m, extrato.SaldoAnterior);
        Assert.Equal([700m, 685m], extrato.Movimentos.Select(m => m.Saldo));
        Assert.Equal(685m, extrato.SaldoFinal);

        // Fecha até ontem contando 680: a falta de 5 vira ajuste e a data fica travada.
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            servico.FecharAsync(new FechamentoInput { ContaFinanceiraId = nova.Id, Ate = Hoje.AddDays(-1), SaldoInformado = 680m }, Ct));
        await servico.FecharAsync(new FechamentoInput { ContaFinanceiraId = nova.Id, Ate = Hoje.AddDays(-1), SaldoInformado = 680m, Observacao = "Conferência" }, Ct);
        Assert.Equal(680m, (await consultas.ListarContasAsync(ct: Ct)).Single(c => c.Id == nova.Id).Saldo);
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.ExcluirMovimentoAsync(tarifa, Ct));
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            servico.LancarAsync(new MovimentoInput { ContaFinanceiraId = nova.Id, Data = Hoje.AddDays(-1), Valor = 1m, Descricao = "Atrasado" }, Ct));
        Assert.Single(await consultas.ListarFechamentosAsync(nova.Id, Ct));
        Assert.All((await consultas.ExtratoAsync(nova.Id, Hoje.AddDays(-5), Hoje, Ct)).Movimentos, m => Assert.True(m.Conferido));

        // Reabrir desfaz o ajuste; aí a transferência pode ser excluída (as duas pontas).
        await servico.ReabrirAsync(nova.Id, Ct);
        Assert.Equal(685m, await servico.SaldoAsync(nova.Id, Hoje, Ct));
        var saldoBanco = await servico.SaldoAsync(contaBanco.Id, Hoje, Ct);
        await servico.ExcluirMovimentoAsync(extrato.Movimentos[0].Id, Ct);
        Assert.Equal(985m, await servico.SaldoAsync(nova.Id, Hoje, Ct));
        Assert.Equal(saldoBanco - 300m, await servico.SaldoAsync(contaBanco.Id, Hoje, Ct));
    }

    // ------------------------- Parcelamento de contas a pagar -------------------------

    [Fact]
    public async Task Compra_parcelada_gera_as_parcelas_com_documento_e_codigo_do_parcelamento()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var fornecedor = await Lookup(TipoLookup.Fornecedores);
        var servico = banco.Obter<FinanceiroService>();
        var id = await servico.ParcelarAsync(new ParcelamentoInput
        {
            Descricao = $"Câmara fria {marca}",
            Categoria = CategoriaDespesa.Manutencao,
            FornecedorId = fornecedor.Id,
            Documento = "NF 9.876",
            ValorTotal = 10_000m,
            Plano = new PlanoPagamentoInput { ValorEntrada = 1_000m, VencimentoEntrada = Hoje, Quantidade = 3, PrimeiroVencimento = Hoje.AddMonths(1) }
        }, Ct);

        var parcelas = await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct);
        Assert.Equal(4, parcelas.Count);
        Assert.Equal([1_000m, 3_000m, 3_000m, 3_000m], parcelas.Select(p => p.Valor));
        Assert.All(parcelas, p => Assert.Equal(($"PAR-{id:D5}", "NF 9.876", fornecedor.Id), (p.Parcelamento, p.Documento, p.FornecedorId!.Value)));
        Assert.Equal($"Câmara fria {marca} (3/3)", parcelas[^1].Descricao);

        // Parcela não é excluída sozinha: desfaz-se o parcelamento inteiro.
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.ExcluirContaAsync(parcelas[0].Id, Ct));
        await servico.DesfazerParcelamentoAsync(id, Ct);
        Assert.Empty(await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct));
    }

    [Fact]
    public async Task Contas_juntadas_e_reparceladas_voltam_ao_desfazer_depois_do_estorno()
    {
        var fornecedor = await Lookup(TipoLookup.Fornecedores);
        var contas = await NovasContas(fornecedor.Id, 700m, 500m);
        var marca = contas[0].Descricao.Split(' ')[1];
        var servico = banco.Obter<FinanceiroService>();
        var id = await servico.ParcelarAsync(new ParcelamentoInput
        {
            Descricao = $"Acordo {marca}",
            ContasAgrupadasIds = contas.Select(c => c.Id).ToList(),
            Plano = new PlanoPagamentoInput { Quantidade = 2, PrimeiroVencimento = Hoje, IntervaloDias = 15 }
        }, Ct);

        var financeiro = banco.Obter<IConsultasFinanceiro>();
        var lista = await financeiro.ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct);
        var originais = lista.Where(c => contas.Any(o => o.Id == c.Id)).ToList();
        var parcelas = lista.Where(c => c.ParcelamentoId == id).OrderBy(c => c.Vencimento).ToList();
        Assert.All(originais, c => Assert.Equal((StatusTitulo.Cancelado, $"→ PAR-{id:D5}"), (c.Status, c.Parcelamento)));
        Assert.Equal([600m, 600m], parcelas.Select(p => p.Valor));
        Assert.Equal(Hoje.AddDays(15).ToDateTime(TimeOnly.MinValue), parcelas[1].Vencimento);
        Assert.All(parcelas, p => Assert.Equal((CategoriaDespesa.Insumos, fornecedor.Id), (p.Categoria, p.FornecedorId!.Value)));

        // Com parcela paga não se desfaz; estornado o pagamento, sim.
        var baixas = banco.Obter<BaixaService>();
        var baixaId = await baixas.RegistrarAsync(new BaixaInput
        {
            Tipo = TipoBaixa.Pagamento,
            Data = Hoje,
            Titulos = [new BaixaTituloInput { TituloId = parcelas[0].Id, Valor = 600m }],
            Formas = [new BaixaFormaInput { FormaPagamentoId = (await Forma("Pix")).Id, Valor = 600m, ContaFinanceiraId = (await Banco()).Id }]
        }, Ct);
        Assert.False((await servico.ObterParcelamentoAsync(id, Ct)).PodeDesfazer);
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.DesfazerParcelamentoAsync(id, Ct));

        await baixas.EstornarAsync(baixaId, Ct);
        await servico.DesfazerParcelamentoAsync(id, Ct);
        var depois = await financeiro.ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct);
        Assert.Equal(contas.Select(c => c.Id).Order(), depois.Select(c => c.Id).Order());
        Assert.All(depois, c => Assert.Equal((StatusTitulo.Aberto, (string?)null), (c.Status, c.Parcelamento)));
    }

    [Fact]
    public async Task Base_de_demonstracao_tem_compra_parcelada_com_parcelas_vencidas_pagas()
    {
        var parcelas = await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = "Forno combinado" }, Hoje, Ct);

        Assert.Equal(6, parcelas.Count);
        Assert.Equal(18_900m, parcelas.Sum(p => p.Valor));
        Assert.Single(parcelas.Select(p => p.Parcelamento).Distinct());
        Assert.All(parcelas.Where(p => p.Vencimento < DateTime.Today), p => Assert.Equal(StatusTitulo.Pago, p.Status));
    }

    [Fact]
    public async Task Detalhe_do_pagamento_mostra_formas_faturas_contas_juntas_e_o_restante_do_parcial()
    {
        // Outro fornecedor: o teste do pagamento agrupado procura as faturas do primeiro pela descrição.
        var fornecedor = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Fornecedores, ct: Ct)).Last();
        var contas = await NovasContas(fornecedor.Id, 700m, 500m);
        var marca = contas[0].Descricao.Split(' ')[1];
        var (contaBanco, cartao) = (await Banco(), await Lookup(TipoLookup.CartoesCredito));
        var baixas = banco.Obter<BaixaService>();
        await baixas.RegistrarAsync(new BaixaInput
        {
            Tipo = TipoBaixa.Pagamento,
            Data = Hoje,
            Observacao = "Acerto do mês",
            Titulos =
            [
                new BaixaTituloInput { TituloId = contas[0].Id, Valor = 400m, Juros = 10m },
                new BaixaTituloInput { TituloId = contas[1].Id, Valor = 500m, Desconto = 20m }
            ],
            Formas =
            [
                new BaixaFormaInput { FormaPagamentoId = (await Forma("Pix")).Id, Valor = 290m, ContaFinanceiraId = contaBanco.Id },
                new BaixaFormaInput { FormaPagamentoId = (await Forma("CartaoCredito")).Id, Valor = 600m, CartaoCreditoId = cartao.Id, Parcelas = 2 }
            ]
        }, Ct);

        var detalhe = await baixas.DetalharDoTituloAsync(TipoBaixa.Pagamento, contas[1].Id, Ct);
        Assert.NotNull(detalhe);
        Assert.Equal((Hoje, 890m, "Acerto do mês"), (detalhe.Data, detalhe.Total, detalhe.Observacao));
        Assert.Equal(contas[1].Id, detalhe.Titulos[0].TituloId); // a conta consultada vem primeiro
        Assert.Equal((500m, 20m, 480m), (detalhe.Titulos[0].Abatido, detalhe.Titulos[0].Desconto, detalhe.Titulos[0].Pago));
        var parcial = detalhe.Titulos.Single(t => t.TituloId == contas[0].Id);
        Assert.Equal((400m, 10m, 410m), (parcial.Abatido, parcial.Juros, parcial.Pago));
        Assert.Contains("(saldo)", parcial.Restante);
        Assert.Contains("em aberto", parcial.Restante);

        var pix = detalhe.Formas.Single(f => f.Valor == 290m);
        Assert.Equal($"Saiu de {contaBanco.Nome}", pix.Onde);
        var noCartao = detalhe.Formas.Single(f => f.Valor == 600m);
        Assert.EndsWith("em 2x", noCartao.Onde);
        Assert.Equal(2, noCartao.Faturas.Count);
        Assert.Equal((TipoFormaPagamento.Pix, TipoFormaPagamento.CartaoCredito), (pix.Tipo, noCartao.Tipo));

        // Na listagem, a conta paga traz o tipo da forma principal (a de maior valor: o cartão).
        var listada = (await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct))
            .Single(c => c.Id == contas[1].Id);
        Assert.Equal(TipoFormaPagamento.CartaoCredito, listada.TipoFormaPagamento);

        // A conta com o restante sabe de onde veio.
        var saldo = (await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = marca }, Hoje, Ct))
            .Single(c => c.Descricao.EndsWith("(saldo)"));
        Assert.Equal(300m, saldo.Valor);
        Assert.Contains("foram pagos", await baixas.OrigemDoSaldoAsync(TipoBaixa.Pagamento, saldo.Id, Ct));
        Assert.Null(await baixas.DetalharDoTituloAsync(TipoBaixa.Pagamento, saldo.Id, Ct));
    }
}
