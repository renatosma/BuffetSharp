using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Modelos;

// Importação do XML da NF-e: os dados lidos do arquivo e a análise contra os cadastros (fornecedor, produtos, recebimentos).

/// <summary>Dados da NF-e lidos do XML do fornecedor.</summary>
public sealed record NotaFiscalEletronica
{
    public string ChaveAcesso { get; init; } = string.Empty;

    /// <summary>55 = NF-e; 65 = NFC-e (cupom).</summary>
    public string Modelo { get; init; } = "55";

    public string Numero { get; init; } = string.Empty;
    public string? Serie { get; init; }
    public DateOnly DataEmissao { get; init; }
    public EmitenteNfe Emitente { get; init; } = new();
    public string? DestinatarioDocumento { get; init; }
    public string? DestinatarioNome { get; init; }
    public decimal ValorProdutos { get; init; }
    public decimal ValorFrete { get; init; }
    public decimal ValorDesconto { get; init; }

    /// <summary>Seguro, outras despesas e impostos cobrados à parte (IPI, ICMS-ST).</summary>
    public decimal ValorOutros { get; init; }

    public decimal ValorTotal { get; init; }
    public IReadOnlyList<ItemNfe> Itens { get; init; } = [];

    /// <summary>Boletos da nota (cobrança); vazio em compra à vista.</summary>
    public IReadOnlyList<DuplicataNfe> Duplicatas { get; init; } = [];

    public string Descricao => string.IsNullOrWhiteSpace(Serie) ? $"NF {Numero}" : $"NF {Numero}/{Serie}";
}

public sealed record EmitenteNfe
{
    /// <summary>CNPJ ou CPF, só dígitos.</summary>
    public string Documento { get; init; } = string.Empty;
    public string RazaoSocial { get; init; } = string.Empty;
    public string? NomeFantasia { get; init; }
    public string? Telefone { get; init; }
    public Endereco Endereco { get; init; } = new();

    public string Nome => string.IsNullOrWhiteSpace(NomeFantasia) ? RazaoSocial : NomeFantasia;
}

/// <param name="Numero">Número do item na nota (nItem).</param>
/// <param name="Codigo">Código do produto no fornecedor (cProd).</param>
/// <param name="Unidade">Unidade comercial (uCom), ex.: CX, UN, KG.</param>
/// <param name="ValorTotal">Valor bruto do item (vProd).</param>
/// <param name="Desconto">Desconto do item (vDesc).</param>
public sealed record ItemNfe(int Numero, string Codigo, string Descricao, string Unidade, decimal Quantidade, decimal ValorUnitario,
    decimal ValorTotal, decimal Desconto)
{
    /// <summary>Custo de uma unidade da nota, já com o desconto do item (o frete fica no total da nota).</summary>
    public decimal CustoUnitario => Quantidade == 0 ? ValorUnitario : Math.Round((ValorTotal - Desconto) / Quantidade, 4);
}

public sealed record DuplicataNfe(string? Numero, DateOnly Vencimento, decimal Valor);

/// <summary>Como o item da nota foi ligado a um produto do sistema.</summary>
public enum OrigemVinculo
{
    /// <summary>Não reconhecido: o usuário escolhe.</summary>
    Nenhuma,

    /// <summary>Vínculo gravado numa importação anterior deste fornecedor.</summary>
    Vinculo,

    /// <summary>Código do fornecedor cadastrado na embalagem do produto.</summary>
    CodigoFornecedor,

    /// <summary>Nome parecido: é só uma sugestão, convém conferir.</summary>
    Nome
}

/// <summary>A NF-e confrontada com os cadastros.</summary>
public sealed record AnaliseNfe
{
    public NotaFiscalEletronica Nota { get; init; } = new();

    /// <summary>Fornecedor cadastrado com o CNPJ/CPF do emitente (nulo = será cadastrado).</summary>
    public int? FornecedorId { get; init; }
    public string Fornecedor { get; init; } = string.Empty;

    /// <summary>Recebimentos do fornecedor aguardando nota.</summary>
    public IReadOnlyList<int> RecebimentosAFaturar { get; init; } = [];

    /// <summary>O que cada recebimento a faturar trouxe, na unidade do estoque (para conferir com a nota).</summary>
    public IReadOnlyList<ProdutoRecebido> Recebido { get; init; } = [];

    public IReadOnlyList<ItemNfeAnalisado> Itens { get; init; } = [];

    /// <summary>Confere os itens reconhecidos da nota com os recebimentos escolhidos. Vazio = tudo confere.</summary>
    public IReadOnlyList<string> Divergencias(IReadOnlyCollection<int> recebimentos)
    {
        var recebido = Recebido.Where(r => recebimentos.Contains(r.RecebimentoId))
            .GroupBy(r => r.ProdutoId)
            .ToDictionary(g => g.Key, g => (Quantidade: g.Sum(r => r.Quantidade), g.First().Produto, g.First().Unidade));
        var naNota = Itens.Where(i => i.ProdutoId is > 0)
            .GroupBy(i => i.ProdutoId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantidadeEstoque));

        var divergencias = new List<string>();
        foreach (var (produtoId, quantidade) in naNota)
        {
            var item = Itens.First(i => i.ProdutoId == produtoId);
            if (!recebido.TryGetValue(produtoId, out var r))
                divergencias.Add($"{item.Produto}: está na nota ({quantidade:0.###} {item.UnidadeEstoque}) mas não nos recebimentos");
            else if (r.Quantidade != quantidade)
                divergencias.Add($"{r.Produto}: nota {quantidade:0.###} {r.Unidade}, recebido {r.Quantidade:0.###} {r.Unidade}");
        }
        foreach (var (produtoId, r) in recebido.Where(r => !naNota.ContainsKey(r.Key)))
            divergencias.Add($"{r.Produto}: recebido ({r.Quantidade:0.###} {r.Unidade}) mas não reconhecido na nota");
        divergencias.AddRange(Itens.Where(i => i.ProdutoId is null).Select(i => $"Item {i.Item.Numero} da nota não reconhecido: {i.Item.Descricao}"));
        return divergencias;
    }
}

/// <param name="Quantidade">Na unidade do estoque.</param>
public sealed record ProdutoRecebido(int RecebimentoId, int ProdutoId, string Produto, string Unidade, decimal Quantidade);

public sealed record ItemNfeAnalisado
{
    public ItemNfe Item { get; init; } = new(0, "", "", "", 0, 0, 0, 0);

    /// <summary>Produto do sistema reconhecido (nulo = não reconhecido).</summary>
    public int? ProdutoId { get; init; }
    public string? Produto { get; init; }

    /// <summary>Unidade de compra em que o item vem (0 = unidade do estoque).</summary>
    public int ProdutoEmbalagemId { get; init; }

    /// <summary>Unidades do estoque em cada unidade da nota.</summary>
    public decimal Fator { get; init; } = 1;

    public string UnidadeEstoque { get; init; } = string.Empty;
    public OrigemVinculo Origem { get; init; }
    public decimal QuantidadeEstoque => Math.Round(Item.Quantidade * Fator, 3);
}

/// <summary>Compra sem pedido lançada pela NF-e: a entrada no estoque e a nota (com as contas) de uma vez.</summary>
public sealed class NotaSemPedidoInput
{
    public NotaFiscalEletronica Nota { get; set; } = new();

    /// <summary>Fornecedor cadastrado (nulo = cadastrar com os dados do emitente).</summary>
    public int? FornecedorId { get; set; }

    public DateOnly DataRecebimento { get; set; }
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public string? Observacoes { get; set; }
    public List<ItemSemPedidoInput> Itens { get; set; } = [];
    public List<VencimentoInput> Vencimentos { get; set; } = [];
}

/// <param name="ProdutoId">Produto do sistema; nulo = o item não entra no estoque (só no valor da nota).</param>
/// <param name="ProdutoEmbalagemId">Unidade em que o item vem (0 = unidade do estoque).</param>
public sealed record ItemSemPedidoInput(int Numero, int? ProdutoId, int ProdutoEmbalagemId);
