using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

/// <summary>O que pede atenção hoje. Os lembretes são calculados a partir dos dados (não são cadastrados).</summary>
public enum TipoLembrete
{
    /// <summary>Proposta enviada perto de perder a validade.</summary>
    OrcamentoVencendo,

    /// <summary>Degustação agendada para hoje ou amanhã.</summary>
    Degustacao,

    /// <summary>Aniversário de cliente.</summary>
    Aniversario,

    ParcelaAVencer,
    ParcelaVencida,

    /// <summary>Retorno combinado com um lead.</summary>
    RetornoLead,

    ContaPagarAVencer,

    /// <summary>Material de evento já realizado que ainda não voltou.</summary>
    MaterialNaoDevolvido,

    /// <summary>Tarefa do checklist do evento com prazo vencido ou para hoje.</summary>
    TarefaEvento,

    /// <summary>A data que o primeiro da lista de espera queria ficou livre.</summary>
    EsperaData
}

public enum CanalLembrete
{
    WhatsApp,
    Email,

    /// <summary>Resolvido sem mensagem pelo sistema (ligação, conversa, já pago...).</summary>
    Manual,

    /// <summary>E-mail enviado sozinho pelo sistema.</summary>
    EmailAutomatico,

    /// <summary>WhatsApp enviado sozinho pelo sistema (Z-API).</summary>
    WhatsAppAutomatico
}

/// <summary>
/// Lembrete já tratado: não aparece mais na lista. A chave identifica a ocorrência (ex.: o aniversário de 2026, a parcela
/// que vence em tal dia), então uma nova ocorrência volta a aparecer.
/// </summary>
public class LembreteTratado : Entidade
{
    public const int TamanhoChave = 80;

    private LembreteTratado()
    {
    }

    public LembreteTratado(string chave, TipoLembrete tipo, CanalLembrete canal, string? usuario, DateTime agora)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(chave) && chave.Length <= TamanhoChave, "Lembrete inválido.");
        Chave = chave;
        Tipo = tipo;
        Canal = canal;
        Usuario = usuario;
        TratadoEm = agora;
    }

    public string Chave { get; private set; } = string.Empty;
    public TipoLembrete Tipo { get; private set; }
    public CanalLembrete Canal { get; private set; }
    public string? Usuario { get; private set; }
    public DateTime TratadoEm { get; private set; }
}
