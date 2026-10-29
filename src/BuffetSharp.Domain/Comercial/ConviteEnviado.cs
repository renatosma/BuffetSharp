using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

public enum CanalConvite
{
    Email,
    WhatsApp,

    /// <summary>Arquivo .ics salvo e entregue por fora.</summary>
    Arquivo
}

/// <summary>
/// Convite de agenda (.ics) enviado: guarda o horário e a situação mandados para cada compromisso (o mesmo identificador UID
/// a cada envio), para saber quando a agenda de quem recebeu ficou desatualizada e mandar a atualização com a sequência seguinte.
/// </summary>
public class ConviteEnviado : Entidade
{
    public const int TamanhoUid = 120;
    public const int TamanhoDestinatario = 150;

    private ConviteEnviado()
    {
    }

    public ConviteEnviado(string uid, int sequencia, DateTime inicio, DateTime fim, bool cancelado, string destinatario, string? contato,
        CanalConvite canal, string? usuario, DateTime agora, int? eventoId = null, int? degustacaoId = null)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(uid) && uid.Length <= TamanhoUid, "Convite inválido.");
        Validador.Garantir(eventoId is not null || degustacaoId is not null, "O convite é de um evento ou de uma degustação.");
        Uid = uid;
        Sequencia = sequencia;
        Inicio = inicio;
        Fim = fim;
        Cancelado = cancelado;
        Destinatario = destinatario.Length > TamanhoDestinatario ? destinatario[..TamanhoDestinatario] : destinatario;
        Contato = contato is { Length: > TamanhoDestinatario } ? contato[..TamanhoDestinatario] : contato;
        Canal = canal;
        Usuario = usuario;
        EnviadoEm = agora;
        EventoId = eventoId;
        DegustacaoId = degustacaoId;
    }

    public string Uid { get; private set; } = string.Empty;
    public int Sequencia { get; private set; }
    public DateTime Inicio { get; private set; }
    public DateTime Fim { get; private set; }
    public bool Cancelado { get; private set; }
    public string Destinatario { get; private set; } = string.Empty;
    public string? Contato { get; private set; }
    public CanalConvite Canal { get; private set; }
    public string? Usuario { get; private set; }
    public DateTime EnviadoEm { get; private set; }
    public int? EventoId { get; private set; }
    public int? DegustacaoId { get; private set; }

    /// <summary>O compromisso mudou desde este envio (outro horário, ou cancelado/restabelecido).</summary>
    public bool Diferente(DateTime inicio, DateTime fim, bool cancelado) => Inicio != inicio || Fim != fim || Cancelado != cancelado;
}
