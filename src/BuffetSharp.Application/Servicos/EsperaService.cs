using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Espera na lista, com a posição na fila e se a data já está livre.</summary>
public sealed record EsperaResumo(int Id, string Interessado, bool Lead, string? Telefone, string? Email, DateTime Inicio, DateTime Fim,
    int? EspacoId, string Espaco, int Convidados, string? TipoEvento, SituacaoEspera Situacao, DateTime EntrouEm, DateTime? AvisadoEm,
    int? Posicao, string? EspacosLivres, string? Observacao, int? OrcamentoId, int? ClienteId, int? TipoEventoId)
{
    public bool DataLivre => EspacosLivres is not null;

    /// <summary>A data está livre e é a vez dele na fila.</summary>
    public bool SuaVez => Situacao == SituacaoEspera.Aguardando && Posicao == 1 && DataLivre;
}

/// <summary>Data livre sugerida no lugar da ocupada (mesmo dia da semana, a mais próxima).</summary>
public sealed record DataAlternativa(DateOnly Data, int EspacoId, string Espaco);

/// <summary>
/// Lista de espera de datas: quem quer uma data ocupada entra na fila (pela ordem de chegada, por data e espaço). Quando a
/// data libera (evento cancelado, mudado de data...), o primeiro da fila vira um lembrete para o comercial com o aviso pronto;
/// depois do aviso, ele tem alguns dias para responder antes de a vez passar ao próximo.
/// </summary>
public sealed class EsperaService(IBuffetDbContextFactory fabrica, DisponibilidadeService disponibilidade, LeadService leads,
    BuffetOptions opcoes, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    public async Task<IReadOnlyList<EsperaResumo>> ListarAsync(bool somenteAtivas = true, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var consulta = db.EsperasData.AsNoTracking().Include(e => e.Cliente).Include(e => e.Lead).Include(e => e.Espaco).Include(e => e.TipoEvento)
            .AsQueryable();
        if (somenteAtivas)
            consulta = consulta.Where(e => e.Situacao == SituacaoEspera.Aguardando || e.Situacao == SituacaoEspera.Avisado);
        var esperas = await consulta.OrderBy(e => e.Inicio).ThenBy(e => e.EntrouEm).ToListAsync(ct);
        var espacos = await db.Espacos.AsNoTracking().Where(e => e.Ativo).OrderBy(e => e.Nome).ToListAsync(ct);

        var resumos = new List<EsperaResumo>();
        foreach (var grupo in esperas.GroupBy(e => (e.Data, e.EspacoId)))
        {
            var fila = EsperaData.Fila(grupo, Agora, opcoes.DiasRespostaEspera).ToList();
            string? livres = null;
            if (grupo.Any(e => e.Ativa) && grupo.Key.Data > DateOnly.FromDateTime(Agora))
                livres = await EspacosLivresAsync(fila.FirstOrDefault() ?? grupo.First(), espacos, ct);
            foreach (var espera in grupo)
            {
                var posicao = fila.IndexOf(espera);
                resumos.Add(Resumo(espera, posicao < 0 ? null : posicao + 1, espera.Ativa ? livres : null));
            }
        }
        return resumos;
    }

    /// <summary>Lembretes "data liberada" para o primeiro da fila de cada data que ficou livre.</summary>
    public async Task<IReadOnlyList<Lembrete>> LembretesAsync(CancellationToken ct = default) =>
        (await ListarAsync(somenteAtivas: true, ct)).Where(e => e.SuaVez).Select(Lembrete).ToList();

    /// <summary>O aviso de data liberada como lembrete (a mensagem pronta e o registro do aviso usam o mesmo caminho).</summary>
    public static Lembrete Lembrete(EsperaResumo e) => new()
    {
            Tipo = TipoLembrete.EsperaData,
            ReferenciaId = e.Id,
            Data = e.Inicio,
            Titulo = $"Data liberada para {e.Interessado} (lista de espera)",
            Detalhe = $"{e.Inicio:dd/MM/yyyy} das {e.Inicio:HH:mm} às {e.Fim:HH:mm} • livre: {e.EspacosLivres}",
            Pessoa = e.Interessado,
            Telefone = e.Telefone,
            Email = e.Email,
            ClienteId = e.ClienteId,
            Chave = $"espera:{e.Id}"
        };

    /// <summary>Quantas esperas ativas há em cada dia do período (para a agenda).</summary>
    public async Task<IReadOnlyDictionary<DateOnly, int>> PorDiaAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var datas = await db.EsperasData.AsNoTracking()
            .Where(e => (e.Situacao == SituacaoEspera.Aguardando || e.Situacao == SituacaoEspera.Avisado) && e.Inicio >= inicio && e.Inicio < fim)
            .Select(e => e.Inicio).ToListAsync(ct);
        return datas.GroupBy(DateOnly.FromDateTime).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<int> SalvarAsync(EsperaInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        EsperaData espera;
        if (input.Id == 0)
        {
            espera = new EsperaData(input.ClienteId, input.LeadId, input.Data, input.HoraInicio, input.HoraFim, input.EspacoId, input.TipoEventoId,
                input.Convidados, input.Observacao, Agora);
            db.EsperasData.Add(espera);
        }
        else
        {
            espera = await ObterAsync(db, input.Id, ct);
            espera.Alterar(input.Data, input.HoraInicio, input.HoraFim, input.EspacoId, input.TipoEventoId, input.Convidados, input.Observacao, Agora);
        }
        if (input.EspacoId is { } espacoId)
        {
            var espaco = await db.Espacos.FindAsync([espacoId], ct) ?? throw new RegraNegocioException("Espaço não encontrado.");
            Validador.Garantir(input.Convidados <= espaco.CapacidadeMaxima, $"O espaço '{espaco.Nome}' comporta no máximo {espaco.CapacidadeMaxima} pessoas.");
        }
        await db.SaveChangesAsync(ct);
        return espera.Id;
    }

    public Task RegistrarAvisoAsync(int esperaId, CancellationToken ct = default) => AlterarAsync(esperaId, e => e.RegistrarAviso(Agora), ct);

    public Task VoltarParaFilaAsync(int esperaId, CancellationToken ct = default) => AlterarAsync(esperaId, e => e.VoltarParaFila(), ct);

    public Task DesistirAsync(int esperaId, CancellationToken ct = default) => AlterarAsync(esperaId, e => e.Desistir(Agora), ct);

    public Task ConverterAsync(int esperaId, int orcamentoId, CancellationToken ct = default) =>
        AlterarAsync(esperaId, e => e.Converter(orcamentoId, Agora), ct);

    /// <summary>Cliente para o orçamento da espera: o lead interessado vira cliente (cadastrado com os dados dele).</summary>
    public async Task<int> ClienteParaOrcamentoAsync(int esperaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var espera = await ObterAsync(db, esperaId, ct);
        if (espera.ClienteId is { } clienteId)
            return clienteId;
        var novo = await leads.ConverterAsync(new ConversaoLeadInput { LeadId = espera.LeadId!.Value }, ct);
        espera.DefinirCliente(novo);
        await db.SaveChangesAsync(ct);
        return novo;
    }

    /// <summary>
    /// Datas livres no lugar da ocupada: o mesmo dia da semana, nas semanas mais próximas (antes ou depois), no espaço pedido ou
    /// em qualquer um que comporte os convidados.
    /// </summary>
    public async Task<IReadOnlyList<DataAlternativa>> AlternativasAsync(DateOnly data, TimeOnly horaInicio, TimeOnly horaFim, int? espacoId,
        int convidados, int quantidade = 4, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var espacos = await db.Espacos.AsNoTracking()
            .Where(e => e.Ativo && e.CapacidadeMaxima >= convidados && (espacoId == null || e.Id == espacoId))
            .OrderBy(e => e.Nome).ToListAsync(ct);
        var hoje = DateOnly.FromDateTime(Agora);
        var alternativas = new List<DataAlternativa>();
        for (var semanas = 1; semanas <= 26 && alternativas.Count < quantidade; semanas++)
        {
            foreach (var candidata in new[] { data.AddDays(-7 * semanas), data.AddDays(7 * semanas) }.Where(d => d > hoje))
            {
                var (inicio, fim) = PeriodoEvento.Calcular(candidata, horaInicio, horaFim);
                foreach (var espaco in espacos)
                {
                    if (await disponibilidade.BuscarConflitoAsync(espaco.Id, inicio, fim, null, ct) is not null)
                        continue;
                    alternativas.Add(new DataAlternativa(candidata, espaco.Id, espaco.Nome));
                    break;
                }
                if (alternativas.Count >= quantidade)
                    break;
            }
        }
        return alternativas.OrderBy(a => Math.Abs(a.Data.DayNumber - data.DayNumber)).ThenBy(a => a.Data).ToList();
    }

    private async Task<string?> EspacosLivresAsync(EsperaData espera, IReadOnlyList<Domain.Cadastros.Espaco> espacos, CancellationToken ct)
    {
        var candidatos = espacos.Where(e => espera.EspacoId is null ? e.CapacidadeMaxima >= espera.Convidados : e.Id == espera.EspacoId);
        var livres = new List<string>();
        foreach (var espaco in candidatos)
        {
            if (await disponibilidade.BuscarConflitoAsync(espaco.Id, espera.Inicio, espera.Fim, null, ct) is null)
                livres.Add(espaco.Nome);
        }
        return livres.Count == 0 ? null : string.Join(", ", livres);
    }

    private EsperaResumo Resumo(EsperaData e, int? posicao, string? livres) => new(e.Id, e.Interessado, e.ClienteId is null,
        e.Cliente is { } c ? c.Celular ?? c.Telefone : e.Lead?.Telefone, e.Cliente?.Email ?? e.Lead?.Email, e.Inicio, e.Fim, e.EspacoId,
        e.Espaco?.Nome ?? "(qualquer espaço)", e.Convidados, e.TipoEvento?.Nome, e.Situacao, e.EntrouEm, e.AvisadoEm, posicao, livres, e.Observacao,
        e.OrcamentoId, e.ClienteId, e.TipoEventoId);

    private async Task AlterarAsync(int esperaId, Action<EsperaData> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        acao(await ObterAsync(db, esperaId, ct));
        await db.SaveChangesAsync(ct);
    }

    private static async Task<EsperaData> ObterAsync(IBuffetDbContext db, int id, CancellationToken ct) =>
        await db.EsperasData.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new RegraNegocioException("Espera não encontrada.");
}
