using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Evento realizado e a situação da pesquisa de satisfação.</summary>
public sealed record PesquisaResumo(int EventoId, string Titulo, string Cliente, string? Telefone, string? Email, DateTime Data, string TipoEvento,
    string? Vendedor, DateTime? EnviadaEm, DateTime? RespondidaEm, int? Nps, int? Comida, int? Atendimento, int? Espaco, int? Organizacao,
    string? Comentario, DateTime? AvaliacaoGooglePedidaEm, string? TratativaDetrator, int? ClienteId)
{
    public string Codigo => $"EVT-{EventoId:D5}";
    public CategoriaNps? Categoria => Nps is { } nota ? PesquisaSatisfacao.Classificar(nota) : null;
    public bool DetratorPendente => Categoria == CategoriaNps.Detrator && TratativaDetrator is null;
}

/// <summary>Média e NPS de um grupo (tipo de evento ou vendedor).</summary>
public sealed record GrupoSatisfacao(string Nome, int Respostas, decimal? Nps, decimal? MediaAreas);

public sealed record PainelSatisfacao(int Eventos, int Enviadas, int Respondidas, decimal? Nps, int Promotores, int Neutros, int Detratores,
    decimal? Comida, decimal? Atendimento, decimal? Espaco, decimal? Organizacao, IReadOnlyList<GrupoSatisfacao> PorTipo,
    IReadOnlyList<GrupoSatisfacao> PorVendedor, IReadOnlyList<PesquisaResumo> Comentarios);

/// <summary>Resposta registrada pelo comercial (o que o cliente mandou pelo WhatsApp ou disse por telefone).</summary>
public sealed class RespostaPesquisaInput
{
    public int Nps { get; set; } = 10;
    public int? Comida { get; set; }
    public int? Atendimento { get; set; }
    public int? Espaco { get; set; }
    public int? Organizacao { get; set; }
    public string? Comentario { get; set; }
}

/// <summary>
/// Pesquisa de satisfação dos eventos realizados: envio (lembrete alguns dias depois da festa, também automático), registro da
/// resposta, alerta de detrator para o administrador, pedido de avaliação no Google aos promotores e o painel com o NPS.
/// </summary>
public sealed class PesquisaService(IBuffetDbContextFactory fabrica, LembretesOptions opcoes, BuffetOptions empresa, ISessaoUsuario sessao,
    TimeProvider relogio)
{
    /// <summary>Até quando depois da festa a pesquisa ainda é pedida (depois disso, a lembrança já se perdeu).</summary>
    private const int DiasLimitePesquisa = 30;

    private DateTime Agora => relogio.GetLocalNow().DateTime;

    /// <summary>Eventos realizados que terminaram no período, com a pesquisa de cada um.</summary>
    public async Task<IReadOnlyList<PesquisaResumo>> ListarAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await using var db = fabrica.Criar();
        return await ResumosAsync(db, db.Eventos.Where(e => e.Status == StatusEvento.Realizado && e.Fim >= inicio && e.Fim < fim), ct);
    }

    /// <summary>Lembretes da pesquisa: enviar a pesquisa, ligar para o detrator e pedir a avaliação no Google ao promotor.</summary>
    public async Task<IReadOnlyList<Lembrete>> LembretesAsync(CancellationToken ct = default)
    {
        var agora = Agora;
        var limite = agora.AddDays(-DiasLimitePesquisa * 3);
        await using var db = fabrica.Criar();
        var resumos = await ResumosAsync(db, db.Eventos.Where(e => e.Status == StatusEvento.Realizado && e.Fim >= limite), ct);

        var lembretes = new List<Lembrete>();
        foreach (var p in resumos)
        {
            if (p.EnviadaEm is null && p.RespondidaEm is null && p.Data <= agora.AddDays(-opcoes.DiasPesquisaSatisfacao) &&
                p.Data >= agora.AddDays(-DiasLimitePesquisa))
                lembretes.Add(Lembrete(p, TipoLembrete.PesquisaSatisfacao, $"Pesquisa de satisfação de {p.Cliente}", $"pesquisa:{p.EventoId}"));
            else if (p.DetratorPendente)
                lembretes.Add(Lembrete(p, TipoLembrete.Detrator, $"Cliente insatisfeito (nota {p.Nps}): ligar para {p.Cliente}", $"detrator:{p.EventoId}",
                    p.Comentario));
            else if (p.Categoria == CategoriaNps.Promotor && p.AvaliacaoGooglePedidaEm is null && !string.IsNullOrWhiteSpace(empresa.LinkAvaliacaoGoogle) &&
                     p.RespondidaEm >= agora.AddDays(-DiasLimitePesquisa))
                lembretes.Add(Lembrete(p, TipoLembrete.AvaliacaoGoogle, $"Pedir avaliação no Google a {p.Cliente} (nota {p.Nps})", $"google:{p.EventoId}"));
        }
        return lembretes;
    }

    /// <summary>A pesquisa como lembrete: a tela de pesquisas usa o mesmo caminho dos lembretes para a mensagem pronta e o registro.</summary>
    public static Lembrete Lembrete(PesquisaResumo p, TipoLembrete tipo) => Lembrete(p, tipo, p.Cliente, $"{tipo}:{p.EventoId}");

    private static Lembrete Lembrete(PesquisaResumo p, TipoLembrete tipo, string titulo, string chave, string? detalhe = null) => new()
    {
        Tipo = tipo,
        ReferenciaId = p.EventoId,
        EventoId = p.EventoId,
        Data = p.Data,
        Titulo = titulo,
        Detalhe = detalhe ?? $"{p.Codigo} - {p.Titulo} ({p.Data:dd/MM/yyyy})",
        Pessoa = p.Cliente,
        Telefone = p.Telefone,
        Email = p.Email,
        ClienteId = p.ClienteId,
        Chave = chave
    };

    public Task RegistrarEnvioAsync(int eventoId, CancellationToken ct = default) => AlterarAsync(eventoId, p => p.RegistrarEnvio(Agora), ct);

    public Task RegistrarRespostaAsync(int eventoId, RespostaPesquisaInput r, CancellationToken ct = default) =>
        AlterarAsync(eventoId, p => p.RegistrarResposta(r.Nps, r.Comida, r.Atendimento, r.Espaco, r.Organizacao, r.Comentario, sessao.Usuario?.Login, Agora), ct);

    public Task RegistrarPedidoAvaliacaoAsync(int eventoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, p => p.RegistrarPedidoAvaliacao(Agora), ct);

    public Task TratarDetratorAsync(int eventoId, string tratativa, CancellationToken ct = default) =>
        AlterarAsync(eventoId, p => p.TratarDetrator(tratativa, Agora), ct);

    /// <summary>NPS, médias por área, por tipo de evento e por vendedor e os comentários dos eventos do período.</summary>
    public async Task<PainelSatisfacao> PainelAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var pesquisas = await ListarAsync(de, ate, ct);
        var respondidas = pesquisas.Where(p => p.Nps is not null).ToList();
        static decimal? Media(IEnumerable<int?> notas) => notas.Where(n => n is not null).Select(n => (decimal)n!.Value).ToList() is { Count: > 0 } lista
            ? Math.Round(lista.Average(), 2) : null;
        static decimal? MediaAreas(IEnumerable<PesquisaResumo> grupo) =>
            Media(grupo.SelectMany(p => new[] { p.Comida, p.Atendimento, p.Espaco, p.Organizacao }));
        IReadOnlyList<GrupoSatisfacao> Grupos(Func<PesquisaResumo, string> chave) => respondidas.GroupBy(chave)
            .Select(g => new GrupoSatisfacao(g.Key, g.Count(), PesquisaSatisfacao.CalcularNps(g.Select(p => p.Nps!.Value)), MediaAreas(g)))
            .OrderByDescending(g => g.Nps).ThenBy(g => g.Nome).ToList();

        return new PainelSatisfacao(pesquisas.Count, pesquisas.Count(p => p.EnviadaEm is not null || p.RespondidaEm is not null), respondidas.Count,
            PesquisaSatisfacao.CalcularNps(respondidas.Select(p => p.Nps!.Value)),
            respondidas.Count(p => p.Categoria == CategoriaNps.Promotor), respondidas.Count(p => p.Categoria == CategoriaNps.Neutro),
            respondidas.Count(p => p.Categoria == CategoriaNps.Detrator),
            Media(respondidas.Select(p => p.Comida)), Media(respondidas.Select(p => p.Atendimento)), Media(respondidas.Select(p => p.Espaco)),
            Media(respondidas.Select(p => p.Organizacao)),
            Grupos(p => p.TipoEvento), Grupos(p => p.Vendedor ?? "(sem vendedor)"),
            respondidas.Where(p => p.Comentario is not null).OrderByDescending(p => p.RespondidaEm).ToList());
    }

    private static async Task<List<PesquisaResumo>> ResumosAsync(IBuffetDbContext db, IQueryable<Evento> eventos, CancellationToken ct)
    {
        var lista = await eventos.AsNoTracking()
            .Select(e => new
            {
                e.Id, e.Titulo, e.Fim, Cliente = e.Cliente!.Nome, e.ClienteId, Telefone = e.Cliente.Celular ?? e.Cliente.Telefone, e.Cliente.Email,
                Tipo = e.TipoEvento!.Nome, Vendedor = e.Vendedor != null ? e.Vendedor.Nome : null
            })
            .ToListAsync(ct);
        var ids = lista.Select(e => e.Id).ToList();
        var pesquisas = new Dictionary<int, PesquisaSatisfacao>();
        foreach (var bloco in ids.Chunk(500))
            foreach (var p in await db.PesquisasSatisfacao.AsNoTracking().Where(p => bloco.Contains(p.EventoId)).ToListAsync(ct))
                pesquisas[p.EventoId] = p;
        return lista.OrderByDescending(e => e.Fim).Select(e =>
        {
            var p = pesquisas.GetValueOrDefault(e.Id);
            return new PesquisaResumo(e.Id, e.Titulo, e.Cliente, e.Telefone, e.Email, e.Fim, e.Tipo, e.Vendedor, p?.EnviadaEm, p?.RespondidaEm, p?.Nps,
                p?.NotaComida, p?.NotaAtendimento, p?.NotaEspaco, p?.NotaOrganizacao, p?.Comentario, p?.AvaliacaoGooglePedidaEm, p?.TratativaDetrator,
                e.ClienteId);
        }).ToList();
    }

    /// <summary>A pesquisa do evento (criada na primeira vez), que precisa estar realizado.</summary>
    private async Task AlterarAsync(int eventoId, Action<PesquisaSatisfacao> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var status = await db.Eventos.Where(e => e.Id == eventoId).Select(e => (StatusEvento?)e.Status).FirstOrDefaultAsync(ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        Validador.Garantir(status == StatusEvento.Realizado, "A pesquisa de satisfação é feita depois do evento realizado.");
        var pesquisa = await db.PesquisasSatisfacao.FirstOrDefaultAsync(p => p.EventoId == eventoId, ct);
        if (pesquisa is null)
        {
            pesquisa = new PesquisaSatisfacao(eventoId);
            db.PesquisasSatisfacao.Add(pesquisa);
        }
        acao(pesquisa);
        await db.SaveChangesAsync(ct);
    }
}
