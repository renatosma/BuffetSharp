using System.Globalization;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Compra registrada no estoque (entrada não estornada), com o preço na unidade do estoque e o que foi comprado na unidade do
/// fornecedor.
/// </summary>
public sealed record PrecoCompra(int MovimentacaoId, DateTime Data, int ProdutoId, string Produto, string Unidade, int? FornecedorId,
    string Fornecedor, decimal Quantidade, decimal CustoUnitario, string? UnidadeCompra, decimal? QuantidadeCompra, decimal? CustoUnitarioCompra)
{
    /// <summary>Variação sobre a compra anterior do mesmo produto, em % (nula na primeira).</summary>
    public decimal? Variacao { get; init; }

    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);

    public string Compra => UnidadeCompra is null || QuantidadeCompra is null
        ? string.Create(PrecoCompraService.PtBr, $"{Quantidade:N3} {Unidade}")
        : string.Create(PrecoCompraService.PtBr, $"{QuantidadeCompra:0.###} {UnidadeCompra} a {CustoUnitarioCompra:C2}");
}

/// <summary>Preços de um produto no período: último, menor, maior, médio (pela quantidade) e a variação do primeiro ao último.</summary>
public sealed record PrecoProdutoResumo(int ProdutoId, string Produto, string Unidade, int Compras, decimal Primeiro, decimal Ultimo,
    DateTime UltimaCompra, string UltimoFornecedor, decimal Menor, decimal Maior, decimal Medio, string? MelhorFornecedor, decimal? MelhorPreco)
{
    public decimal Variacao => PrecoCompraService.Variacao(Primeiro, Ultimo);
}

/// <summary>Último preço de um fornecedor para o produto, comparado com o mais barato entre os fornecedores.</summary>
public sealed record PrecoFornecedor(int ProdutoId, string Produto, string Unidade, int FornecedorId, string Fornecedor, decimal Ultimo,
    DateTime Data, int Compras, decimal MelhorDoProduto, int FornecedoresDoProduto)
{
    public bool MaisBarato => Ultimo == MelhorDoProduto;

    /// <summary>Quanto o último preço deste fornecedor está acima do mais barato, em %.</summary>
    public decimal AcimaDoMelhor => PrecoCompraService.Variacao(MelhorDoProduto, Ultimo);
}

/// <summary>Variação do preço no período (primeira e última compra) e quanto ela pesa por mês, pelo que se compra do produto.</summary>
public sealed record AltaPreco(int ProdutoId, string Produto, string Unidade, int Compras, decimal Primeiro, DateTime PrimeiraCompra,
    decimal Ultimo, DateTime UltimaCompra, decimal QuantidadeMes)
{
    public decimal Variacao => PrecoCompraService.Variacao(Primeiro, Ultimo);
    public decimal ImpactoMes => Math.Round((Ultimo - Primeiro) * QuantidadeMes, 2);
}

/// <summary>Último preço pago pelo produto (na unidade do estoque).</summary>
public sealed record UltimoPreco(decimal Custo, DateTime Data, string Fornecedor);

/// <summary>Item de uma compra para conferir o preço: custo na unidade de compra (embalagem) ou do estoque (sem embalagem).</summary>
public sealed record ItemPreco(int ProdutoId, int? ProdutoEmbalagemId, decimal CustoUnitario);

/// <summary>Preço da compra acima do último pago além do limite (Buffet:LimiteAltaPrecoCompra).</summary>
public sealed record AvisoPreco(string Produto, string Unidade, decimal Novo, UltimoPreco Anterior)
{
    public decimal Variacao => PrecoCompraService.Variacao(Anterior.Custo, Novo);

    public string Texto => string.Create(PrecoCompraService.PtBr, $"{Produto}: {Novo:C4} por {Unidade}, {Variacao:+0.0;-0.0}% sobre o último pago " +
                                                                 $"({Anterior.Custo:C4} em {Anterior.Data:dd/MM/yyyy}, {Anterior.Fornecedor})");
}

/// <summary>
/// Histórico de preços de compra, tirado das entradas no estoque (manual, recebimento do pedido e NF-e), sem as estornadas: o
/// preço de cada compra por produto e fornecedor, o comparativo entre fornecedores, as maiores altas e o aviso de preço acima do
/// último pago na hora de comprar.
/// </summary>
public sealed class PrecoCompraService(IBuffetDbContextFactory fabrica, BuffetOptions opcoes, TimeProvider relogio)
{
    public const string SemFornecedor = "Sem fornecedor informado";

    internal static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    /// <summary>Alta (em %) acima da qual a compra pede confirmação; 0 desliga o aviso.</summary>
    public decimal LimiteAlta => opcoes.LimiteAltaPrecoCompra;

    public static decimal Variacao(decimal de, decimal para) => de == 0 ? 0 : Math.Round((para / de - 1) * 100, 1);

    /// <summary>Compras (entradas com custo) do período, da mais antiga para a mais recente.</summary>
    private static async Task<List<PrecoCompra>> ComprasAsync(IBuffetDbContext db, int? produtoId, DateOnly? de, DateOnly? ate, CancellationToken ct)
    {
        var entradas = db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.Tipo == TipoMovimentacao.Entrada && m.EstornadaEm == null && m.CustoUnitario > 0);
        if (produtoId is { } id)
            entradas = entradas.Where(m => m.ProdutoId == id);
        if (de is { } inicio)
        {
            var desde = inicio.ToDateTime(TimeOnly.MinValue);
            entradas = entradas.Where(m => m.Data >= desde);
        }
        if (ate is { } fim)
        {
            var antes = fim.AddDays(1).ToDateTime(TimeOnly.MinValue);
            entradas = entradas.Where(m => m.Data < antes);
        }

        var linhas = await (from m in entradas
                join f in db.Fornecedores on m.FornecedorId equals f.Id into fornecedores
                from f in fornecedores.DefaultIfEmpty()
                orderby m.Data, m.Id
                select new
                {
                    m.Id, m.Data, m.ProdutoId, Produto = m.Produto!.Nome, Unidade = m.Produto.UnidadeMedida!.Sigla, m.FornecedorId,
                    Fantasia = f == null ? null : f.NomeFantasia, Razao = f == null ? null : f.RazaoSocial,
                    m.Quantidade, m.CustoUnitario, m.UnidadeCompra, m.QuantidadeCompra, m.CustoUnitarioCompra
                })
            .ToListAsync(ct);
        return linhas.Select(l => new PrecoCompra(l.Id, l.Data, l.ProdutoId, l.Produto, l.Unidade, l.FornecedorId,
                !string.IsNullOrWhiteSpace(l.Fantasia) ? l.Fantasia : l.Razao ?? SemFornecedor, l.Quantidade, l.CustoUnitario, l.UnidadeCompra,
                l.QuantidadeCompra, l.CustoUnitarioCompra))
            .ToList();
    }

    /// <summary>Compras do produto no período, com a variação sobre a compra anterior (a anterior pode ser de antes do período).</summary>
    public async Task<IReadOnlyList<PrecoCompra>> HistoricoAsync(int produtoId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var compras = await ComprasAsync(db, produtoId, null, ate, ct);
        var resultado = new List<PrecoCompra>();
        for (var i = 0; i < compras.Count; i++)
        {
            if (DateOnly.FromDateTime(compras[i].Data) < de)
                continue;
            resultado.Add(compras[i] with { Variacao = i == 0 ? null : Variacao(compras[i - 1].CustoUnitario, compras[i].CustoUnitario) });
        }
        return resultado;
    }

    /// <summary>Um resumo por produto comprado no período, do que mais subiu para o que mais caiu.</summary>
    public async Task<IReadOnlyList<PrecoProdutoResumo>> ResumoAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var compras = await ComprasAsync(db, null, de, ate, ct);
        return compras.GroupBy(c => c.ProdutoId).Select(g =>
            {
                var lista = g.ToList();
                var ultima = lista[^1];
                var melhor = UltimosPorFornecedor(lista).MinBy(c => c.CustoUnitario);
                var quantidade = lista.Sum(c => c.Quantidade);
                return new PrecoProdutoResumo(g.Key, ultima.Produto, ultima.Unidade, lista.Count, lista[0].CustoUnitario, ultima.CustoUnitario,
                    ultima.Data, ultima.Fornecedor, lista.Min(c => c.CustoUnitario), lista.Max(c => c.CustoUnitario),
                    quantidade == 0 ? 0 : Math.Round(lista.Sum(c => c.Quantidade * c.CustoUnitario) / quantidade, 4),
                    melhor?.Fornecedor, melhor?.CustoUnitario);
            })
            .OrderByDescending(r => r.Variacao).ThenBy(r => r.Produto)
            .ToList();
    }

    /// <summary>Última compra de cada fornecedor informado (as compras sem fornecedor não entram no comparativo).</summary>
    private static IEnumerable<PrecoCompra> UltimosPorFornecedor(IEnumerable<PrecoCompra> compras) =>
        compras.Where(c => c.FornecedorId is not null).GroupBy(c => c.FornecedorId).Select(f => f.Last());

    /// <summary>Último preço de cada fornecedor por produto, no período, com o mais barato do produto.</summary>
    public async Task<IReadOnlyList<PrecoFornecedor>> ComparativoAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var compras = await ComprasAsync(db, null, de, ate, ct);
        return compras.GroupBy(c => c.ProdutoId).SelectMany(produto =>
            {
                var ultimos = UltimosPorFornecedor(produto).ToList();
                if (ultimos.Count == 0)
                    return [];
                var melhor = ultimos.Min(c => c.CustoUnitario);
                return ultimos.Select(c => new PrecoFornecedor(c.ProdutoId, c.Produto, c.Unidade, c.FornecedorId!.Value, c.Fornecedor, c.CustoUnitario,
                    c.Data, produto.Count(p => p.FornecedorId == c.FornecedorId), melhor, ultimos.Count));
            })
            .OrderBy(p => p.Produto).ThenBy(p => p.Ultimo).ThenBy(p => p.Fornecedor)
            .ToList();
    }

    /// <summary>Produtos com duas compras ou mais no período, do que mais subiu para o que mais caiu.</summary>
    public async Task<IReadOnlyList<AltaPreco>> MaioresAltasAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var compras = await ComprasAsync(db, null, de, ate, ct);
        var meses = Math.Max(1m, (ate.DayNumber - de.DayNumber + 1) / 30.4m);
        return compras.GroupBy(c => c.ProdutoId).Where(g => g.Count() >= 2).Select(g =>
            {
                var primeira = g.First();
                var ultima = g.Last();
                return new AltaPreco(g.Key, ultima.Produto, ultima.Unidade, g.Count(), primeira.CustoUnitario, primeira.Data, ultima.CustoUnitario,
                    ultima.Data, Math.Round(g.Sum(c => c.Quantidade) / meses, 3));
            })
            .OrderByDescending(a => a.Variacao).ThenByDescending(a => a.ImpactoMes)
            .ToList();
    }

    /// <summary>
    /// Último preço pago por produto (na unidade do estoque): de qualquer fornecedor e, com <paramref name="fornecedorId"/>, o
    /// desse fornecedor.
    /// </summary>
    internal static async Task<Dictionary<int, (UltimoPreco Geral, UltimoPreco? DoFornecedor)>> UltimosAsync(IBuffetDbContext db, int? fornecedorId,
        IReadOnlyCollection<int>? produtos, CancellationToken ct)
    {
        var entradas = db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.Tipo == TipoMovimentacao.Entrada && m.EstornadaEm == null && m.CustoUnitario > 0);
        if (produtos is not null)
            entradas = entradas.Where(m => produtos.Contains(m.ProdutoId));
        var linhas = await (from m in entradas
                join f in db.Fornecedores on m.FornecedorId equals f.Id into fornecedores
                from f in fornecedores.DefaultIfEmpty()
                select new { m.Id, m.ProdutoId, m.FornecedorId, m.Data, m.CustoUnitario, Fantasia = f == null ? null : f.NomeFantasia,
                    Razao = f == null ? null : f.RazaoSocial })
            .ToListAsync(ct);
        return linhas.GroupBy(l => l.ProdutoId).ToDictionary(g => g.Key, g =>
        {
            var ordenadas = g.OrderBy(l => l.Data).ThenBy(l => l.Id).ToList();
            var geral = ordenadas[^1];
            var doFornecedor = fornecedorId is null ? null : ordenadas.LastOrDefault(l => l.FornecedorId == fornecedorId);
            UltimoPreco Converter(DateTime data, decimal custo, string? fantasia, string? razao) =>
                new(custo, data, !string.IsNullOrWhiteSpace(fantasia) ? fantasia : razao ?? SemFornecedor);
            return (Converter(geral.Data, geral.CustoUnitario, geral.Fantasia, geral.Razao),
                doFornecedor is null ? null : Converter(doFornecedor.Data, doFornecedor.CustoUnitario, doFornecedor.Fantasia, doFornecedor.Razao));
        });
    }

    /// <summary>Último preço pago pelo produto (unidade do estoque), ou nulo se nunca foi comprado.</summary>
    public async Task<UltimoPreco?> UltimoAsync(int produtoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return (await UltimosAsync(db, null, [produtoId], ct)).TryGetValue(produtoId, out var ultimo) ? ultimo.Geral : null;
    }

    /// <summary>
    /// Itens da compra cujo preço (convertido para a unidade do estoque pela embalagem) passa do último pago além do limite
    /// (Buffet:LimiteAltaPrecoCompra). Vazio quando o aviso está desligado ou nada subiu tanto.
    /// </summary>
    public async Task<IReadOnlyList<AvisoPreco>> AvisosAsync(IEnumerable<ItemPreco> itens, CancellationToken ct = default)
    {
        var lista = itens.Where(i => i.ProdutoId > 0 && i.CustoUnitario > 0).ToList();
        if (LimiteAlta <= 0 || lista.Count == 0)
            return [];
        await using var db = fabrica.Criar();
        var ids = lista.Select(i => i.ProdutoId).Distinct().ToList();
        var ultimos = await UltimosAsync(db, null, ids, ct);
        var embalagens = await db.ProdutoEmbalagens.AsNoTracking().Where(e => ids.Contains(e.ProdutoId)).ToDictionaryAsync(e => e.Id, e => e.Fator, ct);
        var produtos = await db.Produtos.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, Unidade = p.UnidadeMedida!.Sigla }).ToDictionaryAsync(p => p.Id, ct);

        var avisos = new List<AvisoPreco>();
        foreach (var item in lista)
        {
            if (!ultimos.TryGetValue(item.ProdutoId, out var ultimo))
                continue;
            var fator = item.ProdutoEmbalagemId is { } embalagem && embalagens.TryGetValue(embalagem, out var f) && f > 0 ? f : 1;
            var novo = Math.Round(item.CustoUnitario / fator, 4);
            if (Variacao(ultimo.Geral.Custo, novo) > LimiteAlta && produtos.TryGetValue(item.ProdutoId, out var produto))
                avisos.Add(new AvisoPreco(produto.Nome, produto.Unidade, novo, ultimo.Geral));
        }
        return avisos;
    }

    /// <summary>Período padrão das telas: os últimos 12 meses.</summary>
    public (DateOnly De, DateOnly Ate) UltimoAno => (Hoje.AddMonths(-12).AddDays(1), Hoje);
}
