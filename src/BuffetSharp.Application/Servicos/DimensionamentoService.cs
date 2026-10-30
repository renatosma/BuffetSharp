using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Um cargo do evento: quantos precisa (pela regra), quantos estão escalados e quem.</summary>
public sealed record LinhaEquipe(int CargoId, string Cargo, int Necessarios, int Escalados, IReadOnlyList<string> Nomes, string Regra)
{
    public int Faltam => Math.Max(0, Necessarios - Escalados);
}

public sealed record DimensionamentoEvento(int EventoId, string Evento, int Convidados, IReadOnlyList<LinhaEquipe> Equipe, bool TemRegras)
{
    public int Faltam => Equipe.Sum(l => l.Faltam);

    /// <summary>Ex.: "Garçom 6/8 • Cozinheiro(a) 2/2".</summary>
    public string Resumo => string.Join("  •  ", Equipe.Select(l => $"{l.Cargo} {l.Escalados}/{l.Necessarios}"));
}

/// <summary>Bebida do evento: quanto vai, na unidade do estoque e na embalagem de compra.</summary>
public sealed record BebidaPlano(string Produto, decimal Quantidade, string Unidade, string? Embalagem, bool PorHora, bool SoAdultos);

public sealed record PlanoEquipeBebidas(string Evento, DateTime Inicio, DateTime Fim, string? Espaco, ContextoConsumo Contexto,
    DimensionamentoEvento Equipe, IReadOnlyList<BebidaPlano> Bebidas);

/// <summary>O que a tentativa de completar a equipe conseguiu: quem foi escalado e o que ainda falta (sem gente livre do cargo).</summary>
public sealed record ResultadoCompletarEquipe(IReadOnlyList<string> Escalados, IReadOnlyList<string> Faltando);

/// <summary>
/// Dimensionamento da equipe pelas regras por tipo de evento (1 a cada N convidados, com mínimo; a regra geral vale para os
/// cargos sem regra do tipo): quanto falta escalar, completar com quem está livre, lembrete do evento subdimensionado e o
/// plano de equipe e bebidas para a coordenação.
/// </summary>
public sealed class DimensionamentoService(
    IBuffetDbContextFactory fabrica,
    EventoService eventos,
    EscalaService escalas,
    LembretesOptions lembretes,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    internal static Task<List<RegraEquipe>> RegrasAsync(IBuffetDbContext db, CancellationToken ct) =>
        db.RegrasEquipe.AsNoTracking().Include(r => r.Cargo).Where(r => r.Ativo).ToListAsync(ct);

    /// <summary>Dimensiona o evento (com a equipe e os funcionários carregados).</summary>
    internal static DimensionamentoEvento Dimensionar(Evento evento, IReadOnlyList<RegraEquipe> regras)
    {
        var necessidades = DimensionamentoEquipe.Calcular(regras, evento.TipoEventoId, evento.QuantidadeConvidados);
        var linhas = necessidades.Select(n =>
        {
            var escalados = evento.Equipe.Where(q => q.Funcionario?.CargoId == n.CargoId).ToList();
            return new LinhaEquipe(n.CargoId, n.Cargo, n.Quantidade, escalados.Count,
                escalados.Select(q => q.Funcionario!.Nome).Order().ToList(), n.Regra);
        }).ToList();
        return new DimensionamentoEvento(evento.Id, $"{evento.Codigo} - {evento.Titulo}", evento.QuantidadeConvidados, linhas,
            DimensionamentoEquipe.TemRegras(regras, evento.TipoEventoId));
    }

    public async Task<DimensionamentoEvento> EquipeAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Equipe).ThenInclude(q => q.Funcionario)
                         .FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        return Dimensionar(evento, await RegrasAsync(db, ct));
    }

    /// <summary>
    /// Escala, para cada cargo que falta, quem está livre (sem conflito de escala nem indisponibilidade), pelos de menor
    /// diária. O que não der para completar volta como falta.
    /// </summary>
    public async Task<ResultadoCompletarEquipe> CompletarAsync(int eventoId, CancellationToken ct = default)
    {
        var dimensionamento = await EquipeAsync(eventoId, ct);
        Validador.Garantir(dimensionamento.TemRegras, "Não há regra de equipe para este tipo de evento: cadastre em Cadastros › Equipe por tipo de evento.");
        var disponiveis = await escalas.DisponibilidadeAsync(eventoId, ct);
        var escalados = new List<string>();
        var faltando = new List<string>();
        foreach (var linha in dimensionamento.Equipe.Where(l => l.Faltam > 0))
        {
            var faltam = linha.Faltam;
            foreach (var funcionario in disponiveis.Where(d => d.Livre && d.Cargo == linha.Cargo && !linha.Nomes.Contains(d.Nome))
                         .OrderBy(d => d.Diaria).ThenBy(d => d.Nome))
            {
                if (faltam == 0)
                    break;
                try
                {
                    await eventos.EscalarFuncionarioAsync(eventoId, funcionario.FuncionarioId, null, null, ct);
                    escalados.Add($"{funcionario.Nome} ({linha.Cargo})");
                    faltam--;
                }
                catch (RegraNegocioException)
                {
                    // Escalado em outro evento entre a consulta e agora: tenta o próximo.
                }
            }
            if (faltam > 0)
                faltando.Add($"{faltam} {linha.Cargo}");
        }
        return new ResultadoCompletarEquipe(escalados, faltando);
    }

    /// <summary>Quantos faltam na equipe de cada evento confirmado do período (só os que têm falta).</summary>
    public async Task<IReadOnlyDictionary<int, int>> FaltasAsync(DateTime de, DateTime ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var regras = await RegrasAsync(db, ct);
        if (regras.Count == 0)
            return new Dictionary<int, int>();
        var lista = await db.Eventos.AsNoTracking().Include(e => e.Equipe).ThenInclude(q => q.Funcionario)
            .Where(e => e.Status == StatusEvento.Confirmado && e.Inicio >= de && e.Inicio < ate)
            .ToListAsync(ct);
        return lista.Select(e => (e.Id, Dimensionar(e, regras).Faltam)).Where(f => f.Faltam > 0)
            .ToDictionary(f => f.Id, f => f.Faltam);
    }

    /// <summary>Eventos confirmados dos próximos dias com menos equipe que o dimensionado.</summary>
    public async Task<IReadOnlyList<Lembrete>> LembretesAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var regras = await RegrasAsync(db, ct);
        if (regras.Count == 0 || lembretes.DiasEquipe <= 0)
            return [];
        var (inicio, fim) = (Agora, Agora.Date.AddDays(lembretes.DiasEquipe + 1));
        var lista = await db.Eventos.AsNoTracking().Include(e => e.Equipe).ThenInclude(q => q.Funcionario)
            .Where(e => e.Status == StatusEvento.Confirmado && e.Inicio >= inicio && e.Inicio < fim)
            .ToListAsync(ct);
        return lista.Select(e => (Evento: e, Dimensionamento: Dimensionar(e, regras)))
            .Where(d => d.Dimensionamento.Faltam > 0)
            .Select(d => new Lembrete
            {
                Tipo = TipoLembrete.EquipeIncompleta,
                ReferenciaId = d.Evento.Id,
                EventoId = d.Evento.Id,
                Data = d.Evento.Inicio,
                Titulo = $"{d.Evento.Codigo}: faltam {d.Dimensionamento.Faltam} na equipe",
                Detalhe = $"{d.Evento.Titulo} • faltam " +
                          string.Join(", ", d.Dimensionamento.Equipe.Where(l => l.Faltam > 0).Select(l => $"{l.Faltam} {l.Cargo}")),
                Chave = $"equipe:{d.Evento.Id}:{d.Dimensionamento.Faltam}"
            }).ToList();
    }

    /// <summary>Equipe por cargo (dimensionada × escalada) e as bebidas do evento (seção de bebidas do cardápio), para a coordenação.</summary>
    public async Task<PlanoEquipeBebidas> PlanoAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTrackingWithIdentityResolution()
                         .Include(e => e.Espaco)
                         .Include(e => e.Equipe).ThenInclude(q => q.Funcionario)
                         .ComCardapioCompleto()
                         .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Ingredientes)
                         .ThenInclude(f => f.Produto!).ThenInclude(p => p.Embalagens).ThenInclude(m => m.UnidadeMedida)
                         .FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        var contexto = evento.ContextoConsumo(evento.QuantidadeConvidados);
        var plano = evento.PlanejarCardapio(evento.QuantidadeConvidados);
        var bebidas = plano.Porcoes
            .Where(p => p.Value > 0 && p.Key.CategoriaCardapio?.Nome.Contains("Bebida", StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(p => p.Key.Ingredientes.Select(f => (Ficha: f, Quantidade: f.Quantidade(p.Value, contexto))))
            .GroupBy(f => f.Ficha.ProdutoId)
            .Select(g =>
            {
                var produto = g.First().Ficha.Produto!;
                var quantidade = Math.Round(g.Sum(f => f.Quantidade), 3);
                var embalagem = produto.EmbalagemDeCompra(produto.FornecedorId, quantidade) is { Fator: > 0 } e
                    ? $"{Math.Ceiling(quantidade / e.Fator):0} {e.UnidadeMedida?.Sigla ?? "emb."} de {e.Fator:0.###}"
                    : null;
                return new BebidaPlano(produto.Nome, quantidade, produto.UnidadeMedida?.Sigla ?? "", embalagem,
                    g.Any(f => f.Ficha.PorHora), g.Any(f => f.Ficha.SoAdultos));
            })
            .OrderBy(b => b.Produto).ToList();
        return new PlanoEquipeBebidas($"{evento.Codigo} - {evento.Titulo}", evento.Inicio, evento.Fim, evento.Espaco?.Nome, contexto,
            Dimensionar(evento, await RegrasAsync(db, ct)), bebidas);
    }
}
