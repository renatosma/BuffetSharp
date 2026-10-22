using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Domain.Estoque;

public enum StatusNotaEntrada
{
    Lancada,
    Cancelada
}

/// <summary>
/// Nota fiscal de entrada: fatura um ou mais recebimentos do mesmo fornecedor e gera as contas a pagar com os vencimentos
/// dos boletos. O valor da nota pode diferir da mercadoria recebida (frete, desconto, impostos).
/// </summary>
public class NotaFiscalEntrada : Entidade
{
    public const int TamanhoNumero = ContaPagar.TamanhoDocumento;
    public const int TamanhoSerie = 5;

    private readonly List<RecebimentoCompra> _recebimentos = [];

    private NotaFiscalEntrada()
    {
    }

    public NotaFiscalEntrada(int fornecedorId, string numero, string? serie, DateOnly dataEmissao, CategoriaDespesa categoria,
        IReadOnlyCollection<RecebimentoCompra> recebimentos, decimal valorTotal, string? observacoes, DateOnly hoje)
    {
        Validador.Garantir(fornecedorId > 0, "Selecione o fornecedor da nota.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(numero), "Informe o número da nota fiscal.");
        Validador.Garantir(numero.Trim().Length <= TamanhoNumero, $"O número da nota pode ter no máximo {TamanhoNumero} caracteres.");
        Validador.Garantir(serie is null || serie.Trim().Length <= TamanhoSerie, $"A série pode ter no máximo {TamanhoSerie} caracteres.");
        Validador.Garantir(dataEmissao <= hoje, "A data de emissão da nota não pode ser futura.");
        Validador.Garantir(categoria != CategoriaDespesa.FaturaCartao, "Escolha a categoria da despesa da compra.");
        Validador.Garantir(observacoes is null || observacoes.Trim().Length <= 500, "As observações podem ter no máximo 500 caracteres.");
        Validador.Garantir(recebimentos.Count > 0, "Selecione ao menos um recebimento para a nota.");
        Validador.Garantir(recebimentos.All(r => r.FornecedorId == fornecedorId), "Todos os recebimentos da nota precisam ser do mesmo fornecedor.");
        Validador.Garantir(valorTotal > 0, "Informe o valor total da nota.");

        FornecedorId = fornecedorId;
        Numero = numero.Trim();
        Serie = string.IsNullOrWhiteSpace(serie) ? null : serie.Trim();
        DataEmissao = dataEmissao;
        Categoria = categoria;
        ValorProdutos = recebimentos.Sum(r => r.Total);
        ValorTotal = valorTotal;
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
        foreach (var recebimento in recebimentos)
        {
            recebimento.Faturar(this);
            _recebimentos.Add(recebimento);
        }
    }

    public int FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public string? Serie { get; private set; }
    public DateOnly DataEmissao { get; private set; }
    public CategoriaDespesa Categoria { get; private set; }

    /// <summary>Mercadoria dos recebimentos, pelo custo dos pedidos.</summary>
    public decimal ValorProdutos { get; private set; }

    /// <summary>Total da nota (o que será pago).</summary>
    public decimal ValorTotal { get; private set; }

    public string? Observacoes { get; private set; }
    public StatusNotaEntrada Status { get; private set; } = StatusNotaEntrada.Lancada;
    public DateTime? CanceladaEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Conta a pagar única (um boleto).</summary>
    public int? ContaPagarId { get; private set; }
    public ContaPagar? ContaPagar { get; private set; }

    /// <summary>Compra parcelada (vários boletos).</summary>
    public int? ParcelamentoPagarId { get; private set; }
    public ParcelamentoPagar? ParcelamentoPagar { get; private set; }

    public IReadOnlyCollection<RecebimentoCompra> Recebimentos => _recebimentos;

    public string Descricao => Serie is null ? $"NF {Numero}" : $"NF {Numero}/{Serie}";

    /// <summary>
    /// Lança a nota em contas a pagar: um vencimento vira uma conta, vários viram uma compra parcelada. A soma precisa bater
    /// com o total da nota.
    /// </summary>
    public (ContaPagar? Conta, ParcelamentoPagar? Parcelamento) LancarContas(IReadOnlyList<(DateOnly Vencimento, decimal Valor)> vencimentos,
        string? fornecedor)
    {
        Validador.Garantir(ContaPagar is null && ParcelamentoPagar is null && ContaPagarId is null && ParcelamentoPagarId is null,
            "As contas desta nota já foram lançadas.");
        Validador.Garantir(vencimentos.Count is >= 1 and <= PlanoParcelas.MaximoParcelas,
            $"Informe de 1 a {PlanoParcelas.MaximoParcelas} vencimentos.");
        Validador.Garantir(vencimentos.All(v => v.Valor > 0), "Cada vencimento precisa de um valor maior que zero.");
        Validador.Garantir(vencimentos.All(v => v.Vencimento >= DataEmissao), "Nenhum vencimento pode ser antes da emissão da nota.");
        Validador.Garantir(vencimentos.Sum(v => v.Valor) == ValorTotal,
            $"A soma dos vencimentos ({vencimentos.Sum(v => v.Valor):C}) deve ser igual ao total da nota ({ValorTotal:C}).");

        var descricao = $"Compra {Descricao}" + (string.IsNullOrWhiteSpace(fornecedor) ? "" : $" - {fornecedor}");
        descricao = descricao[..Math.Min(descricao.Length, 120)];
        var pedidos = _recebimentos.Select(r => r.PedidoCompra?.Codigo ?? $"PC-{r.PedidoCompraId:D5}").Distinct();
        var observacao = $"Nota de entrada com {_recebimentos.Count} recebimento(s) do(s) pedido(s) {string.Join(", ", pedidos)}.";
        observacao = observacao[..Math.Min(observacao.Length, ParcelamentoPagar.TamanhoObservacao)];
        if (vencimentos.Count == 1)
        {
            ContaPagar = new ContaPagar(descricao, Categoria, ValorTotal, vencimentos[0].Vencimento, FornecedorId) { Observacao = observacao };
            ContaPagar.DefinirDocumento(Numero);
        }
        else
        {
            ParcelamentoPagar = new ParcelamentoPagar(descricao, Categoria, ValorTotal, FornecedorId, null, Numero, observacao);
            ParcelamentoPagar.GerarParcelas(vencimentos);
        }
        return (ContaPagar, ParcelamentoPagar);
    }

    /// <summary>Contas a pagar da nota (a única ou as parcelas).</summary>
    public IReadOnlyList<ContaPagar> Contas() =>
        ContaPagar is not null ? [ContaPagar] : ParcelamentoPagar?.Parcelas.ToList() ?? [];

    /// <summary>
    /// Nota lançada errada: as contas são canceladas e os recebimentos voltam a aguardar nota (a mercadoria continua no
    /// estoque). Recusado se algo já foi pago.
    /// </summary>
    public void Cancelar(string motivo, DateTime agora)
    {
        Validador.Garantir(Status == StatusNotaEntrada.Lancada, "A nota já está cancelada.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        Validador.Garantir(Contas().All(c => c.Status != StatusTitulo.Pago),
            "A nota já foi paga (total ou parcialmente). Estorne o pagamento em contas a pagar antes de cancelá-la.");
        foreach (var conta in Contas().Where(c => c.Status == StatusTitulo.Aberto))
            conta.Cancelar();
        foreach (var recebimento in _recebimentos)
            recebimento.VoltarAFaturar();
        _recebimentos.Clear();
        Status = StatusNotaEntrada.Cancelada;
        CanceladaEm = agora;
        MotivoCancelamento = motivo.Trim()[..Math.Min(motivo.Trim().Length, 500)];
    }

    /// <summary>A conta foi excluída ou o parcelamento desfeito em contas a pagar: a nota continua lançada, sem o vínculo.</summary>
    public void DesvincularConta()
    {
        ContaPagar = null;
        ContaPagarId = null;
        ParcelamentoPagar = null;
        ParcelamentoPagarId = null;
    }
}
