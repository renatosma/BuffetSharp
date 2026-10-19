using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Financeiro;

/// <summary>
/// Cartão de crédito do buffet, usado para pagar fornecedores. Cada pagamento com ele gera as parcelas da fatura em
/// contas a pagar, com o vencimento calculado pelos dias de fechamento e de vencimento.
/// </summary>
public class CartaoCredito : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome do cartão.")]
    [MaxLength(60)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Quatro últimos dígitos, só para identificação.</summary>
    [MaxLength(4)]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Informe os 4 últimos dígitos.")]
    public string? Final { get; set; }

    [Range(1, 31, ErrorMessage = "O dia de fechamento vai de 1 a 31.")]
    public int DiaFechamento { get; set; } = 25;

    [Range(1, 31, ErrorMessage = "O dia de vencimento vai de 1 a 31.")]
    public int DiaVencimento { get; set; } = 5;

    /// <summary>Banco emissor (fornecedor das contas da fatura). Opcional.</summary>
    public int? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Final is null ? Nome : $"{Nome} (final {Final})";
}

/// <summary>
/// Em que fatura cai uma compra no cartão. A fatura fecha no dia de fechamento: compra antes dele entra na fatura desse
/// mês; no dia ou depois, na do mês seguinte. A fatura vence no dia de vencimento, no mesmo mês do fechamento se o
/// vencimento vier depois dele, senão no mês seguinte. Dias que o mês não tem (31 em fevereiro) viram o último dia.
/// </summary>
public static class FaturaCartao
{
    public static DateOnly PrimeiroVencimento(DateOnly compra, int diaFechamento, int diaVencimento)
    {
        Validador.Garantir(diaFechamento is >= 1 and <= 31 && diaVencimento is >= 1 and <= 31,
            "Os dias de fechamento e vencimento vão de 1 a 31.");

        var mesCompra = new DateOnly(compra.Year, compra.Month, 1);
        var mesFechamento = compra < Dia(mesCompra, diaFechamento) ? mesCompra : mesCompra.AddMonths(1);
        var mesVencimento = diaVencimento > diaFechamento ? mesFechamento : mesFechamento.AddMonths(1);
        return Dia(mesVencimento, diaVencimento);
    }

    /// <summary>Vencimentos das N parcelas: a primeira fatura e as dos meses seguintes.</summary>
    public static IReadOnlyList<DateOnly> Vencimentos(DateOnly compra, int diaFechamento, int diaVencimento, int parcelas)
    {
        var primeiro = PrimeiroVencimento(compra, diaFechamento, diaVencimento);
        var mes = new DateOnly(primeiro.Year, primeiro.Month, 1);
        return Enumerable.Range(0, Math.Max(parcelas, 1)).Select(i => Dia(mes.AddMonths(i), diaVencimento)).ToList();
    }

    /// <summary>Divide o valor em parcelas iguais; os centavos que sobram vão para a última.</summary>
    public static IReadOnlyList<decimal> Dividir(decimal valor, int parcelas)
    {
        var parcela = Math.Round(valor / parcelas, 2, MidpointRounding.ToZero);
        return Enumerable.Range(0, parcelas).Select(i => i == parcelas - 1 ? valor - parcela * (parcelas - 1) : parcela).ToList();
    }

    private static DateOnly Dia(DateOnly mes, int dia) => new(mes.Year, mes.Month, Math.Min(dia, DateTime.DaysInMonth(mes.Year, mes.Month)));
}
