using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.Tests.Dominio;

public class CadastrosTests
{
    [Fact]
    public void Cadastro_simples_exige_nome()
    {
        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(new TipoEvento()));
        Assert.Contains(erro.Erros, e => e.MemberNames.Contains(nameof(TipoEvento.Nome)));
    }

    [Fact]
    public void Cliente_com_cpf_invalido_e_rejeitado_no_campo_documento()
    {
        var cliente = new Cliente { Nome = "Fulano", Celular = "(11) 99999-0000", Documento = "12345678900" };

        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(cliente));

        Assert.Contains(erro.Erros, e => e.MemberNames.Contains(nameof(Cliente.Documento)));
    }

    [Fact]
    public void Cliente_precisa_de_ao_menos_um_contato()
    {
        var erro = Assert.Throws<ValidacaoException>(() => Validador.Validar(new Cliente { Nome = "Sem contato" }));
        Assert.Contains("contato", erro.Message);
    }

    [Fact]
    public void Entrada_de_estoque_recalcula_custo_medio_ponderado()
    {
        var produto = new Produto { Nome = "Filé", CustoMedio = 0 };

        produto.RegistrarEntrada(10, 80m, DateTime.Now, null);
        produto.RegistrarEntrada(10, 100m, DateTime.Now, null);

        Assert.Equal(20, produto.EstoqueAtual);
        Assert.Equal(90m, produto.CustoMedio);
    }

    [Fact]
    public void Estorno_de_entrada_devolve_o_saldo_e_o_custo_medio_anterior()
    {
        var produto = new Produto { Nome = "Filé", CustoMedio = 0 };
        produto.RegistrarEntrada(10, 80m, DateTime.Now, null);
        var errada = produto.RegistrarEntrada(10, 100m, DateTime.Now, null);

        var estorno = produto.EstornarEntrada(errada, DateTime.Now, "Lançada em duplicidade");

        Assert.Equal(10, produto.EstoqueAtual);
        Assert.Equal(80m, produto.CustoMedio);
        Assert.Equal(TipoMovimentacao.Estorno, estorno.Tipo);
        Assert.Equal(10, estorno.Quantidade);
        Assert.True(errada.Estornada);
        Assert.Same(errada, estorno.EstornoDe);
        Assert.Throws<RegraNegocioException>(() => produto.EstornarEntrada(errada, DateTime.Now, "De novo"));
        Assert.Throws<RegraNegocioException>(() => produto.EstornarEntrada(estorno, DateTime.Now, "Estorno do estorno"));
    }

    [Fact]
    public void Entrada_ja_usada_nao_pode_ser_estornada()
    {
        var produto = new Produto { Nome = "Arroz" };
        var entrada = produto.RegistrarEntrada(10, 5m, DateTime.Now, null);
        produto.RegistrarSaida(4, DateTime.Now, "Consumo");

        var erro = Assert.Throws<RegraNegocioException>(() => produto.EstornarEntrada(entrada, DateTime.Now, "Engano"));
        Assert.Contains("inventário", erro.Message);
        Assert.Equal(6, produto.EstoqueAtual);
    }

    [Fact]
    public void Saida_maior_que_o_saldo_e_bloqueada_salvo_quando_permitido()
    {
        var produto = new Produto { Nome = "Arroz" };
        produto.RegistrarEntrada(5, 6m, DateTime.Now, null);

        Assert.Throws<RegraNegocioException>(() => produto.RegistrarSaida(6, DateTime.Now, null));

        var mov = produto.RegistrarSaida(6, DateTime.Now, null, permitirSaldoNegativo: true);
        Assert.Equal(-1, produto.EstoqueAtual);
        Assert.Equal(-1, mov.SaldoApos);
    }

    [Fact]
    public void Ajuste_de_inventario_registra_a_diferenca()
    {
        var produto = new Produto { Nome = "Tomate" };
        produto.RegistrarEntrada(10, 8m, DateTime.Now, null);

        var mov = produto.AjustarSaldo(7.5m, DateTime.Now, null);

        Assert.Equal(TipoMovimentacao.Ajuste, mov.Tipo);
        Assert.Equal(-2.5m, mov.Quantidade);
        Assert.Equal(7.5m, produto.EstoqueAtual);
    }
}
