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
