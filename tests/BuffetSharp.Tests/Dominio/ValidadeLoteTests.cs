using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.Tests.Dominio;

public class ValidadeLoteTests
{
    private static readonly DateTime Agora = new(2026, 10, 20, 10, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);

    private static Produto Perecivel() => new() { Nome = "Creme de leite", ControlaValidade = true };

    private static MovimentacaoEstoque Entrada(Produto produto, decimal quantidade, decimal custo, int diasValidade, string? lote = null) =>
        produto.RegistrarEntrada(quantidade, custo, Agora.AddDays(-1), null, [new LoteInformado(lote, Hoje.AddDays(diasValidade))]);

    [Fact]
    public void Entrada_de_perecivel_exige_a_validade_e_vira_lote()
    {
        var produto = Perecivel();

        Assert.Throws<RegraNegocioException>(() => produto.RegistrarEntrada(5, 10m, Agora, null));
        var entrada = Entrada(produto, 5, 10m, 10, "A1");

        var lote = Assert.Single(produto.Lotes);
        Assert.Equal(("A1", Hoje.AddDays(10), 5m, 5m, 10m), (lote.Numero, lote.Validade, lote.Quantidade, lote.Saldo, lote.CustoUnitario));
        Assert.Equal(5m, Assert.Single(entrada.Lotes).Quantidade);
        Assert.Equal(0m, produto.SaldoSemLote);
    }

    [Fact]
    public void Produto_sem_controle_de_validade_ignora_os_lotes()
    {
        var produto = new Produto { Nome = "Arroz" };

        produto.RegistrarEntrada(5, 10m, Agora, null, [new LoteInformado("X", Hoje.AddDays(5))]);

        Assert.Empty(produto.Lotes);
        Assert.Equal(5m, produto.SaldoSemLote);
    }

    [Fact]
    public void Entrada_com_varios_lotes_divide_a_quantidade_e_o_ultimo_fica_com_o_resto()
    {
        var produto = Perecivel();

        produto.RegistrarEntrada(10, 8m, Agora, null, [new LoteInformado("A", Hoje.AddDays(5), 4), new LoteInformado("B", Hoje.AddDays(9), 4)]);

        Assert.Equal([("A", 4m), ("B", 6m)], produto.Lotes.Select(l => (l.Numero, l.Saldo)));
    }

    [Fact]
    public void Saida_usa_primeiro_o_saldo_sem_lote_e_depois_o_que_vence_antes()
    {
        var produto = Perecivel();
        produto.ControlaValidade = false;
        produto.RegistrarEntrada(2, 10m, Agora, null); // histórico de antes de controlar validade
        produto.ControlaValidade = true;
        Entrada(produto, 5, 10m, 20, "TARDE");
        Entrada(produto, 5, 10m, 4, "CEDO");

        var saida = produto.RegistrarSaida(9, Agora, "Evento");

        Assert.Equal([("CEDO", 5m), ("TARDE", 2m)], saida.Lotes.Select(l => (l.Lote!.Numero, l.Quantidade)));
        Assert.Equal([("TARDE", 3m), ("CEDO", 0m)], produto.Lotes.Select(l => (l.Numero, l.Saldo)));
        Assert.Equal(3m, produto.EstoqueAtual);
        Assert.Equal(0m, produto.SaldoSemLote);
    }

    [Fact]
    public void Lote_vencido_nao_sai_para_evento()
    {
        var produto = Perecivel();
        Entrada(produto, 4, 10m, -1, "VENCIDO");
        Entrada(produto, 3, 10m, 6, "BOM");

        var erro = Assert.Throws<RegraNegocioException>(() => produto.RegistrarSaida(5, Agora, null));
        Assert.Contains("vencido", erro.Message);

        var saida = produto.RegistrarSaida(3, Agora, null);
        Assert.Equal("BOM", Assert.Single(saida.Lotes).Lote!.Numero);
        Assert.Equal(4m, produto.Lotes.Single(l => l.Numero == "VENCIDO").Saldo);
    }

    [Fact]
    public void Situacao_do_lote_pelo_prazo_de_alerta()
    {
        var produto = Perecivel();
        Entrada(produto, 1, 1m, -1, "V");
        Entrada(produto, 1, 1m, 7, "A");
        Entrada(produto, 1, 1m, 8, "O");

        Assert.Equal([SituacaoLote.Vencido, SituacaoLote.Vencendo, SituacaoLote.Ok], produto.Lotes.Select(l => l.Situacao(Hoje, 7)));
        Assert.False(produto.Lotes[1].Vencido(Hoje.AddDays(7))); // vence no fim do dia da validade
    }

    [Fact]
    public void Descarte_baixa_o_lote_como_perda_pelo_custo_do_lote()
    {
        var produto = Perecivel();
        Entrada(produto, 4, 12m, -2, "VENCIDO");
        Entrada(produto, 6, 18m, 10, "BOM");
        var vencido = produto.Lotes[0];

        Assert.Throws<RegraNegocioException>(() => produto.Descartar(vencido, 5, Agora, "Vencido"));
        Assert.Throws<RegraNegocioException>(() => produto.Descartar(vencido, 1, Agora, " "));
        Assert.Throws<RegraNegocioException>(() => produto.Descartar(null, 1, Agora, "Sem lote")); // perecível: escolha o lote
        var perda = produto.Descartar(vencido, 4, Agora, "Produto vencido");

        Assert.Equal((TipoMovimentacao.Perda, 4m, 12m), (perda.Tipo, perda.Quantidade, perda.CustoUnitario));
        Assert.Contains("Produto vencido", perda.Observacao);
        Assert.Equal(0m, vencido.Saldo);
        Assert.Equal(6m, produto.EstoqueAtual);
    }

    [Fact]
    public void Descarte_de_produto_sem_lote_usa_o_custo_medio()
    {
        var produto = new Produto { Nome = "Alface" };
        produto.RegistrarEntrada(10, 3m, Agora, null);

        var perda = produto.Descartar(null, 2, Agora, "Murchou");

        Assert.Equal((TipoMovimentacao.Perda, 3m, 8m), (perda.Tipo, perda.CustoUnitario, produto.EstoqueAtual));
        Assert.Throws<RegraNegocioException>(() => produto.Descartar(null, 9, Agora, "Murchou"));
    }

    [Fact]
    public void Estorno_da_entrada_baixa_o_lote_e_e_recusado_se_o_lote_ja_foi_usado()
    {
        var produto = Perecivel();
        var usada = Entrada(produto, 5, 10m, 3, "USADO");
        var intacta = Entrada(produto, 5, 10m, 30, "INTACTO");
        produto.RegistrarSaida(1, Agora, null); // sai do que vence primeiro

        Assert.Throws<RegraNegocioException>(() => produto.EstornarEntrada(usada, Agora, "Lançada errada"));
        produto.EstornarEntrada(intacta, Agora, "Lançada errada");

        Assert.Equal([("USADO", 4m), ("INTACTO", 0m)], produto.Lotes.Select(l => (l.Numero, l.Saldo)));
        Assert.Equal(4m, produto.EstoqueAtual);
    }

    [Fact]
    public void Inventario_que_falta_consome_ate_o_vencido_e_o_que_sobra_fica_sem_lote()
    {
        var produto = Perecivel();
        Entrada(produto, 3, 10m, -1, "VENCIDO");
        Entrada(produto, 3, 10m, 9, "BOM");

        var falta = produto.AjustarSaldo(2, Agora, "Contagem");
        Assert.Equal([("VENCIDO", 3m), ("BOM", 1m)], falta.Lotes.Select(l => (l.Lote!.Numero, l.Quantidade)));

        produto.AjustarSaldo(5, Agora, "Contagem");
        Assert.Equal(3m, produto.SaldoSemLote);
        Assert.Equal(2m, produto.Lotes.Sum(l => l.Saldo));
    }

    [Fact]
    public void Leitor_da_nfe_traz_os_lotes_do_grupo_rastro()
    {
        var validade = new DateOnly(2026, 12, 15);
        var xml = NfeExemplo.Xml("901", "12345678000195", "Laticínios",
        [
            new("CL1", "CREME DE LEITE 1L", "CX", 2, 120m, Rastros: [("L77", 1.5m, validade), ("L78", 0.5m, validade.AddDays(10))]),
            new("MT2", "MANTEIGA 500G", "UN", 3, 22m)
        ]);

        var nota = LeitorNfe.Ler(xml);

        var lotes = nota.Itens[0].Lotes;
        Assert.Equal([("L77", 1.5m, validade), ("L78", 0.5m, validade.AddDays(10))], lotes.Select(l => (l.Numero, l.Quantidade, l.Validade)));
        Assert.Equal(validade.AddMonths(-1), lotes[0].Fabricacao);
        Assert.Empty(nota.Itens[1].Lotes);
    }
}
