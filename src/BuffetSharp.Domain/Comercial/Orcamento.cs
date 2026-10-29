using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Domain.Comercial;

public enum StatusOrcamento
{
    Rascunho,
    Enviado,
    Aprovado,
    Recusado
}

public enum CanalEnvio
{
    Email,
    WhatsApp
}

/// <summary>
/// Nível 3 - Entidade rica: proposta comercial com ciclo de vida
/// (Rascunho → Enviado → Aprovado/Recusado). Ao ser aprovada, gera um <see cref="Evento"/>.
/// </summary>
public class Orcamento : Entidade
{
    private readonly List<OrcamentoServico> _servicos = [];
    private readonly List<EnvioProposta> _envios = [];

    private Orcamento()
    {
    }

    public Orcamento(int clienteId, int tipoEventoId, DateOnly data, TimeOnly horaInicio, TimeOnly horaFim,
        QuantidadesConvidados convidados, DateOnly validoAte)
    {
        AlterarDados(clienteId, tipoEventoId, data, horaInicio, horaFim, convidados, validoAte, null);
    }

    public string Codigo => EhNovo ? "(novo)" : $"ORC-{Id:D5}";

    public int ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }

    public int TipoEventoId { get; private set; }
    public TipoEvento? TipoEvento { get; private set; }

    public int? EspacoId { get; private set; }
    public Espaco? Espaco { get; private set; }

    public int? PacoteId { get; private set; }
    public Pacote? Pacote { get; private set; }

    /// <summary>Usuário responsável pela venda (recebe a comissão quando o evento é pago).</summary>
    public int? VendedorId { get; private set; }
    public Usuario? Vendedor { get; private set; }

    /// <summary>Condição de pagamento proposta (impressa na proposta e levada para o evento na aprovação).</summary>
    public int? CondicaoPagamentoId { get; private set; }
    public CondicaoPagamento? CondicaoPagamento { get; private set; }

    /// <summary>Meio de pagamento combinado (PIX, cartão, boleto...); a condição diz quando e em quantas vezes.</summary>
    public int? FormaPagamentoId { get; private set; }
    public FormaPagamento? FormaPagamento { get; private set; }

    public DateTime Inicio { get; private set; }
    public DateTime Fim { get; private set; }

    /// <summary>Total de pessoas (inteira + meia + isentos).</summary>
    public int QuantidadeConvidados { get; private set; }
    public int ConvidadosMeia { get; private set; }
    public int ConvidadosIsentos { get; private set; }
    public DateOnly ValidoAte { get; private set; }
    public StatusOrcamento Status { get; private set; } = StatusOrcamento.Rascunho;

    /// <summary>Valores "congelados" no momento da negociação.</summary>
    public decimal ValorPorPessoa { get; private set; }
    public decimal ValorEspaco { get; private set; }
    public decimal PercentualDesconto { get; private set; }
    public decimal ValorTotal { get; private set; }

    public const int TamanhoDescricaoAjuste = 300;

    /// <summary>
    /// Ajuste de preço da data (regras por dia da semana, temporada e data especial) sobre a tabela, congelado na negociação:
    /// o valor por pessoa e a locação sugeridos já o incluem.
    /// </summary>
    public decimal AjustePessoaPercentual { get; private set; }

    public decimal AjusteLocacaoPercentual { get; private set; }

    /// <summary>Regras que valem na data, para a proposta (ex.: "Sábado +15%; Alta temporada +10%").</summary>
    public string? AjusteDescricao { get; private set; }

    /// <summary>Mínimo de convidados pagantes exigido pelas regras da data.</summary>
    public int? MinimoConvidadosData { get; private set; }

    /// <summary>Regra de idades do pacote, congelada (a meia pode ser negociada).</summary>
    public int IdadeMaximaIsento { get; private set; } = RegraFaixasEtarias.Padrao.IdadeMaximaIsento;
    public int IdadeMaximaMeia { get; private set; } = RegraFaixasEtarias.Padrao.IdadeMaximaMeia;
    public decimal PercentualMeia { get; private set; } = RegraFaixasEtarias.Padrao.PercentualMeia;

    public string? Observacoes { get; private set; }
    public string? MotivoRecusa { get; private set; }
    public DateTime? EnviadoEm { get; private set; }
    public DateTime? DecididoEm { get; private set; }

    public Evento? Evento { get; private set; }

    public IReadOnlyCollection<OrcamentoServico> Servicos => _servicos;

    /// <summary>Histórico de envios da proposta ao cliente (e-mail e WhatsApp).</summary>
    public IReadOnlyCollection<EnvioProposta> Envios => _envios;

    public QuantidadesConvidados QuantidadesPorFaixa => new(QuantidadeConvidados, ConvidadosMeia, ConvidadosIsentos);
    public RegraFaixasEtarias RegraFaixas => new(IdadeMaximaIsento, IdadeMaximaMeia, PercentualMeia);

    public DateOnly DataEvento => DateOnly.FromDateTime(Inicio);
    public TimeOnly HoraInicio => TimeOnly.FromDateTime(Inicio);
    public TimeOnly HoraFim => TimeOnly.FromDateTime(Fim);

    public bool PodeEditar => Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado;

    public bool EstaExpirado(DateOnly hoje) => Status == StatusOrcamento.Enviado && ValidoAte < hoje;

    public ResumoValores Resumo => CalculadoraValores.Calcular(
        ValorPorPessoa, QuantidadesPorFaixa, ValorEspaco, _servicos.Sum(s => s.Total), PercentualDesconto, RegraFaixas);

    public void AlterarDados(int clienteId, int tipoEventoId, DateOnly data, TimeOnly horaInicio, TimeOnly horaFim,
        QuantidadesConvidados convidados, DateOnly validoAte, string? observacoes)
    {
        GarantirEditavel();
        Validador.Garantir(clienteId > 0, "Selecione o cliente.");
        Validador.Garantir(tipoEventoId > 0, "Selecione o tipo de evento.");
        convidados.Validar();
        Validador.Garantir(validoAte <= data, "A validade do orçamento não pode ser posterior à data do evento.");

        (Inicio, Fim) = PeriodoEvento.Calcular(data, horaInicio, horaFim);
        ClienteId = clienteId;
        TipoEventoId = tipoEventoId;
        QuantidadeConvidados = convidados.Total;
        ConvidadosMeia = convidados.Meia;
        ConvidadosIsentos = convidados.Isentos;
        ValidoAte = validoAte;
        Observacoes = observacoes;
        Recalcular();
    }

    /// <param name="valorNegociado">Preço por pessoa negociado; quando nulo, usa o preço de tabela do pacote.</param>
    /// <param name="percentualMeiaNegociado">Percentual da meia negociado; quando nulo, usa o do pacote.</param>
    public void DefinirPacote(Pacote? pacote, decimal? valorNegociado = null, decimal? percentualMeiaNegociado = null)
    {
        GarantirEditavel();
        if (pacote is null)
        {
            PacoteId = null;
            Pacote = null;
            ValorPorPessoa = 0;
        }
        else
        {
            Validador.Garantir(pacote.Ativo, $"O pacote '{pacote.Nome}' está inativo.");
            Validador.Garantir(QuantidadesPorFaixa.Pagantes >= pacote.MinimoConvidados,
                $"O pacote '{pacote.Nome}' exige no mínimo {pacote.MinimoConvidados} convidados pagantes.");
            Validador.Garantir(valorNegociado is null or > 0, "O valor por pessoa deve ser maior que zero.");
            var regra = pacote.RegraFaixas with { PercentualMeia = percentualMeiaNegociado ?? pacote.PercentualMeia };
            regra.Validar();

            PacoteId = pacote.Id;
            Pacote = pacote;
            ValorPorPessoa = valorNegociado ?? pacote.PrecoPorPessoa;
            (IdadeMaximaIsento, IdadeMaximaMeia, PercentualMeia) = regra;
        }
        Recalcular();
    }

    public void DefinirEspaco(Espaco? espaco, decimal? valorNegociado = null)
    {
        GarantirEditavel();
        if (espaco is null)
        {
            EspacoId = null;
            Espaco = null;
            ValorEspaco = 0;
        }
        else
        {
            Validador.Garantir(espaco.Ativo, $"O espaço '{espaco.Nome}' está inativo.");
            Validador.Garantir(QuantidadeConvidados <= espaco.CapacidadeMaxima,
                $"O espaço '{espaco.Nome}' comporta no máximo {espaco.CapacidadeMaxima} pessoas.");
            Validador.Garantir(valorNegociado is null or >= 0, "O valor do espaço não pode ser negativo.");

            EspacoId = espaco.Id;
            Espaco = espaco;
            ValorEspaco = valorNegociado ?? espaco.ValorLocacao;
        }
        Recalcular();
    }

    /// <summary>Registra o ajuste das regras de preço da data, que pode exigir um mínimo de convidados pagantes.</summary>
    public void AplicarRegrasPreco(AjustePreco ajuste)
    {
        GarantirEditavel();
        Validador.Garantir(ajuste.MinimoConvidados is not { } minimo || QuantidadesPorFaixa.Pagantes >= minimo,
            $"Nesta data ({ajuste.Descricao}) o mínimo é de {ajuste.MinimoConvidados} convidados pagantes.");
        AjustePessoaPercentual = ajuste.PercentualPessoa;
        AjusteLocacaoPercentual = ajuste.PercentualLocacao;
        var descricao = ajuste.Regras.Count == 0 ? null : ajuste.Descricao;
        AjusteDescricao = descricao is { Length: > TamanhoDescricaoAjuste } ? descricao[..TamanhoDescricaoAjuste] : descricao;
        MinimoConvidadosData = ajuste.MinimoConvidados;
    }

    public void DefinirServicos(IEnumerable<(ServicoAdicional Servico, decimal Quantidade, decimal ValorUnitario)> servicos)
    {
        GarantirEditavel();
        var lista = servicos.ToList();
        Validador.Garantir(lista.All(s => s.Quantidade > 0), "A quantidade dos serviços deve ser maior que zero.");
        Validador.Garantir(lista.All(s => s.ValorUnitario >= 0), "O valor dos serviços não pode ser negativo.");

        _servicos.Clear();
        foreach (var (servico, quantidade, valor) in lista)
            _servicos.Add(new OrcamentoServico(servico.Id, servico.Nome, quantidade, valor));
        Recalcular();
    }

    public void DefinirVendedor(int? vendedorId)
    {
        GarantirEditavel();
        Validador.Garantir(vendedorId is null or > 0, "Vendedor inválido.");
        VendedorId = vendedorId;
    }

    public void DefinirCondicaoPagamento(int? condicaoPagamentoId)
    {
        GarantirEditavel();
        Validador.Garantir(condicaoPagamentoId is null or > 0, "Condição de pagamento inválida.");
        CondicaoPagamentoId = condicaoPagamentoId;
    }

    public void DefinirFormaPagamento(int? formaPagamentoId)
    {
        GarantirEditavel();
        Validador.Garantir(formaPagamentoId is null or > 0, "Forma de pagamento inválida.");
        FormaPagamentoId = formaPagamentoId;
    }

    public void AplicarDesconto(decimal percentual)
    {
        GarantirEditavel();
        Validador.Garantir(percentual is >= 0 and <= CalculadoraValores.DescontoMaximoPercentual,
            $"O desconto deve estar entre 0% e {CalculadoraValores.DescontoMaximoPercentual}%.");
        PercentualDesconto = percentual;
        Recalcular();
    }

    public void Enviar(DateTime agora)
    {
        Validador.Garantir(Status == StatusOrcamento.Rascunho, "Somente orçamentos em rascunho podem ser enviados.");
        Validador.Garantir(PacoteId is not null || _servicos.Count > 0, "Inclua um pacote ou serviços antes de enviar.");
        Validador.Garantir(ValorTotal > 0, "O orçamento precisa ter valor total maior que zero.");
        Status = StatusOrcamento.Enviado;
        EnviadoEm = agora;
    }

    /// <summary>
    /// Registra o envio da proposta ao cliente. Um orçamento em rascunho passa a "Enviado".
    /// </summary>
    public EnvioProposta RegistrarEnvio(CanalEnvio canal, string destinatario, string? usuario, DateTime agora)
    {
        Validador.Garantir(PodeEditar, "Somente orçamentos em rascunho ou enviados podem ser enviados ao cliente.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(destinatario), "Informe o destinatário.");
        if (Status == StatusOrcamento.Rascunho)
            Enviar(agora);

        var envio = new EnvioProposta(canal, destinatario.Trim(), usuario, agora);
        _envios.Add(envio);
        return envio;
    }

    /// <summary>
    /// Aprova o orçamento e cria o evento correspondente. A verificação de disponibilidade do
    /// espaço (que depende do banco) é feita pelo serviço de aplicação antes desta chamada.
    /// </summary>
    /// <param name="percentualComissao">Comissão atual do vendedor, congelada no evento.</param>
    public Evento Aprovar(string tituloEvento, DateTime agora, decimal percentualComissao = 0)
    {
        Validador.Garantir(Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado,
            "Este orçamento não pode mais ser aprovado.");
        Validador.Garantir(!EstaExpirado(DateOnly.FromDateTime(agora)),
            "O orçamento está expirado. Altere a validade antes de aprovar.");
        Validador.Garantir(EspacoId is not null, "Defina o espaço do evento antes de aprovar.");
        Validador.Garantir(ValorTotal > 0, "O orçamento precisa ter valor total maior que zero.");
        Validador.Garantir(Inicio > agora, "A data do evento já passou.");

        Status = StatusOrcamento.Aprovado;
        DecididoEm = agora;
        Evento = Evento.CriarDeOrcamento(this, tituloEvento, percentualComissao);
        return Evento;
    }

    public void Recusar(string motivo, DateTime agora)
    {
        Validador.Garantir(PodeEditar, "Este orçamento não pode ser recusado.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo da recusa.");
        Status = StatusOrcamento.Recusado;
        MotivoRecusa = motivo.Trim();
        DecididoEm = agora;
    }

    public void Reabrir()
    {
        Validador.Garantir(Status is StatusOrcamento.Recusado or StatusOrcamento.Enviado,
            "Somente orçamentos enviados ou recusados podem voltar para rascunho.");
        Status = StatusOrcamento.Rascunho;
        MotivoRecusa = null;
        DecididoEm = null;
    }

    /// <summary>Cria uma cópia em rascunho (útil para nova data ou variação de proposta).</summary>
    public Orcamento Duplicar(DateOnly hoje, int diasValidade)
    {
        var data = DataEvento < hoje ? hoje.AddDays(30) : DataEvento;
        var validade = hoje.AddDays(diasValidade);
        var copia = new Orcamento(ClienteId, TipoEventoId, data, HoraInicio, HoraFim, QuantidadesPorFaixa,
            validade > data ? data : validade)
        {
            PacoteId = PacoteId,
            EspacoId = EspacoId,
            VendedorId = VendedorId,
            CondicaoPagamentoId = CondicaoPagamentoId,
            FormaPagamentoId = FormaPagamentoId,
            ValorPorPessoa = ValorPorPessoa,
            ValorEspaco = ValorEspaco,
            PercentualDesconto = PercentualDesconto,
            AjustePessoaPercentual = AjustePessoaPercentual,
            AjusteLocacaoPercentual = AjusteLocacaoPercentual,
            AjusteDescricao = AjusteDescricao,
            MinimoConvidadosData = MinimoConvidadosData,
            IdadeMaximaIsento = IdadeMaximaIsento,
            IdadeMaximaMeia = IdadeMaximaMeia,
            PercentualMeia = PercentualMeia,
            Observacoes = Observacoes
        };
        foreach (var s in _servicos)
            copia._servicos.Add(new OrcamentoServico(s.ServicoAdicionalId, s.Descricao, s.Quantidade, s.ValorUnitario));
        copia.Recalcular();
        return copia;
    }

    private void Recalcular() => ValorTotal = Resumo.Total;

    private void GarantirEditavel() =>
        Validador.Garantir(PodeEditar, $"Orçamento {Status.ToString().ToLowerInvariant()} não pode ser alterado.");
}

public class OrcamentoServico
{
    private OrcamentoServico()
    {
    }

    internal OrcamentoServico(int? servicoAdicionalId, string descricao, decimal quantidade, decimal valorUnitario)
    {
        ServicoAdicionalId = servicoAdicionalId;
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
    }

    public int Id { get; private set; }
    public int OrcamentoId { get; private set; }
    public int? ServicoAdicionalId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal Total => Math.Round(Quantidade * ValorUnitario, 2);
}

public class EnvioProposta
{
    private EnvioProposta()
    {
    }

    internal EnvioProposta(CanalEnvio canal, string destinatario, string? usuario, DateTime enviadoEm)
    {
        Canal = canal;
        Destinatario = destinatario;
        Usuario = usuario;
        EnviadoEm = enviadoEm;
    }

    public int Id { get; private set; }
    public int OrcamentoId { get; private set; }
    public CanalEnvio Canal { get; private set; }

    /// <summary>E-mail ou telefone de destino.</summary>
    public string Destinatario { get; private set; } = string.Empty;

    /// <summary>Login de quem enviou.</summary>
    public string? Usuario { get; private set; }

    public DateTime EnviadoEm { get; private set; }
}
