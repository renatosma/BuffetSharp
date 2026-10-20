using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Reajuste das tabelas de preço (pacotes, serviços adicionais e espaços). Com vigência futura, fica agendado e é aplicado
/// sozinho quando a data chega (ao entrar no sistema). Orçamentos e eventos existentes não mudam.
/// </summary>
public sealed class ReajusteService(IBuffetDbContextFactory fabrica, ISessaoUsuario sessao, TimeProvider relogio,
    ILogger<ReajusteService> log)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    public async Task<IReadOnlyList<ReajusteResumo>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.ReajustesPreco.AsNoTracking()
            .OrderByDescending(r => r.VigenciaEm).ThenByDescending(r => r.Id)
            .Select(r => new ReajusteResumo(r.Id, r.Descricao, r.Percentual, r.VigenciaEm, r.Alcance, r.Arredondamento, r.Status,
                r.Itens.Count, r.AplicadoEm, r.DesfeitoEm, r.Usuario))
            .ToListAsync(ct);
    }

    /// <summary>Como ficariam os preços (itens ativos) com o reajuste informado, sem gravar nada.</summary>
    public async Task<IReadOnlyList<ItemReajusteInfo>> SimularAsync(ReajusteInput input, CancellationToken ct = default)
    {
        var reajuste = new ReajustePreco(string.IsNullOrWhiteSpace(input.Descricao) ? "Simulação" : input.Descricao, input.Percentual,
            input.VigenciaEm, input.Alcance, input.Arredondamento, null);
        await using var db = fabrica.Criar();
        return (await ItensAsync(db, reajuste, ct)).Select(i => i.Info).ToList();
    }

    /// <summary>Cria o reajuste; com vigência até hoje, já aplica os novos preços. Retorna o Id.</summary>
    public async Task<int> CriarAsync(ReajusteInput input, CancellationToken ct = default)
    {
        Validador.Garantir(input.VigenciaEm >= Hoje.AddDays(-30), "A vigência não pode ser anterior a 30 dias atrás.");
        await using var db = fabrica.Criar();
        var reajuste = new ReajustePreco(input.Descricao, input.Percentual, input.VigenciaEm, input.Alcance, input.Arredondamento,
            sessao.Usuario?.Login);
        db.ReajustesPreco.Add(reajuste);
        if (reajuste.PodeAplicar(Hoje))
            await AplicarAsync(db, reajuste, ct);
        await db.SaveChangesAsync(ct);
        return reajuste.Id;
    }

    /// <summary>Aplica os reajustes agendados cuja vigência chegou. Retorna quantos foram aplicados.</summary>
    public async Task<int> AplicarPendentesAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var hoje = Hoje;
        var pendentes = await db.ReajustesPreco.Where(r => r.Status == StatusReajuste.Agendado && r.VigenciaEm <= hoje)
            .OrderBy(r => r.VigenciaEm).ThenBy(r => r.Id).ToListAsync(ct);
        foreach (var reajuste in pendentes)
        {
            // Um de cada vez: o segundo reajuste parte dos preços já reajustados pelo primeiro.
            await AplicarAsync(db, reajuste, ct);
            await db.SaveChangesAsync(ct);
            log.LogInformation("Reajuste de preços \"{Descricao}\" ({Percentual}%) aplicado: {Itens} item(ns)", reajuste.Descricao,
                reajuste.Percentual, reajuste.Itens.Count);
        }
        return pendentes.Count;
    }

    public async Task CancelarAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var reajuste = await db.ReajustesPreco.FirstOrDefaultAsync(r => r.Id == id, ct)
                       ?? throw new RegraNegocioException("Reajuste não encontrado.");
        reajuste.Cancelar(Agora);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Volta os preços ao valor anterior ao reajuste. Itens cujo preço foi alterado depois (à mão ou por outro reajuste)
    /// ficam como estão e são listados no resultado.
    /// </summary>
    public async Task<ResultadoDesfazerReajuste> DesfazerAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var reajuste = await db.ReajustesPreco.Include(r => r.Itens).FirstOrDefaultAsync(r => r.Id == id, ct)
                       ?? throw new RegraNegocioException("Reajuste não encontrado.");
        reajuste.RegistrarDesfazer(Agora);

        var restaurados = 0;
        var mantidos = new List<string>();
        foreach (var item in reajuste.Itens)
        {
            var (atual, definir) = await PrecoAsync(db, item.Tipo, item.ItemId, ct);
            if (atual == item.ValorNovo && definir is not null)
            {
                definir(item.ValorAnterior);
                restaurados++;
            }
            else
            {
                mantidos.Add(item.Nome);
            }
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoDesfazerReajuste(restaurados, mantidos);
    }

    public async Task<IReadOnlyList<ItemReajusteInfo>> ItensAplicadosAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var reajuste = await db.ReajustesPreco.AsNoTracking().Include(r => r.Itens).FirstOrDefaultAsync(r => r.Id == id, ct)
                       ?? throw new RegraNegocioException("Reajuste não encontrado.");
        return reajuste.Itens.OrderBy(i => i.Tipo).ThenBy(i => i.Nome)
            .Select(i => new ItemReajusteInfo(i.Tipo, i.ItemId, i.Nome, i.ValorAnterior, i.ValorNovo)).ToList();
    }

    // ------------------------- Infra -------------------------

    private async Task AplicarAsync(IBuffetDbContext db, ReajustePreco reajuste, CancellationToken ct)
    {
        var itens = await ItensAsync(db, reajuste, ct);
        foreach (var item in itens)
            item.Definir(item.Info.ValorNovo);
        reajuste.RegistrarAplicacao(itens.Select(i => (i.Info.Tipo, i.Info.ItemId, i.Info.Nome, i.Info.ValorAnterior, i.Info.ValorNovo)), Agora);
    }

    private sealed record ItemPreco(ItemReajusteInfo Info, Action<decimal> Definir);

    /// <summary>Itens ativos alcançados pelo reajuste cujo preço muda (rastreados, para a aplicação gravar o novo preço).</summary>
    private static async Task<List<ItemPreco>> ItensAsync(IBuffetDbContext db, ReajustePreco reajuste, CancellationToken ct)
    {
        var itens = new List<ItemPreco>();
        if (reajuste.Alcanca(TipoItemReajuste.Pacote))
            foreach (var p in await db.Pacotes.Where(p => p.Ativo).OrderBy(p => p.Nome).ToListAsync(ct))
                itens.Add(new ItemPreco(new ItemReajusteInfo(TipoItemReajuste.Pacote, p.Id, p.Nome, p.PrecoPorPessoa,
                    reajuste.Calcular(p.PrecoPorPessoa)), v => p.PrecoPorPessoa = v));
        if (reajuste.Alcanca(TipoItemReajuste.ServicoAdicional))
            foreach (var s in await db.ServicosAdicionais.Where(s => s.Ativo).OrderBy(s => s.Nome).ToListAsync(ct))
                itens.Add(new ItemPreco(new ItemReajusteInfo(TipoItemReajuste.ServicoAdicional, s.Id, s.Nome, s.Preco,
                    reajuste.Calcular(s.Preco)), v => s.Preco = v));
        if (reajuste.Alcanca(TipoItemReajuste.Espaco))
            foreach (var e in await db.Espacos.Where(e => e.Ativo).OrderBy(e => e.Nome).ToListAsync(ct))
                itens.Add(new ItemPreco(new ItemReajusteInfo(TipoItemReajuste.Espaco, e.Id, e.Nome, e.ValorLocacao,
                    reajuste.Calcular(e.ValorLocacao)), v => e.ValorLocacao = v));
        return itens.Where(i => i.Info.ValorNovo != i.Info.ValorAnterior).ToList();
    }

    private static async Task<(decimal? Atual, Action<decimal>? Definir)> PrecoAsync(IBuffetDbContext db, TipoItemReajuste tipo, int id,
        CancellationToken ct)
    {
        switch (tipo)
        {
            case TipoItemReajuste.Pacote:
                var pacote = await db.Pacotes.FirstOrDefaultAsync(p => p.Id == id, ct);
                return (pacote?.PrecoPorPessoa, pacote is null ? null : v => pacote.PrecoPorPessoa = v);
            case TipoItemReajuste.ServicoAdicional:
                var servico = await db.ServicosAdicionais.FirstOrDefaultAsync(s => s.Id == id, ct);
                return (servico?.Preco, servico is null ? null : v => servico.Preco = v);
            default:
                var espaco = await db.Espacos.FirstOrDefaultAsync(e => e.Id == id, ct);
                return (espaco?.ValorLocacao, espaco is null ? null : v => espaco.ValorLocacao = v);
        }
    }
}
