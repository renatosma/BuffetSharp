using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Domain.Eventos;

public enum SituacaoFornecedorEvento
{
    Contratado,
    Confirmado,
    Cancelado
}

/// <summary>
/// Terceiro contratado para a festa (DJ, decoração, fotografia, bolo...). O valor combinado vira contas a pagar
/// (<see cref="Pagamento"/>), a chegada vira uma etapa do cronograma e, depois do evento, a contratação recebe uma nota.
/// </summary>
public class EventoFornecedor
{
    public const int TamanhoServico = 100;
    public const int TamanhoResponsavel = 60;
    public const int TamanhoTelefone = 20;
    public const int TamanhoObservacao = 300;
    public const int TamanhoComentario = 300;

    private EventoFornecedor()
    {
    }

    internal EventoFornecedor(Fornecedor fornecedor, decimal valor)
    {
        FornecedorId = fornecedor.Id;
        Fornecedor = fornecedor;
        Valor = valor;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public int FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }

    /// <summary>Serviço vendido ao cliente que este fornecedor executa (base da margem); nulo quando é só custo do buffet.</summary>
    public int? EventoServicoId { get; private set; }

    /// <summary>O que o fornecedor faz na festa (ex.: "DJ e iluminação").</summary>
    public string Servico { get; private set; } = string.Empty;

    public decimal Valor { get; internal set; }
    public SituacaoFornecedorEvento Situacao { get; internal set; } = SituacaoFornecedorEvento.Contratado;

    /// <summary>Quem do fornecedor responde no dia e o telefone dele.</summary>
    public string? Responsavel { get; private set; }

    public string? Telefone { get; private set; }
    public string? Observacao { get; private set; }
    public DateTime? ConfirmadoEm { get; internal set; }

    /// <summary>Contas a pagar do valor combinado.</summary>
    public ParcelamentoPagar? Pagamento { get; private set; }

    /// <summary>Etapa do cronograma com a chegada do fornecedor: o horário fica nela e acompanha as mudanças do evento.</summary>
    public EventoEtapa? EtapaChegada { get; internal set; }

    public DateTime? Chegada => EtapaChegada?.Horario;

    /// <summary>Avaliação depois do evento (1 a 5).</summary>
    public int? Nota { get; internal set; }

    public string? Comentario { get; internal set; }
    public DateTime? AvaliadoEm { get; internal set; }

    public bool Ativo => Situacao != SituacaoFornecedorEvento.Cancelado;
    public string NomeFornecedor => Fornecedor?.NomeExibicao ?? string.Empty;

    /// <summary>Quanto já foi pago ao fornecedor.</summary>
    public decimal ValorPago => Pagamento?.Parcelas.Where(p => p.Status == StatusTitulo.Pago).Sum(p => p.ValorPago ?? p.Valor) ?? 0;

    internal void Alterar(string servico, int? eventoServicoId, string? responsavel, string? telefone, string? observacao)
    {
        Servico = servico;
        EventoServicoId = eventoServicoId;
        Responsavel = responsavel;
        Telefone = telefone;
        Observacao = observacao;
    }

    internal void DefinirPagamento(ParcelamentoPagar pagamento) => Pagamento = pagamento;
}
