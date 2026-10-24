using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

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

    /// <summary>
    /// Parcelas definidas uma a uma (prazo em dias e percentual de cada uma, ex.: 28 dias 50% + 42 dias 50%). Preenchida,
    /// substitui as regras de entrada e parcelas; vazia, valem as regras.
    /// </summary>
    public List<CondicaoPagamentoParcela> Parcelas { get; set; } = [];

    public bool ParcelasDefinidas => Parcelas.Count > 0;

    public bool AVista => ParcelasDefinidas ? Parcelas.Count == 1 : QuantidadeParcelas == 0;

    /// <summary>Resumo calculado das regras, ex.: "30% de entrada + 5x mensais (quitação até 10 dias antes do evento)".</summary>
    public string Resumo
    {
        get
        {
            if (ParcelasDefinidas)
                return ResumoParcelas();
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

    /// <summary>"28/35/42 dias" quando as parcelas são iguais; senão "40% à vista, 30% em 30 dias e 30% em 60 dias".</summary>
    private string ResumoParcelas()
    {
        var ordenadas = Parcelas.OrderBy(p => p.Dias).ToList();
        var texto = ordenadas.Count == 1 && ordenadas[0].Dias == 0
            ? "À vista"
            : ordenadas[0].Dias > 0 && Iguais(ordenadas)
                ? ordenadas.Count == 1 ? $"{ordenadas[0].Dias} dias" : $"{string.Join("/", ordenadas.Select(p => p.Dias))} dias"
                : Juntar(ordenadas.Select(p => $"{p.Percentual.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))}% " +
                                               (p.Dias == 0 ? "à vista" : $"em {p.Dias} dias")).ToList());
        return QuitarDiasAntesEvento is { } dias ? $"{texto} (quitação até {dias} dias antes do evento)" : texto;

        // Iguais, ou divididas por igual com os centavos na última (ex.: 33,33% + 33,33% + 33,34%).
        static bool Iguais(IReadOnlyList<CondicaoPagamentoParcela> parcelas)
        {
            var cada = Math.Round(100m / parcelas.Count, 2, MidpointRounding.ToZero);
            return parcelas.Select(p => p.Percentual).Distinct().Count() == 1 ||
                   (parcelas.SkipLast(1).All(p => p.Percentual == cada) && parcelas[^1].Percentual == 100 - cada * (parcelas.Count - 1));
        }

        static string Juntar(IReadOnlyList<string> partes) =>
            partes.Count == 1 ? partes[0] : $"{string.Join(", ", partes.Take(partes.Count - 1))} e {partes[^1]}";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ParcelasDefinidas)
        {
            if (Parcelas.Count > PlanoParcelas.MaximoParcelas)
                yield return new ValidationResult($"Informe no máximo {PlanoParcelas.MaximoParcelas} parcelas.", [nameof(Parcelas)]);
            if (Parcelas.Any(p => p.Percentual <= 0))
                yield return new ValidationResult("Cada parcela precisa de um percentual maior que zero.", [nameof(Parcelas)]);
            if (Parcelas.Any(p => p.Dias is < 0 or > 730))
                yield return new ValidationResult("O prazo de cada parcela deve estar entre 0 e 730 dias.", [nameof(Parcelas)]);
            if (Parcelas.Select(p => p.Dias).Distinct().Count() != Parcelas.Count)
                yield return new ValidationResult("Há duas parcelas com o mesmo prazo.", [nameof(Parcelas)]);
            if (Parcelas.Sum(p => p.Percentual) != 100)
                yield return new ValidationResult(
                    $"Os percentuais das parcelas somam {Parcelas.Sum(p => p.Percentual):0.##}%: devem somar 100%.", [nameof(Parcelas)]);
            yield break;
        }

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

    /// <summary>
    /// Vencimentos e valores para o valor informado, com os prazos contados a partir de <paramref name="dataBase"/>: as
    /// parcelas definidas (percentual de cada uma, centavos na última) ou as regras de entrada e parcelas. Com evento e
    /// quitação antes dele, nenhum vencimento passa do limite.
    /// </summary>
    public IReadOnlyList<ParcelaPlanejada> Vencimentos(decimal valor, DateOnly dataBase, DateOnly? dataEvento = null)
    {
        Validador.Garantir(valor > 0, "Não há saldo a parcelar.");
        var evento = dataEvento ?? DateOnly.MaxValue.AddDays(-1000);
        if (!ParcelasDefinidas)
        {
            var plano = Simular(valor, dataBase, evento);
            var vencimentos = new List<ParcelaPlanejada>();
            if (plano.ValorEntrada > 0)
                vencimentos.Add(new ParcelaPlanejada(0, plano.Quantidade, plano.ValorEntrada, plano.VencimentoEntrada ?? dataBase));
            for (var i = 0; i < plano.Quantidade; i++)
            {
                var valorDaVez = i == plano.Quantidade - 1 ? valor - plano.ValorEntrada - plano.ValorParcela * (plano.Quantidade - 1) : plano.ValorParcela;
                vencimentos.Add(new ParcelaPlanejada(i + 1, plano.Quantidade, valorDaVez, plano.Vencimento(i)));
            }
            return vencimentos;
        }

        DateOnly? limite = QuitarDiasAntesEvento is { } dias && dataEvento is { } data ? data.AddDays(-dias) : null;
        var ordenadas = Parcelas.OrderBy(p => p.Dias).ToList();
        var resultado = new List<ParcelaPlanejada>();
        for (var i = 0; i < ordenadas.Count; i++)
        {
            var vencimento = dataBase.AddDays(ordenadas[i].Dias);
            if (limite is { } l && vencimento > l)
                vencimento = l < dataBase ? dataBase : l;
            var valorDaVez = i == ordenadas.Count - 1
                ? valor - resultado.Sum(r => r.Valor)
                : Math.Round(valor * ordenadas[i].Percentual / 100, 2, MidpointRounding.ToZero);
            resultado.Add(new ParcelaPlanejada(i + 1, ordenadas.Count, valorDaVez, vencimento, ordenadas[i].FormaPagamentoId));
        }
        return resultado;
    }

    public override string ToString() => Nome;
}

/// <summary>Parcela de uma condição de pagamento: o prazo (dias após a data base) e o percentual do valor.</summary>
public class CondicaoPagamentoParcela : Entidade
{
    public int CondicaoPagamentoId { get; set; }

    /// <summary>Dias após a data base (fechamento do orçamento, emissão da nota...). 0 = à vista.</summary>
    public int Dias { get; set; }

    /// <summary>Percentual do valor pago nesta parcela.</summary>
    public decimal Percentual { get; set; }

    /// <summary>Forma desta parcela (ex.: entrada no PIX, demais no boleto). Vazia = a forma geral do pedido ou do evento.</summary>
    public int? FormaPagamentoId { get; set; }
    public FormaPagamento? FormaPagamento { get; set; }
}
