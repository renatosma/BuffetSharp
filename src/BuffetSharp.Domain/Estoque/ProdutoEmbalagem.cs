using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

/// <summary>
/// Conversão de unidade de compra para a unidade do estoque, por fornecedor (ex.: no Atacadão a caixa tem 12 UN; na
/// Distribuidora X o fardo tem 6 UN). Sem fornecedor, a conversão vale para qualquer um.
/// </summary>
public class ProdutoEmbalagem : Entidade
{
    public const decimal FatorMaximo = 1_000_000;

    public int ProdutoId { get; set; }

    /// <summary>Fornecedor que vende nesta embalagem (vazio = qualquer fornecedor).</summary>
    public int? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Unidade em que o produto é comprado (CX, FD, PCT...).</summary>
    public int UnidadeMedidaId { get; set; }
    public UnidadeMedida? UnidadeMedida { get; set; }

    /// <summary>Quantas unidades do estoque há em uma unidade de compra (ex.: 1 CX = 12 UN → 12).</summary>
    public decimal Fator { get; set; } = 1;

    /// <summary>Descrição livre (ex.: "Caixa com 12 latas de 350 ml").</summary>
    public string? Descricao { get; set; }

    /// <summary>Código do produto no catálogo do fornecedor.</summary>
    public string? CodigoFornecedor { get; set; }

    /// <summary>Converte a quantidade comprada para a unidade do estoque.</summary>
    public decimal ParaEstoque(decimal quantidade) => Math.Round(quantidade * Fator, 3);

    /// <summary>Custo de uma unidade do estoque a partir do custo de uma unidade de compra.</summary>
    public decimal CustoNoEstoque(decimal custoUnidadeCompra) => Math.Round(custoUnidadeCompra / Fator, 4);

    public override string ToString() =>
        $"{UnidadeMedida?.Sigla ?? "?"} = {Fator:0.####}" + (string.IsNullOrWhiteSpace(Descricao) ? "" : $" ({Descricao})");
}
