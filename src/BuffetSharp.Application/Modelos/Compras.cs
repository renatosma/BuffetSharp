using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Modelos;

// Compras: cotação -> pedido de compra -> recebimento (estoque) -> nota fiscal de entrada (contas a pagar).

public sealed class PedidoCompraInput
{
    public int Id { get; set; }

    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public int FornecedorId { get; set; }
    public DateOnly Data { get; set; }
    public DateOnly? PrevisaoEntrega { get; set; }
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public string? Observacoes { get; set; }
    public List<PedidoCompraItemInput> Itens { get; set; } = [];

    // Somente leitura
    public string Codigo { get; set; } = "(novo)";
    public StatusPedidoCompra Status { get; set; } = StatusPedidoCompra.Aberto;
    public decimal Total => Itens.Sum(i => i.Total);
}

public sealed class PedidoCompraItemInput
{
    public int ProdutoId { get; set; }

    /// <summary>Unidade de compra (0 = unidade do estoque).</summary>
    public int ProdutoEmbalagemId { get; set; }

    public decimal Quantidade { get; set; }

    /// <summary>Custo de uma unidade de compra.</summary>
    public decimal CustoUnitario { get; set; }

    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

/// <summary>O que chegou de um pedido (pode ser só parte dele).</summary>
public sealed class RecebimentoInput
{
    public int PedidoId { get; set; }
    public DateOnly Data { get; set; }
    public string? Observacoes { get; set; }
    public List<RecebimentoItemInput> Itens { get; set; } = [];
}

/// <param name="Quantidade">Na unidade de compra do pedido; zero = não veio.</param>
public sealed record RecebimentoItemInput(int PedidoCompraItemId, decimal Quantidade);

/// <summary>Nota fiscal de entrada: fatura recebimentos de um fornecedor e gera as contas a pagar.</summary>
public sealed class NotaEntradaInput
{
    public int FornecedorId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public DateOnly DataEmissao { get; set; }
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public decimal ValorTotal { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Chave de acesso da NF-e, quando os dados vieram do XML.</summary>
    public string? ChaveAcesso { get; set; }

    public List<int> Recebimentos { get; set; } = [];
    public List<VencimentoInput> Vencimentos { get; set; } = [];
}

public sealed class VencimentoInput
{
    public DateOnly Vencimento { get; set; }
    public decimal Valor { get; set; }
}

/// <summary>Produto que pode ser comprado, com as unidades de compra (embalagens) que valem para o fornecedor.</summary>
public sealed record ProdutoCompra(int Id, string Nome, string Unidade, decimal CustoMedio, int? FornecedorPadraoId,
    IReadOnlyList<UnidadeCompra> Unidades);

/// <param name="Id">0 = unidade do estoque.</param>
/// <param name="Fator">Unidades do estoque em uma unidade de compra.</param>
public sealed record UnidadeCompra(int Id, string Nome, decimal Fator);

// ------------------------------- Cotação -------------------------------

public sealed class CotacaoInput
{
    public int Id { get; set; }
    public int? Versao { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public DateOnly? PrazoResposta { get; set; }
    public int? EventoId { get; set; }
    public string? Observacoes { get; set; }
    public List<CotacaoItemInput> Itens { get; set; } = [];
    public List<CotacaoParticipanteInput> Participantes { get; set; } = [];

    // Somente leitura
    public string Codigo { get; set; } = "(nova)";
    public StatusCotacao Status { get; set; } = StatusCotacao.Aberta;
}

public sealed class CotacaoItemInput
{
    public int ProdutoId { get; set; }

    /// <summary>Na unidade do estoque.</summary>
    public decimal Quantidade { get; set; }
    public string? Especificacao { get; set; }
}

public sealed class CotacaoParticipanteInput
{
    /// <summary>0 = novo participante.</summary>
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Fornecedor cadastrado (opcional na cotação; obrigatório para virar pedido).</summary>
    public int? FornecedorId { get; set; }
    public string? Contato { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public DateOnly? PrevisaoEntrega { get; set; }
    public string? CondicaoPagamento { get; set; }
    public string? Observacoes { get; set; }
}

/// <summary>Mapa de cotação: os preços de cada participante por produto e o vencedor de cada produto.</summary>
public sealed class MapaCotacao
{
    public int CotacaoId { get; set; }
    public int? Versao { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public StatusCotacao Status { get; set; }
    public List<MapaCotacaoItem> Itens { get; set; } = [];
    public List<MapaCotacaoParticipante> Participantes { get; set; } = [];
    public List<MapaCotacaoPreco> Precos { get; set; } = [];

    public MapaCotacaoPreco? Preco(int itemId, int participanteId) =>
        Precos.FirstOrDefault(p => p.ItemId == itemId && p.ParticipanteId == participanteId);

    /// <summary>Menor preço do produto (empate: o participante que aparece primeiro).</summary>
    public MapaCotacaoPreco? MenorPreco(int itemId) =>
        Precos.Where(p => p.ItemId == itemId && p.PrecoUnitario > 0)
            .OrderBy(p => p.PrecoUnitario)
            .ThenBy(p => Participantes.FindIndex(x => x.Id == p.ParticipanteId))
            .FirstOrDefault();

    /// <summary>Quem vence o produto: o escolhido ou, sem escolha, o menor preço.</summary>
    public int? Vencedor(MapaCotacaoItem item) =>
        item.Escolhido is { } escolhido && Preco(item.ItemId, escolhido) is { PrecoUnitario: > 0 } ? escolhido : MenorPreco(item.ItemId)?.ParticipanteId;
}

public sealed class MapaCotacaoItem
{
    public int ItemId { get; set; }
    public string Produto { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public string? Especificacao { get; set; }

    /// <summary>Participante escolhido à mão (nulo = menor preço).</summary>
    public int? Escolhido { get; set; }
    public string? Motivo { get; set; }
}

public sealed class MapaCotacaoParticipante
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int? FornecedorId { get; set; }
    public DateOnly? PrevisaoEntrega { get; set; }
    public string? CondicaoPagamento { get; set; }
}

public sealed class MapaCotacaoPreco
{
    public int ItemId { get; set; }
    public int ParticipanteId { get; set; }

    /// <summary>Preço de uma unidade do estoque; zero = não cotou.</summary>
    public decimal PrecoUnitario { get; set; }
    public string? Observacao { get; set; }
}
