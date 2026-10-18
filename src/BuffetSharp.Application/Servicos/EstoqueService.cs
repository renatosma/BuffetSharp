using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class EstoqueService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    public Task<MovimentacaoEstoque> RegistrarEntradaAsync(int produtoId, decimal quantidade, decimal custoUnitario,
        string? observacao, CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.RegistrarEntrada(quantidade, custoUnitario, agora, observacao), ct);

    /// <summary>Entrada de uma compra: converte a embalagem do fornecedor para a unidade do estoque.</summary>
    public async Task<MovimentacaoEstoque> RegistrarCompraAsync(EntradaEstoqueInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == input.ProdutoId, ct)
                      ?? throw new RegraNegocioException("Produto não encontrado.");
        var embalagem = input.ProdutoEmbalagemId is { } embalagemId
            ? await db.ProdutoEmbalagens.AsNoTracking().Include(e => e.UnidadeMedida).FirstOrDefaultAsync(e => e.Id == embalagemId, ct)
              ?? throw new RegraNegocioException("Embalagem não encontrada.")
            : null;

        var movimentacao = produto.RegistrarCompra(input.Quantidade, input.CustoUnitario, relogio.GetLocalNow().DateTime,
            input.Observacao, input.FornecedorId, embalagem);
        db.MovimentacoesEstoque.Add(movimentacao);
        await db.SaveChangesAsync(ct);
        return movimentacao;
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

    private async Task<MovimentacaoEstoque> MovimentarAsync(int produtoId,
        Func<Produto, DateTime, MovimentacaoEstoque> operacao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == produtoId, ct)
                      ?? throw new RegraNegocioException("Produto não encontrado.");

        var movimentacao = operacao(produto, relogio.GetLocalNow().DateTime);
        db.MovimentacoesEstoque.Add(movimentacao);
        await db.SaveChangesAsync(ct);
        return movimentacao;
    }
}
