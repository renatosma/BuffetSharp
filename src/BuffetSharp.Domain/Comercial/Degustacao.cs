using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

public enum StatusDegustacao
{
    Agendada,
    Realizada,
    NaoCompareceu,
    Cancelada
}

/// <summary>
/// Degustação do cardápio oferecida a um cliente em negociação. Ciclo: Agendada → Realizada (com
/// avaliação de 1 a 5) / Não compareceu / Cancelada. O limite de degustações simultâneas depende das
/// demais agendas e é verificado pelo serviço de aplicação.
/// </summary>
public class Degustacao : Entidade
{
    public const int MaximoPessoas = 10;

    private Degustacao()
    {
    }

    public Degustacao(int clienteId, DateTime dataHora, int pessoas, int? pacoteId, int? orcamentoId, string? observacoes,
        DateTime agora)
    {
        Alterar(clienteId, dataHora, pessoas, pacoteId, orcamentoId, observacoes, agora);
    }

    public int ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }

    /// <summary>Pacote cujo cardápio será degustado.</summary>
    public int? PacoteId { get; private set; }
    public Pacote? Pacote { get; private set; }

    /// <summary>Orçamento em negociação que motivou a degustação.</summary>
    public int? OrcamentoId { get; private set; }
    public Orcamento? Orcamento { get; private set; }

    public DateTime DataHora { get; private set; }
    public int Pessoas { get; private set; }
    public StatusDegustacao Status { get; private set; } = StatusDegustacao.Agendada;
    public string? Observacoes { get; private set; }

    /// <summary>Nota de 1 a 5 dada pelo cliente.</summary>
    public int? Avaliacao { get; private set; }
    public string? Comentario { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public void Alterar(int clienteId, DateTime dataHora, int pessoas, int? pacoteId, int? orcamentoId, string? observacoes,
        DateTime agora)
    {
        GarantirAgendada();
        Validador.Garantir(clienteId > 0, "Selecione o cliente.");
        Validador.Garantir(pessoas is >= 1 and <= MaximoPessoas, $"A degustação comporta de 1 a {MaximoPessoas} pessoas.");
        Validador.Garantir(dataHora == DataHora || dataHora > agora, "Agende a degustação para uma data e hora futuras.");
        Validador.Garantir(dataHora.Hour is >= 9 and <= 20, "As degustações acontecem entre 9h e 20h.");

        ClienteId = clienteId;
        DataHora = dataHora;
        Pessoas = pessoas;
        PacoteId = pacoteId;
        OrcamentoId = orcamentoId;
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
    }

    public void Realizar(int avaliacao, string? comentario, DateTime agora)
    {
        GarantirAgendada();
        Validador.Garantir(agora >= DataHora, "A degustação ainda não aconteceu.");
        Validador.Garantir(avaliacao is >= 1 and <= 5, "A avaliação deve ser de 1 a 5.");
        Status = StatusDegustacao.Realizada;
        Avaliacao = avaliacao;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
    }

    public void RegistrarAusencia(DateTime agora)
    {
        GarantirAgendada();
        Validador.Garantir(agora >= DataHora, "Só é possível registrar a ausência após o horário agendado.");
        Status = StatusDegustacao.NaoCompareceu;
    }

    public void Cancelar(string motivo)
    {
        GarantirAgendada();
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        Status = StatusDegustacao.Cancelada;
        MotivoCancelamento = motivo.Trim();
    }

    private void GarantirAgendada() =>
        Validador.Garantir(Status == StatusDegustacao.Agendada, "Somente degustações agendadas podem ser alteradas.");
}
