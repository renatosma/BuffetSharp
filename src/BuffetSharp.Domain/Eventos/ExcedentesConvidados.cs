using BuffetSharp.Domain.Comercial;

namespace BuffetSharp.Domain.Eventos;

/// <summary>
/// Pessoas que vieram além do contratado, por faixa de cobrança, e o valor a cobrar por elas. A vaga contratada de quem
/// paga mais e não veio cobre quem paga menos (a criança da meia no lugar de um adulto que faltou não é cobrada), nunca o
/// contrário: o adulto além dos contratados paga inteira. Os isentos excedentes não pagam; entram só na contagem.
/// </summary>
public sealed record ExcedentesConvidados(int Inteira, int Meia, int Isentos, decimal Valor)
{
    public static readonly ExcedentesConvidados Nenhum = new(0, 0, 0, 0);

    public int Total => Inteira + Meia + Isentos;

    public int Pagantes => Inteira + Meia;

    public static ExcedentesConvidados Calcular(QuantidadesConvidados contratado, QuantidadesConvidados presentes,
        RegraFaixasEtarias regra, decimal valorPorPessoa)
    {
        var inteira = Math.Max(0, presentes.Inteira - contratado.Inteira);
        var vagas = Math.Max(0, contratado.Inteira - presentes.Inteira);

        var meia = Math.Max(0, presentes.Meia - contratado.Meia);
        var meiaCoberta = Math.Min(meia, vagas);
        meia -= meiaCoberta;
        vagas += Math.Max(0, contratado.Meia - presentes.Meia) - meiaCoberta;

        var isentos = Math.Max(0, presentes.Isentos - contratado.Isentos);
        isentos -= Math.Min(isentos, vagas);

        var valor = Math.Round(inteira * valorPorPessoa, 2) + meia * regra.ValorMeia(valorPorPessoa);
        return new ExcedentesConvidados(inteira, meia, isentos, valor);
    }
}
