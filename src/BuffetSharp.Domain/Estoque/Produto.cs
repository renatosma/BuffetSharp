using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

/// <summary>
/// Insumo controlado em estoque. O saldo e o custo médio só mudam por movimentações.
/// </summary>
public class Produto : Entidade, IAtivavel, ICamposControlados
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Codigo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a categoria.")]
    public int CategoriaProdutoId { get; set; }
    public CategoriaProduto? CategoriaProduto { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a unidade de medida.")]
    public int UnidadeMedidaId { get; set; }
    public UnidadeMedida? UnidadeMedida { get; set; }

    public int? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Custo médio ponderado por unidade. Pode ser informado no cadastro e é recalculado a cada entrada.</summary>
    [Range(0, 1_000_000, ErrorMessage = "Custo inválido.")]
    public decimal CustoMedio { get; set; }

    public decimal EstoqueAtual { get; private set; }

    [Range(0, 1_000_000, ErrorMessage = "Estoque mínimo inválido.")]
    public decimal EstoqueMinimo { get; set; }

    public bool Ativo { get; set; } = true;

    public bool AbaixoDoMinimo => EstoqueAtual < EstoqueMinimo;

    public decimal ValorEmEstoque => Math.Round(Math.Max(EstoqueAtual, 0) * CustoMedio, 2);

    IReadOnlyCollection<string> ICamposControlados.CamposControladosPeloSistema => [nameof(EstoqueAtual)];

    public MovimentacaoEstoque RegistrarEntrada(decimal quantidade, decimal custoUnitario, DateTime data, string? observacao)
    {
        Validador.Garantir(quantidade > 0, "A quantidade da entrada deve ser maior que zero.");
        Validador.Garantir(custoUnitario >= 0, "O custo unitário não pode ser negativo.");

        var saldoValorizado = Math.Max(EstoqueAtual, 0);
        var novoSaldoValorizado = saldoValorizado + quantidade;
        CustoMedio = Math.Round((saldoValorizado * CustoMedio + quantidade * custoUnitario) / novoSaldoValorizado, 4);
        EstoqueAtual += quantidade;

        return new MovimentacaoEstoque(this, TipoMovimentacao.Entrada, quantidade, custoUnitario, data, observacao);
    }

    public MovimentacaoEstoque RegistrarSaida(decimal quantidade, DateTime data, string? observacao,
        int? eventoId = null, bool permitirSaldoNegativo = false)
    {
        Validador.Garantir(quantidade > 0, "A quantidade da saída deve ser maior que zero.");
        Validador.Garantir(permitirSaldoNegativo || quantidade <= EstoqueAtual,
            $"Saldo insuficiente de '{Nome}': disponível {EstoqueAtual:N3}, solicitado {quantidade:N3}.");

        EstoqueAtual -= quantidade;
        return new MovimentacaoEstoque(this, TipoMovimentacao.Saida, quantidade, CustoMedio, data, observacao, eventoId);
    }

    /// <summary>Inventário: ajusta o saldo para o valor contado fisicamente.</summary>
    public MovimentacaoEstoque AjustarSaldo(decimal saldoContado, DateTime data, string? observacao)
    {
        Validador.Garantir(saldoContado >= 0, "O saldo contado não pode ser negativo.");
        Validador.Garantir(saldoContado != EstoqueAtual, "O saldo informado é igual ao saldo atual.");

        var diferenca = saldoContado - EstoqueAtual;
        EstoqueAtual = saldoContado;
        return new MovimentacaoEstoque(this, TipoMovimentacao.Ajuste, diferenca, CustoMedio, data, observacao);
    }

    public override string ToString() => Nome;
}
