using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Domain.Estoque;

public enum StatusPedidoCompra
{
    /// <summary>Pedido feito ao fornecedor; os itens ainda podem mudar.</summary>
    Aberto,

    /// <summary>Mercadoria recebida com a nota fiscal: os itens entraram no estoque e a conta a pagar foi gerada.</summary>
    Recebido,

    Cancelado
}

/// <summary>
/// Pedido de compra a um fornecedor, com vários produtos. No recebimento informa-se a nota fiscal, a data da compra e os
/// vencimentos: todos os itens entram no estoque de uma vez e a nota vira uma conta a pagar (ou parcelas), ligada ao pedido.
/// </summary>
public class PedidoCompra : Entidade
{
    public const int TamanhoNotaFiscal = ContaPagar.TamanhoDocumento;
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

    /// <summary>Categoria da despesa lançada em contas a pagar.</summary>
    public CategoriaDespesa Categoria { get; private set; } = CategoriaDespesa.Insumos;

    public string? Observacoes { get; private set; }
    public StatusPedidoCompra Status { get; private set; } = StatusPedidoCompra.Aberto;

    public string? NotaFiscal { get; private set; }

    /// <summary>Data da compra (emissão da nota).</summary>
    public DateOnly? DataCompra { get; private set; }

    public DateTime? RecebidoEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Conta a pagar da nota (vencimento único).</summary>
    public int? ContaPagarId { get; private set; }
    public ContaPagar? ContaPagar { get; private set; }

    /// <summary>Parcelas da nota (mais de um vencimento).</summary>
    public int? ParcelamentoPagarId { get; private set; }
    public ParcelamentoPagar? ParcelamentoPagar { get; private set; }

    public IReadOnlyCollection<PedidoCompraItem> Itens => _itens;

    public decimal Total => _itens.Sum(i => i.Total);

    public void Alterar(int fornecedorId, DateOnly data, DateOnly? previsaoEntrega, CategoriaDespesa categoria, string? observacoes)
    {
        GarantirAberto();
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

    /// <summary>
    /// Substitui os itens do pedido. A embalagem, quando informada, deve ser do produto e deste fornecedor (ou valer para qualquer um).
    /// </summary>
    public void DefinirItens(IEnumerable<(Produto Produto, ProdutoEmbalagem? Embalagem, decimal Quantidade, decimal CustoUnitario)> itens)
    {
        GarantirAberto();
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

    /// <summary>
    /// Registra o recebimento: confere a nota e os vencimentos. As entradas no estoque e a conta a pagar são criadas pelo serviço,
    /// que as liga ao pedido com <see cref="VincularRecebimento"/>.
    /// </summary>
    public void ConferirRecebimento(string notaFiscal, DateOnly dataCompra, IReadOnlyList<(DateOnly Vencimento, decimal Valor)> vencimentos,
        DateOnly hoje)
    {
        GarantirAberto();
        Validador.Garantir(_itens.Count > 0, "O pedido não tem itens.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(notaFiscal), "Informe o número da nota fiscal.");
        Validador.Garantir(notaFiscal.Trim().Length <= TamanhoNotaFiscal, $"A nota fiscal pode ter no máximo {TamanhoNotaFiscal} caracteres.");
        Validador.Garantir(dataCompra <= hoje, "A data da compra não pode ser futura.");
        Validador.Garantir(Total > 0, "O pedido está sem valor: informe o custo dos itens.");
        Validador.Garantir(vencimentos.Count is >= 1 and <= PlanoParcelas.MaximoParcelas,
            $"Informe de 1 a {PlanoParcelas.MaximoParcelas} vencimentos.");
        Validador.Garantir(vencimentos.All(v => v.Valor > 0), "Cada vencimento precisa de um valor maior que zero.");
        Validador.Garantir(vencimentos.All(v => v.Vencimento >= dataCompra), "Nenhum vencimento pode ser antes da data da compra.");
        Validador.Garantir(vencimentos.Sum(v => v.Valor) == Total,
            $"A soma dos vencimentos ({vencimentos.Sum(v => v.Valor):C}) deve ser igual ao total do pedido ({Total:C}).");
    }

    public void VincularRecebimento(string notaFiscal, DateOnly dataCompra, ContaPagar? conta, ParcelamentoPagar? parcelamento,
        IReadOnlyList<MovimentacaoEstoque> entradas, DateTime agora)
    {
        GarantirAberto();
        Validador.Garantir(conta is null ^ parcelamento is null, "Informe a conta única ou as parcelas da nota.");
        Validador.Garantir(entradas.Count == _itens.Count, "Cada item precisa da sua entrada no estoque.");
        var itens = _itens.OrderBy(i => i.Id).ToList();
        for (var i = 0; i < itens.Count; i++)
        {
            itens[i].DefinirEntrada(entradas[i]);
            entradas[i].MarcarDePedido();
        }

        NotaFiscal = notaFiscal.Trim();
        DataCompra = dataCompra;
        ContaPagar = conta;
        ParcelamentoPagar = parcelamento;
        RecebidoEm = agora;
        Status = StatusPedidoCompra.Recebido;
    }

    /// <summary>Contas a pagar geradas pela nota (a única ou as parcelas).</summary>
    public IReadOnlyList<ContaPagar> ContasDaNota() =>
        ContaPagar is not null ? [ContaPagar] : ParcelamentoPagar?.Parcelas.ToList() ?? [];

    /// <summary>
    /// Desfaz o recebimento (nota lançada errada): o pedido volta a ficar aberto. O serviço estorna as entradas e cancela as
    /// contas antes; se alguma conta já foi paga, o estorno é recusado.
    /// </summary>
    public void EstornarRecebimento()
    {
        Validador.Garantir(Status == StatusPedidoCompra.Recebido, "Somente pedidos recebidos podem ter o recebimento estornado.");
        Validador.Garantir(ContasDaNota().All(c => c.Status != StatusTitulo.Pago),
            "A nota deste pedido já foi paga (total ou parcialmente). Estorne o pagamento em contas a pagar antes.");
        foreach (var conta in ContasDaNota().Where(c => c.Status == StatusTitulo.Aberto))
            conta.Cancelar();
        foreach (var item in _itens)
            item.DefinirEntrada(null);

        NotaFiscal = null;
        DataCompra = null;
        RecebidoEm = null;
        ContaPagar = null;
        ContaPagarId = null;
        ParcelamentoPagar = null;
        ParcelamentoPagarId = null;
        Status = StatusPedidoCompra.Aberto;
    }

    /// <summary>A conta foi excluída ou o parcelamento desfeito em contas a pagar: o pedido continua recebido, sem o vínculo.</summary>
    public void DesvincularConta()
    {
        ContaPagar = null;
        ContaPagarId = null;
        ParcelamentoPagar = null;
        ParcelamentoPagarId = null;
    }

    public void Cancelar(string motivo)
    {
        GarantirAberto();
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        Status = StatusPedidoCompra.Cancelado;
        MotivoCancelamento = motivo.Trim();
    }

    private void GarantirAberto() =>
        Validador.Garantir(Status == StatusPedidoCompra.Aberto,
            Status == StatusPedidoCompra.Recebido
                ? "Pedido já recebido. Para corrigir, estorne o recebimento."
                : "Pedido cancelado não pode ser alterado.");
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

    /// <summary>Quantidade na unidade de compra.</summary>
    public decimal Quantidade { get; private set; }

    /// <summary>Custo de uma unidade de compra.</summary>
    public decimal CustoUnitario { get; private set; }

    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);

    /// <summary>Entrada no estoque feita no recebimento.</summary>
    public int? MovimentacaoEstoqueId { get; private set; }

    public MovimentacaoEstoque? Entrada { get; private set; }

    internal void DefinirEntrada(MovimentacaoEstoque? entrada)
    {
        Entrada = entrada;
        if (entrada is null)
            MovimentacaoEstoqueId = null;
    }
}
