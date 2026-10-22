using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Estoque;

/// <summary>
/// Vínculo do código de um produto na nota do fornecedor (cProd da NF-e) com o produto do sistema e a unidade em que ele
/// vem. Gravado na primeira importação; nas próximas notas do fornecedor o item é reconhecido sozinho.
/// </summary>
public class CodigoProdutoFornecedor : Entidade
{
    public const int TamanhoCodigo = 60;
    public const int TamanhoDescricao = 120;

    private CodigoProdutoFornecedor()
    {
    }

    public CodigoProdutoFornecedor(int fornecedorId, string codigo, string? descricao, int produtoId, int? produtoEmbalagemId)
    {
        Validador.Garantir(fornecedorId > 0, "Informe o fornecedor.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(codigo), "Informe o código do produto no fornecedor.");
        FornecedorId = fornecedorId;
        Codigo = Cortar(codigo, TamanhoCodigo)!;
        Vincular(descricao, produtoId, produtoEmbalagemId);
    }

    public int FornecedorId { get; private set; }

    /// <summary>Código do produto no fornecedor (como vem na nota).</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Descrição do produto na nota do fornecedor (para conferência).</summary>
    public string? Descricao { get; private set; }

    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Unidade de compra em que o item vem na nota (nula = unidade do estoque).</summary>
    public int? ProdutoEmbalagemId { get; private set; }
    public ProdutoEmbalagem? Embalagem { get; private set; }

    public void Vincular(string? descricao, int produtoId, int? produtoEmbalagemId)
    {
        Validador.Garantir(produtoId > 0, "Escolha o produto do sistema.");
        Descricao = Cortar(descricao, TamanhoDescricao);
        ProdutoId = produtoId;
        ProdutoEmbalagemId = produtoEmbalagemId is > 0 ? produtoEmbalagemId : null;
        Produto = null;
        Embalagem = null;
    }

    private static string? Cortar(string? texto, int tamanho) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(texto.Trim().Length, tamanho)];
}
