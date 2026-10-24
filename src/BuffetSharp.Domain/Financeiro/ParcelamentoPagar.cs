using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Domain.Financeiro;

/// <summary>
/// Nível 3 - Parcelamento de contas a pagar, o equivalente ao plano de pagamento do evento: uma compra parcelada
/// (valor total em entrada + parcelas) ou várias contas em aberto do mesmo fornecedor juntadas e reparceladas (as
/// originais ficam canceladas, ligadas ao parcelamento). Enquanto nenhuma parcela for paga, pode ser desfeito.
/// </summary>
public class ParcelamentoPagar : Entidade
{
    public const int TamanhoObservacao = 500;

    private readonly List<ContaPagar> _parcelas = [];
    private readonly List<ContaPagar> _agrupadas = [];

    private ParcelamentoPagar()
    {
    }

    /// <summary>Compra parcelada.</summary>
    public ParcelamentoPagar(string descricao, CategoriaDespesa categoria, decimal valorTotal, int? fornecedorId = null,
        int? eventoId = null, string? documento = null, string? observacao = null)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Informe a descrição.");
        Validador.Garantir(valorTotal > 0, "Informe o valor total.");
        Validador.Garantir(documento is null || documento.Trim().Length <= ContaPagar.TamanhoDocumento,
            $"O documento pode ter no máximo {ContaPagar.TamanhoDocumento} caracteres.");
        Validador.Garantir(observacao is null || observacao.Length <= TamanhoObservacao,
            $"A observação pode ter no máximo {TamanhoObservacao} caracteres.");

        // Cabe o sufixo da parcela, ex.: " (12/12)".
        var texto = descricao.Trim();
        Descricao = texto[..Math.Min(texto.Length, ContaPagar.TamanhoDescricao - 10)];
        Categoria = categoria;
        ValorTotal = valorTotal;
        FornecedorId = fornecedorId;
        EventoId = eventoId;
        Documento = string.IsNullOrWhiteSpace(documento) ? null : documento.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    /// <summary>
    /// Junta contas em aberto do mesmo fornecedor num parcelamento só. Elas precisam ter a mesma categoria e o mesmo
    /// evento, para os relatórios de despesa e o resultado do evento continuarem corretos.
    /// </summary>
    public static ParcelamentoPagar Agrupar(IReadOnlyCollection<ContaPagar> contas, string descricao, string? documento = null,
        string? observacao = null)
    {
        Validador.Garantir(contas.Count > 0, "Selecione ao menos uma conta para parcelar.");
        var primeira = contas.First();
        Validador.Garantir(contas.All(c => c.FornecedorId == primeira.FornecedorId), "Só é possível juntar contas do mesmo fornecedor.");
        Validador.Garantir(contas.All(c => c.Categoria == primeira.Categoria),
            "Só é possível juntar contas da mesma categoria (assim os relatórios de despesa continuam corretos).");
        Validador.Garantir(contas.All(c => c.EventoId == primeira.EventoId),
            "Só é possível juntar contas vinculadas ao mesmo evento (ou todas sem evento).");
        foreach (var conta in contas)
        {
            Validador.Garantir(conta.Status == StatusTitulo.Aberto, $"'{conta.Descricao}' não está em aberto.");
            Validador.Garantir(!conta.EhFaturaCartao, $"'{conta.Descricao}' é parcela da fatura de um cartão e não pode ser reparcelada.");
        }

        var parcelamento = new ParcelamentoPagar(descricao, primeira.Categoria, contas.Sum(c => c.Valor), primeira.FornecedorId,
            primeira.EventoId, documento ?? (contas.Select(c => c.Documento).Distinct().Count() == 1 ? primeira.Documento : null),
            observacao);
        foreach (var conta in contas)
        {
            conta.AgruparEm(parcelamento);
            parcelamento._agrupadas.Add(conta);
        }
        return parcelamento;
    }

    public string Codigo => EhNovo ? "(novo)" : $"PAR-{Id:D5}";

    public string Descricao { get; private set; } = string.Empty;
    public CategoriaDespesa Categoria { get; private set; }
    public decimal ValorTotal { get; private set; }

    public int? FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }

    public int? EventoId { get; private set; }
    public Evento? Evento { get; private set; }

    /// <summary>Nota fiscal, boleto ou fatura (copiado para as parcelas).</summary>
    public string? Documento { get; private set; }

    public string? Observacao { get; private set; }

    /// <summary>Parcelas geradas (inclui os saldos de pagamentos parciais).</summary>
    public IReadOnlyCollection<ContaPagar> Parcelas => _parcelas;

    /// <summary>Contas originais que foram juntadas neste parcelamento (vazio numa compra parcelada).</summary>
    public IReadOnlyCollection<ContaPagar> Agrupadas => _agrupadas;

    public bool PodeDesfazer => _parcelas.All(p => p.Status != StatusTitulo.Pago && !p.FoiAgrupada);

    /// <param name="quantidade">Parcelas após a entrada.</param>
    /// <param name="intervaloDias">Dias entre as parcelas; nulo = mensais (mesmo dia de cada mês).</param>
    public IReadOnlyList<ContaPagar> GerarParcelas(int quantidade, DateOnly primeiroVencimento, decimal valorEntrada = 0,
        DateOnly? vencimentoEntrada = null, int? formaPagamentoId = null, int? intervaloDias = null)
    {
        Validador.Garantir(_parcelas.Count == 0, "As parcelas já foram geradas. Desfaça o parcelamento para refazê-lo.");
        var plano = PlanoParcelas.Calcular(ValorTotal, quantidade, primeiroVencimento, valorEntrada, vencimentoEntrada, intervaloDias);
        _parcelas.AddRange(plano.Select(p => new ContaPagar(this, p, formaPagamentoId)));
        return _parcelas;
    }

    /// <summary>Parcelas com os vencimentos e valores informados (ex.: os boletos de uma nota fiscal).</summary>
    public IReadOnlyList<ContaPagar> GerarParcelas(IReadOnlyList<(DateOnly Vencimento, decimal Valor)> vencimentos, int? formaPagamentoId = null) =>
        GerarParcelas(vencimentos.Select(v => new VencimentoPlanejado(v.Vencimento, v.Valor)).ToList(), formaPagamentoId);

    /// <summary>Parcelas com os vencimentos informados; cada uma com a sua forma ou, sem ela, <paramref name="formaPagamentoId"/>.</summary>
    public IReadOnlyList<ContaPagar> GerarParcelas(IReadOnlyList<VencimentoPlanejado> vencimentos, int? formaPagamentoId = null)
    {
        Validador.Garantir(_parcelas.Count == 0, "As parcelas já foram geradas. Desfaça o parcelamento para refazê-lo.");
        Validador.Garantir(vencimentos.Count is >= 1 and <= PlanoParcelas.MaximoParcelas,
            $"Informe de 1 a {PlanoParcelas.MaximoParcelas} vencimentos.");
        Validador.Garantir(vencimentos.All(v => v.Valor > 0), "Cada parcela precisa de um valor maior que zero.");
        Validador.Garantir(vencimentos.Sum(v => v.Valor) == ValorTotal,
            $"A soma das parcelas ({vencimentos.Sum(v => v.Valor):C}) deve ser igual ao valor total ({ValorTotal:C}).");
        var ordenados = vencimentos.OrderBy(v => v.Vencimento).ToList();
        _parcelas.AddRange(ordenados.Select((v, i) =>
            new ContaPagar(this, new ParcelaPlanejada(i + 1, ordenados.Count, v.Valor, v.Vencimento), v.FormaPagamentoId ?? formaPagamentoId)));
        return _parcelas;
    }

    /// <summary>
    /// Desfaz o parcelamento: as contas juntadas voltam a ficar em aberto e as parcelas deixam de existir.
    /// </summary>
    /// <returns>O que deve ser excluído: as parcelas e o próprio parcelamento.</returns>
    public IReadOnlyList<object> Desfazer()
    {
        Validador.Garantir(_parcelas.All(p => p.Status != StatusTitulo.Pago),
            "Este parcelamento já tem parcela paga. Estorne o pagamento antes de desfazê-lo.");
        Validador.Garantir(_parcelas.All(p => !p.FoiAgrupada),
            "Uma das parcelas foi juntada em outro parcelamento. Desfaça aquele primeiro.");

        foreach (var conta in _agrupadas)
            conta.Desagrupar();
        _agrupadas.Clear();

        var remover = new List<object>(_parcelas) { this };
        return remover;
    }
}
