using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

public enum MotivoDevolucao
{
    Avariado,
    Vencido,
    ProdutoErrado,
    ForaDoPedido,
    Qualidade,
    Outro
}

/// <summary>
/// Devolução de parte (ou de tudo) de um recebimento ao fornecedor: os itens saem do estoque pelo custo da entrada (nos
/// perecíveis, do lote que veio naquele recebimento). Se o recebimento ainda aguarda a nota, a nota já vem sem o devolvido;
/// se já foi faturado, o valor é abatido das contas em aberto da nota e o que não couber (nota já paga) vira crédito com o
/// fornecedor, descontado na próxima nota dele.
/// </summary>
public class DevolucaoCompra : Entidade
{
    public const int TamanhoObservacoes = 500;
    public const int TamanhoNotaDevolucao = 60;

    private readonly List<DevolucaoItem> _itens = [];

    private DevolucaoCompra()
    {
    }

    internal DevolucaoCompra(RecebimentoCompra recebimento, DateOnly data, MotivoDevolucao motivo, bool fornecedorRepoe, string? notaDevolucao,
        string? observacoes, string? usuario, DateTime agora)
    {
        Validador.Garantir(Enum.IsDefined(motivo), "Escolha o motivo da devolução.");
        Validador.Garantir(notaDevolucao is null || notaDevolucao.Trim().Length <= TamanhoNotaDevolucao,
            $"O número da nota de devolução pode ter no máximo {TamanhoNotaDevolucao} caracteres.");
        Validador.Garantir(observacoes is null || observacoes.Trim().Length <= TamanhoObservacoes,
            $"As observações podem ter no máximo {TamanhoObservacoes} caracteres.");
        Validador.Garantir(motivo != MotivoDevolucao.Outro || !string.IsNullOrWhiteSpace(observacoes),
            "Com o motivo \"outro\", descreva o motivo nas observações.");
        RecebimentoCompra = recebimento;
        RecebimentoCompraId = recebimento.Id;
        PedidoCompraId = recebimento.PedidoCompraId;
        FornecedorId = recebimento.FornecedorId;
        Data = data;
        Motivo = motivo;
        FornecedorRepoe = fornecedorRepoe;
        NotaDevolucao = string.IsNullOrWhiteSpace(notaDevolucao) ? null : notaDevolucao.Trim();
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
        Usuario = usuario;
        RegistradoEm = agora;
    }

    public string Codigo => EhNovo ? "(nova)" : $"DEV-{Id:D5}";

    public int RecebimentoCompraId { get; private set; }
    public RecebimentoCompra? RecebimentoCompra { get; private set; }
    public int PedidoCompraId { get; private set; }
    public int FornecedorId { get; private set; }
    public DateOnly Data { get; private set; }
    public MotivoDevolucao Motivo { get; private set; }

    /// <summary>O fornecedor vai repor: a quantidade volta ao saldo do pedido. Sem reposição, o pedido fica com o que ficou.</summary>
    public bool FornecedorRepoe { get; private set; }

    /// <summary>Número da nota fiscal de devolução emitida ao fornecedor.</summary>
    public string? NotaDevolucao { get; private set; }

    public string? Observacoes { get; private set; }
    public string? Usuario { get; private set; }
    public DateTime RegistradoEm { get; private set; }

    /// <summary>Nota de entrada (já lançada) da qual o valor foi abatido.</summary>
    public int? NotaFiscalEntradaId { get; private set; }
    public NotaFiscalEntrada? NotaFiscalEntrada { get; private set; }

    /// <summary>Mercadoria devolvida, pelo custo do pedido.</summary>
    public decimal Valor { get; private set; }

    /// <summary>Quanto saiu das contas em aberto da nota.</summary>
    public decimal ValorAbatido { get; private set; }

    /// <summary>Quanto virou crédito com o fornecedor (a nota já estava paga) e quanto desse crédito já foi usado.</summary>
    public decimal ValorCredito { get; private set; }

    public decimal CreditoUtilizado { get; private set; }

    public decimal CreditoDisponivel => ValorCredito - CreditoUtilizado;

    public IReadOnlyCollection<DevolucaoItem> Itens => _itens;

    internal void AdicionarItem(RecebimentoItem item, decimal quantidade, MovimentacaoEstoque saida)
    {
        var novo = new DevolucaoItem(item, quantidade, saida);
        _itens.Add(novo);
        Valor += novo.Total;
    }

    /// <summary>Recebimento já faturado: parte abatida das contas da nota e o restante como crédito.</summary>
    internal void DefinirAbatimento(NotaFiscalEntrada nota, decimal abatido)
    {
        NotaFiscalEntrada = nota;
        ValorAbatido = abatido;
        ValorCredito = Valor - abatido;
    }

    internal decimal UsarCredito(decimal maximo)
    {
        var parte = Math.Min(maximo, CreditoDisponivel);
        CreditoUtilizado += parte;
        return parte;
    }

    /// <summary>A nota que usou o crédito foi cancelada: o crédito volta a ficar disponível.</summary>
    internal void DevolverCredito(decimal valor) => CreditoUtilizado = Math.Max(0, CreditoUtilizado - valor);
}

/// <summary>Quanto foi devolvido de um item do recebimento (na unidade de compra), com a saída do estoque que gerou.</summary>
public class DevolucaoItem
{
    private DevolucaoItem()
    {
    }

    internal DevolucaoItem(RecebimentoItem item, decimal quantidade, MovimentacaoEstoque saida)
    {
        RecebimentoItem = item;
        RecebimentoItemId = item.Id;
        Quantidade = quantidade;
        CustoUnitario = item.CustoUnitario;
        Saida = saida;
    }

    public int Id { get; private set; }
    public int DevolucaoCompraId { get; private set; }
    public int RecebimentoItemId { get; private set; }
    public RecebimentoItem? RecebimentoItem { get; private set; }

    /// <summary>Quantidade devolvida, na unidade de compra do pedido.</summary>
    public decimal Quantidade { get; private set; }

    public decimal CustoUnitario { get; private set; }

    public int? MovimentacaoEstoqueId { get; private set; }
    public MovimentacaoEstoque? Saida { get; private set; }

    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

/// <summary>Crédito de uma devolução usado para pagar parte de uma nota de entrada do mesmo fornecedor.</summary>
public class CreditoUsadoNota
{
    private CreditoUsadoNota()
    {
    }

    internal CreditoUsadoNota(DevolucaoCompra devolucao, decimal valor)
    {
        Devolucao = devolucao;
        DevolucaoCompraId = devolucao.Id;
        Valor = valor;
    }

    public int Id { get; private set; }
    public int NotaFiscalEntradaId { get; private set; }
    public int DevolucaoCompraId { get; private set; }
    public DevolucaoCompra? Devolucao { get; private set; }
    public decimal Valor { get; private set; }
}
