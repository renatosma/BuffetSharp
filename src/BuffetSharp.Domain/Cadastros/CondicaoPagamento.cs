using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Cadastros;

/// <summary>Plano de pagamento calculado por uma <see cref="CondicaoPagamento"/> para um valor e uma data de evento.</summary>
/// <param name="IntervaloDias">Dias entre as parcelas; nulo quando as parcelas são mensais (mesmo dia de cada mês).</param>
public sealed record PlanoSugerido(decimal ValorEntrada, DateOnly? VencimentoEntrada, int Quantidade, DateOnly PrimeiroVencimento,
    int? IntervaloDias, decimal ValorParcela)
{
    public DateOnly Vencimento(int indice) =>
        IntervaloDias is { } dias ? PrimeiroVencimento.AddDays(indice * dias) : PrimeiroVencimento.AddMonths(indice);

    public DateOnly UltimoVencimento => Vencimento(Quantidade - 1);
}

/// <summary>
/// Regra de prazos de pagamento (ex.: "30% de entrada + 5x mensais", "À vista", "50% na assinatura + 50% até 10 dias
/// antes"). A forma de pagamento diz o meio (PIX, cartão...); a condição diz quando e em quantas vezes.
/// </summary>
public class CondicaoPagamento : Entidade, IAtivavel, IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Texto para a proposta e o contrato. Vazio: usa o resumo calculado (ex.: "30% de entrada + 5x mensais").</summary>
    [MaxLength(300)]
    public string? Descricao { get; set; }

    /// <summary>Percentual do valor pago na entrada. 100% com zero parcelas = pagamento à vista.</summary>
    [Range(0, 100, ErrorMessage = "A entrada deve estar entre 0% e 100%.")]
    public decimal PercentualEntrada { get; set; }

    /// <summary>Dias, a partir da geração do plano, para o vencimento da entrada (ou do pagamento à vista).</summary>
    [Range(0, 365, ErrorMessage = "O prazo da entrada deve estar entre 0 e 365 dias.")]
    public int DiasEntrada { get; set; } = 2;

    /// <summary>Parcelas após a entrada (0 = à vista).</summary>
    [Range(0, 48, ErrorMessage = "A quantidade de parcelas deve estar entre 0 e 48.")]
    public int QuantidadeParcelas { get; set; } = 1;

    /// <summary>Dias, a partir da geração do plano, para o vencimento da primeira parcela.</summary>
    [Range(0, 730, ErrorMessage = "O prazo da primeira parcela deve estar entre 0 e 730 dias.")]
    public int DiasPrimeiraParcela { get; set; } = 30;

    /// <summary>Parcelas no mesmo dia de cada mês. Quando falso, usa <see cref="IntervaloDias"/>.</summary>
    public bool Mensal { get; set; } = true;

    [Range(1, 365, ErrorMessage = "O intervalo entre as parcelas deve estar entre 1 e 365 dias.")]
    public int IntervaloDias { get; set; } = 30;

    /// <summary>
    /// Quitação até N dias antes do evento (vazio = sem limite). As parcelas que passariam do limite são reduzidas.
    /// </summary>
    [Range(0, 365, ErrorMessage = "A quitação deve estar entre 0 e 365 dias antes do evento.")]
    public int? QuitarDiasAntesEvento { get; set; }

    /// <summary>Forma de pagamento sugerida ao gerar as parcelas.</summary>
    public int? FormaPagamentoId { get; set; }
    public FormaPagamento? FormaPagamento { get; set; }

    public bool Ativo { get; set; } = true;

    public bool AVista => QuantidadeParcelas == 0;

    /// <summary>Resumo calculado das regras, ex.: "30% de entrada + 5x mensais (quitação até 10 dias antes do evento)".</summary>
    public string Resumo
    {
        get
        {
            var periodicidade = Mensal ? "mensais" : $"a cada {IntervaloDias} dias";
            var texto = AVista
                ? "À vista"
                : PercentualEntrada > 0
                    ? $"{PercentualEntrada:0.##}% de entrada + {QuantidadeParcelas}x {periodicidade}"
                    : $"{QuantidadeParcelas}x {periodicidade}";
            return QuitarDiasAntesEvento is { } dias ? $"{texto} (quitação até {dias} dias antes do evento)" : texto;
        }
    }

    /// <summary>Texto exibido na proposta e no contrato.</summary>
    public string Texto => string.IsNullOrWhiteSpace(Descricao) ? Resumo : Descricao.Trim();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AVista && PercentualEntrada != 100)
            yield return new ValidationResult("Sem parcelas, a condição é à vista: informe 100% de entrada.", [nameof(PercentualEntrada)]);
        if (!AVista && PercentualEntrada >= 100)
            yield return new ValidationResult("Com 100% de entrada não há parcelas: informe zero parcelas (à vista).",
                [nameof(QuantidadeParcelas)]);
    }

    /// <summary>
    /// Calcula o plano para o saldo informado. Vencimentos contados a partir de <paramref name="hoje"/>; com quitação
    /// antes do evento, a quantidade de parcelas é reduzida até caber no prazo (no mínimo uma).
    /// </summary>
    public PlanoSugerido Simular(decimal saldo, DateOnly hoje, DateOnly dataEvento)
    {
        Validador.Garantir(saldo > 0, "Não há saldo a parcelar.");
        DateOnly? limite = QuitarDiasAntesEvento is { } dias ? dataEvento.AddDays(-dias) : null;
        DateOnly Limitar(DateOnly data) => limite is { } l && data > l ? (l < hoje ? hoje : l) : data;
        int? intervalo = Mensal ? null : IntervaloDias;

        if (AVista)
        {
            var vencimento = Limitar(hoje.AddDays(DiasEntrada));
            return new PlanoSugerido(0, null, 1, vencimento, intervalo, saldo);
        }

        var entrada = Math.Round(saldo * PercentualEntrada / 100, 2);
        DateOnly? vencimentoEntrada = entrada > 0 ? Limitar(hoje.AddDays(DiasEntrada)) : null;
        var primeiro = Limitar(hoje.AddDays(DiasPrimeiraParcela));
        var quantidade = QuantidadeParcelas;
        while (quantidade > 1 && limite is { } l &&
               new PlanoSugerido(entrada, vencimentoEntrada, quantidade, primeiro, intervalo, 0).UltimoVencimento > l)
            quantidade--;

        var valorParcela = Math.Round((saldo - entrada) / quantidade, 2, MidpointRounding.ToZero);
        return new PlanoSugerido(entrada, vencimentoEntrada, quantidade, primeiro, intervalo, valorParcela);
    }

    public override string ToString() => Nome;
}
