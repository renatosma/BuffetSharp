using BuffetSharp.Domain.Estoque;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>
/// Linha da grade de embalagens do produto. Usa 0 em vez de nulo ("qualquer fornecedor", "selecione") porque as
/// listas da grade não lidam bem com valores nulos.
/// </summary>
internal sealed class LinhaEmbalagem
{
    private ProdutoEmbalagem? _origem;

    public int FornecedorId { get; set; }
    public int UnidadeMedidaId { get; set; }
    public decimal Fator { get; set; } = 1;
    public string? Descricao { get; set; }
    public string? CodigoFornecedor { get; set; }

    public static LinhaEmbalagem De(ProdutoEmbalagem embalagem) => new()
    {
        _origem = embalagem,
        FornecedorId = embalagem.FornecedorId ?? 0,
        UnidadeMedidaId = embalagem.UnidadeMedidaId,
        Fator = embalagem.Fator,
        Descricao = embalagem.Descricao,
        CodigoFornecedor = embalagem.CodigoFornecedor
    };

    /// <summary>Devolve a embalagem do cadastro (a mesma, quando já existia) com os valores da linha.</summary>
    public ProdutoEmbalagem ParaEmbalagem()
    {
        var embalagem = _origem ?? new ProdutoEmbalagem();
        embalagem.FornecedorId = FornecedorId > 0 ? FornecedorId : null;
        embalagem.UnidadeMedidaId = UnidadeMedidaId;
        embalagem.Fator = Fator;
        embalagem.Descricao = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao.Trim();
        embalagem.CodigoFornecedor = string.IsNullOrWhiteSpace(CodigoFornecedor) ? null : CodigoFornecedor.Trim();
        return embalagem;
    }
}
