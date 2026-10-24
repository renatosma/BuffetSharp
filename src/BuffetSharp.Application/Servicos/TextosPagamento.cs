using System.Globalization;
using BuffetSharp.Domain.Cadastros;

namespace BuffetSharp.Application.Servicos;

/// <summary>Textos da condição de pagamento usados na proposta, no WhatsApp e no contrato.</summary>
public static class TextosPagamento
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    /// <summary>
    /// Simulação para o valor informado, ex.: "Entrada de R$ 6.000,00 + 5x mensais de R$ 4.800,00" ou
    /// "Pagamento único de R$ 30.000,00". Os vencimentos contam a partir de <paramref name="hoje"/>.
    /// </summary>
    public static string Simulacao(CondicaoPagamento condicao, decimal valor, DateOnly hoje, DateOnly dataEvento)
    {
        if (valor <= 0)
            return string.Empty;

        if (condicao.ParcelasDefinidas)
        {
            var definidas = condicao.Vencimentos(valor, hoje, dataEvento);
            var formas = condicao.Parcelas.OrderBy(p => p.Dias).Select(p => p.FormaPagamento?.Nome).ToList();
            string Forma(int i) => i < formas.Count && formas[i] is { } nome ? $" ({nome})" : "";
            return definidas.Count == 1
                ? $"Pagamento único de {Moeda(valor)} até {definidas[0].Vencimento:dd/MM/yyyy}{Forma(0)}"
                : $"{definidas.Count} parcelas: " + string.Join(", ", definidas.Select((p, i) => $"{Moeda(p.Valor)} em {p.Vencimento:dd/MM/yyyy}{Forma(i)}"));
        }

        var plano = condicao.Simular(valor, hoje, dataEvento);
        if (condicao.AVista)
            return $"Pagamento único de {Moeda(valor)} até {plano.PrimeiroVencimento:dd/MM/yyyy}";

        var periodicidade = plano.IntervaloDias is { } dias ? $"a cada {dias} dias" : "mensais";
        var parcelas = $"{plano.Quantidade}x {periodicidade} de {Moeda(plano.ValorParcela)}";
        return plano.ValorEntrada > 0 ? $"Entrada de {Moeda(plano.ValorEntrada)} + {parcelas}" : char.ToUpper(parcelas[0]) + parcelas[1..];
    }

    /// <summary>"Condição: 30% de entrada + 5x mensais (Entrada de R$ 6.000,00 + 5x mensais de R$ 4.800,00)".</summary>
    public static string Completo(CondicaoPagamento condicao, decimal valor, DateOnly hoje, DateOnly dataEvento)
    {
        var simulacao = Simulacao(condicao, valor, hoje, dataEvento);
        return simulacao.Length == 0 ? condicao.Texto : $"{condicao.Texto} ({simulacao})";
    }
}
