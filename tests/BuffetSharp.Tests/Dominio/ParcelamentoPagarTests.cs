using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class ParcelamentoPagarTests
{
    private static readonly DateOnly Hoje = new(2026, 3, 10);

    private static ContaPagar Conta(decimal valor, int? fornecedorId = 7, CategoriaDespesa categoria = CategoriaDespesa.Insumos,
        int? eventoId = null) =>
        new($"Boleto {valor}", categoria, valor, Hoje, fornecedorId, eventoId);

    [Fact]
    public void Plano_divide_em_entrada_e_parcelas_com_os_centavos_na_ultima()
    {
        var plano = PlanoParcelas.Calcular(1000m, 3, Hoje.AddMonths(1), valorEntrada: 100m, vencimentoEntrada: Hoje);

        Assert.Equal([0, 1, 2, 3], plano.Select(p => p.Numero));
        Assert.Equal([100m, 300m, 300m, 300m], plano.Select(p => p.Valor));
        Assert.Equal([Hoje, Hoje.AddMonths(1), Hoje.AddMonths(2), Hoje.AddMonths(3)], plano.Select(p => p.Vencimento));

        var tresVezes = PlanoParcelas.Calcular(100m, 3, Hoje, intervaloDias: 15);
        Assert.Equal([33.33m, 33.33m, 33.34m], tresVezes.Select(p => p.Valor));
        Assert.Equal(Hoje.AddDays(30), tresVezes[^1].Vencimento);
    }

    [Theory]
    [InlineData(0, 1, 0)] // sem valor
    [InlineData(100, 0, 0)] // sem parcelas
    [InlineData(100, 49, 0)]
    [InlineData(100, 2, 100)] // entrada igual ao total com parcelas
    [InlineData(100, 1, -1)]
    public void Plano_invalido_e_rejeitado(decimal valor, int quantidade, decimal entrada)
    {
        Assert.Throws<RegraNegocioException>(() => PlanoParcelas.Calcular(valor, quantidade, Hoje, entrada));
    }

    [Fact]
    public void Compra_parcelada_gera_contas_numeradas_com_os_dados_da_compra()
    {
        var compra = new ParcelamentoPagar("Forno combinado", CategoriaDespesa.Manutencao, 18_900m, fornecedorId: 3, eventoId: 5,
            documento: " NF 123 ");
        var parcelas = compra.GerarParcelas(5, Hoje.AddMonths(1), 3_900m, Hoje, formaPagamentoId: 2);

        Assert.Equal(6, parcelas.Count);
        Assert.Equal(18_900m, parcelas.Sum(p => p.Valor));
        Assert.Equal("Forno combinado (entrada)", parcelas[0].Descricao);
        Assert.Equal("Forno combinado (5/5)", parcelas[^1].Descricao);
        Assert.All(parcelas, p =>
        {
            Assert.Equal(CategoriaDespesa.Manutencao, p.Categoria);
            Assert.Equal((3, 5, "NF 123", 2), (p.FornecedorId!.Value, p.EventoId!.Value, p.Documento, p.FormaPagamentoId!.Value));
            Assert.True(p.EhParcela);
        });
        Assert.Throws<RegraNegocioException>(() => compra.GerarParcelas(2, Hoje));
    }

    [Fact]
    public void Juntar_contas_cancela_as_originais_e_desfazer_reabre()
    {
        var contas = new[] { Conta(700m), Conta(500m) };
        var parcelamento = ParcelamentoPagar.Agrupar(contas, "Acordo com o fornecedor");
        parcelamento.GerarParcelas(2, Hoje.AddDays(30), intervaloDias: 30);

        Assert.Equal(1200m, parcelamento.ValorTotal);
        Assert.Equal([600m, 600m], parcelamento.Parcelas.Select(p => p.Valor));
        Assert.All(contas, c => Assert.Equal((StatusTitulo.Cancelado, true), (c.Status, c.FoiAgrupada)));
        Assert.Equal(7, parcelamento.FornecedorId);

        var remover = parcelamento.Desfazer();
        Assert.Equal(3, remover.Count); // 2 parcelas + o parcelamento
        Assert.All(contas, c => Assert.Equal((StatusTitulo.Aberto, false), (c.Status, c.FoiAgrupada)));
    }

    [Fact]
    public void So_junta_contas_em_aberto_do_mesmo_fornecedor_categoria_e_evento()
    {
        Assert.Throws<RegraNegocioException>(() => ParcelamentoPagar.Agrupar([Conta(10m, 1), Conta(10m, 2)], "x"));
        Assert.Throws<RegraNegocioException>(() =>
            ParcelamentoPagar.Agrupar([Conta(10m), Conta(10m, categoria: CategoriaDespesa.Servicos)], "x"));
        Assert.Throws<RegraNegocioException>(() => ParcelamentoPagar.Agrupar([Conta(10m, eventoId: 1), Conta(10m)], "x"));
        Assert.Throws<RegraNegocioException>(() => ParcelamentoPagar.Agrupar([], "x"));

        var paga = Conta(10m);
        paga.RegistrarPagamento(Hoje, 10m, null);
        var aberta = Conta(10m);
        Assert.Throws<RegraNegocioException>(() => ParcelamentoPagar.Agrupar([aberta, paga], "x"));
        Assert.Equal(StatusTitulo.Aberto, aberta.Status); // nada muda quando a validação falha
    }

    [Fact]
    public void Parcelamento_com_parcela_paga_nao_pode_ser_desfeito()
    {
        var parcelamento = ParcelamentoPagar.Agrupar([Conta(300m)], "Acordo");
        parcelamento.GerarParcelas(3, Hoje);
        parcelamento.Parcelas.First().RegistrarPagamento(Hoje, 100m, null);

        Assert.False(parcelamento.PodeDesfazer);
        Assert.Throws<RegraNegocioException>(() => parcelamento.Desfazer());
    }

    [Fact]
    public void Saldo_de_pagamento_parcial_continua_na_mesma_parcela_e_o_fornecedor_nao_muda()
    {
        var compra = new ParcelamentoPagar("Mesas", CategoriaDespesa.Outros, 1000m, fornecedorId: 4);
        var parcela = compra.GerarParcelas(2, Hoje)[1];

        var saldo = (ContaPagar)parcela.Desmembrar(200m);
        Assert.Equal((parcela.Parcelamento, 2, 300m), (saldo.Parcelamento, saldo.NumeroParcela!.Value, saldo.Valor));

        Assert.Throws<RegraNegocioException>(() =>
            parcela.Alterar(parcela.Descricao, parcela.Categoria, parcela.Valor, parcela.Vencimento, 9, null, null));
        parcela.Alterar("Mesas - renegociada", parcela.Categoria, parcela.Valor, Hoje.AddDays(5), 4, null, null);
        Assert.Equal(Hoje.AddDays(5), parcela.Vencimento);
    }
}
