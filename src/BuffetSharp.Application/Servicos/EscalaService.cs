using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Funcionário diante do evento: livre, ocupado em outro evento, sem descanso suficiente ou indisponível.</summary>
public sealed record SituacaoFuncionario(int FuncionarioId, string Nome, string? Cargo, decimal Diaria, IReadOnlyList<ConflitoEscala> Conflitos)
{
    public bool Livre => Conflitos.Count == 0;

    public string Resumo => Conflitos.Count == 0 ? "livre" : Conflitos[0].Tipo switch
    {
        TipoConflitoEscala.Indisponivel => $"indisponível: {Conflitos[0].Descricao}",
        TipoConflitoEscala.Sobreposicao => $"ocupado: {Conflitos[0].Descricao.Replace("Já escalado em ", "")}",
        _ => "pouco descanso: " + Conflitos[0].Descricao
    };

    public override string ToString() => $"{Nome}  •  {Resumo}";
}

/// <summary>Linha da agenda da equipe: uma escala num evento ou um período de indisponibilidade.</summary>
public sealed record ItemAgendaEquipe(int FuncionarioId, string Funcionario, DateTime Inicio, DateTime Fim, int? EventoId, string Descricao,
    string? Funcao, string? Local, string? Conflito, string? Excecao)
{
    public bool Indisponibilidade => EventoId is null;
}

/// <summary>
/// Conflitos da escala da equipe: o mesmo funcionário em eventos que se sobrepõem (contando o deslocamento, que é o intervalo
/// mínimo entre eventos da configuração), sem o descanso mínimo entre um trabalho e outro, ou indisponível (folga, férias,
/// atestado). Também monta a disponibilidade para escolher quem escalar e a agenda da equipe.
/// </summary>
public sealed class EscalaService(IBuffetDbContextFactory fabrica, BuffetOptions opcoes)
{
    /// <summary>Margem da busca: o cronograma vai de 12 horas antes a 18 horas depois do início, e o descanso pode passar de um dia.</summary>
    private static readonly TimeSpan Margem = TimeSpan.FromDays(3);

    /// <summary>Conflitos de escalar o funcionário no evento (que já deve estar carregado com o cronograma).</summary>
    public async Task<IReadOnlyList<ConflitoEscala>> ConflitosAsync(IBuffetDbContext db, Evento evento, int funcionarioId, CancellationToken ct)
    {
        var periodo = PeriodoTrabalho.Do(evento);
        var (escalas, indisponibilidades) = await CarregarAsync(db, periodo.Inicio - Margem, periodo.Fim + Margem, [funcionarioId], ct);
        return Verificar(periodo, funcionarioId, escalas, indisponibilidades);
    }

    /// <summary>Situação de cada funcionário ativo diante do evento, para a escolha de quem escalar.</summary>
    public async Task<IReadOnlyList<SituacaoFuncionario>> DisponibilidadeAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var periodo = await PeriodoAsync(db, eventoId, ct);
        var funcionarios = await db.Funcionarios.AsNoTracking().Include(f => f.Cargo).Where(f => f.Ativo).OrderBy(f => f.Nome).ToListAsync(ct);
        var (escalas, indisponibilidades) = await CarregarAsync(db, periodo.Inicio - Margem, periodo.Fim + Margem, null, ct);
        return funcionarios.Select(f => new SituacaoFuncionario(f.Id, f.Nome, f.Cargo?.Nome, f.ValorDiaria,
            Verificar(periodo, f.Id, escalas, indisponibilidades))).ToList();
    }

    /// <summary>Conflitos de quem já está escalado no evento (aparecem quando o horário do evento ou da outra escala mudou).</summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<ConflitoEscala>>> ConflitosDaEquipeAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var periodo = await PeriodoAsync(db, eventoId, ct);
        var equipe = await db.Eventos.Where(e => e.Id == eventoId).SelectMany(e => e.Equipe).Select(q => q.FuncionarioId).ToListAsync(ct);
        if (equipe.Count == 0)
            return new Dictionary<int, IReadOnlyList<ConflitoEscala>>();
        var (escalas, indisponibilidades) = await CarregarAsync(db, periodo.Inicio - Margem, periodo.Fim + Margem, equipe, ct);
        return equipe.Distinct().ToDictionary(id => id, id => Verificar(periodo, id, escalas, indisponibilidades))
            .Where(c => c.Value.Count > 0).ToDictionary(c => c.Key, c => c.Value);
    }

    /// <summary>Escalas e indisponibilidades da equipe no período (dias inteiros), com os conflitos de cada escala.</summary>
    public async Task<IReadOnlyList<ItemAgendaEquipe>> AgendaAsync(DateOnly de, DateOnly ate, int? funcionarioId = null, CancellationToken ct = default)
    {
        Validador.Garantir(de <= ate, "O início do período deve ser anterior ao fim.");
        Validador.Garantir(ate.DayNumber - de.DayNumber <= 366, "Consulte no máximo um ano por vez.");
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        await using var db = fabrica.Criar();
        var filtro = funcionarioId is { } id ? new[] { id } : null;
        var (escalas, indisponibilidades) = await CarregarAsync(db, inicio - Margem, fim + Margem, filtro, ct);
        var nomes = await db.Funcionarios.AsNoTracking().Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var itens = new List<ItemAgendaEquipe>();
        foreach (var (idFuncionario, lista) in escalas)
        {
            foreach (var escala in lista.Where(e => e.Periodo.Inicio < fim && e.Periodo.Fim >= inicio))
            {
                var conflitos = Verificar(escala.Periodo, idFuncionario, escalas, indisponibilidades);
                itens.Add(new ItemAgendaEquipe(idFuncionario, nomes.GetValueOrDefault(idFuncionario, ""), escala.Periodo.Inicio, escala.Periodo.Fim,
                    escala.Periodo.EventoId, escala.Periodo.Descricao, escala.Funcao, escala.Local,
                    conflitos.Count == 0 ? null : string.Join("; ", conflitos.Select(c => c.Descricao)), escala.Excecao));
            }
        }
        foreach (var (idFuncionario, lista) in indisponibilidades)
        {
            foreach (var indisponivel in lista.Where(i => i.Cobre(inicio, fim.AddTicks(-1))))
                itens.Add(new ItemAgendaEquipe(idFuncionario, nomes.GetValueOrDefault(idFuncionario, ""), indisponivel.Inicio.ToDateTime(TimeOnly.MinValue),
                    indisponivel.Fim.ToDateTime(new TimeOnly(23, 59)), null, indisponivel.Descricao, null, null, null, indisponivel.Observacao));
        }
        return itens.OrderBy(i => i.Funcionario).ThenBy(i => i.Inicio).ToList();
    }

    // ------------------------- Infra -------------------------

    private sealed record Escala(PeriodoTrabalho Periodo, string Funcao, string? Local, string? Excecao);

    private IReadOnlyList<ConflitoEscala> Verificar(PeriodoTrabalho periodo, int funcionarioId,
        IReadOnlyDictionary<int, List<Escala>> escalas, IReadOnlyDictionary<int, List<IndisponibilidadeFuncionario>> indisponibilidades) =>
        RegrasEscala.Verificar(periodo, escalas.GetValueOrDefault(funcionarioId)?.Select(e => e.Periodo) ?? [],
            indisponibilidades.GetValueOrDefault(funcionarioId) ?? [], opcoes.IntervaloMinimoEntreEventosMinutos, opcoes.DescansoMinimoEquipeHoras);

    private static async Task<PeriodoTrabalho> PeriodoAsync(IBuffetDbContext db, int eventoId, CancellationToken ct)
    {
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Etapas).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        return PeriodoTrabalho.Do(evento);
    }

    /// <summary>Escalas (por funcionário) dos eventos não cancelados que começam na janela e as indisponibilidades que a tocam.</summary>
    private static async Task<(Dictionary<int, List<Escala>> Escalas, Dictionary<int, List<IndisponibilidadeFuncionario>> Indisponibilidades)>
        CarregarAsync(IBuffetDbContext db, DateTime de, DateTime ate, IReadOnlyCollection<int>? funcionarios, CancellationToken ct)
    {
        var eventos = await db.Eventos.AsNoTracking()
            .Include(e => e.Equipe).Include(e => e.Etapas).Include(e => e.Espaco)
            .Where(e => e.Status != StatusEvento.Cancelado && e.Inicio >= de && e.Inicio <= ate && e.Equipe.Any())
            .ToListAsync(ct);
        var escalas = new Dictionary<int, List<Escala>>();
        foreach (var evento in eventos)
        {
            var periodo = PeriodoTrabalho.Do(evento);
            foreach (var escala in evento.Equipe.Where(q => funcionarios is null || funcionarios.Contains(q.FuncionarioId)))
            {
                if (!escalas.TryGetValue(escala.FuncionarioId, out var lista))
                    escalas[escala.FuncionarioId] = lista = [];
                lista.Add(new Escala(periodo, escala.Funcao, evento.Espaco?.Nome, escala.ExcecaoConflito));
            }
        }

        var deDia = DateOnly.FromDateTime(de);
        var ateDia = DateOnly.FromDateTime(ate);
        var consulta = db.IndisponibilidadesFuncionario.AsNoTracking().Where(i => i.Inicio <= ateDia && i.Fim >= deDia);
        if (funcionarios is not null)
            consulta = consulta.Where(i => funcionarios.Contains(i.FuncionarioId));
        var indisponibilidades = (await consulta.ToListAsync(ct)).GroupBy(i => i.FuncionarioId).ToDictionary(g => g.Key, g => g.ToList());
        return (escalas, indisponibilidades);
    }
}
