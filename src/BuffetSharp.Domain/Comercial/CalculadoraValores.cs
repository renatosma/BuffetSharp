using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

public sealed record ResumoValores(
    decimal Alimentacao,
    decimal Espaco,
    decimal Servicos,
    decimal Subtotal,
    decimal Desconto,
    decimal Total);

/// <summary>
/// Regra única de cálculo do valor de um evento, usada pelo orçamento e pela tela
/// (pré-visualização em tempo real).
/// </summary>
public static class CalculadoraValores
{
    public const decimal DescontoMaximoPercentual = 30m;

    public static ResumoValores Calcular(decimal valorPorPessoa, int convidados, decimal valorEspaco,
        decimal totalServicos, decimal percentualDesconto)
    {
        var alimentacao = Math.Round(valorPorPessoa * convidados, 2);
        var subtotal = alimentacao + valorEspaco + totalServicos;
        var desconto = Math.Round(subtotal * percentualDesconto / 100m, 2);
        return new ResumoValores(alimentacao, valorEspaco, totalServicos, subtotal, desconto, subtotal - desconto);
    }
}

/// <summary>
/// Converte data + horário de início/fim em um intervalo. Eventos podem terminar após a meia-noite.
/// </summary>
public static class PeriodoEvento
{
    public static (DateTime Inicio, DateTime Fim) Calcular(DateOnly data, TimeOnly horaInicio, TimeOnly horaFim)
    {
        var inicio = data.ToDateTime(horaInicio);
        var fim = data.ToDateTime(horaFim);
        if (fim <= inicio)
            fim = fim.AddDays(1);

        Validador.Garantir((fim - inicio).TotalHours <= 16, "A duração do evento não pode passar de 16 horas.");
        return (inicio, fim);
    }
}
