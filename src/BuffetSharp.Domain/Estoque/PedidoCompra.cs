using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Domain.Estoque;

public enum StatusPedidoCompra
{
    /// <summary>Pedido feito ao fornecedor; nada recebido ainda (os itens podem mudar).</summary>
    Aberto,

    /// <summary>Parte dos itens já chegou; o saldo ainda é esperado.</summary>
    ParcialmenteRecebido,

    /// <summary>Tudo recebido.</summary>
    Recebido,

    /// <summary>Recebido em parte e o saldo foi dispensado (não vem mais).</summary>
    Encerrado,

    Cancelado
}

/// <summary>
/// Pedido de compra a um fornecedor, com vários produtos. A mercadoria chega em um ou mais recebimentos (que dão entrada no
/// estoque) e depois é faturada pela nota fiscal de entrada, que gera as contas a pagar.
/// </summary>
public class PedidoCompra : Entidade
{
    private readonly List<PedidoCompraItem> _itens = [];

    private PedidoCompra()
    {
    }

    public PedidoCompra(int fornecedorId, DateOnly data)
    {
        Alterar(fornecedorId, data, null, CategoriaDespesa.Insumos, null);
    }

    public string Codigo => EhNovo ? "(novo)" : $"PC-{Id:D5}";

    public int FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }
    public DateOnly Data { get; private set; }
    public DateOnly? PrevisaoEntrega { get; private set; }

    /// <summary>Categoria da despesa lançada em contas a pagar pela nota de entrada.</summary>
    public CategoriaDespesa Categoria { get; private set; } = CategoriaDespesa.Insumos;

    public string? Observacoes { get; private set; }

    /// <summary>Prazo combinado com o fornecedor: sugere os vencimentos da nota de entrada.</summary>
    public int? CondicaoPagamentoId { get; private set; }
    public CondicaoPagamento? CondicaoPagamento { get; private set; }

    /// <summary>Meio de pagamento combinado (boleto, PIX...): vai para as contas a pagar da nota.</summary>
    public int? FormaPagamentoId { get; private set; }
    public FormaPagamento? FormaPagamento { get; private set; }

    public StatusPedidoCompra Status { get; private set; } = StatusPedidoCompra.Aberto;
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Cotação que originou o pedido (quando houver).</summary>
    public int? CotacaoCompraId { get; private set; }
    public CotacaoCompra? CotacaoCompra { get; private set; }

    public IReadOnlyCollection<PedidoCompraItem> Itens => _itens;

    public decimal Total => _itens.Sum(i => i.Total);

    public bool PodeReceber => Status is StatusPedidoCompra.Aberto or StatusPedidoCompra.ParcialmenteRecebido;

    public void Alterar(int fornecedorId, DateOnly data, DateOnly? previsaoEntrega, CategoriaDespesa categoria, string? observacoes)
    {
        GarantirEditavel();
        Validador.Garantir(fornecedorId > 0, "Selecione o fornecedor.");
        Validador.Garantir(categoria != CategoriaDespesa.FaturaCartao, "Escolha a categoria da despesa da compra.");
        Validador.Garantir(previsaoEntrega is null || previsaoEntrega >= data, "A previsão de entrega não pode ser antes do pedido.");
        Validador.Garantir(observacoes is null || observacoes.Trim().Length <= 1000, "As observações podem ter no máximo 1.000 caracteres.");
        FornecedorId = fornecedorId;
        Data = data;
        PrevisaoEntrega = previsaoEntrega;
        Categoria = categoria;
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
    }

    /// <summary>Substitui os itens. A embalagem, quando informada, deve ser do produto e deste fornecedor (ou valer para qualquer um).</summary>
    public void DefinirItens(IEnumerable<(Produto Produto, ProdutoEmbalagem? Embalagem, decimal Quantidade, decimal CustoUnitario)> itens)
    {
        GarantirEditavel();
        var novos = itens.ToList();
        Validador.Garantir(novos.Count > 0, "Inclua ao menos um produto no pedido.");
        foreach (var (produto, embalagem, quantidade, custo) in novos)
        {
            Validador.Garantir(quantidade > 0, $"Informe a quantidade de '{produto.Nome}'.");
            Validador.Garantir(custo >= 0, $"O custo de '{produto.Nome}' não pode ser negativo.");
            Validador.Garantir(embalagem is null || (embalagem.ProdutoId == produto.Id &&
                                                     (embalagem.FornecedorId is null || embalagem.FornecedorId == FornecedorId)),
                $"A unidade de compra escolhida não vale para '{produto.Nome}' neste fornecedor.");
        }

        _itens.Clear();
        foreach (var (produto, embalagem, quantidade, custo) in novos)
            _itens.Add(new PedidoCompraItem(produto, embalagem, quantidade, custo));
    }

    public void DefinirPagamento(int? condicaoPagamentoId, int? formaPagamentoId)
    {
        GarantirEditavel();
        Validador.Garantir(condicaoPagamentoId is null or > 0 && formaPagamentoId is null or > 0, "Condição ou forma de pagamento inválida.");
        CondicaoPagamentoId = condicaoPagamentoId;
        FormaPagamentoId = formaPagamentoId;
    }

    internal void VincularCotacao(CotacaoCompra cotacao)
    {
        CotacaoCompra = cotacao;
        CotacaoCompraId = cotacao.EhNovo ? null : cotacao.Id;
    }

    /// <summary>
    /// Registra o que chegou (quantidades na unidade de compra, por item). A entrada no estoque é feita pelo serviço, que liga
    /// cada movimentação ao item do recebimento.
    /// </summary>
    /// <param name="lotes">Lote(s) e validade de cada item (quantidades na unidade do estoque), para os produtos que controlam validade.</param>
    public RecebimentoCompra Receber(DateOnly data, IReadOnlyDictionary<PedidoCompraItem, decimal> quantidades, string? observacoes,
        string? usuario, DateTime agora, IReadOnlyDictionary<PedidoCompraItem, IReadOnlyList<LoteInformado>>? lotes = null)
    {
        Validador.Garantir(PodeReceber, Status switch
        {
            StatusPedidoCompra.Cancelado => "Pedido cancelado não pode ser recebido.",
            _ => "Este pedido já foi todo recebido (ou teve o saldo encerrado)."
        });
        Validador.Garantir(data <= DateOnly.FromDateTime(agora), "A data do recebimento não pode ser futura.");
        var recebidos = quantidades.Where(q => q.Value > 0).ToList();
        Validador.Garantir(recebidos.Count > 0, "Informe a quantidade recebida de ao menos um produto.");
        foreach (var (item, quantidade) in recebidos)
        {
            Validador.Garantir(_itens.Contains(item), "Um dos itens não é deste pedido.");
            Validador.Garantir(quantidade <= item.Saldo,
                $"'{item.Produto?.Nome}': chegaram {quantidade:0.###}, mas o saldo do pedido é {item.Saldo:0.###}.");
        }

        var recebimento = new RecebimentoCompra(this, data, observacoes, usuario, agora);
        foreach (var (item, quantidade) in recebidos)
        {
            item.QuantidadeRecebida += quantidade;
            recebimento.AdicionarItem(item, quantidade, lotes?.GetValueOrDefault(item));
        }
        AtualizarStatus();
        return recebimento;
    }

    /// <summary>Recebimento estornado: as quantidades voltam ao saldo do pedido.</summary>
    internal void DesfazerRecebimento(RecebimentoCompra recebimento)
    {
        foreach (var item in recebimento.Itens)
            item.PedidoCompraItem!.QuantidadeRecebida -= item.Quantidade;
        if (Status == StatusPedidoCompra.Encerrado)
            Status = StatusPedidoCompra.ParcialmenteRecebido;
        AtualizarStatus();
    }

    /// <summary>Devolução que o fornecedor vai repor: as quantidades voltam ao saldo do pedido (que fica esperando a entrega).</summary>
    internal void VoltarAoSaldo(IEnumerable<(PedidoCompraItem Item, decimal Quantidade)> devolvidos)
    {
        foreach (var (item, quantidade) in devolvidos)
            item.QuantidadeRecebida -= quantidade;
        if (Status == StatusPedidoCompra.Encerrado)
            Status = StatusPedidoCompra.ParcialmenteRecebido;
        AtualizarStatus();
    }

    /// <summary>Dispensa o saldo que não veio: o pedido fica encerrado com o que foi recebido.</summary>
    public void EncerrarSaldo(string motivo)
    {
        Validador.Garantir(Status == StatusPedidoCompra.ParcialmenteRecebido,
            "Só é possível encerrar o saldo de um pedido recebido em parte. Para um pedido sem recebimento, cancele-o.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo (ex.: fornecedor sem estoque).");
        Status = StatusPedidoCompra.Encerrado;
        MotivoCancelamento = motivo.Trim();
    }

    public void Cancelar(string motivo)
    {
        Validador.Garantir(Status == StatusPedidoCompra.Aberto,
            Status == StatusPedidoCompra.Cancelado ? "O pedido já está cancelado."
                : "O pedido já teve recebimento: estorne os recebimentos ou encerre o saldo.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        Status = StatusPedidoCompra.Cancelado;
        MotivoCancelamento = motivo.Trim();
    }

    private void AtualizarStatus()
    {
        if (Status is StatusPedidoCompra.Cancelado or StatusPedidoCompra.Encerrado)
            return;
        Status = _itens.All(i => i.QuantidadeRecebida == 0) ? StatusPedidoCompra.Aberto
            : _itens.All(i => i.Saldo <= 0) ? StatusPedidoCompra.Recebido
            : StatusPedidoCompra.ParcialmenteRecebido;
    }

    private void GarantirEditavel() =>
        Validador.Garantir(Status == StatusPedidoCompra.Aberto,
            Status == StatusPedidoCompra.Cancelado
                ? "Pedido cancelado não pode ser alterado."
                : "O pedido já teve recebimento e não pode mais ser alterado. Para corrigir, estorne os recebimentos.");
}

/// <summary>Produto do pedido, na unidade de compra (embalagem do fornecedor) ou na unidade do estoque.</summary>
public class PedidoCompraItem
{
    private PedidoCompraItem()
    {
    }

    internal PedidoCompraItem(Produto produto, ProdutoEmbalagem? embalagem, decimal quantidade, decimal custoUnitario)
    {
        Produto = produto;
        ProdutoId = produto.Id;
        Embalagem = embalagem;
        ProdutoEmbalagemId = embalagem?.Id;
        Quantidade = quantidade;
        CustoUnitario = custoUnitario;
    }

    public int Id { get; private set; }
    public int PedidoCompraId { get; private set; }
    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Unidade de compra (nula = unidade do estoque).</summary>
    public int? ProdutoEmbalagemId { get; private set; }
    public ProdutoEmbalagem? Embalagem { get; private set; }

    /// <summary>Quantidade pedida, na unidade de compra.</summary>
    public decimal Quantidade { get; private set; }

    /// <summary>Custo de uma unidade de compra.</summary>
    public decimal CustoUnitario { get; private set; }

    /// <summary>Quanto já chegou (soma dos recebimentos não estornados), na unidade de compra.</summary>
    public decimal QuantidadeRecebida { get; internal set; }

    public decimal Saldo => Quantidade - QuantidadeRecebida;
    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}
