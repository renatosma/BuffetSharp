using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

public enum TipoConflitoEscala
{
    /// <summary>Os períodos de trabalho se sobrepõem (contando o deslocamento entre os eventos).</summary>
    Sobreposicao,

    /// <summary>Não se sobrepõem, mas o intervalo entre eles é menor que o descanso mínimo.</summary>
    Descanso,

    /// <summary>Folga, férias ou atestado no período.</summary>
    Indisponivel
}

/// <summary>
/// Período em que a equipe trabalha no evento: da primeira etapa do cronograma (montagem) à última (desmontagem) ou, sem
/// cronograma, do início ao fim da festa.
/// </summary>
public sealed record PeriodoTrabalho(int EventoId, string Titulo, DateTime Inicio, DateTime Fim)
{
    public string Codigo => $"EVT-{EventoId:D5}";

    public string Horario => Inicio.Date == Fim.Date ? $"{Inicio:dd/MM} das {Inicio:HH:mm} às {Fim:HH:mm}" : $"{Inicio:dd/MM HH:mm} às {Fim:dd/MM HH:mm}";

    public string Descricao => $"{Codigo} - {Titulo} ({Horario})";

    public static PeriodoTrabalho Do(Evento evento)
    {
        var inicio = evento.Etapas.Count == 0 ? evento.Inicio : new[] { evento.Inicio, evento.Etapas.Min(t => t.Horario) }.Min();
        var fim = evento.Etapas.Count == 0 ? evento.Fim : new[] { evento.Fim, evento.Etapas.Max(t => t.Fim) }.Max();
        return new PeriodoTrabalho(evento.Id, evento.Titulo, inicio, fim);
    }
}

public sealed record ConflitoEscala(TipoConflitoEscala Tipo, string Descricao, int? EventoId = null)
{
    /// <summary>Com exceção autorizada por um administrador, a escala pode ser feita; indisponibilidade não tem exceção.</summary>
    public bool AdmiteExcecao => Tipo != TipoConflitoEscala.Indisponivel;
}

/// <summary>A escala esbarra em outro evento ou no descanso: só um administrador pode fazê-la, informando o motivo.</summary>
public sealed class ConflitoEscalaException(string funcionario, IReadOnlyList<ConflitoEscala> conflitos)
    : RegraNegocioException($"{funcionario} não pode ser escalado: {string.Join("; ", conflitos.Select(c => c.Descricao))}.")
{
    public IReadOnlyList<ConflitoEscala> Conflitos { get; } = conflitos;
}

/// <summary>Regras de conflito da escala: outro evento no mesmo período (com deslocamento), descanso mínimo e indisponibilidade.</summary>
public static class RegrasEscala
{
    /// <param name="deslocamentoMinutos">Folga exigida entre dois eventos (ida de um local ao outro).</param>
    /// <param name="descansoHoras">Intervalo mínimo entre o fim de um trabalho e o início do próximo (0 = sem exigência).</param>
    public static IReadOnlyList<ConflitoEscala> Verificar(PeriodoTrabalho periodo, IEnumerable<PeriodoTrabalho> outrasEscalas,
        IEnumerable<IndisponibilidadeFuncionario> indisponibilidades, int deslocamentoMinutos, int descansoHoras)
    {
        var conflitos = new List<ConflitoEscala>();
        foreach (var indisponivel in indisponibilidades.Where(i => i.Cobre(periodo.Inicio, periodo.Fim)).OrderBy(i => i.Inicio))
            conflitos.Add(new ConflitoEscala(TipoConflitoEscala.Indisponivel, indisponivel.Descricao));

        foreach (var outro in outrasEscalas.Where(o => o.EventoId != periodo.EventoId).OrderBy(o => o.Inicio))
        {
            if (periodo.Inicio < outro.Fim.AddMinutes(deslocamentoMinutos) && outro.Inicio < periodo.Fim.AddMinutes(deslocamentoMinutos))
            {
                conflitos.Add(new ConflitoEscala(TipoConflitoEscala.Sobreposicao, $"Já escalado em {outro.Descricao}", outro.EventoId));
                continue;
            }

            var intervalo = periodo.Inicio >= outro.Fim ? periodo.Inicio - outro.Fim : outro.Inicio - periodo.Fim;
            if (descansoHoras > 0 && intervalo < TimeSpan.FromHours(descansoHoras))
                conflitos.Add(new ConflitoEscala(TipoConflitoEscala.Descanso,
                    $"Só {(int)intervalo.TotalHours}h{intervalo.Minutes:00} de descanso {(periodo.Inicio >= outro.Fim ? "depois de" : "antes de")} " +
                    $"{outro.Descricao} (mínimo de {descansoHoras}h)", outro.EventoId));
        }
        return conflitos;
    }
}
