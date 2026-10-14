using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Domain.Eventos;

public enum StatusEvento
{
    Confirmado,
    Realizado,
    Cancelado
}

/// <summary>
/// Nível 3 - Agregado principal do sistema: o evento contratado, com equipe escalada,
/// lista de convidados (RSVP e check-in), plano de pagamento e baixa de estoque.
/// </summary>
public class Evento : Entidade
{
    private readonly List<EventoServico> _servicos = [];
    private readonly List<EventoFuncionario> _equipe = [];
    private readonly List<Convidado> _convidados = [];
    private readonly List<Parcela> _parcelas = [];

    private Evento()
    {
    }

    /// <summary>Evento fechado diretamente, sem orçamento prévio.</summary>
    public Evento(string titulo, int clienteId, int tipoEventoId, int espacoId, DateOnly data,
        TimeOnly horaInicio, TimeOnly horaFim, int convidados, decimal valorTotal)
    {
        Validador.Garantir(clienteId > 0, "Selecione o cliente.");
        Validador.Garantir(tipoEventoId > 0, "Selecione o tipo de evento.");
        Validador.Garantir(valorTotal > 0, "Informe o valor total do evento.");
        ClienteId = clienteId;
        TipoEventoId = tipoEventoId;
        ValorTotal = valorTotal;
        AlterarDados(titulo, espacoId, data, horaInicio, horaFim, convidados, null);
    }

    public string Codigo => EhNovo ? "(novo)" : $"EVT-{Id:D5}";

    public string Titulo { get; private set; } = string.Empty;

    public int ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }

    public int TipoEventoId { get; private set; }
    public TipoEvento? TipoEvento { get; private set; }

    public int EspacoId { get; private set; }
    public Espaco? Espaco { get; private set; }

    public int? PacoteId { get; private set; }
    public Pacote? Pacote { get; private set; }

    public int? OrcamentoId { get; private set; }
    public Orcamento? Orcamento { get; private set; }

    public DateTime Inicio { get; private set; }
    public DateTime Fim { get; private set; }
    public int QuantidadeConvidados { get; private set; }

    public decimal ValorPorPessoa { get; private set; }
    public decimal ValorTotal { get; private set; }

    public StatusEvento Status { get; private set; } = StatusEvento.Confirmado;
    public string? Observacoes { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public DateTime? RealizadoEm { get; private set; }
    public bool EstoqueBaixado { get; private set; }

    public IReadOnlyCollection<EventoServico> Servicos => _servicos;
    public IReadOnlyCollection<EventoFuncionario> Equipe => _equipe;
    public IReadOnlyCollection<Convidado> Convidados => _convidados;
    public IReadOnlyCollection<Parcela> Parcelas => _parcelas;

    public DateOnly DataEvento => DateOnly.FromDateTime(Inicio);
    public TimeOnly HoraInicio => TimeOnly.FromDateTime(Inicio);
    public TimeOnly HoraFim => TimeOnly.FromDateTime(Fim);

    // ---------------- Indicadores ----------------

    public decimal TotalEquipe => _equipe.Sum(e => e.ValorDiaria);

    /// <summary>Valor efetivamente recebido (inclui juros/multa pagos).</summary>
    public decimal TotalRecebido => _parcelas.Where(p => p.Status == StatusTitulo.Pago).Sum(p => p.ValorPago ?? p.Valor);

    /// <summary>Soma das parcelas em aberto.</summary>
    public decimal SaldoAReceber => _parcelas.Where(p => p.Status == StatusTitulo.Aberto).Sum(p => p.Valor);

    /// <summary>Parte do valor do contrato ainda sem parcela gerada.</summary>
    public decimal ValorNaoParcelado =>
        ValorTotal - _parcelas.Where(p => p.Status != StatusTitulo.Cancelado).Sum(p => p.Valor);

    public int TotalPessoasNaLista => _convidados.Sum(c => 1 + c.Acompanhantes);
    public int PessoasConfirmadas => _convidados.Where(c => c.Rsvp == StatusRsvp.Confirmado).Sum(c => 1 + c.Acompanhantes);
    public int PessoasPresentes => _convidados.Where(c => c.CheckInEm is not null).Sum(c => 1 + c.Acompanhantes);

    // ---------------- Criação e dados ----------------

    internal static Evento CriarDeOrcamento(Orcamento orcamento, string titulo)
    {
        var evento = new Evento
        {
            Orcamento = orcamento,
            OrcamentoId = orcamento.EhNovo ? null : orcamento.Id,
            ClienteId = orcamento.ClienteId,
            TipoEventoId = orcamento.TipoEventoId,
            PacoteId = orcamento.PacoteId,
            ValorPorPessoa = orcamento.ValorPorPessoa,
            ValorTotal = orcamento.ValorTotal
        };
        evento.AlterarDados(titulo, orcamento.EspacoId ?? 0, orcamento.DataEvento, orcamento.HoraInicio,
            orcamento.HoraFim, orcamento.QuantidadeConvidados, orcamento.Observacoes);

        foreach (var s in orcamento.Servicos)
            evento._servicos.Add(new EventoServico(s.ServicoAdicionalId, s.Descricao, s.Quantidade, s.ValorUnitario));

        return evento;
    }

    public void AlterarDados(string titulo, int espacoId, DateOnly data, TimeOnly horaInicio, TimeOnly horaFim,
        int convidados, string? observacoes)
    {
        GarantirConfirmado();
        Validador.Garantir(!string.IsNullOrWhiteSpace(titulo), "Informe o título do evento.");
        Validador.Garantir(espacoId > 0, "Selecione o espaço do evento.");
        Validador.Garantir(convidados > 0, "Informe a quantidade de convidados.");

        Titulo = titulo.Trim();
        EspacoId = espacoId;
        (Inicio, Fim) = PeriodoEvento.Calcular(data, horaInicio, horaFim);
        QuantidadeConvidados = convidados;
        Observacoes = observacoes;
    }

    /// <summary>Aditivo de contrato: altera o valor total (as parcelas abertas devem ser regeradas).</summary>
    public void AjustarValorTotal(decimal novoValor)
    {
        GarantirConfirmado();
        var jaFaturado = _parcelas.Where(p => p.Status == StatusTitulo.Pago).Sum(p => p.Valor);
        Validador.Garantir(novoValor > 0, "O valor total deve ser maior que zero.");
        Validador.Garantir(novoValor >= jaFaturado,
            $"O valor total não pode ser menor que o já recebido ({jaFaturado:C}).");
        ValorTotal = novoValor;
    }

    // ---------------- Equipe ----------------

    public EventoFuncionario EscalarFuncionario(Funcionario funcionario, string? funcao = null, decimal? valorDiaria = null)
    {
        GarantirConfirmado();
        Validador.Garantir(funcionario.Ativo, $"O funcionário '{funcionario.Nome}' está inativo.");
        Validador.Garantir(_equipe.All(e => e.FuncionarioId != funcionario.Id),
            $"'{funcionario.Nome}' já está escalado neste evento.");
        Validador.Garantir(valorDiaria is null or >= 0, "O valor da diária não pode ser negativo.");

        var escala = new EventoFuncionario(funcionario.Id, funcao ?? funcionario.Cargo?.Nome ?? "Equipe",
            valorDiaria ?? funcionario.ValorDiaria);
        _equipe.Add(escala);
        return escala;
    }

    public void RemoverDaEquipe(int funcionarioId)
    {
        GarantirConfirmado();
        _equipe.RemoveAll(e => e.FuncionarioId == funcionarioId);
    }

    public void RegistrarPresenca(int funcionarioId, bool presente)
    {
        Validador.Garantir(Status != StatusEvento.Cancelado, "Evento cancelado.");
        ObterEscala(funcionarioId).Presente = presente;
    }

    // ---------------- Convidados ----------------

    public Convidado AdicionarConvidado(string nome, string? telefone = null, string? email = null,
        int acompanhantes = 0, string? mesa = null)
    {
        GarantirConfirmado();
        Validador.Garantir(!string.IsNullOrWhiteSpace(nome), "Informe o nome do convidado.");
        Validador.Garantir(acompanhantes is >= 0 and <= 20, "Quantidade de acompanhantes inválida.");
        Validador.Garantir(_convidados.All(c => !c.Nome.Equals(nome.Trim(), StringComparison.CurrentCultureIgnoreCase)),
            $"'{nome.Trim()}' já está na lista de convidados.");

        var convidado = new Convidado(nome.Trim(), telefone, email, acompanhantes, mesa);
        _convidados.Add(convidado);
        return convidado;
    }

    public void RemoverConvidado(int convidadoId)
    {
        GarantirConfirmado();
        _convidados.Remove(ObterConvidado(convidadoId));
    }

    public void DefinirRsvp(int convidadoId, StatusRsvp rsvp)
    {
        GarantirConfirmado();
        ObterConvidado(convidadoId).Rsvp = rsvp;
    }

    public void RegistrarCheckIn(int convidadoId, DateTime agora)
    {
        Validador.Garantir(Status == StatusEvento.Confirmado, "Check-in disponível apenas para eventos confirmados.");
        Validador.Garantir(agora >= Inicio.AddHours(-3) && agora <= Fim.AddHours(2),
            "O check-in só pode ser feito no dia/horário do evento.");
        var convidado = ObterConvidado(convidadoId);
        Validador.Garantir(convidado.CheckInEm is null, $"'{convidado.Nome}' já fez check-in.");
        convidado.CheckInEm = agora;
        convidado.Rsvp = StatusRsvp.Confirmado;
    }

    public void DesfazerCheckIn(int convidadoId) => ObterConvidado(convidadoId).CheckInEm = null;

    // ---------------- Financeiro ----------------

    /// <summary>
    /// (Re)gera o plano de pagamento sobre o saldo ainda não pago. Parcelas abertas são
    /// substituídas; parcelas pagas são preservadas.
    /// </summary>
    public IReadOnlyList<Parcela> GerarParcelas(int quantidade, DateOnly primeiroVencimento, decimal valorEntrada = 0,
        DateOnly? vencimentoEntrada = null, int? formaPagamentoId = null)
    {
        GarantirConfirmado();
        Validador.Garantir(quantidade is >= 1 and <= 48, "A quantidade de parcelas deve estar entre 1 e 48.");
        Validador.Garantir(valorEntrada >= 0, "O valor de entrada não pode ser negativo.");

        foreach (var aberta in _parcelas.Where(p => p.Status == StatusTitulo.Aberto).ToList())
            _parcelas.Remove(aberta);

        var saldo = ValorNaoParcelado;
        Validador.Garantir(saldo > 0, "Não há saldo a parcelar para este evento.");
        Validador.Garantir(valorEntrada < saldo || (valorEntrada == saldo && quantidade == 1),
            "A entrada deve ser menor que o saldo a parcelar.");

        var numero = _parcelas.Count == 0 ? 0 : _parcelas.Max(p => p.Numero);
        var geradas = new List<Parcela>();

        if (valorEntrada > 0)
        {
            geradas.Add(new Parcela(this, ++numero, valorEntrada, vencimentoEntrada ?? primeiroVencimento,
                formaPagamentoId, "Entrada"));
            saldo -= valorEntrada;
        }

        if (saldo > 0)
        {
            var valorParcela = Math.Round(saldo / quantidade, 2, MidpointRounding.ToZero);
            for (var i = 0; i < quantidade; i++)
            {
                var valor = i == quantidade - 1 ? saldo - valorParcela * (quantidade - 1) : valorParcela;
                geradas.Add(new Parcela(this, ++numero, valor, primeiroVencimento.AddMonths(i), formaPagamentoId,
                    $"Parcela {i + 1}/{quantidade}"));
            }
        }

        Validador.Garantir(geradas.Max(p => p.Vencimento) <= DataEvento.AddDays(60),
            "O plano de pagamento não pode terminar mais de 60 dias após o evento.");

        _parcelas.AddRange(geradas);
        return geradas;
    }

    // ---------------- Ciclo de vida ----------------

    /// <summary>
    /// Encerra o evento. A baixa de estoque (insumos × convidados) é feita pelo serviço de aplicação,
    /// que chama <see cref="MarcarEstoqueBaixado"/> em seguida.
    /// </summary>
    public void Realizar(DateTime agora)
    {
        GarantirConfirmado();
        Validador.Garantir(agora >= Inicio, "O evento ainda não começou.");
        Status = StatusEvento.Realizado;
        RealizadoEm = agora;
    }

    public void MarcarEstoqueBaixado()
    {
        Validador.Garantir(!EstoqueBaixado, "O estoque deste evento já foi baixado.");
        EstoqueBaixado = true;
    }

    public void Cancelar(string motivo)
    {
        GarantirConfirmado();
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        foreach (var parcela in _parcelas.Where(p => p.Status == StatusTitulo.Aberto))
            parcela.Cancelar();
        Status = StatusEvento.Cancelado;
        MotivoCancelamento = motivo.Trim();
    }

    private void GarantirConfirmado() =>
        Validador.Garantir(Status == StatusEvento.Confirmado,
            $"Evento {Status.ToString().ToLowerInvariant()} não pode ser alterado.");

    private Convidado ObterConvidado(int id) =>
        _convidados.FirstOrDefault(c => c.Id == id) ?? throw new RegraNegocioException("Convidado não encontrado.");

    private EventoFuncionario ObterEscala(int funcionarioId) =>
        _equipe.FirstOrDefault(e => e.FuncionarioId == funcionarioId)
        ?? throw new RegraNegocioException("Funcionário não está escalado neste evento.");
}
