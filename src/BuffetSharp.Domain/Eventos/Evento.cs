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
    private readonly List<EventoTarefa> _tarefas = [];

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

    /// <summary>Meio de pagamento combinado (vem do orçamento); sugerido nas parcelas do plano de pagamento.</summary>
    public int? FormaPagamentoId { get; private set; }
    public FormaPagamento? FormaPagamento { get; private set; }

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

    /// <summary>Pessoas contadas no evento (check-in + portaria); nulo enquanto não houve a apuração dos excedentes.</summary>
    public int? PresentesApurados { get; private set; }
    public DateTime? ExcedentesApuradosEm { get; private set; }
    public int ExcedentesInteira { get; private set; }
    public int ExcedentesMeia { get; private set; }
    public int ExcedentesIsentos { get; private set; }

    /// <summary>Valor cobrado pelos excedentes (já somado ao <see cref="ValorTotal"/>).</summary>
    public decimal ValorExcedentes { get; private set; }

    public IReadOnlyCollection<EventoServico> Servicos => _servicos;
    public IReadOnlyCollection<EventoFuncionario> Equipe => _equipe;
    public IReadOnlyCollection<Convidado> Convidados => _convidados;
    public IReadOnlyCollection<Parcela> Parcelas => _parcelas;
    public IReadOnlyCollection<EventoMaterial> Materiais => _materiais;
    public IReadOnlyCollection<EventoTarefa> Tarefas => _tarefas;

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

    public bool ExcedentesApurados => ExcedentesApuradosEm is not null;
    public ExcedentesConvidados Excedentes => new(ExcedentesInteira, ExcedentesMeia, ExcedentesIsentos, ValorExcedentes);

    /// <summary>O contratado ou, se vieram mais pessoas, as contadas na apuração: base do consumo de insumos.</summary>
    public int PessoasAtendidas => Math.Max(QuantidadeConvidados, PresentesApurados ?? 0);

    /// <summary>Saldo que o plano de pagamento divide: fora o já pago e a cobrança dos excedentes, que tem parcela própria.</summary>
    public decimal SaldoAParcelar =>
        ValorTotal - _parcelas.Where(p => p.Status == StatusTitulo.Pago || (p.Excedentes && p.Status == StatusTitulo.Aberto)).Sum(p => p.Valor);

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
            FormaPagamentoId = orcamento.FormaPagamentoId,
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
        Validador.Garantir(!ExcedentesApurados || convidados == QuantidadesPorFaixa,
            "Os excedentes já foram apurados sobre os convidados contratados. Desfaça a apuração para alterá-los.");

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

    public void DefinirFormaPagamento(int? formaPagamentoId)
    {
        GarantirConfirmado();
        Validador.Garantir(formaPagamentoId is null or > 0, "Forma de pagamento inválida.");
        FormaPagamentoId = formaPagamentoId;
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

    // ---------------- Convidados excedentes ----------------

    public ExcedentesConvidados CalcularExcedentes(QuantidadesConvidados presentes, decimal valorPorPessoa) =>
        ExcedentesConvidados.Calcular(QuantidadesPorFaixa, presentes, RegraFaixas, valorPorPessoa);

    /// <summary>
    /// Registra quantas pessoas vieram (o check-in mais quem entrou pela portaria) e cobra as que passaram do contratado,
    /// como prevê o contrato: o valor entra no total do evento numa parcela própria, fora do plano de pagamento. Vale
    /// também depois de realizado, quando a contagem é fechada. Retorna a parcela (nula quando não há o que cobrar).
    /// </summary>
    public Parcela? ApurarExcedentes(QuantidadesConvidados presentes, decimal valorPorPessoa, DateOnly vencimento,
        int? formaPagamentoId, DateTime agora)
    {
        Validador.Garantir(Status != StatusEvento.Cancelado, "Evento cancelado.");
        Validador.Garantir(agora >= Inicio, "Os excedentes são apurados a partir do início do evento.");
        Validador.Garantir(!ExcedentesApurados, "Os excedentes deste evento já foram apurados. Desfaça a apuração para refazê-la.");
        presentes.Validar();
        Validador.Garantir(presentes.Total >= PessoasPresentes,
            $"O check-in registrou {PessoasPresentes} pessoas: a contagem não pode ser menor.");
        Validador.Garantir(valorPorPessoa >= 0, "O valor por pessoa não pode ser negativo.");

        var excedentes = CalcularExcedentes(presentes, valorPorPessoa);
        Parcela? parcela = null;
        if (excedentes.Valor > 0)
        {
            Validador.Garantir(vencimento <= DataEvento.AddDays(60), "A cobrança dos excedentes não pode vencer mais de 60 dias após o evento.");
            var numero = _parcelas.Count == 0 ? 1 : _parcelas.Max(p => p.Numero) + 1;
            parcela = new Parcela(this, numero, excedentes.Valor, vencimento, formaPagamentoId ?? FormaPagamentoId,
                $"Convidados excedentes ({excedentes.Pagantes})") { Excedentes = true };
            _parcelas.Add(parcela);
            ValorTotal += excedentes.Valor;
        }

        PresentesApurados = presentes.Total;
        ExcedentesApuradosEm = agora;
        (ExcedentesInteira, ExcedentesMeia, ExcedentesIsentos, ValorExcedentes) =
            (excedentes.Inteira, excedentes.Meia, excedentes.Isentos, excedentes.Valor);
        return parcela;
    }

    /// <summary>Desfaz a apuração (contagem errada, cortesia): cancela a cobrança, que não pode ter sido paga, e devolve o total.</summary>
    public void DesfazerApuracaoExcedentes()
    {
        Validador.Garantir(Status != StatusEvento.Cancelado, "Evento cancelado.");
        Validador.Garantir(ExcedentesApurados, "Os excedentes deste evento não foram apurados.");
        var cobranca = _parcelas.Where(p => p.Excedentes && p.Status != StatusTitulo.Cancelado).ToList();
        Validador.Garantir(cobranca.All(p => p.Status == StatusTitulo.Aberto),
            "A cobrança dos excedentes já recebeu pagamento. Estorne o pagamento para desfazer a apuração.");

        foreach (var parcela in cobranca)
            parcela.Cancelar();
        ValorTotal -= ValorExcedentes;
        PresentesApurados = null;
        ExcedentesApuradosEm = null;
        (ExcedentesInteira, ExcedentesMeia, ExcedentesIsentos, ValorExcedentes) = (0, 0, 0, 0);
    }

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

    // ---------------- Checklist ----------------

    public EventoTarefa AdicionarTarefa(string descricao, DateOnly prazo, string? responsavel)
    {
        GarantirConfirmado();
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Descreva a tarefa.");
        Validador.Garantir(descricao.Trim().Length <= EventoTarefa.TamanhoDescricao,
            $"A tarefa pode ter no máximo {EventoTarefa.TamanhoDescricao} caracteres.");
        Validador.Garantir(responsavel is null || responsavel.Trim().Length <= 60, "O responsável pode ter no máximo 60 caracteres.");
        var tarefa = new EventoTarefa(descricao.Trim(), prazo, string.IsNullOrWhiteSpace(responsavel) ? null : responsavel.Trim(),
            _tarefas.Count == 0 ? 1 : _tarefas.Max(t => t.Ordem) + 1);
        _tarefas.Add(tarefa);
        return tarefa;
    }

    /// <summary>
    /// Inclui as tarefas de um modelo de checklist, com o prazo contado a partir da data do evento. Tarefas com a mesma
    /// descrição já existentes no evento não são repetidas. Retorna quantas foram incluídas.
    /// </summary>
    public int AplicarChecklist(ModeloChecklist modelo)
    {
        GarantirConfirmado();
        var incluidas = 0;
        foreach (var item in modelo.Itens.OrderBy(i => i.Ordem).ThenByDescending(i => i.DiasAntes))
        {
            if (_tarefas.Any(t => t.Descricao.Equals(item.Descricao.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                continue;
            AdicionarTarefa(item.Descricao, DataEvento.AddDays(-item.DiasAntes), item.Responsavel);
            incluidas++;
        }
        return incluidas;
    }

    /// <summary>Tarefas podem ser concluídas também depois do evento (ex.: pesquisa de satisfação).</summary>
    public void ConcluirTarefa(int tarefaId, string? usuario, DateTime agora)
    {
        Validador.Garantir(Status != StatusEvento.Cancelado, "Evento cancelado.");
        ObterTarefa(tarefaId).Concluir(usuario, agora);
    }

    public void ReabrirTarefa(int tarefaId)
    {
        Validador.Garantir(Status != StatusEvento.Cancelado, "Evento cancelado.");
        ObterTarefa(tarefaId).Reabrir();
    }

    public void RemoverTarefa(int tarefaId)
    {
        GarantirConfirmado();
        _tarefas.Remove(ObterTarefa(tarefaId));
    }

    // ---------------- Financeiro ----------------

    /// <summary>
    /// (Re)gera o plano de pagamento sobre o saldo ainda não pago. Parcelas abertas são
    /// substituídas; parcelas pagas e a cobrança dos excedentes são preservadas.
    /// </summary>
    /// <param name="intervaloDias">Dias entre as parcelas; nulo = mensais (mesmo dia de cada mês).</param>
    public IReadOnlyList<Parcela> GerarParcelas(int quantidade, DateOnly primeiroVencimento, decimal valorEntrada = 0,
        DateOnly? vencimentoEntrada = null, int? formaPagamentoId = null, int? intervaloDias = null)
    {
        GarantirConfirmado();
        var saldo = SaldoAParcelar;
        Validador.Garantir(saldo > 0, "Não há saldo a parcelar para este evento.");
        var plano = PlanoParcelas.Calcular(saldo, quantidade, primeiroVencimento, valorEntrada, vencimentoEntrada, intervaloDias);

        _parcelas.RemoveAll(p => p.Status == StatusTitulo.Aberto && !p.Excedentes);
        var numero = _parcelas.Count == 0 ? 0 : _parcelas.Max(p => p.Numero);
        var geradas = plano.Select(p => new Parcela(this, ++numero, p.Valor, p.Vencimento, formaPagamentoId,
            p.Entrada ? "Entrada" : $"Parcela {p.Numero}/{p.Quantidade}")).ToList();

        Validador.Garantir(geradas.Max(p => p.Vencimento) <= DataEvento.AddDays(60),
            "O plano de pagamento não pode terminar mais de 60 dias após o evento.");

        _parcelas.AddRange(geradas);
        return geradas;
    }

    /// <summary>
    /// Plano com os vencimentos e valores informados (ex.: as parcelas definidas da condição de pagamento, com percentuais
    /// diferentes). Substitui as parcelas em aberto; a soma precisa ser o saldo a parcelar.
    /// </summary>
    public IReadOnlyList<Parcela> GerarParcelas(IReadOnlyList<(DateOnly Vencimento, decimal Valor)> vencimentos, int? formaPagamentoId = null) =>
        GerarParcelas(vencimentos.Select(v => new VencimentoPlanejado(v.Vencimento, v.Valor)).ToList(), formaPagamentoId);

    /// <inheritdoc cref="GerarParcelas(IReadOnlyList{ValueTuple{DateOnly, decimal}}, int?)"/>
    /// <param name="formaPagamentoId">Forma das parcelas que não têm a sua (ex.: entrada no PIX, demais no cartão).</param>
    public IReadOnlyList<Parcela> GerarParcelas(IReadOnlyList<VencimentoPlanejado> vencimentos, int? formaPagamentoId = null)
    {
        GarantirConfirmado();
        var saldo = SaldoAParcelar;
        Validador.Garantir(saldo > 0, "Não há saldo a parcelar para este evento.");
        Validador.Garantir(vencimentos.Count is >= 1 and <= PlanoParcelas.MaximoParcelas,
            $"Informe de 1 a {PlanoParcelas.MaximoParcelas} vencimentos.");
        Validador.Garantir(vencimentos.All(v => v.Valor > 0), "Cada parcela precisa de um valor maior que zero.");
        Validador.Garantir(vencimentos.Sum(v => v.Valor) == saldo,
            $"A soma das parcelas ({vencimentos.Sum(v => v.Valor):C}) deve ser igual ao saldo a parcelar ({saldo:C}).");
        Validador.Garantir(vencimentos.Max(v => v.Vencimento) <= DataEvento.AddDays(60),
            "O plano de pagamento não pode terminar mais de 60 dias após o evento.");

        _parcelas.RemoveAll(p => p.Status == StatusTitulo.Aberto && !p.Excedentes);
        var numero = _parcelas.Count == 0 ? 0 : _parcelas.Max(p => p.Numero);
        var ordenados = vencimentos.OrderBy(v => v.Vencimento).ToList();
        var geradas = ordenados.Select((v, i) => new Parcela(this, ++numero, v.Valor, v.Vencimento, v.FormaPagamentoId ?? formaPagamentoId,
            $"Parcela {i + 1}/{ordenados.Count}")).ToList();
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

    private EventoTarefa ObterTarefa(int id) =>
        _tarefas.FirstOrDefault(t => t.Id == id) ?? throw new RegraNegocioException("Tarefa não encontrada.");

    private EventoFuncionario ObterEscala(int funcionarioId) =>
        _equipe.FirstOrDefault(e => e.FuncionarioId == funcionarioId)
        ?? throw new RegraNegocioException("Funcionário não está escalado neste evento.");
}
