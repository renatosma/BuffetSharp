using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// O que entra nos custos previstos de um orçamento, lido dos cadastros: custo dos insumos do pacote por pessoa, equipe
/// estimada do tipo de evento, custo do espaço e dos serviços, comissão do vendedor, taxa da forma de pagamento, a alíquota
/// de impostos e as margens mínima e meta. A tela usa os mesmos parâmetros para mostrar a margem enquanto se edita.
/// </summary>
public sealed record ParametrosMargem(
    IReadOnlyDictionary<int, CustoInsumos> CustoPacote,
    IReadOnlyDictionary<int, (decimal Fixo, decimal PorPessoa)> EquipePorTipo,
    IReadOnlyList<RegraEquipe> RegrasEquipe,
    IReadOnlyDictionary<int, decimal> CustoEspaco,
    IReadOnlyDictionary<int, decimal> CustoServico,
    IReadOnlyDictionary<int, decimal> ComissaoVendedor,
    IReadOnlyDictionary<int, decimal> TaxaForma,
    decimal AliquotaImpostos,
    decimal MargemMinima,
    decimal MargemMeta,
    bool PodeVerCustos)
{
    public PercentuaisReceita Percentuais(int? vendedorId, int? formaPagamentoId) =>
        new(vendedorId is { } v ? ComissaoVendedor.GetValueOrDefault(v) : 0, AliquotaImpostos,
            formaPagamentoId is { } f ? TaxaForma.GetValueOrDefault(f) : 0);

    /// <summary>
    /// Equipe: pelo dimensionamento (quantos de cada cargo × diária do cargo) quando há regra para o tipo de evento; sem regra,
    /// pelo custo estimado do tipo de evento.
    /// </summary>
    public decimal CustoEquipe(int tipoEventoId, int convidados)
    {
        if (DimensionamentoEquipe.TemRegras(RegrasEquipe, tipoEventoId))
            return DimensionamentoEquipe.Calcular(RegrasEquipe, tipoEventoId, convidados).Sum(n => n.Custo);
        var estimada = EquipePorTipo.GetValueOrDefault(tipoEventoId);
        return estimada.Fixo + estimada.PorPessoa * convidados;
    }

    /// <summary>Custos previstos para a receita e a composição informadas.</summary>
    /// <param name="consumo">Pessoas (os isentos também comem e são servidos), adultos e horas do evento.</param>
    public CustosOrcamento Calcular(decimal receita, int? pacoteId, ContextoConsumo consumo, int tipoEventoId, int? espacoId,
        IEnumerable<(int ServicoId, decimal Quantidade)> servicos, int? vendedorId, int? formaPagamentoId)
    {
        return CalculadoraMargem.Calcular(receita,
            pacoteId is { } p ? CustoPacote.GetValueOrDefault(p, CustoInsumos.Nenhum).Calcular(consumo) : 0,
            CustoEquipe(tipoEventoId, consumo.Pessoas),
            servicos.Sum(s => s.Quantidade * CustoServico.GetValueOrDefault(s.ServicoId)),
            espacoId is { } e ? CustoEspaco.GetValueOrDefault(e) : 0,
            Percentuais(vendedorId, formaPagamentoId));
    }

    public FaixaMargem Faixa(decimal? margem) => CalculadoraMargem.Faixa(margem, MargemMinima, MargemMeta);
}

/// <summary>Margem prevista de um orçamento, para o relatório.</summary>
public sealed record MargemOrcamento(int OrcamentoId, DateTime CriadoEm, string Cliente, string? Vendedor, string? Pacote, string TipoEvento,
    StatusOrcamento Status, decimal Receita, decimal Custo, FaixaMargem Faixa)
{
    public string Codigo => $"ORC-{OrcamentoId:D5}";
    public decimal Margem => Receita - Custo;
    public decimal? Percentual => Receita <= 0 ? null : Math.Round(Margem / Receita * 100, 1);
}

public sealed record MargemGrupo(string Grupo, int Orcamentos, decimal Receita, decimal Custo, int AbaixoDaMinima, int Aprovados)
{
    public decimal Margem => Receita - Custo;
    public decimal? Percentual => Receita <= 0 ? null : Math.Round(Margem / Receita * 100, 1);
}

/// <summary>Uma linha do previsto (orçamento) × realizado (evento encerrado).</summary>
public sealed record LinhaComparativo(string Item, decimal Previsto, decimal Realizado, bool Receita = false)
{
    public decimal Diferenca => Realizado - Previsto;
}

/// <param name="Desperdicio">Custo do que foi descartado (já está nos insumos realizados); nulo sem as sobras registradas.</param>
public sealed record ComparativoMargem(string Evento, string? Orcamento, IReadOnlyList<LinhaComparativo> Linhas, bool Encerrado,
    decimal? Desperdicio = null)
{
    public decimal ReceitaPrevista => Linhas.Where(l => l.Receita).Sum(l => l.Previsto);
    public decimal ReceitaRealizada => Linhas.Where(l => l.Receita).Sum(l => l.Realizado);
    public decimal CustoPrevisto => Linhas.Where(l => !l.Receita).Sum(l => l.Previsto);
    public decimal CustoRealizado => Linhas.Where(l => !l.Receita).Sum(l => l.Realizado);
    public decimal? MargemPrevista => ReceitaPrevista <= 0 ? null : Math.Round((ReceitaPrevista - CustoPrevisto) / ReceitaPrevista * 100, 1);
    public decimal? MargemRealizada => ReceitaRealizada <= 0 ? null : Math.Round((ReceitaRealizada - CustoRealizado) / ReceitaRealizada * 100, 1);
}

/// <summary>
/// Margem do orçamento: custos previstos congelados ao salvar, semáforo para o vendedor (custos só para administrador e
/// financeiro), relatório por vendedor, pacote e tipo de evento, e o previsto × realizado do evento encerrado.
/// </summary>
public sealed class MargemService(IBuffetDbContextFactory fabrica, BuffetOptions opcoes, ISessaoUsuario sessao)
{
    /// <summary>Administrador e financeiro veem os custos; o comercial vê só o semáforo.</summary>
    public bool PodeVerCustos => sessao.Usuario is not { } usuario || usuario.PossuiPerfil(PerfilUsuario.Financeiro);

    public async Task<ParametrosMargem> ParametrosAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await ParametrosAsync(db, ct);
    }

    internal async Task<ParametrosMargem> ParametrosAsync(IBuffetDbContext db, CancellationToken ct)
    {
        var pacotes = await db.Pacotes.AsNoTracking()
            .Include(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Ingredientes).ThenInclude(f => f.Produto)
            .ToListAsync(ct);
        return new ParametrosMargem(
            pacotes.ToDictionary(p => p.Id, p => p.CustoInsumos),
            await db.TiposEvento.AsNoTracking().ToDictionaryAsync(t => t.Id, t => (t.CustoEquipeFixo, t.CustoEquipePorPessoa), ct),
            await DimensionamentoService.RegrasAsync(db, ct),
            await db.Espacos.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.CustoPorEvento, ct),
            await db.ServicosAdicionais.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Custo, ct),
            await db.Usuarios.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.PercentualComissao, ct),
            await db.FormasPagamento.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.TaxaPercentual, ct),
            opcoes.Margem.AliquotaImpostos, opcoes.Margem.MargemMinima, opcoes.Margem.MargemMeta, PodeVerCustos);
    }

    /// <summary>Custos previstos do orçamento como está (composição, vendedor e forma já definidos).</summary>
    internal async Task<CustosOrcamento> CalcularAsync(IBuffetDbContext db, Orcamento orcamento, CancellationToken ct) =>
        (await ParametrosAsync(db, ct)).Calcular(orcamento.ValorTotal, orcamento.PacoteId,
            ContextoConsumo.De(orcamento.QuantidadeConvidados, orcamento.QuantidadesPorFaixa.Inteira, orcamento.Inicio, orcamento.Fim),
            orcamento.TipoEventoId, orcamento.EspacoId, orcamento.Servicos.Where(s => s.ServicoAdicionalId is not null)
                .Select(s => (s.ServicoAdicionalId!.Value, s.Quantidade)),
            orcamento.VendedorId, orcamento.FormaPagamentoId);

    // ---------------- Relatório ----------------

    /// <summary>Orçamentos criados no período com custos calculados.</summary>
    public async Task<IReadOnlyList<MargemOrcamento>> OrcamentosAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        GarantirCustos();
        await using var db = fabrica.Criar();
        var (inicio, fim) = (de.ToDateTime(TimeOnly.MinValue), ate.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var linhas = await db.Orcamentos.AsNoTracking()
            .Where(o => o.CriadoEm >= inicio && o.CriadoEm < fim)
            .OrderBy(o => o.CriadoEm)
            .Select(o => new
            {
                o.Id, o.CriadoEm, Cliente = o.Cliente!.Nome, Vendedor = o.Vendedor != null ? o.Vendedor.Nome : null,
                Pacote = o.Pacote != null ? o.Pacote.Nome : null, TipoEvento = o.TipoEvento!.Nome, o.Status, o.ValorTotal,
                Custo = o.CustoInsumos + o.CustoEquipe + o.CustoServicos + o.CustoEspaco + o.CustoComissao + o.CustoImpostos + o.CustoTaxa
            }).ToListAsync(ct);
        return linhas.Where(o => o.Custo > 0).Select(o =>
        {
            var margem = o.ValorTotal <= 0 ? (decimal?)null : Math.Round((o.ValorTotal - o.Custo) / o.ValorTotal * 100, 1);
            return new MargemOrcamento(o.Id, o.CriadoEm, o.Cliente, o.Vendedor, o.Pacote, o.TipoEvento, o.Status, o.ValorTotal, o.Custo,
                CalculadoraMargem.Faixa(margem, opcoes.Margem.MargemMinima, opcoes.Margem.MargemMeta));
        }).ToList();
    }

    public static IReadOnlyList<MargemGrupo> Agrupar(IEnumerable<MargemOrcamento> orcamentos, Func<MargemOrcamento, string?> chave) =>
        orcamentos.GroupBy(o => chave(o) ?? "(sem)")
            .Select(g => new MargemGrupo(g.Key, g.Count(), g.Sum(o => o.Receita), g.Sum(o => o.Custo),
                g.Count(o => o.Faixa == FaixaMargem.AbaixoDaMinima), g.Count(o => o.Status == StatusOrcamento.Aprovado)))
            .OrderBy(g => g.Percentual ?? 0).ToList();

    // ---------------- Previsto × realizado ----------------

    /// <summary>
    /// Margem prevista no orçamento × a do evento: receita (com os excedentes), insumos baixados do estoque, equipe escalada,
    /// serviços e despesas lançados no evento, materiais, comissão, impostos e taxa das parcelas recebidas.
    /// </summary>
    public async Task<ComparativoMargem> ComparativoAsync(int eventoId, CancellationToken ct = default)
    {
        GarantirCustos();
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking()
                         .Include(e => e.Orcamento)
                         .Include(e => e.Espaco)
                         .Include(e => e.Equipe)
                         .Include(e => e.Materiais).ThenInclude(m => m.Material)
                         .Include(e => e.Parcelas).ThenInclude(p => p.FormaPagamento)
                         .Include(e => e.Sobras)
                         .FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        var orcamento = evento.Orcamento;
        var previsto = orcamento?.Custos ?? CustosOrcamento.Nenhum;

        // Os insumos que voltaram ao estoque (não usados) saem do realizado.
        var insumos = (await db.MovimentacoesEstoque.AsNoTracking()
                .Where(m => m.EventoId == eventoId && (m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.RetornoEvento))
                .Select(m => new { m.Tipo, Valor = m.Quantidade * m.CustoUnitario }).ToListAsync(ct))
            .Sum(m => m.Tipo == TipoMovimentacao.Saida ? Math.Abs(m.Valor) : -Math.Abs(m.Valor));
        var despesas = await db.ContasPagar.AsNoTracking()
            .Where(c => c.EventoId == eventoId && c.Status != StatusTitulo.Cancelado &&
                        c.Categoria != CategoriaDespesa.Pessoal && c.Categoria != CategoriaDespesa.LocacaoMateriais)
            .SumAsync(c => (decimal?)c.Valor, ct) ?? 0;
        var receita = evento.ValorTotal + evento.ValorExcedentes;
        var taxa = evento.Parcelas.Where(p => p.Status == StatusTitulo.Pago)
            .Sum(p => Math.Round((p.ValorPago ?? p.Valor) * (p.FormaPagamento?.TaxaPercentual ?? 0) / 100, 2));

        LinhaComparativo Linha(string item, decimal prevista, decimal realizada) => new(item, Math.Round(prevista, 2), Math.Round(realizada, 2));
        var linhas = new List<LinhaComparativo>
        {
            new("Receita", orcamento?.ValorTotal ?? evento.ValorTotal, receita, Receita: true),
            Linha("Insumos do cardápio", previsto.Insumos, insumos),
            Linha("Equipe", previsto.Equipe, evento.TotalEquipe),
            Linha("Serviços e despesas do evento", previsto.Servicos, despesas),
            Linha("Espaço", previsto.Espaco, evento.Espaco?.CustoPorEvento ?? 0),
            Linha("Materiais (locação e perdas)", 0, evento.CustoMateriais),
            Linha("Comissão", previsto.Comissao, receita * evento.PercentualComissao / 100),
            Linha("Impostos", previsto.Impostos, receita * opcoes.Margem.AliquotaImpostos / 100),
            Linha("Taxa das formas de pagamento", previsto.Taxa, taxa)
        };
        return new ComparativoMargem($"{evento.Codigo} - {evento.Titulo}", orcamento?.Codigo, linhas, evento.Status == StatusEvento.Realizado,
            evento.SobrasRegistradasEm is null ? null : evento.CustoDesperdicio);
    }

    private void GarantirCustos() =>
        Validador.Garantir(PodeVerCustos, "Somente o administrador e o financeiro veem os custos e a margem.");
}
