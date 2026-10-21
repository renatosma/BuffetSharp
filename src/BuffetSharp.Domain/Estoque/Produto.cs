using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

/// <summary>
/// Insumo controlado em estoque. O saldo e o custo médio só mudam por movimentações.
/// </summary>
public class Produto : Entidade, IAtivavel, ICamposControlados, IValidatableObject
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

    /// <summary>Embalagens de compra com a conversão para a unidade do estoque (por fornecedor).</summary>
    public List<ProdutoEmbalagem> Embalagens { get; set; } = [];

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

    /// <summary>
    /// Entrada de uma compra informada na unidade do fornecedor: a quantidade e o custo são convertidos para a unidade do
    /// estoque pela embalagem, e a movimentação guarda o que foi comprado (unidade, quantidade, fator e fornecedor).
    /// </summary>
    /// <param name="quantidade">Quantidade na unidade de compra (ou na do estoque, sem embalagem).</param>
    /// <param name="custoUnitario">Custo de uma unidade de compra (ou do estoque, sem embalagem).</param>
    public MovimentacaoEstoque RegistrarCompra(decimal quantidade, decimal custoUnitario, DateTime data, string? observacao,
        int? fornecedorId, ProdutoEmbalagem? embalagem)
    {
        Validador.Garantir(quantidade > 0, "A quantidade da entrada deve ser maior que zero.");
        Validador.Garantir(custoUnitario >= 0, "O custo unitário não pode ser negativo.");
        if (embalagem is null)
        {
            var simples = RegistrarEntrada(quantidade, custoUnitario, data, observacao);
            simples.RegistrarCompra(fornecedorId, null, null, null, null);
            return simples;
        }

        Validador.Garantir(embalagem.ProdutoId == Id, "A embalagem informada não é deste produto.");
        Validador.Garantir(embalagem.FornecedorId is null || embalagem.FornecedorId == fornecedorId,
            "Esta embalagem é de outro fornecedor. Escolha o fornecedor da compra ou outra embalagem.");
        Validador.Garantir(embalagem.Fator > 0, "A conversão da embalagem é inválida.");

        var movimentacao = RegistrarEntrada(embalagem.ParaEstoque(quantidade), embalagem.CustoNoEstoque(custoUnitario), data, observacao);
        movimentacao.RegistrarCompra(fornecedorId, embalagem.UnidadeMedida?.Sigla, quantidade, embalagem.Fator, custoUnitario);
        return movimentacao;
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

    /// <summary>
    /// Desfaz uma entrada lançada por engano: o saldo volta e o custo médio é recalculado sem o custo dela. Se o produto já
    /// foi usado e o saldo não cobre a entrada, o estorno é recusado (corrija pelo inventário).
    /// </summary>
    public MovimentacaoEstoque EstornarEntrada(MovimentacaoEstoque entrada, DateTime data, string motivo)
    {
        Validador.Garantir(entrada.ProdutoId == Id, "A entrada não é deste produto.");
        Validador.Garantir(entrada.Tipo == TipoMovimentacao.Entrada, "Somente entradas podem ser estornadas.");
        Validador.Garantir(!entrada.Estornada, "Esta entrada já foi estornada.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do estorno.");
        Validador.Garantir(entrada.Quantidade <= EstoqueAtual,
            $"O saldo de '{Nome}' ({EstoqueAtual:N3}) é menor que a entrada ({entrada.Quantidade:N3}): o produto já foi usado. " +
            "Corrija o saldo pelo inventário em vez de estornar.");

        // Custo médio sem a entrada: valor em estoque menos o valor dela, dividido pelo saldo que sobra.
        var saldo = EstoqueAtual - entrada.Quantidade;
        if (saldo > 0)
        {
            var custo = Math.Round((EstoqueAtual * CustoMedio - entrada.Quantidade * entrada.CustoUnitario) / saldo, 4);
            if (custo > 0)
                CustoMedio = custo;
        }
        EstoqueAtual = saldo;

        var texto = $"Estorno da entrada de {entrada.Data:dd/MM/yyyy}: {motivo.Trim()}";
        var estorno = new MovimentacaoEstoque(this, TipoMovimentacao.Estorno, entrada.Quantidade, entrada.CustoUnitario, data,
            texto[..Math.Min(texto.Length, 300)]);
        entrada.MarcarEstornada(estorno, data);
        return estorno;
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var embalagem in Embalagens)
        {
            if (embalagem.UnidadeMedidaId <= 0)
                yield return new ValidationResult("Selecione a unidade de compra de cada embalagem.", [nameof(Embalagens)]);
            if (embalagem.Fator <= 0 || embalagem.Fator > ProdutoEmbalagem.FatorMaximo)
                yield return new ValidationResult("A quantidade de cada embalagem (conversão) deve ser maior que zero.", [nameof(Embalagens)]);
            if (embalagem.Descricao?.Length > 60 || embalagem.CodigoFornecedor?.Length > 30)
                yield return new ValidationResult("Descrição da embalagem até 60 caracteres e código no fornecedor até 30.", [nameof(Embalagens)]);
        }

        if (Embalagens.GroupBy(e => (e.FornecedorId, e.UnidadeMedidaId)).Any(g => g.Count() > 1))
            yield return new ValidationResult("Há embalagens repetidas (mesmo fornecedor e mesma unidade de compra).", [nameof(Embalagens)]);
    }

    public override string ToString() => Nome;
}
