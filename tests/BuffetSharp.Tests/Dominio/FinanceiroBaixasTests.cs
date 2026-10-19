using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class FinanceiroBaixasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 19);

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static readonly FormaPagamento Dinheiro = ComId(new FormaPagamento { Nome = "Dinheiro", Tipo = TipoFormaPagamento.Dinheiro }, 1);
    private static readonly FormaPagamento Pix = ComId(new FormaPagamento { Nome = "PIX", Tipo = TipoFormaPagamento.Pix }, 2);
    private static readonly FormaPagamento Credito = ComId(new FormaPagamento { Nome = "Crédito", Tipo = TipoFormaPagamento.CartaoCredito }, 3);

    private static ContaFinanceira Caixa() =>
        ComId(new ContaFinanceira { Nome = "Caixa", Tipo = TipoContaFinanceira.Caixa, DataSaldoInicial = Hoje.AddYears(-1) }, 1);

    private static ContaFinanceira Banco() =>
        ComId(new ContaFinanceira { Nome = "Banco", Tipo = TipoContaFinanceira.ContaCorrente, DataSaldoInicial = Hoje.AddYears(-1) }, 2);

    private static Evento Evento(decimal valor = 3000m)
    {
        var evento = new Evento("Festa", 1, 1, 1, Hoje.AddMonths(2), new TimeOnly(19, 0), new TimeOnly(23, 0), 50, valor);
        evento.GerarParcelas(3, Hoje.AddDays(-10));
        return evento;
    }

    // ------------------------- Fatura do cartão -------------------------

    [Theory]
    // fecha dia 25, vence dia 5 (do mês seguinte ao fechamento)
    [InlineData("2026-10-10", 25, 5, "2026-11-05")]   // antes do fechamento: fatura de outubro, vence em novembro
    [InlineData("2026-10-25", 25, 5, "2026-12-05")]   // no dia do fechamento: já vai para a fatura seguinte
    [InlineData("2026-12-28", 25, 5, "2027-02-05")]   // virada do ano
    // fecha dia 3, vence dia 10 (no mesmo mês do fechamento)
    [InlineData("2026-10-01", 3, 10, "2026-10-10")]
    [InlineData("2026-10-15", 3, 10, "2026-11-10")]
    // vencimento no dia 31 em mês curto
    [InlineData("2027-01-10", 20, 31, "2027-01-31")]
    [InlineData("2027-01-25", 20, 31, "2027-02-28")]
    public void Primeiro_vencimento_da_fatura(string compra, int fechamento, int vencimento, string esperado)
    {
        Assert.Equal(DateOnly.Parse(esperado), FaturaCartao.PrimeiroVencimento(DateOnly.Parse(compra), fechamento, vencimento));
    }

    [Fact]
    public void Parcelas_da_fatura_vencem_no_mesmo_dia_dos_meses_seguintes_e_centavos_vao_para_a_ultima()
    {
        Assert.Equal([new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28)],
            FaturaCartao.Vencimentos(new DateOnly(2026, 11, 30), 25, 31, 3));
        Assert.Equal([33.33m, 33.33m, 33.34m], FaturaCartao.Dividir(100m, 3));
    }

    // ------------------------- Baixa -------------------------

    [Fact]
    public void Recebimento_de_varias_parcelas_com_duas_formas_movimenta_caixa_e_banco()
    {
        var evento = Evento();
        var (p1, p2) = (evento.Parcelas.ElementAt(0), evento.Parcelas.ElementAt(1));
        var (caixa, banco) = (Caixa(), Banco());

        var baixa = new Baixa(TipoBaixa.Recebimento, Hoje, Hoje, null, "teste");
        baixa.AdicionarTitulo(p1, p1.Valor, juros: 10m, desconto: 0);
        baixa.AdicionarTitulo(p2, p2.Valor, 0, desconto: 5m);
        baixa.AdicionarForma(Dinheiro, 500m, caixa, null);
        baixa.AdicionarForma(Pix, baixa.Total - 500m, banco, null);
        baixa.Concluir("Cliente - 2 parcelas");

        Assert.Equal(2005m, baixa.Total);
        Assert.All([p1, p2], p => Assert.Equal(StatusTitulo.Pago, p.Status));
        Assert.Equal(1010m, p1.ValorPago);
        Assert.Equal(995m, p2.ValorPago);
        Assert.Equal(500m, baixa.Formas.First().Movimento!.Valor);
        Assert.Same(caixa, baixa.Formas.First().Movimento!.Conta);
        Assert.Equal(1505m, baixa.Formas.Last().Movimento!.Valor);
        Assert.Equal(2005m, evento.TotalRecebido);
    }

    [Fact]
    public void Formas_que_nao_fecham_com_o_total_sao_recusadas()
    {
        var parcela = Evento().Parcelas.First();
        var baixa = new Baixa(TipoBaixa.Recebimento, Hoje, Hoje, null, null);
        baixa.AdicionarTitulo(parcela, parcela.Valor, 0, 0);
        baixa.AdicionarForma(Pix, parcela.Valor - 1, Banco(), null);

        var erro = Assert.Throws<RegraNegocioException>(() => baixa.Concluir("x"));
        Assert.Contains("precisa ser igual", erro.Message);
        Assert.Equal(StatusTitulo.Aberto, parcela.Status);
    }

    [Fact]
    public void Cada_forma_exige_a_conta_certa()
    {
        var baixa = new Baixa(TipoBaixa.Recebimento, Hoje, Hoje, null, null);
        Assert.Throws<RegraNegocioException>(() => baixa.AdicionarForma(Dinheiro, 10m, Banco(), null));
        Assert.Throws<RegraNegocioException>(() => baixa.AdicionarForma(Pix, 10m, Caixa(), null));
        Assert.Throws<RegraNegocioException>(() => baixa.AdicionarForma(Pix, 10m, null, null));
        Assert.Throws<RegraNegocioException>(() => new Baixa(TipoBaixa.Recebimento, Hoje.AddDays(1), Hoje, null, null));

        var pagamento = new Baixa(TipoBaixa.Pagamento, Hoje, Hoje, null, null);
        Assert.Throws<RegraNegocioException>(() => pagamento.AdicionarForma(Credito, 10m, null, null)); // sem cartão
    }

    [Fact]
    public void Pagamento_parcial_desmembra_o_saldo_e_o_estorno_o_reincorpora()
    {
        var conta = new ContaPagar("Carnes", CategoriaDespesa.Insumos, 1000m, Hoje);
        var baixa = new Baixa(TipoBaixa.Pagamento, Hoje, Hoje, null, null);
        baixa.AdicionarTitulo(conta, 400m, 0, 0);
        baixa.AdicionarForma(Pix, 400m, Banco(), null);
        baixa.Concluir("Boi Nobre - Carnes");

        var saldo = Assert.IsType<ContaPagar>(baixa.Titulos.Single().Saldo);
        Assert.Equal((400m, StatusTitulo.Pago), (conta.Valor, conta.Status));
        Assert.Equal((600m, StatusTitulo.Aberto), (saldo.Valor, saldo.Status));
        Assert.Equal("Carnes (saldo)", saldo.Descricao);
        Assert.Equal(-400m, baixa.Formas.Single().Movimento!.Valor);

        var remover = baixa.Estornar();

        Assert.Equal((1000m, StatusTitulo.Aberto), (conta.Valor, conta.Status));
        Assert.Contains(saldo, remover);
        Assert.Contains(baixa.Formas.Single().Movimento!, remover);
        Assert.Contains(baixa, remover);
    }

    [Fact]
    public void Pagamento_com_cartao_de_credito_gera_a_fatura_e_so_estorna_com_ela_em_aberto()
    {
        var cartao = ComId(new CartaoCredito { Nome = "Empresarial", DiaFechamento = 25, DiaVencimento = 5, FornecedorId = 9 }, 1);
        var conta = new ContaPagar("Anúncios", CategoriaDespesa.Marketing, 900m, Hoje);
        var baixa = new Baixa(TipoBaixa.Pagamento, Hoje, Hoje, null, null);
        baixa.AdicionarTitulo(conta, 900m, 0, 0);
        baixa.AdicionarForma(Credito, 900m, null, cartao, parcelas: 3);
        baixa.Concluir("Anúncios");

        var faturas = baixa.Formas.Single().Faturas.ToList();
        Assert.Equal([new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 5), new DateOnly(2027, 1, 5)], faturas.Select(f => f.Vencimento));
        Assert.All(faturas, f => Assert.Equal((300m, CategoriaDespesa.FaturaCartao, (int?)9), (f.Valor, f.Categoria, f.FornecedorId)));
        Assert.All(faturas, f => Assert.True(f.EhFaturaCartao));
        Assert.Null(baixa.Formas.Single().Movimento);

        // Fatura não pode ser cancelada nem ter o valor alterado: só pelo estorno do pagamento.
        Assert.Throws<RegraNegocioException>(() => faturas[0].Cancelar());
        Assert.Throws<RegraNegocioException>(() => faturas[0].Alterar("x", CategoriaDespesa.FaturaCartao, 1m, Hoje, 9, null, null));

        faturas[0].RegistrarPagamento(new DateOnly(2026, 11, 5), 300m, null);
        Assert.Throws<RegraNegocioException>(() => baixa.Estornar());

        faturas[0].EstornarPagamento();
        var remover = baixa.Estornar();
        Assert.All(faturas, f => Assert.Contains(f, remover));
        Assert.Equal(StatusTitulo.Aberto, conta.Status);
    }

    [Fact]
    public void Nao_mistura_receber_com_pagar_nem_repete_o_titulo()
    {
        var parcela = Evento().Parcelas.First();
        var pagamento = new Baixa(TipoBaixa.Pagamento, Hoje, Hoje, null, null);
        Assert.Throws<RegraNegocioException>(() => pagamento.AdicionarTitulo(parcela, parcela.Valor, 0, 0));

        var recebimento = new Baixa(TipoBaixa.Recebimento, Hoje, Hoje, null, null);
        recebimento.AdicionarTitulo(parcela, 100m, 0, 0);
        Assert.Throws<RegraNegocioException>(() => recebimento.AdicionarTitulo(parcela, 100m, 0, 0));
        Assert.Throws<RegraNegocioException>(() => recebimento.AdicionarTitulo(Evento().Parcelas.First(), 100m, 0, 100m));
    }

    // ------------------------- Contas e fechamento -------------------------

    [Fact]
    public void Fechamento_trava_a_data_e_lanca_a_diferenca_como_ajuste()
    {
        var caixa = Caixa();
        var fechamento = caixa.Fechar(Hoje.AddDays(-1), Hoje, saldoSistema: 1000m, saldoInformado: 990m, "Faltou troco", "teste");

        Assert.Equal(Hoje.AddDays(-1), caixa.FechadaAte);
        Assert.Equal(-10m, fechamento.Diferenca);
        Assert.Equal(-10m, fechamento.Ajuste!.Valor);
        Assert.Equal(OrigemMovimento.AjusteFechamento, fechamento.Ajuste.Origem);

        Assert.Throws<RegraNegocioException>(() => new MovimentoConta(caixa, Hoje.AddDays(-1), 50m, "Aporte", OrigemMovimento.Lancamento));
        Assert.NotNull(new MovimentoConta(caixa, Hoje, 50m, "Aporte", OrigemMovimento.Lancamento));

        // Diferença sem explicação não fecha; data futura também não.
        var banco = Banco();
        Assert.Throws<RegraNegocioException>(() => banco.Fechar(Hoje, Hoje, 100m, 90m, null, null));
        Assert.Throws<RegraNegocioException>(() => banco.Fechar(Hoje.AddDays(1), Hoje, 100m, 100m, null, null));

        caixa.Reabrir(ComIdFechamento(fechamento, caixa.Id), null);
        Assert.Null(caixa.FechadaAte);
    }

    private static FechamentoConta ComIdFechamento(FechamentoConta fechamento, int contaId)
    {
        typeof(FechamentoConta).GetProperty(nameof(FechamentoConta.ContaFinanceiraId))!.SetValue(fechamento, contaId);
        return fechamento;
    }

    [Fact]
    public void Transferencia_sai_de_uma_conta_e_entra_na_outra()
    {
        var (caixa, banco) = (Caixa(), Banco());
        var (saida, entrada) = MovimentoConta.Transferir(caixa, banco, Hoje, 300m, "Depósito");

        Assert.Equal((-300m, 300m), (saida.Valor, entrada.Valor));
        Assert.Same(entrada, saida.Contrapartida);
        Assert.Throws<RegraNegocioException>(() => MovimentoConta.Transferir(caixa, caixa, Hoje, 1m, null));
        Assert.Throws<RegraNegocioException>(() => new MovimentoConta(caixa, Hoje.AddYears(-2), 1m, "Antes do saldo inicial", OrigemMovimento.Lancamento));
    }
}
