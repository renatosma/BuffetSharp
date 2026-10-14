using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Domain.Financeiro;

/// <summary>
/// Fechamento das comissões de um vendedor em um período: congela a base (valores recebidos) e o valor
/// da comissão e gera a conta a pagar correspondente. Períodos de um mesmo vendedor não podem se sobrepor.
/// </summary>
public class FechamentoComissao : Entidade
{
    private FechamentoComissao()
    {
    }

    public FechamentoComissao(Usuario vendedor, DateOnly de, DateOnly ate, decimal baseCalculo, decimal valor, DateOnly vencimento)
    {
        Validador.Garantir(de <= ate, "O início do período deve ser anterior ao fim.");
        Validador.Garantir(valor > 0, "Não há comissão a pagar neste período.");
        Validador.Garantir(baseCalculo >= valor, "A base de cálculo não pode ser menor que a comissão.");

        Vendedor = vendedor;
        VendedorId = vendedor.Id;
        De = de;
        Ate = ate;
        BaseCalculo = baseCalculo;
        Valor = valor;
        ContaPagar = new ContaPagar($"Comissão de vendas - {vendedor.Nome} ({de:dd/MM/yyyy} a {ate:dd/MM/yyyy})",
            CategoriaDespesa.Comissoes, valor, vencimento);
    }

    public int VendedorId { get; private set; }
    public Usuario? Vendedor { get; private set; }
    public DateOnly De { get; private set; }
    public DateOnly Ate { get; private set; }

    /// <summary>Total recebido dos eventos do vendedor no período.</summary>
    public decimal BaseCalculo { get; private set; }

    public decimal Valor { get; private set; }

    public int ContaPagarId { get; private set; }
    public ContaPagar? ContaPagar { get; private set; }

    public bool Sobrepoe(DateOnly de, DateOnly ate) => De <= ate && de <= Ate;
}
