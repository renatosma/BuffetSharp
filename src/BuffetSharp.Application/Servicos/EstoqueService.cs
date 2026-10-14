using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class EstoqueService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    public Task<MovimentacaoEstoque> RegistrarEntradaAsync(int produtoId, decimal quantidade, decimal custoUnitario,
        string? observacao, CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.RegistrarEntrada(quantidade, custoUnitario, agora, observacao), ct);

    public Task<MovimentacaoEstoque> RegistrarSaidaAsync(int produtoId, decimal quantidade, string? observacao,
        CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.RegistrarSaida(quantidade, agora, observacao), ct);

    public Task<MovimentacaoEstoque> AjustarSaldoAsync(int produtoId, decimal saldoContado, string? observacao,
        CancellationToken ct = default) =>
        MovimentarAsync(produtoId, (p, agora) => p.AjustarSaldo(saldoContado, agora, observacao ?? "Ajuste de inventário"), ct);

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
