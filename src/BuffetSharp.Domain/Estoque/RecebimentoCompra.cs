using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

public enum StatusRecebimento
{
    /// <summary>Mercadoria no estoque, aguardando a nota fiscal do fornecedor.</summary>
    AFaturar,

    /// <summary>Incluído numa nota fiscal de entrada (que gerou as contas a pagar).</summary>
    Faturado,

    Estornado
}

/// <summary>
/// Chegada da mercadoria de um pedido de compra (toda ou parte dela). Os itens entram no estoque na hora; a nota fiscal de
/// entrada vem depois e pode juntar vários recebimentos do mesmo fornecedor.
/// </summary>
public class RecebimentoCompra : Entidade
{
    public const int TamanhoObservacoes = 500;

    private readonly List<RecebimentoItem> _itens = [];

    private RecebimentoCompra()
    {
    }

    internal RecebimentoCompra(PedidoCompra pedido, DateOnly data, string? observacoes, string? usuario, DateTime agora)
    {
        Validador.Garantir(observacoes is null || observacoes.Trim().Length <= TamanhoObservacoes,
            $"As observações podem ter no máximo {TamanhoObservacoes} caracteres.");
        PedidoCompra = pedido;
        PedidoCompraId = pedido.Id;
        FornecedorId = pedido.FornecedorId;
        Data = data;
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
        Usuario = usuario;
        RegistradoEm = agora;
    }

    public string Codigo => EhNovo ? "(novo)" : $"REC-{Id:D5}";

    public int PedidoCompraId { get; private set; }
    public PedidoCompra? PedidoCompra { get; private set; }

    /// <summary>Fornecedor do pedido (repetido aqui para juntar os recebimentos na nota).</summary>
    public int FornecedorId { get; private set; }

    public DateOnly Data { get; private set; }
    public string? Observacoes { get; private set; }
    public string? Usuario { get; private set; }
    public DateTime RegistradoEm { get; private set; }
    public StatusRecebimento Status { get; private set; } = StatusRecebimento.AFaturar;

    /// <summary>Nota fiscal de entrada que faturou o recebimento.</summary>
    public int? NotaFiscalEntradaId { get; private set; }
    public NotaFiscalEntrada? NotaFiscalEntrada { get; private set; }

    public DateTime? EstornadoEm { get; private set; }
    public string? MotivoEstorno { get; private set; }

    public IReadOnlyCollection<RecebimentoItem> Itens => _itens;

    /// <summary>Valor da mercadoria recebida, pelo custo do pedido.</summary>
    public decimal Total => _itens.Sum(i => i.Total);

    internal void AdicionarItem(PedidoCompraItem item, decimal quantidade) =>
        _itens.Add(new RecebimentoItem(item, quantidade));

    /// <summary>
    /// Dá entrada de cada item no estoque (convertido pela embalagem, com o fornecedor). Exige o produto e a embalagem dos
    /// itens do pedido carregados.
    /// </summary>
    public IReadOnlyList<MovimentacaoEstoque> DarEntrada(DateTime quando)
    {
        var codigo = PedidoCompra?.Codigo ?? $"PC-{PedidoCompraId:D5}";
        var entradas = new List<MovimentacaoEstoque>();
        foreach (var item in _itens)
        {
            var doPedido = item.PedidoCompraItem!;
            var entrada = doPedido.Produto!.RegistrarCompra(item.Quantidade, item.CustoUnitario, quando, $"Recebimento do pedido {codigo}",
                FornecedorId, doPedido.Embalagem);
            entrada.MarcarDePedido();
            item.DefinirEntrada(entrada);
            entradas.Add(entrada);
        }
        return entradas;
    }

    /// <summary>
    /// Mercadoria recebida por engano (ou devolvida): as entradas são estornadas e as quantidades voltam ao saldo do pedido.
    /// Recebimento já faturado precisa ter a nota cancelada antes.
    /// </summary>
    public IReadOnlyList<MovimentacaoEstoque> Estornar(string motivo, DateTime agora)
    {
        Validador.Garantir(Status != StatusRecebimento.Estornado, "Este recebimento já foi estornado.");
        Validador.Garantir(Status == StatusRecebimento.AFaturar,
            $"O recebimento já está na nota fiscal {NotaFiscalEntrada?.Descricao ?? "de entrada"}. Cancele a nota antes de estornar.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do estorno.");

        var texto = $"{Codigo}: {motivo.Trim()}";
        var estornos = _itens
            .Where(i => i.Entrada is { Estornada: false })
            .Select(i => i.Entrada!.Produto!.EstornarEntrada(i.Entrada, agora, texto, permitirPedido: true))
            .ToList();
        PedidoCompra!.DesfazerRecebimento(this);
        Status = StatusRecebimento.Estornado;
        EstornadoEm = agora;
        MotivoEstorno = motivo.Trim()[..Math.Min(motivo.Trim().Length, TamanhoObservacoes)];
        return estornos;
    }

    internal void Faturar(NotaFiscalEntrada nota)
    {
        Validador.Garantir(Status == StatusRecebimento.AFaturar,
            Status == StatusRecebimento.Estornado ? $"O recebimento {Codigo} foi estornado." : $"O recebimento {Codigo} já foi faturado.");
        Status = StatusRecebimento.Faturado;
        NotaFiscalEntrada = nota;
    }

    /// <summary>Nota cancelada: o recebimento volta a aguardar a nota.</summary>
    internal void VoltarAFaturar()
    {
        Status = StatusRecebimento.AFaturar;
        NotaFiscalEntrada = null;
        NotaFiscalEntradaId = null;
    }
}

/// <summary>Quanto chegou de um item do pedido, na unidade de compra, com a entrada no estoque que gerou.</summary>
public class RecebimentoItem
{
    private RecebimentoItem()
    {
    }

    internal RecebimentoItem(PedidoCompraItem item, decimal quantidade)
    {
        PedidoCompraItem = item;
        PedidoCompraItemId = item.Id;
        Quantidade = quantidade;
        CustoUnitario = item.CustoUnitario;
    }

    public int Id { get; private set; }
    public int RecebimentoCompraId { get; private set; }
    public int PedidoCompraItemId { get; private set; }
    public PedidoCompraItem? PedidoCompraItem { get; private set; }

    /// <summary>Quantidade recebida, na unidade de compra do pedido.</summary>
    public decimal Quantidade { get; private set; }

    /// <summary>Custo de uma unidade de compra (o do pedido).</summary>
    public decimal CustoUnitario { get; private set; }

    public int? MovimentacaoEstoqueId { get; private set; }
    public MovimentacaoEstoque? Entrada { get; private set; }

    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);

    internal void DefinirEntrada(MovimentacaoEstoque entrada) => Entrada = entrada;
}
