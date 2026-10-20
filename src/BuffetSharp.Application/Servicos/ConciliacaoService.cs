using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public enum SituacaoConciliacao
{
    /// <summary>Lançamento do extrato já conferido com um movimento do sistema.</summary>
    Conciliado,

    /// <summary>Há um movimento do sistema com o mesmo valor em data próxima: confirme para conciliar.</summary>
    Sugerido,

    /// <summary>Não há movimento correspondente: lance no sistema (tarifa, rendimento...) ou investigue.</summary>
    SemCorrespondencia
}

/// <summary>Lançamento do extrato com o movimento do sistema correspondente (quando houver).</summary>
public sealed record ItemConciliacao(LancamentoExtrato Extrato, SituacaoConciliacao Situacao, int? MovimentoId, string? Movimento,
    DateOnly? DataMovimento)
{
    public int DiferencaDias => DataMovimento is { } data ? Math.Abs(data.DayNumber - Extrato.Data.DayNumber) : 0;
}

/// <summary>Movimento do sistema no período do extrato que o banco não mostrou (ainda não compensado ou lançado errado).</summary>
public sealed record MovimentoSemExtrato(int Id, DateOnly Data, string Descricao, decimal Valor);

public sealed record AnaliseConciliacao(int ContaId, string Conta, ExtratoOfx Extrato, IReadOnlyList<ItemConciliacao> Itens,
    IReadOnlyList<MovimentoSemExtrato> SemExtrato, decimal SaldoSistemaNaDataDoExtrato)
{
    /// <summary>Diferença entre o saldo informado pelo banco e o do sistema na data do saldo (nula se o OFX não trouxe saldo).</summary>
    public decimal? DiferencaSaldo => Extrato.Saldo is { } saldo ? saldo - SaldoSistemaNaDataDoExtrato : null;
}

/// <summary>
/// Conciliação bancária: compara o extrato do banco (arquivo OFX) com os movimentos da conta no sistema, sugere os pares
/// (mesmo valor, data até <see cref="ToleranciaDias"/> dias de diferença), concilia os confirmados e lança no sistema o que
/// só aparece no banco (tarifas, rendimentos...).
/// </summary>
public sealed class ConciliacaoService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    public const int ToleranciaDias = 5;
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    public async Task<AnaliseConciliacao> AnalisarAsync(int contaId, ExtratoOfx extrato, CancellationToken ct = default)
    {
        Validador.Garantir(extrato.Lancamentos.Count > 0, "O extrato não tem lançamentos.");
        await using var db = fabrica.Criar();
        var conta = await db.ContasFinanceiras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contaId, ct)
                    ?? throw new RegraNegocioException("Conta não encontrada.");
        Validador.Garantir(!conta.EhCaixa, "A conciliação com extrato é feita nas contas bancárias, não no caixa.");

        var inicio = (extrato.Inicio is { } i && i < extrato.Lancamentos.Min(l => l.Data) ? i : extrato.Lancamentos.Min(l => l.Data))
            .AddDays(-ToleranciaDias);
        var fim = (extrato.Fim is { } f && f > extrato.Lancamentos.Max(l => l.Data) ? f : extrato.Lancamentos.Max(l => l.Data))
            .AddDays(ToleranciaDias);
        var movimentos = await db.MovimentosConta.AsNoTracking()
            .Where(m => m.ContaFinanceiraId == contaId && m.Data >= inicio && m.Data <= fim)
            .OrderBy(m => m.Data).ThenBy(m => m.Id)
            .ToListAsync(ct);
        var idsExtrato = extrato.Lancamentos.Select(l => l.Id).ToList();
        var jaConciliados = await db.MovimentosConta.AsNoTracking()
            .Where(m => m.ContaFinanceiraId == contaId && m.IdExtrato != null && idsExtrato.Contains(m.IdExtrato))
            .ToDictionaryAsync(m => m.IdExtrato!, ct);

        var usados = new HashSet<int>(jaConciliados.Values.Select(m => m.Id));
        var itens = new List<ItemConciliacao>();
        foreach (var lancamento in extrato.Lancamentos)
        {
            if (jaConciliados.TryGetValue(lancamento.Id, out var conciliado))
            {
                itens.Add(new ItemConciliacao(lancamento, SituacaoConciliacao.Conciliado, conciliado.Id, conciliado.Descricao, conciliado.Data));
                continue;
            }

            var par = movimentos
                .Where(m => !m.Conciliado && !usados.Contains(m.Id) && m.Valor == lancamento.Valor &&
                            Math.Abs(m.Data.DayNumber - lancamento.Data.DayNumber) <= ToleranciaDias)
                .OrderBy(m => Math.Abs(m.Data.DayNumber - lancamento.Data.DayNumber)).ThenBy(m => m.Id)
                .FirstOrDefault();
            if (par is null)
            {
                itens.Add(new ItemConciliacao(lancamento, SituacaoConciliacao.SemCorrespondencia, null, null, null));
                continue;
            }
            usados.Add(par.Id);
            itens.Add(new ItemConciliacao(lancamento, SituacaoConciliacao.Sugerido, par.Id, par.Descricao, par.Data));
        }

        var periodoInicio = extrato.Inicio ?? extrato.Lancamentos.Min(l => l.Data);
        var periodoFim = extrato.Fim ?? extrato.Lancamentos.Max(l => l.Data);
        var semExtrato = movimentos
            .Where(m => !m.Conciliado && !usados.Contains(m.Id) && m.Data >= periodoInicio && m.Data <= periodoFim)
            .Select(m => new MovimentoSemExtrato(m.Id, m.Data, m.Descricao, m.Valor))
            .ToList();

        var dataSaldo = extrato.DataSaldo ?? periodoFim;
        var saldoSistema = conta.SaldoInicial +
                           (await db.MovimentosConta.Where(m => m.ContaFinanceiraId == contaId && m.Data <= dataSaldo)
                               .SumAsync(m => (decimal?)m.Valor, ct) ?? 0);
        return new AnaliseConciliacao(contaId, conta.Nome, extrato, itens, semExtrato, saldoSistema);
    }

    /// <summary>Concilia os pares confirmados (movimento do sistema × lançamento do extrato). Retorna quantos foram conciliados.</summary>
    public async Task<int> ConciliarAsync(int contaId, IEnumerable<ItemConciliacao> confirmados, CancellationToken ct = default)
    {
        var pares = confirmados.Where(i => i.Situacao == SituacaoConciliacao.Sugerido && i.MovimentoId is not null).ToList();
        Validador.Garantir(pares.Count > 0, "Marque ao menos uma sugestão para conciliar.");

        await using var db = fabrica.Criar();
        var ids = pares.Select(p => p.MovimentoId!.Value).ToList();
        var movimentos = await db.MovimentosConta.Where(m => ids.Contains(m.Id) && m.ContaFinanceiraId == contaId).ToDictionaryAsync(m => m.Id, ct);
        Validador.Garantir(movimentos.Count == ids.Distinct().Count(), "Um dos movimentos não existe mais. Analise o extrato de novo.");
        foreach (var par in pares)
            movimentos[par.MovimentoId!.Value].Conciliar(par.Extrato.Id, par.Extrato.Valor, Agora);
        await db.SaveChangesAsync(ct);
        return pares.Count;
    }

    /// <summary>
    /// Lança no sistema um lançamento que só aparece no extrato (tarifa, rendimento, depósito não registrado), já conciliado.
    /// </summary>
    public async Task<int> LancarAsync(int contaId, LancamentoExtrato lancamento, string? descricao = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var conta = await db.ContasFinanceiras.FirstOrDefaultAsync(c => c.Id == contaId, ct)
                    ?? throw new RegraNegocioException("Conta não encontrada.");
        Validador.Garantir(!await db.MovimentosConta.AnyAsync(m => m.ContaFinanceiraId == contaId && m.IdExtrato == lancamento.Id, ct),
            "Este lançamento do extrato já está conciliado.");
        var texto = string.IsNullOrWhiteSpace(descricao) ? $"Extrato: {lancamento.Descricao}" : descricao.Trim();
        var movimento = new MovimentoConta(conta, lancamento.Data, lancamento.Valor, texto, OrigemMovimento.Lancamento);
        movimento.Conciliar(lancamento.Id, lancamento.Valor, Agora);
        db.MovimentosConta.Add(movimento);
        await db.SaveChangesAsync(ct);
        return movimento.Id;
    }

    public async Task DesfazerAsync(int movimentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var movimento = await db.MovimentosConta.FirstOrDefaultAsync(m => m.Id == movimentoId, ct)
                        ?? throw new RegraNegocioException("Movimento não encontrado.");
        movimento.DesfazerConciliacao();
        await db.SaveChangesAsync(ct);
    }
}
