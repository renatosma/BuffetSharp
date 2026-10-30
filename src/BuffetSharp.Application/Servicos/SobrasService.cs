using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Um prato do evento na tela de sobras: o produzido e o que sobrou.</summary>
public sealed class SobraLinha
{
    public int? ItemCardapioId { get; init; }
    public string Prato { get; init; } = string.Empty;
    public string Secao { get; init; } = string.Empty;
    public int Porcoes { get; init; }

    /// <summary>Custo dos insumos de uma porção, pelo custo médio (para o custo da sobra).</summary>
    public decimal CustoPorcao { get; init; }

    public decimal Percentual { get; set; }
    public DestinoSobra Destino { get; set; }

    /// <summary>O destino como número, para a lista de opções da grade.</summary>
    public int DestinoValor
    {
        get => (int)Destino;
        set => Destino = (DestinoSobra)value;
    }

    public string? Instituicao { get; set; }
    public decimal Custo => Math.Round(Porcoes * CustoPorcao * Percentual / 100, 2);
}

/// <summary>Um insumo baixado para o evento: o que foi, o que já voltou e o que volta agora.</summary>
public sealed class RetornoLinha
{
    public int ProdutoId { get; init; }
    public string Produto { get; init; } = string.Empty;
    public string Unidade { get; init; } = string.Empty;
    public decimal Consumido { get; init; }
    public decimal JaVoltou { get; init; }
    public decimal CustoUnitario { get; init; }

    /// <summary>Perecível não volta ao estoque.</summary>
    public bool Perecivel { get; init; }

    public decimal Voltar { get; set; }
    public decimal NaoDevolvido => Consumido - JaVoltou;
}

public sealed class SobrasInput
{
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public DateTime Inicio { get; init; }
    public int Pessoas { get; init; }
    public bool Encerrado { get; init; }
    public DateTime? RegistradasEm { get; init; }
    public List<SobraLinha> Pratos { get; init; } = [];
    public List<RetornoLinha> Insumos { get; init; } = [];
}

/// <summary>Evento com as sobras registradas, para o relatório de desperdício.</summary>
public sealed record DesperdicioEvento(int EventoId, string Codigo, string Titulo, DateTime Data, string? Pacote, string TipoEvento,
    decimal CustoInsumos, IReadOnlyList<SobraPrato> Sobras)
{
    public decimal Sobra => Sobras.Sum(s => s.Custo);
    public decimal Desperdicio => Sobras.Where(s => s.Desperdicio).Sum(s => s.Custo);
    public decimal Doado => Sobras.Where(s => s.Destino == DestinoSobra.Doacao).Sum(s => s.Custo);
}

public enum AgrupamentoDesperdicio
{
    Evento,
    Prato,
    Pacote,
    TipoEvento,
    Mes
}

public sealed record DesperdicioGrupo(string Grupo, int Eventos, decimal CustoInsumos, decimal Sobra, decimal Desperdicio, decimal Doado,
    decimal SobraMedia)
{
    /// <summary>Desperdício sobre o custo dos insumos (sem o custo dos insumos, como no agrupamento por prato, nulo).</summary>
    public decimal? PercentualDesperdicio => CustoInsumos <= 0 ? null : Math.Round(Desperdicio / CustoInsumos * 100, 1);
}

/// <summary>Prato que sobra muito: a sobra média nos últimos eventos e a ficha técnica com as quantidades reduzidas.</summary>
public sealed record SugestaoFicha(int ItemCardapioId, string Prato, int Eventos, decimal SobraMedia, decimal Fator,
    IReadOnlyList<(string Insumo, decimal Atual, decimal Sugerida, string Unidade)> Ingredientes)
{
    public string Resumo => string.Join("; ", Ingredientes.Select(i => $"{i.Insumo} {i.Atual:0.####} → {i.Sugerida:0.####} {i.Unidade}"));
}

/// <summary>
/// Sobras e desperdício: depois do evento, quanto sobrou de cada prato e o destino (descarte, doação, refeição da equipe,
/// cliente levou) e os insumos não usados que voltam ao estoque. Lembrete de registrar, relatório de desperdício e sugestão
/// de reduzir a ficha técnica dos pratos que sobram muito.
/// </summary>
public sealed class SobrasService(
    IBuffetDbContextFactory fabrica,
    BuffetOptions opcoes,
    LembretesOptions lembretes,
    ISessaoUsuario sessao,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    /// <summary>Pratos do evento (o plano do cardápio, com os substitutos) e os insumos baixados, com o já registrado.</summary>
    public async Task<SobrasInput> PrepararAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTrackingWithIdentityResolution().ComCardapioCompleto().Include(e => e.Sobras)
                         .FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        Validador.Garantir(evento.Pacote is not null, "O evento não tem pacote de cardápio.");
        var pessoas = evento.PessoasAtendidas;
        var contexto = evento.ContextoConsumo(pessoas);
        var plano = evento.PlanejarCardapio(pessoas);
        var registradas = evento.Sobras.Where(s => s.ItemCardapioId is not null).GroupBy(s => s.ItemCardapioId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var pratos = plano.Porcoes.Where(p => p.Value > 0)
            .OrderBy(p => p.Key.CategoriaCardapio?.Ordem ?? 99).ThenBy(p => p.Key.Nome)
            .Select(p =>
            {
                var registrada = registradas.GetValueOrDefault(p.Key.Id);
                return new SobraLinha
                {
                    ItemCardapioId = p.Key.Id,
                    Prato = p.Key.Nome,
                    Secao = p.Key.CategoriaCardapio?.Nome ?? "",
                    Porcoes = p.Value,
                    CustoPorcao = Math.Round(p.Key.Ingredientes.Sum(f => f.Quantidade(p.Value, contexto) * (f.Produto?.CustoMedio ?? 0)) / p.Value, 4),
                    Percentual = registrada?.Percentual ?? 0,
                    Destino = registrada?.Destino ?? DestinoSobra.Descarte,
                    Instituicao = registrada?.Instituicao
                };
            }).ToList();

        var movimentos = await db.MovimentacoesEstoque.AsNoTracking().Include(m => m.Produto!).ThenInclude(p => p.UnidadeMedida)
            .Where(m => m.EventoId == eventoId && (m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.RetornoEvento))
            .ToListAsync(ct);
        var insumos = movimentos.GroupBy(m => m.ProdutoId).Select(g =>
        {
            var saidas = g.Where(m => m.Tipo == TipoMovimentacao.Saida).ToList();
            var consumido = saidas.Sum(m => m.Quantidade);
            var produto = g.First().Produto!;
            return new RetornoLinha
            {
                ProdutoId = g.Key,
                Produto = produto.Nome,
                Unidade = produto.UnidadeMedida?.Sigla ?? "",
                Consumido = consumido,
                JaVoltou = g.Where(m => m.Tipo == TipoMovimentacao.RetornoEvento).Sum(m => m.Quantidade),
                CustoUnitario = consumido == 0 ? 0 : Math.Round(saidas.Sum(m => m.Quantidade * m.CustoUnitario) / consumido, 4),
                Perecivel = produto.ControlaValidade
            };
        }).OrderBy(i => i.Produto).ToList();

        return new SobrasInput
        {
            EventoId = evento.Id,
            Evento = $"{evento.Codigo} - {evento.Titulo}",
            Inicio = evento.Inicio,
            Pessoas = pessoas,
            Encerrado = evento.Status == StatusEvento.Realizado,
            RegistradasEm = evento.SobrasRegistradasEm,
            Pratos = pratos,
            Insumos = insumos
        };
    }

    /// <summary>Registra (ou refaz) as sobras e devolve ao estoque os insumos não usados informados.</summary>
    public async Task RegistrarAsync(SobrasInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.Include(e => e.Sobras).FirstOrDefaultAsync(e => e.Id == input.EventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        evento.RegistrarSobras(input.Pratos.Select(p => new SobraInformada(p.ItemCardapioId, p.Prato, p.Porcoes, p.CustoPorcao, p.Percentual,
            p.Destino, p.Instituicao)), sessao.Usuario?.Login, Agora);

        var retornos = input.Insumos.Where(i => i.Voltar > 0).ToList();
        if (retornos.Count > 0)
        {
            var ids = retornos.Select(r => r.ProdutoId).ToList();
            var produtos = await db.Produtos.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            var naoDevolvido = (await db.MovimentacoesEstoque.Where(m => m.EventoId == evento.Id && ids.Contains(m.ProdutoId) &&
                                                                         (m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.RetornoEvento))
                    .Select(m => new { m.ProdutoId, m.Tipo, m.Quantidade }).ToListAsync(ct))
                .GroupBy(m => m.ProdutoId)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.Tipo == TipoMovimentacao.Saida ? m.Quantidade : -m.Quantidade));
            foreach (var retorno in retornos)
                db.MovimentacoesEstoque.Add(produtos[retorno.ProdutoId].RetornarDoEvento(retorno.Voltar, retorno.CustoUnitario,
                    naoDevolvido.GetValueOrDefault(retorno.ProdutoId), Agora, evento.Id, $"Não usado no evento {evento.Codigo}"));
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Eventos encerrados nos últimos dias sem as sobras registradas.</summary>
    public async Task<IReadOnlyList<Lembrete>> LembretesAsync(CancellationToken ct = default)
    {
        if (lembretes.DiasSobras <= 0)
            return [];
        await using var db = fabrica.Criar();
        var desde = Agora.Date.AddDays(-lembretes.DiasSobras);
        var pendentes = await db.Eventos.AsNoTracking()
            .Where(e => e.Status == StatusEvento.Realizado && e.SobrasRegistradasEm == null && e.PacoteId != null && e.Fim >= desde && e.Fim <= Agora)
            .OrderBy(e => e.Fim)
            .Select(e => new { e.Id, e.Titulo, e.Fim })
            .ToListAsync(ct);
        return pendentes.Select(e => new Lembrete
        {
            Tipo = TipoLembrete.SobrasPendentes,
            ReferenciaId = e.Id,
            EventoId = e.Id,
            Data = e.Fim,
            Titulo = $"EVT-{e.Id:D5}: registrar as sobras e o desperdício",
            Detalhe = $"{e.Titulo} • encerrado em {e.Fim:dd/MM HH:mm}",
            Chave = $"sobras:{e.Id}"
        }).ToList();
    }

    // ---------------- Relatório ----------------

    /// <summary>Eventos do período com as sobras registradas, com o custo dos insumos (baixados menos os que voltaram).</summary>
    public async Task<IReadOnlyList<DesperdicioEvento>> EventosAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var (inicio, fim) = (de.ToDateTime(TimeOnly.MinValue), ate.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var eventos = await db.Eventos.AsNoTracking().Include(e => e.Sobras).Include(e => e.Pacote).Include(e => e.TipoEvento)
            .Where(e => e.SobrasRegistradasEm != null && e.Inicio >= inicio && e.Inicio < fim)
            .OrderBy(e => e.Inicio).ToListAsync(ct);
        var ids = eventos.Select(e => e.Id).ToList();
        var insumos = (await db.MovimentacoesEstoque.AsNoTracking()
                .Where(m => m.EventoId != null && ids.Contains(m.EventoId.Value) &&
                            (m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.RetornoEvento))
                .Select(m => new { m.EventoId, m.Tipo, Valor = m.Quantidade * m.CustoUnitario }).ToListAsync(ct))
            .GroupBy(m => m.EventoId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Tipo == TipoMovimentacao.Saida ? m.Valor : -m.Valor));
        return eventos.Select(e => new DesperdicioEvento(e.Id, e.Codigo, e.Titulo, e.Inicio, e.Pacote?.Nome, e.TipoEvento?.Nome ?? "",
            Math.Round(insumos.GetValueOrDefault(e.Id), 2), e.Sobras.ToList())).ToList();
    }

    public static IReadOnlyList<DesperdicioGrupo> Agrupar(IEnumerable<DesperdicioEvento> eventos, AgrupamentoDesperdicio agrupamento)
    {
        var lista = eventos.ToList();
        if (agrupamento == AgrupamentoDesperdicio.Prato)
            return lista.SelectMany(e => e.Sobras.Select(s => (Evento: e, Sobra: s)))
                .GroupBy(x => x.Sobra.Prato)
                .Select(g => new DesperdicioGrupo(g.Key, g.Select(x => x.Evento.EventoId).Distinct().Count(), 0, g.Sum(x => x.Sobra.Custo),
                    g.Where(x => x.Sobra.Desperdicio).Sum(x => x.Sobra.Custo), g.Where(x => x.Sobra.Destino == DestinoSobra.Doacao).Sum(x => x.Sobra.Custo),
                    Math.Round(g.Average(x => x.Sobra.Percentual), 1)))
                .OrderByDescending(g => g.SobraMedia).ToList();

        Func<DesperdicioEvento, string> chave = agrupamento switch
        {
            AgrupamentoDesperdicio.Pacote => e => e.Pacote ?? "(sem pacote)",
            AgrupamentoDesperdicio.TipoEvento => e => e.TipoEvento,
            AgrupamentoDesperdicio.Mes => e => e.Data.ToString("yyyy-MM"),
            _ => e => $"{e.Codigo} - {e.Titulo}"
        };
        var grupos = lista.GroupBy(chave).Select(g => new DesperdicioGrupo(g.Key, g.Count(), g.Sum(e => e.CustoInsumos), g.Sum(e => e.Sobra),
            g.Sum(e => e.Desperdicio), g.Sum(e => e.Doado), Math.Round(g.SelectMany(e => e.Sobras).Select(s => s.Percentual).DefaultIfEmpty(0).Average(), 1)));
        return agrupamento == AgrupamentoDesperdicio.Mes ? grupos.OrderBy(g => g.Grupo).ToList() : grupos.OrderByDescending(g => g.Desperdicio).ToList();
    }

    // ---------------- Ajuste da ficha técnica ----------------

    /// <summary>
    /// Pratos cuja sobra média nos últimos eventos registrados passa do limite, com a ficha técnica reduzida na proporção (deixando
    /// uma folga).
    /// </summary>
    public async Task<IReadOnlyList<SugestaoFicha>> SugestoesAsync(CancellationToken ct = default)
    {
        var parametros = opcoes.Sobras;
        await using var db = fabrica.Criar();
        var registros = await db.Eventos.AsNoTracking().Where(e => e.SobrasRegistradasEm != null)
            .SelectMany(e => e.Sobras.Where(s => s.ItemCardapioId != null).Select(s => new { s.ItemCardapioId, e.Inicio, Registro = e.SobrasRegistradasEm, s.Percentual }))
            .ToListAsync(ct);
        var ids = registros.Select(r => r.ItemCardapioId!.Value).Distinct().ToList();
        var itens = await db.ItensCardapio.AsNoTracking().Include(i => i.Ingredientes).ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida)
            .Where(i => ids.Contains(i.Id) && i.Ativo).ToListAsync(ct);
        return itens.Select(i =>
        {
            // Só as sobras registradas depois da última alteração do prato: a ficha ajustada recomeça a contagem.
            var ultimos = registros.Where(r => r.ItemCardapioId == i.Id && r.Registro > (i.AtualizadoEm ?? DateTime.MinValue))
                .OrderByDescending(r => r.Inicio).Take(parametros.EventosAnalise).ToList();
            if (ultimos.Count < Math.Min(3, parametros.EventosAnalise) || ultimos.Average(r => r.Percentual) < parametros.LimiteSugestao)
                return null;
            var media = Math.Round(ultimos.Average(r => r.Percentual), 1);
            var fator = AjusteFicha.Fator(media, parametros.Folga);
            return new SugestaoFicha(i.Id, i.Nome, ultimos.Count, media, fator, i.Ingredientes.OrderBy(f => f.Produto?.Nome)
                .Select(f => (f.Produto?.Nome ?? "?", f.QuantidadePorPessoa, Math.Round(f.QuantidadePorPessoa * fator, 4), f.Produto?.UnidadeMedida?.Sigla ?? ""))
                .ToList());
        }).OfType<SugestaoFicha>().Where(s => s.Fator < 1).OrderByDescending(s => s.SobraMedia).ToList();
    }

    /// <summary>Multiplica as quantidades da ficha técnica do prato pelo fator (ex.: 0,85 = 15% menos).</summary>
    public async Task AplicarAsync(int itemCardapioId, decimal fator, CancellationToken ct = default)
    {
        Validador.Garantir(fator is > 0 and <= 1, "O fator de ajuste vai de 0 a 1.");
        await using var db = fabrica.Criar();
        var item = await db.ItensCardapio.Include(i => i.Ingredientes).FirstOrDefaultAsync(i => i.Id == itemCardapioId, ct)
                   ?? throw new RegraNegocioException("Item do cardápio não encontrado.");
        item.DefinirFichaTecnica(item.Ingredientes
            .Select(f => (f.ProdutoId, Math.Max(0.0001m, Math.Round(f.QuantidadePorPessoa * fator, 4)), f.PorHora, f.SoAdultos)).ToList());
        await db.SaveChangesAsync(ct);
    }
}
