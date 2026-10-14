using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class FinanceiroService(IBuffetDbContextFactory fabrica)
{
    // ------------------------- Contas a receber -------------------------

    public Task PagarParcelaAsync(int parcelaId, PagamentoInput pagamento, CancellationToken ct = default) =>
        AlterarAsync<Parcela>(parcelaId, p => p.RegistrarPagamento(pagamento.Data, pagamento.Valor, pagamento.FormaPagamentoId), ct);

    public Task EstornarParcelaAsync(int parcelaId, CancellationToken ct = default) =>
        AlterarAsync<Parcela>(parcelaId, p => p.EstornarPagamento(), ct);

    // ------------------------- Contas a pagar -------------------------

    public async Task<ContaPagarInput> ObterContaAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var c = await db.ContasPagar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Conta não encontrada.");
        return new ContaPagarInput
        {
            Id = c.Id,
            Descricao = c.Descricao,
            Categoria = c.Categoria,
            Valor = c.Valor,
            Vencimento = c.Vencimento,
            FornecedorId = c.FornecedorId,
            EventoId = c.EventoId,
            Observacao = c.Observacao,
            Status = c.Status
        };
    }

    public async Task SalvarContaAsync(ContaPagarInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        if (input.Id == 0)
        {
            Validador.Garantir(input.Repeticoes is >= 1 and <= 24, "As repetições devem estar entre 1 e 24 meses.");
            for (var i = 0; i < input.Repeticoes; i++)
            {
                var descricao = input.Repeticoes > 1 ? $"{input.Descricao} ({i + 1}/{input.Repeticoes})" : input.Descricao;
                var conta = new ContaPagar(descricao, input.Categoria, input.Valor, input.Vencimento.AddMonths(i),
                    input.FornecedorId, input.EventoId) { Observacao = input.Observacao };
                db.ContasPagar.Add(conta);
            }
        }
        else
        {
            var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == input.Id, ct)
                        ?? throw new RegraNegocioException("Conta não encontrada.");
            conta.Alterar(input.Descricao, input.Categoria, input.Valor, input.Vencimento, input.FornecedorId,
                input.EventoId, input.Observacao);
        }
        await db.SaveChangesAsync(ct);
    }

    public Task PagarContaAsync(int contaId, PagamentoInput pagamento, CancellationToken ct = default) =>
        AlterarAsync<ContaPagar>(contaId, c => c.RegistrarPagamento(pagamento.Data, pagamento.Valor, pagamento.FormaPagamentoId), ct);

    public Task EstornarContaAsync(int contaId, CancellationToken ct = default) =>
        AlterarAsync<ContaPagar>(contaId, c => c.EstornarPagamento(), ct);

    public Task CancelarContaAsync(int contaId, CancellationToken ct = default) =>
        AlterarAsync<ContaPagar>(contaId, c => c.Cancelar(), ct);

    public async Task ExcluirContaAsync(int contaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == contaId, ct)
                    ?? throw new RegraNegocioException("Conta não encontrada.");
        Validador.Garantir(conta.Status != StatusTitulo.Pago, "Contas pagas não podem ser excluídas. Estorne antes.");
        db.ContasPagar.Remove(conta);
        await db.SaveChangesAsync(ct);
    }

    private async Task AlterarAsync<T>(int id, Action<T> acao, CancellationToken ct) where T : TituloFinanceiro
    {
        await using var db = fabrica.Criar();
        var titulo = await db.Set<T>().FirstOrDefaultAsync(t => t.Id == id, ct)
                     ?? throw new RegraNegocioException("Título não encontrado.");
        acao(titulo);
        await db.SaveChangesAsync(ct);
    }
}
