using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Movimentos do caixa e das contas bancárias que não vêm de recebimentos ou pagamentos (aportes, retiradas, tarifas,
/// transferências), e o fechamento (conferência) de cada conta.
/// </summary>
public sealed class ContaFinanceiraService(IBuffetDbContextFactory fabrica, TimeProvider relogio, ISessaoUsuario sessao)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    public async Task<int> LancarAsync(MovimentoInput input, CancellationToken ct = default)
    {
        Validador.Garantir(input.Valor > 0, "O valor deve ser maior que zero.");
        Validador.Garantir(input.Data <= Hoje, "A data não pode ser futura.");
        await using var db = fabrica.Criar();
        var conta = await ContaAsync(db, input.ContaFinanceiraId, ct);
        var movimento = new MovimentoConta(conta, input.Data, input.Entrada ? input.Valor : -input.Valor, input.Descricao,
            OrigemMovimento.Lancamento);
        db.MovimentosConta.Add(movimento);
        await db.SaveChangesAsync(ct);
        return movimento.Id;
    }

    public async Task TransferirAsync(TransferenciaInput input, CancellationToken ct = default)
    {
        Validador.Garantir(input.Data <= Hoje, "A data não pode ser futura.");
        await using var db = fabrica.Criar();
        var origem = await ContaAsync(db, input.OrigemId, ct);
        var destino = await ContaAsync(db, input.DestinoId, ct);
        var (saida, entrada) = MovimentoConta.Transferir(origem, destino, input.Data, input.Valor, input.Descricao);
        db.MovimentosConta.AddRange(entrada, saida);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Exclui um lançamento avulso ou uma transferência (as duas pontas).</summary>
    public async Task ExcluirMovimentoAsync(int movimentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var movimento = await db.MovimentosConta.Include(m => m.Conta).Include(m => m.Contrapartida!).ThenInclude(c => c.Conta)
                            .FirstOrDefaultAsync(m => m.Id == movimentoId, ct)
                        ?? throw new RegraNegocioException("Movimento não encontrado. Atualize a lista.");
        movimento.GarantirExclusaoManual();

        if (movimento.Origem == OrigemMovimento.Transferencia)
        {
            var outra = movimento.Contrapartida
                        ?? await db.MovimentosConta.Include(m => m.Conta).FirstOrDefaultAsync(m => m.ContrapartidaId == movimento.Id, ct);
            if (outra is not null)
            {
                outra.GarantirExclusaoManual();
                db.MovimentosConta.Remove(outra);
            }
        }
        db.MovimentosConta.Remove(movimento);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Saldo da conta ao fim do dia (saldo inicial + movimentos até a data).</summary>
    public async Task<decimal> SaldoAsync(int contaId, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await SaldoAsync(db, await ContaAsync(db, contaId, ct), ate, ct);
    }

    private static async Task<decimal> SaldoAsync(IBuffetDbContext db, ContaFinanceira conta, DateOnly ate, CancellationToken ct) =>
        conta.SaldoInicial + await db.MovimentosConta.Where(m => m.ContaFinanceiraId == conta.Id && m.Data <= ate).SumAsync(m => m.Valor, ct);

    /// <summary>
    /// Fecha o caixa (ou concilia a conta) até a data. Diferença entre o saldo do sistema e o informado vira um
    /// movimento de ajuste. Depois disso a conta não aceita movimentos até essa data.
    /// </summary>
    public async Task FecharAsync(FechamentoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var conta = await ContaAsync(db, input.ContaFinanceiraId, ct);
        var saldo = await SaldoAsync(db, conta, input.Ate, ct);
        db.FechamentosConta.Add(conta.Fechar(input.Ate, Hoje, saldo, input.SaldoInformado, input.Observacao, sessao.Usuario?.Login));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Desfaz o último fechamento da conta (e o ajuste que ele gerou).</summary>
    public async Task ReabrirAsync(int contaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var conta = await ContaAsync(db, contaId, ct);
        var fechamentos = await db.FechamentosConta.Include(f => f.Ajuste)
            .Where(f => f.ContaFinanceiraId == contaId).OrderByDescending(f => f.Data).Take(2).ToListAsync(ct);
        Validador.Garantir(fechamentos.Count > 0, $"A conta '{conta.Nome}' não tem fechamento.");

        var ultimo = fechamentos[0];
        conta.Reabrir(ultimo, fechamentos.ElementAtOrDefault(1)?.Data);
        db.FechamentosConta.Remove(ultimo);
        if (ultimo.Ajuste is not null)
            db.MovimentosConta.Remove(ultimo.Ajuste);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<ContaFinanceira> ContaAsync(IBuffetDbContext db, int id, CancellationToken ct) =>
        await db.ContasFinanceiras.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new RegraNegocioException("Conta não encontrada.");
}
