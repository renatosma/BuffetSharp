using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

public enum SituacaoEspera
{
    /// <summary>Na fila, esperando a data liberar.</summary>
    Aguardando,

    /// <summary>A data liberou e o interessado foi avisado; tem alguns dias para responder antes de a vez passar.</summary>
    Avisado,

    /// <summary>Virou orçamento.</summary>
    Convertida,

    /// <summary>Não quer mais a data (ou não respondeu ao aviso).</summary>
    Desistiu
}

/// <summary>
/// Lista de espera de uma data ocupada: o cliente (ou lead do funil) que quer a data, num espaço ou em qualquer um. Os
/// interessados na mesma data e espaço formam uma fila pela ordem de entrada; quando a data libera, o primeiro é avisado.
/// </summary>
public class EsperaData : Entidade
{
    public const int TamanhoObservacao = 300;

    private EsperaData()
    {
    }

    /// <param name="clienteId">Cliente interessado; ou <paramref name="leadId"/>, quando ainda não é cliente.</param>
    public EsperaData(int? clienteId, int? leadId, DateOnly data, TimeOnly horaInicio, TimeOnly horaFim, int? espacoId, int? tipoEventoId,
        int convidados, string? observacao, DateTime agora)
    {
        Validador.Garantir(clienteId is > 0 ^ leadId is > 0, "Informe o cliente ou o lead interessado na data.");
        ClienteId = clienteId;
        LeadId = leadId;
        Alterar(data, horaInicio, horaFim, espacoId, tipoEventoId, convidados, observacao, agora);
        EntrouEm = agora;
    }

    public int? ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public int? LeadId { get; private set; }
    public Lead? Lead { get; private set; }

    public DateTime Inicio { get; private set; }
    public DateTime Fim { get; private set; }

    /// <summary>Espaço desejado; nulo = qualquer espaço que comporte os convidados.</summary>
    public int? EspacoId { get; private set; }

    public Espaco? Espaco { get; private set; }
    public int? TipoEventoId { get; private set; }
    public TipoEvento? TipoEvento { get; private set; }
    public int Convidados { get; private set; }
    public string? Observacao { get; private set; }

    public SituacaoEspera Situacao { get; private set; } = SituacaoEspera.Aguardando;

    /// <summary>Entrada na fila (define a ordem).</summary>
    public DateTime EntrouEm { get; private set; }

    public DateTime? AvisadoEm { get; private set; }
    public DateTime? EncerradaEm { get; private set; }

    /// <summary>Orçamento feito para o interessado (quando convertida).</summary>
    public int? OrcamentoId { get; private set; }

    public DateOnly Data => DateOnly.FromDateTime(Inicio);
    public bool Ativa => Situacao is SituacaoEspera.Aguardando or SituacaoEspera.Avisado;
    public string Interessado => Cliente?.Nome ?? Lead?.Nome ?? string.Empty;

    /// <summary>O aviso foi feito há mais de <paramref name="diasResposta"/> dias sem resposta: a vez passa ao próximo da fila.</summary>
    public bool SemResposta(DateTime agora, int diasResposta) => Situacao == SituacaoEspera.Avisado && AvisadoEm < agora.AddDays(-diasResposta);

    public void Alterar(DateOnly data, TimeOnly horaInicio, TimeOnly horaFim, int? espacoId, int? tipoEventoId, int convidados, string? observacao,
        DateTime agora)
    {
        Validador.Garantir(EhNovo || Ativa, "Somente esperas ativas podem ser alteradas.");
        Validador.Garantir(data > DateOnly.FromDateTime(agora), "A data desejada precisa ser futura.");
        Validador.Garantir(convidados is >= 1 and <= 10_000, "Informe os convidados (de 1 a 10.000).");
        Validador.Garantir((observacao?.Trim().Length ?? 0) <= TamanhoObservacao, $"A observação pode ter no máximo {TamanhoObservacao} caracteres.");
        (Inicio, Fim) = PeriodoEvento.Calcular(data, horaInicio, horaFim);
        EspacoId = espacoId is > 0 ? espacoId : null;
        TipoEventoId = tipoEventoId is > 0 ? tipoEventoId : null;
        Convidados = convidados;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    /// <summary>O lead virou cliente (para fazer o orçamento).</summary>
    public void DefinirCliente(int clienteId)
    {
        Validador.Garantir(clienteId > 0, "Cliente inválido.");
        ClienteId = clienteId;
    }

    /// <summary>O interessado foi avisado de que a data liberou (começa o prazo para responder).</summary>
    public void RegistrarAviso(DateTime agora)
    {
        Validador.Garantir(Ativa, "Esta espera já foi encerrada.");
        Situacao = SituacaoEspera.Avisado;
        AvisadoEm = agora;
    }

    /// <summary>Volta para a fila (ex.: a data foi ocupada de novo antes de o interessado responder).</summary>
    public void VoltarParaFila()
    {
        Validador.Garantir(Situacao == SituacaoEspera.Avisado, "Somente quem foi avisado volta para a fila.");
        Situacao = SituacaoEspera.Aguardando;
        AvisadoEm = null;
    }

    public void Converter(int orcamentoId, DateTime agora)
    {
        Validador.Garantir(Ativa, "Esta espera já foi encerrada.");
        Situacao = SituacaoEspera.Convertida;
        OrcamentoId = orcamentoId;
        EncerradaEm = agora;
    }

    public void Desistir(DateTime agora)
    {
        Validador.Garantir(Ativa, "Esta espera já foi encerrada.");
        Situacao = SituacaoEspera.Desistiu;
        EncerradaEm = agora;
    }

    /// <summary>
    /// A fila de uma data e espaço: as esperas ativas pela ordem de entrada, sem quem foi avisado e deixou o prazo passar. A vez
    /// é do primeiro; enquanto um avisado está no prazo, ninguém mais é chamado.
    /// </summary>
    public static IReadOnlyList<EsperaData> Fila(IEnumerable<EsperaData> esperas, DateTime agora, int diasResposta) =>
        esperas.Where(e => e.Ativa && !e.SemResposta(agora, diasResposta)).OrderBy(e => e.EntrouEm).ThenBy(e => e.Id).ToList();
}
