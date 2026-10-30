using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Lote com a situação da validade (ok, vencendo, vencido).</summary>
public sealed record LoteResumo(int Id, int ProdutoId, string Produto, string Unidade, string? Numero, DateOnly Validade, decimal Quantidade,
    decimal Saldo, decimal CustoUnitario, DateTime EntradaEm, SituacaoLote Situacao, int DiasParaVencer)
{
    public decimal Valor => Math.Round(Saldo * CustoUnitario, 2);
}

/// <summary>Descarte (perda) registrado no estoque.</summary>
public sealed record PerdaResumo(int MovimentacaoId, DateTime Data, string Produto, string Unidade, decimal Quantidade, decimal CustoUnitario,
    string? Observacao)
{
    public decimal Valor => Math.Round(Quantidade * CustoUnitario, 2);
}

/// <summary>
/// Validade dos produtos perecíveis: os lotes com saldo e a situação de cada um, os lembretes do que vence nos próximos dias (e do
/// que já venceu e ainda está no estoque) e as perdas por descarte no período.
/// </summary>
public sealed class ValidadeService(IBuffetDbContextFactory fabrica, LembretesOptions opcoes, TimeProvider relogio)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    /// <param name="somenteComSaldo">Falso inclui os lotes já consumidos ou descartados (histórico).</param>
    public async Task<IReadOnlyList<LoteResumo>> ListarLotesAsync(bool somenteComSaldo = true, CancellationToken ct = default)
    {
        var hoje = Hoje;
        await using var db = fabrica.Criar();
        var consulta = db.LotesEstoque.AsNoTracking().AsQueryable();
        if (somenteComSaldo)
            consulta = consulta.Where(l => l.Saldo > 0);
        var lotes = await consulta
            .Select(l => new { l.Id, l.ProdutoId, Produto = l.Produto!.Nome, Unidade = l.Produto.UnidadeMedida!.Sigla, l.Numero, l.Validade, l.Quantidade,
                l.Saldo, l.CustoUnitario, l.EntradaEm })
            .ToListAsync(ct);
        return lotes.Select(l =>
            {
                var situacao = l.Validade < hoje ? SituacaoLote.Vencido : l.Validade <= hoje.AddDays(opcoes.DiasValidade) ? SituacaoLote.Vencendo : SituacaoLote.Ok;
                return new LoteResumo(l.Id, l.ProdutoId, l.Produto, l.Unidade, l.Numero, l.Validade, l.Quantidade, l.Saldo, l.CustoUnitario, l.EntradaEm,
                    situacao, l.Validade.DayNumber - hoje.DayNumber);
            })
            .OrderBy(l => l.Validade).ThenBy(l => l.Produto).ToList();
    }

    /// <summary>Descartes (perdas) do período, do mais recente para o mais antigo.</summary>
    public async Task<IReadOnlyList<PerdaResumo>> PerdasAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await using var db = fabrica.Criar();
        return await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.Tipo == TipoMovimentacao.Perda && m.Data >= inicio && m.Data < fim)
            .OrderByDescending(m => m.Data)
            .Select(m => new PerdaResumo(m.Id, m.Data, m.Produto!.Nome, m.Produto.UnidadeMedida!.Sigla, m.Quantidade, m.CustoUnitario, m.Observacao))
            .ToListAsync(ct);
    }

    /// <summary>Lotes com saldo que já venceram ou vencem nos próximos dias (Lembretes:DiasValidade).</summary>
    public async Task<IReadOnlyList<Lembrete>> LembretesAsync(CancellationToken ct = default) =>
        (await ListarLotesAsync(somenteComSaldo: true, ct)).Where(l => l.Situacao != SituacaoLote.Ok).Select(l => new Lembrete
        {
            Tipo = TipoLembrete.ValidadeLote,
            ReferenciaId = l.Id,
            Data = l.Validade.ToDateTime(TimeOnly.MinValue),
            Titulo = l.Situacao == SituacaoLote.Vencido
                ? $"{l.Produto}: lote vencido no estoque (descartar)"
                : l.DiasParaVencer == 0 ? $"{l.Produto}: lote vence hoje" : $"{l.Produto}: lote vence em {l.DiasParaVencer} dia(s)",
            Detalhe = $"{(l.Numero is null ? "Lote sem número" : $"Lote {l.Numero}")}, validade {l.Validade:dd/MM/yyyy} • saldo {l.Saldo:N3} {l.Unidade}",
            Valor = l.Valor,
            Chave = $"validade:{l.Id}:{(l.Situacao == SituacaoLote.Vencido ? "vencido" : "vencendo")}"
        }).ToList();
}
