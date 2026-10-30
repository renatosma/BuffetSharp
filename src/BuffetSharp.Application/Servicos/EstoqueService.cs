using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class EstoqueService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    public Task<MovimentacaoEstoque> RegistrarEntradaAsync(int produtoId, decimal quantidade, decimal custoUnitario,
        string? observacao, IReadOnlyList<LoteInformado>? lotes = null, CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.RegistrarEntrada(quantidade, custoUnitario, agora, observacao, lotes), ct);

    /// <summary>
    /// Entrada de uma compra: converte a embalagem do fornecedor para a unidade do estoque e, se pedido, lança o valor em
    /// contas a pagar (conta única ou compra parcelada) na mesma gravação.
    /// </summary>
    public async Task<MovimentacaoEstoque> RegistrarCompraAsync(EntradaEstoqueInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var produto = await db.Produtos.Include(p => p.UnidadeMedida).FirstOrDefaultAsync(p => p.Id == input.ProdutoId, ct)
                      ?? throw new RegraNegocioException("Produto não encontrado.");
        var embalagem = input.ProdutoEmbalagemId is { } embalagemId
            ? await db.ProdutoEmbalagens.AsNoTracking().Include(e => e.UnidadeMedida).FirstOrDefaultAsync(e => e.Id == embalagemId, ct)
              ?? throw new RegraNegocioException("Embalagem não encontrada.")
            : null;

        var movimentacao = produto.RegistrarCompra(input.Quantidade, input.CustoUnitario, relogio.GetLocalNow().DateTime,
            input.Observacao, input.FornecedorId, embalagem, input.LotesInformados);
        db.MovimentacoesEstoque.Add(movimentacao);
        if (input.GerarContaPagar)
            LancarContaPagar(db, input, produto, embalagem, movimentacao);
        await db.SaveChangesAsync(ct);
        return movimentacao;
    }

    private static void LancarContaPagar(IBuffetDbContext db, EntradaEstoqueInput input, Produto produto, ProdutoEmbalagem? embalagem,
        MovimentacaoEstoque entrada)
    {
        Validador.Garantir(input.ValorCompra > 0, "Informe o custo da compra para lançar a conta a pagar.");
        Validador.Garantir(input.Vencimento is not null, "Informe o vencimento da conta a pagar.");
        Validador.Garantir(input.Parcelas is >= 1 and <= 24, "As parcelas devem estar entre 1 e 24.");

        var descricao = $"Compra de {produto.Nome}";
        var unidade = embalagem?.UnidadeMedida?.Sigla ?? produto.UnidadeMedida?.Sigla ?? "";
        var observacao = $"Entrada de estoque: {input.Quantidade:0.###} {unidade} a {input.CustoUnitario:C2}".Trim() +
                         (string.IsNullOrWhiteSpace(input.Observacao) ? "" : $" - {input.Observacao.Trim()}");
        observacao = observacao[..Math.Min(observacao.Length, ParcelamentoPagar.TamanhoObservacao)];

        if (input.Parcelas == 1)
        {
            var conta = new ContaPagar(descricao, input.Categoria, input.ValorCompra, input.Vencimento!.Value, input.FornecedorId)
            {
                Observacao = observacao
            };
            conta.DefinirDocumento(input.Documento);
            db.ContasPagar.Add(conta);
            entrada.VincularConta(conta, null);
            return;
        }

        var parcelamento = new ParcelamentoPagar(descricao, input.Categoria, input.ValorCompra, input.FornecedorId, null, input.Documento,
            observacao);
        parcelamento.GerarParcelas(input.Parcelas, input.Vencimento!.Value, intervaloDias: input.IntervaloDias is > 0 ? input.IntervaloDias : null);
        db.ParcelamentosPagar.Add(parcelamento);
        entrada.VincularConta(null, parcelamento);
    }

    /// <summary>
    /// Estorna uma entrada lançada por engano: o estoque e o custo médio voltam e a conta a pagar da compra (conta única ou
    /// parcelas) é cancelada. Recusado se a conta já foi paga ou se o produto já foi usado.
    /// </summary>
    public async Task<MovimentacaoEstoque> EstornarEntradaAsync(int movimentacaoId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var entrada = await db.MovimentacoesEstoque
                          .Include(m => m.Produto)
                          .Include(m => m.Lotes).ThenInclude(l => l.Lote)
                          .Include(m => m.ContaPagar)
                          .Include(m => m.ParcelamentoPagar!).ThenInclude(p => p.Parcelas)
                          .FirstOrDefaultAsync(m => m.Id == movimentacaoId, ct)
                      ?? throw new RegraNegocioException("Movimentação não encontrada. Atualize a lista.");

        var estorno = entrada.Produto!.EstornarEntrada(entrada, relogio.GetLocalNow().DateTime, motivo);
        entrada.CancelarContaDaCompra();
        db.MovimentacoesEstoque.Add(estorno);
        await db.SaveChangesAsync(ct);
        return estorno;
    }

    /// <summary>Embalagens de compra do produto (com fornecedor e unidade), para a tela de entrada.</summary>
    public async Task<List<ProdutoEmbalagem>> ListarEmbalagensAsync(int produtoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.ProdutoEmbalagens.AsNoTracking()
            .Include(e => e.UnidadeMedida)
            .Include(e => e.Fornecedor)
            .Where(e => e.ProdutoId == produtoId)
            .OrderBy(e => e.FornecedorId == null).ThenBy(e => e.Fator)
            .ToListAsync(ct);
    }

    public Task<MovimentacaoEstoque> RegistrarSaidaAsync(int produtoId, decimal quantidade, string? observacao,
        CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.RegistrarSaida(quantidade, agora, observacao), ct);

    public Task<MovimentacaoEstoque> AjustarSaldoAsync(int produtoId, decimal saldoContado, string? observacao,
        CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.AjustarSaldo(saldoContado, agora, observacao ?? "Ajuste de inventário"), ct);

    /// <summary>Compra (positivo) ou baixa por descarte/perda (negativo) de material do patrimônio.</summary>
    public async Task AjustarPatrimonioAsync(int materialId, int diferenca, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var material = await db.Materiais.FirstOrDefaultAsync(m => m.Id == materialId, ct)
                       ?? throw new RegraNegocioException("Material não encontrado.");
        material.AjustarPatrimonio(diferenca);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Descarte de produto vencido ou estragado: baixa como perda, pelo custo. Nos produtos que controlam validade, informe o
    /// lote (sem ele, sai do saldo sem lote).
    /// </summary>
    public Task<MovimentacaoEstoque> DescartarAsync(int produtoId, int? loteId, decimal quantidade, string motivo, CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.Descartar(loteId is { } id
            ? p.Lotes.FirstOrDefault(l => l.Id == id) ?? throw new RegraNegocioException("Lote não encontrado (ou já sem saldo).")
            : null, quantidade, agora, motivo), ct);

    /// <summary>Produto com os lotes que ainda têm saldo: as saídas, ajustes e descartes consomem deles.</summary>
    public static IQueryable<Produto> ComLotes(IQueryable<Produto> produtos) => produtos.Include(p => p.Lotes.Where(l => l.Saldo > 0));

    private async Task<MovimentacaoEstoque> MovimentarAsync(int produtoId,
        Func<Produto, DateTime, MovimentacaoEstoque> operacao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var produto = await ComLotes(db.Produtos).FirstOrDefaultAsync(p => p.Id == produtoId, ct)
                      ?? throw new RegraNegocioException("Produto não encontrado.");

        var movimentacao = operacao(produto, relogio.GetLocalNow().DateTime);
        db.MovimentacoesEstoque.Add(movimentacao);
        await db.SaveChangesAsync(ct);
        return movimentacao;
    }
}
