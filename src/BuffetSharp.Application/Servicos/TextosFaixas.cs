using System.Globalization;
using BuffetSharp.Domain.Comercial;

namespace BuffetSharp.Application.Servicos;

/// <summary>Textos das faixas etárias (inteira, meia e isentos) usados nas telas, na proposta, no WhatsApp e no contrato.</summary>
public static class TextosFaixas
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    private static string Criancas(int quantidade) => quantidade == 1 ? "1 criança" : $"{quantidade} crianças";

    /// <summary>"100 convidados" ou "100 convidados (70 inteira, 20 meia e 10 isentos)".</summary>
    public static string Resumo(QuantidadesConvidados q)
    {
        if (!q.TemCriancas)
            return $"{q.Total} convidados";
        var partes = new List<string> { $"{q.Inteira} inteira" };
        if (q.Meia > 0)
            partes.Add($"{q.Meia} meia");
        if (q.Isentos > 0)
            partes.Add(q.Isentos == 1 ? "1 isento" : $"{q.Isentos} isentos");
        return $"{q.Total} convidados ({Juntar(partes)})";
    }

    /// <summary>
    /// Linhas da alimentação na proposta: uma só quando todos pagam inteira; senão, inteira, meia e isentos separados.
    /// </summary>
    public static IReadOnlyList<(string Descricao, decimal Valor)> LinhasAlimentacao(decimal valorPorPessoa,
        QuantidadesConvidados q, RegraFaixasEtarias regra)
    {
        if (!q.TemCriancas)
            return [($"Alimentação ({q.Total} × {Moeda(valorPorPessoa)})", Math.Round(valorPorPessoa * q.Total, 2))];

        var valorMeia = regra.ValorMeia(valorPorPessoa);
        var linhas = new List<(string, decimal)>
        {
            ($"Alimentação - inteira ({q.Inteira} × {Moeda(valorPorPessoa)})", Math.Round(valorPorPessoa * q.Inteira, 2))
        };
        if (q.Meia > 0)
            linhas.Add(($"Alimentação - meia, crianças {regra.FaixaMeia} ({q.Meia} × {Moeda(valorMeia)})",
                Math.Round(valorMeia * q.Meia, 2)));
        if (q.Isentos > 0)
            linhas.Add(($"{Criancas(q.Isentos)} {regra.FaixaIsentos} - isentas", 0m));
        return linhas;
    }

    /// <summary>
    /// Complemento da quantidade de convidados no contrato: ", sendo 70 com valor integral, 20 crianças de 6 a 10 anos com
    /// meia (50%) e 10 crianças até 5 anos isentas" (vazio quando todos pagam inteira).
    /// </summary>
    public static string DetalheContrato(QuantidadesConvidados q, RegraFaixasEtarias regra)
    {
        if (!q.TemCriancas)
            return string.Empty;
        var partes = new List<string> { $"{q.Inteira} com valor integral" };
        if (q.Meia > 0)
            partes.Add($"{Criancas(q.Meia)} {regra.FaixaMeia} com meia ({regra.PercentualMeia:0.##}%)");
        if (q.Isentos > 0)
            partes.Add($"{Criancas(q.Isentos)} {regra.FaixaIsentos} {(q.Isentos == 1 ? "isenta" : "isentas")}");
        return ", sendo " + Juntar(partes);
    }

    private static string Juntar(List<string> partes) =>
        partes.Count == 1 ? partes[0] : string.Join(", ", partes[..^1]) + " e " + partes[^1];
}
