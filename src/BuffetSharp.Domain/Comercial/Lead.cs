using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Domain.Comercial;

/// <summary>Por onde o interessado chegou ao buffet (base do relatório de conversão por canal).</summary>
public enum OrigemLead
{
    Instagram,
    Facebook,
    Google,
    Site,
    WhatsApp,
    Indicacao,
    Telefone,
    Feira,
    Visita,
    Outro
}

public enum StatusLead
{
    /// <summary>Contato recebido, ainda sem retorno do buffet.</summary>
    Novo,

    /// <summary>O buffet já conversou com o interessado e acompanha a negociação.</summary>
    EmContato,

    /// <summary>Virou cliente (cadastrado para receber o orçamento).</summary>
    Convertido,

    Perdido
}

/// <summary>
/// Interessado que ainda não é cliente: o primeiro contato (pelo Instagram, por indicação, numa feira...), os retornos
/// combinados e o histórico da conversa, até virar cliente com orçamento ou ser dado como perdido.
/// Ciclo: Novo → Em contato → Convertido / Perdido (um lead perdido pode ser reaberto).
/// </summary>
public class Lead : Entidade
{
    public const int TamanhoNome = 150;
    private readonly List<LeadInteracao> _interacoes = [];

    private Lead()
    {
    }

    public Lead(string nome, string? telefone, string? email, OrigemLead origem)
    {
        Alterar(nome, telefone, email, origem, null, null, null, null, null, null);
    }

    public string Nome { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }
    public OrigemLead Origem { get; private set; }

    /// <summary>Quem indicou, qual anúncio, qual feira...</summary>
    public string? DetalheOrigem { get; private set; }

    public int? TipoEventoId { get; private set; }
    public TipoEvento? TipoEvento { get; private set; }

    /// <summary>Data pretendida para o evento (pode ser aproximada).</summary>
    public DateOnly? DataPrevista { get; private set; }

    public int? Convidados { get; private set; }

    /// <summary>Usuário que acompanha a negociação.</summary>
    public int? ResponsavelId { get; private set; }
    public Usuario? Responsavel { get; private set; }

    public StatusLead Status { get; private set; } = StatusLead.Novo;

    /// <summary>Data combinada para o próximo retorno ao interessado (aparece nos lembretes).</summary>
    public DateOnly? ProximoContato { get; private set; }

    public string? Observacoes { get; private set; }
    public string? MotivoPerda { get; private set; }

    /// <summary>Cliente cadastrado na conversão.</summary>
    public int? ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }

    public DateTime? ConvertidoEm { get; private set; }

    public IReadOnlyCollection<LeadInteracao> Interacoes => _interacoes;

    public bool EmAberto => Status is StatusLead.Novo or StatusLead.EmContato;

    public void Alterar(string nome, string? telefone, string? email, OrigemLead origem, string? detalheOrigem, int? tipoEventoId,
        DateOnly? dataPrevista, int? convidados, int? responsavelId, string? observacoes)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(nome), "Informe o nome do interessado.");
        Validador.Garantir(nome.Trim().Length <= TamanhoNome, $"O nome pode ter no máximo {TamanhoNome} caracteres.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(telefone) || !string.IsNullOrWhiteSpace(email),
            "Informe um telefone ou e-mail para retornar o contato.");
        Validador.Garantir(string.IsNullOrWhiteSpace(email) || System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _), "E-mail inválido.");
        Validador.Garantir(convidados is null or > 0, "A quantidade de convidados deve ser maior que zero.");

        Nome = nome.Trim();
        Telefone = Limpar(telefone);
        Email = Limpar(email);
        Origem = origem;
        DetalheOrigem = Limpar(detalheOrigem);
        TipoEventoId = tipoEventoId is > 0 ? tipoEventoId : null;
        DataPrevista = dataPrevista;
        Convidados = convidados;
        ResponsavelId = responsavelId is > 0 ? responsavelId : null;
        Observacoes = Limpar(observacoes);
    }

    /// <summary>Registra uma conversa com o interessado e, opcionalmente, agenda o próximo retorno.</summary>
    public LeadInteracao RegistrarContato(string descricao, string? usuario, DateTime agora, DateOnly? proximoContato)
    {
        Validador.Garantir(EmAberto, "Este lead não está em negociação. Reabra-o para registrar contatos.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Descreva o contato feito.");
        Validador.Garantir(proximoContato is null || proximoContato >= DateOnly.FromDateTime(agora),
            "O próximo retorno não pode ser numa data passada.");

        var interacao = new LeadInteracao(agora, usuario, descricao.Trim());
        _interacoes.Add(interacao);
        Status = StatusLead.EmContato;
        ProximoContato = proximoContato;
        return interacao;
    }

    public void AgendarRetorno(DateOnly? data, DateOnly hoje)
    {
        Validador.Garantir(EmAberto, "Este lead não está em negociação.");
        Validador.Garantir(data is null || data >= hoje, "O próximo retorno não pode ser numa data passada.");
        ProximoContato = data;
    }

    /// <summary>O interessado virou cliente (cadastrado agora ou já existente).</summary>
    public void Converter(Cliente cliente, string? usuario, DateTime agora)
    {
        Validador.Garantir(EmAberto, "Somente leads em negociação podem ser convertidos.");
        Cliente = cliente;
        ClienteId = cliente.EhNovo ? null : cliente.Id;
        Status = StatusLead.Convertido;
        ConvertidoEm = agora;
        ProximoContato = null;
        _interacoes.Add(new LeadInteracao(agora, usuario, $"Convertido em cliente: {cliente.Nome}."));
    }

    public void MarcarPerdido(string motivo, string? usuario, DateTime agora)
    {
        Validador.Garantir(EmAberto, "Somente leads em negociação podem ser dados como perdidos.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo da perda (preço, data indisponível, desistência...).");
        Status = StatusLead.Perdido;
        MotivoPerda = motivo.Trim();
        ProximoContato = null;
        _interacoes.Add(new LeadInteracao(agora, usuario, $"Perdido: {MotivoPerda}"));
    }

    public void Reabrir(string? usuario, DateTime agora)
    {
        Validador.Garantir(Status == StatusLead.Perdido, "Somente leads perdidos podem ser reabertos.");
        Status = StatusLead.EmContato;
        MotivoPerda = null;
        _interacoes.Add(new LeadInteracao(agora, usuario, "Negociação reaberta."));
    }

    private static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>Uma conversa com o interessado (ligação, mensagem, visita).</summary>
public class LeadInteracao
{
    public const int TamanhoDescricao = 1000;

    private LeadInteracao()
    {
    }

    internal LeadInteracao(DateTime data, string? usuario, string descricao)
    {
        Data = data;
        Usuario = usuario;
        Descricao = descricao.Length > TamanhoDescricao ? descricao[..TamanhoDescricao] : descricao;
    }

    public int Id { get; private set; }
    public int LeadId { get; private set; }
    public DateTime Data { get; private set; }
    public string? Usuario { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
}
