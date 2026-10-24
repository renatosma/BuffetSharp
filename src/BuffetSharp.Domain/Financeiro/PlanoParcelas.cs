using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Financeiro;

/// <summary>Uma parcela calculada por <see cref="PlanoParcelas"/>: <see cref="Numero"/> 0 é a entrada.</summary>
/// <param name="FormaPagamentoId">Forma da parcela, quando a condição a define (nula = a forma geral).</param>
public sealed record ParcelaPlanejada(int Numero, int Quantidade, decimal Valor, DateOnly Vencimento, int? FormaPagamentoId = null)
{
    public bool Entrada => Numero == 0;
}

/// <summary>Vencimento informado (boleto da nota, parcela da condição...), com a forma própria quando houver.</summary>
/// <param name="FormaPagamentoId">Nula = a forma geral do plano.</param>
public sealed record VencimentoPlanejado(DateOnly Vencimento, decimal Valor, int? FormaPagamentoId = null);

/// <summary>
/// Divide um valor em entrada + parcelas (mensais ou a cada N dias), com os centavos na última parcela. É a mesma regra
/// do plano de pagamento do evento (contas a receber) e do parcelamento de contas a pagar.
/// </summary>
public static class PlanoParcelas
{
    public const int MaximoParcelas = 48;

    /// <param name="quantidade">Parcelas após a entrada.</param>
    /// <param name="intervaloDias">Dias entre as parcelas; nulo = mensais (mesmo dia de cada mês).</param>
    public static IReadOnlyList<ParcelaPlanejada> Calcular(decimal valor, int quantidade, DateOnly primeiroVencimento,
        decimal valorEntrada = 0, DateOnly? vencimentoEntrada = null, int? intervaloDias = null)
    {
        Validador.Garantir(quantidade is >= 1 and <= MaximoParcelas, $"A quantidade de parcelas deve estar entre 1 e {MaximoParcelas}.");
        Validador.Garantir(valorEntrada >= 0, "O valor de entrada não pode ser negativo.");
        Validador.Garantir(intervaloDias is null or (>= 1 and <= 365), "O intervalo entre as parcelas deve estar entre 1 e 365 dias.");
        Validador.Garantir(valor > 0, "Não há saldo a parcelar.");
        Validador.Garantir(valorEntrada < valor || (valorEntrada == valor && quantidade == 1),
            "A entrada deve ser menor que o saldo a parcelar.");

        var parcelas = new List<ParcelaPlanejada>();
        if (valorEntrada > 0)
            parcelas.Add(new ParcelaPlanejada(0, quantidade, valorEntrada, vencimentoEntrada ?? primeiroVencimento));

        var saldo = valor - valorEntrada;
        if (saldo > 0)
        {
            var valorParcela = Math.Round(saldo / quantidade, 2, MidpointRounding.ToZero);
            for (var i = 0; i < quantidade; i++)
            {
                var valorDaVez = i == quantidade - 1 ? saldo - valorParcela * (quantidade - 1) : valorParcela;
                var vencimento = intervaloDias is { } dias ? primeiroVencimento.AddDays(i * dias) : primeiroVencimento.AddMonths(i);
                parcelas.Add(new ParcelaPlanejada(i + 1, quantidade, valorDaVez, vencimento));
            }
        }
        return parcelas;
    }
}
