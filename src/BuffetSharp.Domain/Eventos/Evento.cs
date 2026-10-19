using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;

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
    private readonly List<EventoMaterial> _materiais = [];

    private Evento()
    {
    }

    /// <summary>Evento fechado diretamente, sem orçamento prévio.</summary>
    public Evento(string titulo, int clienteId, int tipoEventoId, int espacoId, DateOnly data,
        TimeOnly horaInicio, TimeOnly horaFim, QuantidadesConvidados convidados, decimal valorTotal)
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

    /// <summary>Usuário que vendeu o evento (base da comissão).</summary>
    public int? VendedorId { get; private set; }
    public Usuario? Vendedor { get; private set; }

    /// <summary>Comissão (%) vigente na venda, aplicada sobre cada valor recebido.</summary>
    public decimal PercentualComissao { get; private set; }

    /// <summary>Condição de pagamento negociada (vem do orçamento; sugere o plano de pagamento e aparece no contrato).</summary>
    public int? CondicaoPagamentoId { get; private set; }
    public CondicaoPagamento? CondicaoPagamento { get; private set; }

    public DateTime Inicio { get; private set; }
    public DateTime Fim { get; private set; }

    /// <summary>Total de pessoas contratado (inteira + meia + isentos).</summary>
    public int QuantidadeConvidados { get; private set; }
    public int ConvidadosMeia { get; private set; }
    public int ConvidadosIsentos { get; private set; }

    public decimal ValorPorPessoa { get; private set; }
    public decimal ValorTotal { get; private set; }

    /// <summary>Regra de idades congelada do orçamento (na venda direta, a padrão).</summary>
    public int IdadeMaximaIsento { get; private set; } = RegraFaixasEtarias.Padrao.IdadeMaximaIsento;
    public int IdadeMaximaMeia { get; private set; } = RegraFaixasEtarias.Padrao.IdadeMaximaMeia;
    public decimal PercentualMeia { get; private set; } = RegraFaixasEtarias.Padrao.PercentualMeia;

    public StatusEvento Status { get; private set; } = StatusEvento.Confirmado;
    public string? Observacoes { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public DateTime? RealizadoEm { get; private set; }
    public bool EstoqueBaixado { get; private set; }

    public IReadOnlyCollection<EventoServico> Servicos => _servicos;
    public IReadOnlyCollection<EventoFuncionario> Equipe => _equipe;
    public IReadOnlyCollection<Convidado> Convidados => _convidados;
    public IReadOnlyCollection<Parcela> Parcelas => _parcelas;
    public IReadOnlyCollection<EventoMaterial> Materiais => _materiais;

    public QuantidadesConvidados QuantidadesPorFaixa => new(QuantidadeConvidados, ConvidadosMeia, ConvidadosIsentos);
    public RegraFaixasEtarias RegraFaixas => new(IdadeMaximaIsento, IdadeMaximaMeia, PercentualMeia);

    public DateOnly DataEvento => DateOnly.FromDateTime(Inicio);
    public TimeOnly HoraInicio => TimeOnly.FromDateTime(Inicio);
    public TimeOnly HoraFim => TimeOnly.FromDateTime(Fim);

    // ---------------- Indicadores ----------------

    public decimal TotalEquipe => _equipe.Sum(e => e.ValorDiaria);

    /// <summary>Locação de materiais de terceiros + perdas (quebras e extravios) registradas na devolução.</summary>
    public decimal CustoMateriais => _materiais.Sum(m => m.CustoLocacao + m.CustoPerdas);

    public int MateriaisPendentesDeDevolucao => _materiais.Count(m => !m.Devolvido);

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

    internal static Evento CriarDeOrcamento(Orcamento orcamento, string titulo, decimal percentualComissao)
    {
        var evento = new Evento
        {
            Orcamento = orcamento,
            OrcamentoId = orcamento.EhNovo ? null : orcamento.Id,
            ClienteId = orcamento.ClienteId,
            TipoEventoId = orcamento.TipoEventoId,
            PacoteId = orcamento.PacoteId,
            ValorPorPessoa = orcamento.ValorPorPessoa,
            ValorTotal = orcamento.ValorTotal,
            VendedorId = orcamento.VendedorId,
            PercentualComissao = orcamento.VendedorId is null ? 0 : percentualComissao,
            CondicaoPagamentoId = orcamento.CondicaoPagamentoId,
            IdadeMaximaIsento = orcamento.IdadeMaximaIsento,
            IdadeMaximaMeia = orcamento.IdadeMaximaMeia,
            PercentualMeia = orcamento.PercentualMeia
        };
        evento.AlterarDados(titulo, orcamento.EspacoId ?? 0, orcamento.DataEvento, orcamento.HoraInicio,
            orcamento.HoraFim, orcamento.QuantidadesPorFaixa, orcamento.Observacoes);

        foreach (var s in orcamento.Servicos)
            evento._servicos.Add(new EventoServico(s.ServicoAdicionalId, s.Descricao, s.Quantidade, s.ValorUnitario));

        return evento;
    }

    public void AlterarDados(string titulo, int espacoId, DateOnly data, TimeOnly horaInicio, TimeOnly horaFim,
        QuantidadesConvidados convidados, string? observacoes)
    {
        GarantirConfirmado();
        Validador.Garantir(!string.IsNullOrWhiteSpace(titulo), "Informe o título do evento.");
        Validador.Garantir(espacoId > 0, "Selecione o espaço do evento.");
        convidados.Validar();

        Titulo = titulo.Trim();
        EspacoId = espacoId;
        (Inicio, Fim) = PeriodoEvento.Calcular(data, horaInicio, horaFim);
        QuantidadeConvidados = convidados.Total;
        ConvidadosMeia = convidados.Meia;
        ConvidadosIsentos = convidados.Isentos;
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

    /// <param name="percentual">Comissão do vendedor no momento da venda (ignorada quando não há vendedor).</param>
    public void DefinirVendedor(int? vendedorId, decimal percentual)
    {
        GarantirConfirmado();
        Validador.Garantir(percentual is >= 0 and <= Usuario.ComissaoMaximaPercentual,
            $"A comissão deve estar entre 0% e {Usuario.ComissaoMaximaPercentual}%.");
        VendedorId = vendedorId;
        PercentualComissao = vendedorId is null ? 0 : percentual;
    }

    public void DefinirCondicaoPagamento(int? condicaoPagamentoId)
    {
        GarantirConfirmado();
        Validador.Garantir(condicaoPagamentoId is null or > 0, "Condição de pagamento inválida.");
        CondicaoPagamentoId = condicaoPagamentoId;
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

    // ---------------- Materiais ----------------

    /// <summary>
    /// Reserva (ou altera a reserva de) um material. A disponibilidade do patrimônio na data depende de
    /// outros eventos e é verificada pelo serviço de aplicação.
    /// </summary>
    /// <param name="alugada">Quantas unidades virão de locação de terceiros.</param>
    public EventoMaterial ReservarMaterial(Material material, int quantidade, int alugada = 0)
    {
        GarantirConfirmado();
        Validador.Garantir(quantidade > 0, "Informe a quantidade do material.");
        Validador.Garantir(alugada >= 0 && alugada <= quantidade,
            "A quantidade alugada deve estar entre zero e a quantidade total.");

        var existente = _materiais.FirstOrDefault(m => m.MaterialId == material.Id);
        if (existente is not null)
        {
            Validador.Garantir(!existente.Devolvido, $"A devolução de '{material.Nome}' já foi registrada.");
            existente.Alterar(material, quantidade, alugada);
            return existente;
        }

        Validador.Garantir(material.Ativo, $"O material '{material.Nome}' está inativo.");
        var reserva = new EventoMaterial(material, quantidade, alugada);
        _materiais.Add(reserva);
        return reserva;
    }

    public void RemoverMaterial(int materialId)
    {
        GarantirConfirmado();
        var item = ObterMaterial(materialId);
        Validador.Garantir(!item.Devolvido, "Materiais já devolvidos não podem ser removidos.");
        _materiais.Remove(item);
    }

    /// <summary>
    /// Registra o retorno do material. Devolvidas + perdidas deve fechar com o enviado. Retorna quantas das
    /// perdas saem do patrimônio próprio (considera-se que as unidades próprias se perdem antes das alugadas,
    /// cujas perdas são indenizadas ao locador).
    /// </summary>
    public int RegistrarDevolucao(int materialId, int devolvidas, int perdidas, DateTime agora)
    {
        Validador.Garantir(Status != StatusEvento.Cancelado, "Evento cancelado não tem materiais a devolver.");
        Validador.Garantir(agora >= Inicio, "A devolução só pode ser registrada depois do início do evento.");
        var item = ObterMaterial(materialId);
        Validador.Garantir(!item.Devolvido, "A devolução deste material já foi registrada.");
        Validador.Garantir(devolvidas >= 0 && perdidas >= 0, "As quantidades não podem ser negativas.");
        Validador.Garantir(devolvidas + perdidas == item.Quantidade,
            $"Devolvidas + perdidas deve somar {item.Quantidade} (quantidade enviada).");

        item.RegistrarDevolucao(devolvidas, perdidas, agora);
        return Math.Min(perdidas, item.QuantidadePropria);
    }

    // ---------------- Financeiro ----------------

    /// <summary>
    /// (Re)gera o plano de pagamento sobre o saldo ainda não pago. Parcelas abertas são
    /// substituídas; parcelas pagas são preservadas.
    /// </summary>
    /// <param name="intervaloDias">Dias entre as parcelas; nulo = mensais (mesmo dia de cada mês).</param>
    public IReadOnlyList<Parcela> GerarParcelas(int quantidade, DateOnly primeiroVencimento, decimal valorEntrada = 0,
        DateOnly? vencimentoEntrada = null, int? formaPagamentoId = null, int? intervaloDias = null)
    {
        GarantirConfirmado();
        var restantes = _parcelas.Where(p => p.Status != StatusTitulo.Aberto).ToList();
        var saldo = ValorTotal - restantes.Where(p => p.Status != StatusTitulo.Cancelado).Sum(p => p.Valor);
        Validador.Garantir(saldo > 0, "Não há saldo a parcelar para este evento.");
        var plano = PlanoParcelas.Calcular(saldo, quantidade, primeiroVencimento, valorEntrada, vencimentoEntrada, intervaloDias);

        _parcelas.RemoveAll(p => p.Status == StatusTitulo.Aberto);
        var numero = _parcelas.Count == 0 ? 0 : _parcelas.Max(p => p.Numero);
        var geradas = plano.Select(p => new Parcela(this, ++numero, p.Valor, p.Vencimento, formaPagamentoId,
            p.Entrada ? "Entrada" : $"Parcela {p.Numero}/{p.Quantidade}")).ToList();

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

    private EventoMaterial ObterMaterial(int materialId) =>
        _materiais.FirstOrDefault(m => m.MaterialId == materialId)
        ?? throw new RegraNegocioException("Material não reservado para este evento.");

    private EventoFuncionario ObterEscala(int funcionarioId) =>
        _equipe.FirstOrDefault(e => e.FuncionarioId == funcionarioId)
        ?? throw new RegraNegocioException("Funcionário não está escalado neste evento.");
}
