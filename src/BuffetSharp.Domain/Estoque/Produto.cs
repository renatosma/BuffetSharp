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

    /// <summary>
    /// Perecível: cada entrada informa a validade (e o lote), as saídas consomem do que vence primeiro e o vencido não sai para
    /// eventos.
    /// </summary>
    public bool ControlaValidade { get; set; }

    /// <summary>O que o insumo contém (glúten, lactose, carne...): os pratos herdam pela ficha técnica, para as restrições alimentares.</summary>
    public Cardapio.ConteudoAlimentar Contem { get; set; }

    /// <summary>Lotes criados pelas entradas. Saídas, ajustes, descartes e estornos precisam deles carregados.</summary>
    public List<LoteEstoque> Lotes { get; private set; } = [];

    /// <summary>
    /// Saldo que não está em lote: o que já havia antes de o produto passar a controlar validade e o que entrou por ajuste de
    /// inventário. Sai antes dos lotes.
    /// </summary>
    public decimal SaldoSemLote => ControlaValidade ? EstoqueAtual - Lotes.Sum(l => l.Saldo) : EstoqueAtual;

    public bool AbaixoDoMinimo => EstoqueAtual < EstoqueMinimo;

    /// <summary>
    /// Embalagem para comprar a quantidade (na unidade do estoque) do fornecedor: a dele ou, se não houver, a que vale para
    /// qualquer fornecedor. Entre as possíveis, a maior que não passe da quantidade, para comprar o mínimo de embalagens
    /// sem sobra grande; se todas forem maiores, a menor. Nula = comprar na unidade do estoque.
    /// </summary>
    public ProdutoEmbalagem? EmbalagemDeCompra(int? fornecedorId, decimal quantidade)
    {
        var candidatas = Embalagens.Where(e => e.FornecedorId is not null && e.FornecedorId == fornecedorId && e.Fator > 1).ToList();
        if (candidatas.Count == 0)
            candidatas = Embalagens.Where(e => e.FornecedorId is null && e.Fator > 1).ToList();
        if (candidatas.Count == 0)
            return null;
        return candidatas.Where(e => e.Fator <= quantidade).MaxBy(e => e.Fator) ?? candidatas.MinBy(e => e.Fator)!;
    }

    public decimal ValorEmEstoque => Math.Round(Math.Max(EstoqueAtual, 0) * CustoMedio, 2);

    IReadOnlyCollection<string> ICamposControlados.CamposControladosPeloSistema => [nameof(EstoqueAtual)];

    /// <param name="lotes">Lote(s) e validade da entrada; obrigatório nos produtos que controlam validade (ignorado nos demais).</param>
    public MovimentacaoEstoque RegistrarEntrada(decimal quantidade, decimal custoUnitario, DateTime data, string? observacao,
        IReadOnlyList<LoteInformado>? lotes = null)
    {
        Validador.Garantir(quantidade > 0, "A quantidade da entrada deve ser maior que zero.");
        Validador.Garantir(custoUnitario >= 0, "O custo unitário não pode ser negativo.");
        Validador.Garantir(!ControlaValidade || lotes is { Count: > 0 }, $"'{Nome}' controla validade: informe a validade da entrada.");

        var saldoValorizado = Math.Max(EstoqueAtual, 0);
        var novoSaldoValorizado = saldoValorizado + quantidade;
        CustoMedio = Math.Round((saldoValorizado * CustoMedio + quantidade * custoUnitario) / novoSaldoValorizado, 4);
        EstoqueAtual += quantidade;

        var entrada = new MovimentacaoEstoque(this, TipoMovimentacao.Entrada, quantidade, custoUnitario, data, observacao);
        if (ControlaValidade)
            CriarLotes(entrada, quantidade, custoUnitario, data, lotes!);
        return entrada;
    }

    /// <summary>Um lote por item informado; o último fica com o que sobrar da quantidade da entrada.</summary>
    private void CriarLotes(MovimentacaoEstoque entrada, decimal quantidade, decimal custoUnitario, DateTime data, IReadOnlyList<LoteInformado> lotes)
    {
        var restante = quantidade;
        for (var i = 0; i < lotes.Count && restante > 0; i++)
        {
            var informado = lotes[i];
            var parte = i == lotes.Count - 1 ? restante : Math.Min(informado.Quantidade ?? restante, restante);
            if (parte <= 0)
                continue;
            var lote = new LoteEstoque(this, informado.Numero, informado.Validade, parte, custoUnitario, data);
            Lotes.Add(lote);
            entrada.AdicionarLote(lote, parte);
            restante -= parte;
        }
    }

    /// <summary>
    /// Entrada de uma compra informada na unidade do fornecedor: a quantidade e o custo são convertidos para a unidade do
    /// estoque pela embalagem, e a movimentação guarda o que foi comprado (unidade, quantidade, fator e fornecedor).
    /// </summary>
    /// <param name="quantidade">Quantidade na unidade de compra (ou na do estoque, sem embalagem).</param>
    /// <param name="custoUnitario">Custo de uma unidade de compra (ou do estoque, sem embalagem).</param>
    /// <param name="lotes">Lote(s) e validade, com as quantidades já na unidade do estoque.</param>
    public MovimentacaoEstoque RegistrarCompra(decimal quantidade, decimal custoUnitario, DateTime data, string? observacao,
        int? fornecedorId, ProdutoEmbalagem? embalagem, IReadOnlyList<LoteInformado>? lotes = null)
    {
        Validador.Garantir(quantidade > 0, "A quantidade da entrada deve ser maior que zero.");
        Validador.Garantir(custoUnitario >= 0, "O custo unitário não pode ser negativo.");
        if (embalagem is null)
        {
            var simples = RegistrarEntrada(quantidade, custoUnitario, data, observacao, lotes);
            simples.RegistrarCompra(fornecedorId, null, null, null, null);
            return simples;
        }

        Validador.Garantir(embalagem.ProdutoId == Id, "A embalagem informada não é deste produto.");
        Validador.Garantir(embalagem.FornecedorId is null || embalagem.FornecedorId == fornecedorId,
            "Esta embalagem é de outro fornecedor. Escolha o fornecedor da compra ou outra embalagem.");
        Validador.Garantir(embalagem.Fator > 0, "A conversão da embalagem é inválida.");

        var movimentacao = RegistrarEntrada(embalagem.ParaEstoque(quantidade), embalagem.CustoNoEstoque(custoUnitario), data, observacao, lotes);
        movimentacao.RegistrarCompra(fornecedorId, embalagem.UnidadeMedida?.Sigla, quantidade, embalagem.Fator, custoUnitario);
        return movimentacao;
    }

    public MovimentacaoEstoque RegistrarSaida(decimal quantidade, DateTime data, string? observacao,
        int? eventoId = null, bool permitirSaldoNegativo = false)
    {
        Validador.Garantir(quantidade > 0, "A quantidade da saída deve ser maior que zero.");
        var hoje = DateOnly.FromDateTime(data);
        var vencido = ControlaValidade ? Lotes.Where(l => l.Vencido(hoje)).Sum(l => l.Saldo) : 0;
        Validador.Garantir(permitirSaldoNegativo || quantidade <= EstoqueAtual - vencido,
            $"Saldo insuficiente de '{Nome}': disponível {EstoqueAtual - vencido:N3}" + (vencido > 0 ? $" (fora {vencido:N3} vencido)" : "") +
            $", solicitado {quantidade:N3}.");

        var consumo = ConsumirLotes(quantidade, hoje, incluirVencidos: false);
        EstoqueAtual -= quantidade;
        var saida = new MovimentacaoEstoque(this, TipoMovimentacao.Saida, quantidade, CustoMedio, data, observacao, eventoId);
        foreach (var (lote, parte) in consumo)
            saida.AdicionarLote(lote, parte);
        return saida;
    }

    /// <summary>
    /// De onde sai a quantidade nos produtos que controlam validade: primeiro o saldo sem lote, depois os lotes do que vence
    /// primeiro para o que vence depois (os vencidos só quando <paramref name="incluirVencidos"/>, no inventário).
    /// </summary>
    private List<(LoteEstoque Lote, decimal Quantidade)> ConsumirLotes(decimal quantidade, DateOnly hoje, bool incluirVencidos)
    {
        var consumo = new List<(LoteEstoque, decimal)>();
        if (!ControlaValidade)
            return consumo;
        var restante = quantidade - Math.Max(0, SaldoSemLote);
        foreach (var lote in Lotes.Where(l => l.Saldo > 0 && (incluirVencidos || !l.Vencido(hoje))).OrderBy(l => l.Validade).ThenBy(l => l.EntradaEm))
        {
            if (restante <= 0)
                break;
            var parte = Math.Min(lote.Saldo, restante);
            lote.Saldo -= parte;
            consumo.Add((lote, parte));
            restante -= parte;
        }
        return consumo;
    }

    /// <summary>
    /// Descarte de produto vencido ou estragado: sai do estoque como perda, pelo custo do lote (ou pelo custo médio, sem lote).
    /// Nos produtos que controlam validade, o lote é informado (sem ele, sai do saldo sem lote).
    /// </summary>
    public MovimentacaoEstoque Descartar(LoteEstoque? lote, decimal quantidade, DateTime data, string motivo)
    {
        Validador.Garantir(quantidade > 0, "Informe a quantidade descartada.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do descarte.");
        if (lote is not null)
        {
            Validador.Garantir(Lotes.Contains(lote), "O lote não é deste produto.");
            Validador.Garantir(quantidade <= lote.Saldo, $"O {lote.Descricao.ToLowerInvariant()} tem {lote.Saldo:N3} em estoque.");
        }
        else
            Validador.Garantir(quantidade <= Math.Max(0, SaldoSemLote),
                ControlaValidade ? $"Escolha o lote: o saldo sem lote de '{Nome}' é {Math.Max(0, SaldoSemLote):N3}."
                    : $"Saldo insuficiente de '{Nome}': disponível {EstoqueAtual:N3}.");

        if (lote is not null)
            lote.Saldo -= quantidade;
        EstoqueAtual -= quantidade;
        var texto = $"Perda: {motivo.Trim()}" + (lote is null ? "" : $" ({lote.Descricao})");
        var perda = new MovimentacaoEstoque(this, TipoMovimentacao.Perda, quantidade, lote?.CustoUnitario ?? CustoMedio, data, texto[..Math.Min(texto.Length, 300)]);
        if (lote is not null)
            perda.AdicionarLote(lote, quantidade);
        return perda;
    }

    /// <summary>
    /// Desfaz uma entrada lançada por engano: o saldo volta e o custo médio é recalculado sem o custo dela. Se o produto já
    /// foi usado e o saldo não cobre a entrada, o estorno é recusado (corrija pelo inventário).
    /// </summary>
    /// <param name="permitirPedido">Verdadeiro só no estorno do recebimento do pedido (que estorna todas as entradas dele).</param>
    public MovimentacaoEstoque EstornarEntrada(MovimentacaoEstoque entrada, DateTime data, string motivo, bool permitirPedido = false)
    {
        Validador.Garantir(entrada.ProdutoId == Id, "A entrada não é deste produto.");
        Validador.Garantir(entrada.Tipo == TipoMovimentacao.Entrada, "Somente entradas podem ser estornadas.");
        Validador.Garantir(!entrada.Estornada, "Esta entrada já foi estornada.");
        Validador.Garantir(!entrada.DePedidoCompra || permitirPedido,
            "Esta entrada veio do recebimento de um pedido de compra. Estorne o recebimento (Compras › Recebimentos).");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do estorno.");
        Validador.Garantir(entrada.Quantidade <= EstoqueAtual,
            $"O saldo de '{Nome}' ({EstoqueAtual:N3}) é menor que a entrada ({entrada.Quantidade:N3}): o produto já foi usado. " +
            "Corrija o saldo pelo inventário em vez de estornar.");
        foreach (var item in entrada.Lotes)
            Validador.Garantir(item.Lote!.Saldo >= item.Quantidade,
                $"O {item.Lote.Descricao.ToLowerInvariant()} de '{Nome}' já foi usado. Corrija o saldo pelo inventário em vez de estornar.");
        foreach (var item in entrada.Lotes)
            item.Lote!.Saldo -= item.Quantidade;

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

    /// <summary>
    /// Devolução ao fornecedor de parte de uma entrada (recebimento do pedido): sai pelo custo dela e o custo médio é
    /// recalculado sem ela, como no estorno. Nos perecíveis, sai do lote que veio na entrada (recusado se ele já foi usado).
    /// </summary>
    /// <param name="quantidade">Na unidade do estoque.</param>
    public MovimentacaoEstoque DevolverAoFornecedor(MovimentacaoEstoque entrada, decimal quantidade, DateTime data, string texto)
    {
        Validador.Garantir(entrada.ProdutoId == Id, "A entrada não é deste produto.");
        Validador.Garantir(entrada.Tipo == TipoMovimentacao.Entrada && !entrada.Estornada, "Só se devolve o que entrou (e não foi estornado).");
        Validador.Garantir(quantidade > 0, "Informe a quantidade devolvida.");
        Validador.Garantir(quantidade <= EstoqueAtual,
            $"O saldo de '{Nome}' ({EstoqueAtual:N3}) é menor que a devolução ({quantidade:N3}): o produto já foi usado.");

        var consumo = new List<(LoteEstoque Lote, decimal Quantidade)>();
        var lotes = entrada.Lotes.Select(l => l.Lote!).Distinct().OrderBy(l => l.Validade).ToList();
        if (ControlaValidade && lotes.Count > 0)
        {
            var disponivel = lotes.Sum(l => l.Saldo);
            Validador.Garantir(quantidade <= disponivel,
                $"Do lote recebido de '{Nome}' restam {disponivel:N3} no estoque (o resto já foi usado ou descartado).");
            var restante = quantidade;
            foreach (var lote in lotes.Where(l => l.Saldo > 0))
            {
                var parte = Math.Min(lote.Saldo, restante);
                lote.Saldo -= parte;
                consumo.Add((lote, parte));
                restante -= parte;
                if (restante <= 0)
                    break;
            }
        }

        var saldo = EstoqueAtual - quantidade;
        if (saldo > 0)
        {
            var custo = Math.Round((EstoqueAtual * CustoMedio - quantidade * entrada.CustoUnitario) / saldo, 4);
            if (custo > 0)
                CustoMedio = custo;
        }
        EstoqueAtual = saldo;
        var devolucao = new MovimentacaoEstoque(this, TipoMovimentacao.Devolucao, quantidade, entrada.CustoUnitario, data,
            texto[..Math.Min(texto.Length, 300)]);
        foreach (var (lote, parte) in consumo)
            devolucao.AdicionarLote(lote, parte);
        return devolucao;
    }

    /// <summary>Inventário: ajusta o saldo para o valor contado fisicamente.</summary>
    public MovimentacaoEstoque AjustarSaldo(decimal saldoContado, DateTime data, string? observacao)
    {
        Validador.Garantir(saldoContado >= 0, "O saldo contado não pode ser negativo.");
        Validador.Garantir(saldoContado != EstoqueAtual, "O saldo informado é igual ao saldo atual.");

        var diferenca = saldoContado - EstoqueAtual;

        // Faltou produto: sai do saldo sem lote e dos lotes, inclusive os vencidos. Sobrou: fica sem lote.
        var consumo = diferenca < 0 ? ConsumirLotes(-diferenca, DateOnly.FromDateTime(data), incluirVencidos: true) : [];
        EstoqueAtual = saldoContado;
        var ajuste = new MovimentacaoEstoque(this, TipoMovimentacao.Ajuste, diferenca, CustoMedio, data, observacao);
        foreach (var (lote, parte) in consumo)
            ajuste.AdicionarLote(lote, parte);
        return ajuste;
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
