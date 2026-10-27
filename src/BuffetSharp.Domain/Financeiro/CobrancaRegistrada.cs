using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Financeiro;

/// <summary>Onde a cobrança registrada é emitida.</summary>
public enum AmbienteCobranca
{
    /// <summary>Sem internet nem banco: boleto e PIX de mentira, pagos pelo botão "Simular pagamento" (para treinar).</summary>
    Simulado,

    /// <summary>Sandbox do Asaas: cobranças de teste, pagas pelo painel de testes do Asaas.</summary>
    Sandbox,

    Producao
}

public enum TipoCobranca
{
    /// <summary>Boleto (no Asaas o boleto também traz o QR Code do PIX).</summary>
    Boleto,

    Pix,

    /// <summary>O cliente escolhe boleto, PIX ou cartão na página da fatura.</summary>
    ClienteEscolhe
}

public enum StatusCobranca
{
    /// <summary>Emitida, aguardando o pagamento (mesmo depois do vencimento ela ainda pode ser paga, com multa e juros).</summary>
    Pendente,

    Paga,
    Cancelada
}

/// <summary>
/// Dados de cobrança do buffet (um registro só): ambiente, tipo padrão, multa e juros e a conta bancária onde os pagamentos
/// caem. A chave da API do Asaas não fica aqui: fica na configuração da máquina (appsettings.Local.json).
/// </summary>
public class ConfiguracaoCobranca : Entidade
{
    public AmbienteCobranca Ambiente { get; set; } = AmbienteCobranca.Simulado;
    public TipoCobranca TipoPadrao { get; set; } = TipoCobranca.Boleto;

    /// <summary>Conta bancária que recebe os pagamentos (a conta do Asaas, normalmente).</summary>
    public int? ContaFinanceiraId { get; set; }

    /// <summary>Multa por atraso, em % do valor (o máximo legal é 2%).</summary>
    [Range(typeof(decimal), "0", "2", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "A multa vai de 0% a 2%.")]
    public decimal MultaPercentual { get; set; } = 2;

    /// <summary>Juros de mora, em % ao mês (o usual é 1%).</summary>
    [Range(typeof(decimal), "0", "10", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Os juros vão de 0% a 10% ao mês.")]
    public decimal JurosMensalPercentual { get; set; } = 1;

    /// <summary>O que falta para emitir (vazio = pronto).</summary>
    public IReadOnlyList<string> Pendencias() =>
        ContaFinanceiraId is null ? ["a conta bancária que recebe os pagamentos"] : [];
}

/// <summary>
/// Boleto ou PIX registrado de uma parcela (uma pendente por parcela). Quando o pagamento é confirmado, a parcela é baixada
/// sozinha na conta configurada; se não der (parcela já paga, conta fechada...), o motivo fica na mensagem para a baixa manual.
/// </summary>
public class CobrancaRegistrada : Entidade
{
    private CobrancaRegistrada()
    {
    }

    public CobrancaRegistrada(Parcela parcela, AmbienteCobranca ambiente, TipoCobranca tipo, DateOnly vencimento, DateOnly hoje,
        string? usuario, DateTime agora)
    {
        Validador.Garantir(parcela.Status == StatusTitulo.Aberto, "Só uma parcela em aberto pode ser cobrada.");
        Validador.Garantir(vencimento >= hoje, "O vencimento da cobrança não pode ser no passado. Escolha uma nova data.");
        Validador.Garantir(vencimento <= hoje.AddYears(1), "O vencimento da cobrança pode ser de no máximo um ano.");
        Validador.Garantir(parcela.Valor >= 5, "O Asaas só registra cobranças a partir de R$ 5,00.");

        Parcela = parcela;
        ParcelaId = parcela.Id;
        EventoId = parcela.Evento?.Id ?? parcela.EventoId;
        Descricao = parcela.Descricao;
        Ambiente = ambiente;
        Tipo = tipo;
        Valor = parcela.Valor;
        Vencimento = vencimento;
        Usuario = usuario;
        SolicitadaEm = agora;
        Referencia = $"parc{parcela.Id}-{Guid.NewGuid():N}"[..24];
    }

    /// <summary>Parcela cobrada (fica vazio se a parcela for excluída ao refazer o plano de pagamento).</summary>
    public int? ParcelaId { get; private set; }

    public Parcela? Parcela { get; private set; }
    public int EventoId { get; private set; }

    /// <summary>Descrição da parcela na emissão ("Parcela 2/3").</summary>
    public string Descricao { get; private set; } = string.Empty;

    public AmbienteCobranca Ambiente { get; private set; }
    public TipoCobranca Tipo { get; private set; }
    public StatusCobranca Status { get; private set; } = StatusCobranca.Pendente;
    public decimal Valor { get; private set; }
    public DateOnly Vencimento { get; private set; }

    /// <summary>Referência externa enviada ao provedor (identifica a parcela no painel do Asaas).</summary>
    public string Referencia { get; private set; } = string.Empty;

    /// <summary>Identificador da cobrança no provedor (pay_...).</summary>
    public string? IdExterno { get; private set; }

    /// <summary>Cliente no provedor (cus_...).</summary>
    public string? ClienteExterno { get; private set; }

    public string? LinhaDigitavel { get; private set; }
    public string? NossoNumero { get; private set; }

    /// <summary>PDF do boleto.</summary>
    public string? UrlBoleto { get; private set; }

    /// <summary>Página da fatura (o cliente vê e paga por boleto, PIX ou cartão).</summary>
    public string? UrlFatura { get; private set; }

    public string? PixCopiaECola { get; private set; }

    public DateOnly? DataPagamento { get; private set; }
    public decimal? ValorPago { get; private set; }

    /// <summary>O que caiu na conta, já sem a taxa do provedor.</summary>
    public decimal? ValorLiquido { get; private set; }

    /// <summary>Como o cliente pagou (boleto, PIX ou cartão, na página da fatura).</summary>
    public TipoFormaPagamento? PagaCom { get; private set; }

    /// <summary>A parcela foi baixada sozinha quando o pagamento chegou.</summary>
    public bool Baixada { get; private set; }

    /// <summary>Aviso para conferir (baixa que não deu certo, parcela cancelada com a cobrança pendente...).</summary>
    public string? Mensagem { get; private set; }

    public string? Usuario { get; private set; }
    public DateTime SolicitadaEm { get; private set; }
    public DateTime? CanceladaEm { get; private set; }

    public bool Pendente => Status == StatusCobranca.Pendente;

    public void RegistrarEmissao(string idExterno, string? clienteExterno, string? linhaDigitavel, string? nossoNumero, string? urlBoleto,
        string? urlFatura, string? pixCopiaECola)
    {
        Validador.Garantir(Pendente && IdExterno is null, "A cobrança já foi emitida.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(idExterno), "O provedor não devolveu o identificador da cobrança.");
        IdExterno = Cortar(idExterno, 60);
        ClienteExterno = Cortar(clienteExterno, 60);
        LinhaDigitavel = Cortar(linhaDigitavel, 60);
        NossoNumero = Cortar(nossoNumero, 30);
        UrlBoleto = Cortar(urlBoleto, 500);
        UrlFatura = Cortar(urlFatura, 500);
        PixCopiaECola = Cortar(pixCopiaECola, 600);
    }

    /// <summary>Pagamento confirmado pelo provedor. A baixa da parcela é registrada logo depois (<see cref="RegistrarBaixa"/>).</summary>
    public void RegistrarPagamento(DateOnly data, decimal valorPago, decimal? valorLiquido, TipoFormaPagamento? pagaCom)
    {
        Validador.Garantir(Pendente, "A cobrança não está pendente.");
        Validador.Garantir(valorPago > 0, "O valor pago deve ser maior que zero.");
        Status = StatusCobranca.Paga;
        DataPagamento = data;
        ValorPago = valorPago;
        ValorLiquido = valorLiquido;
        PagaCom = pagaCom;
        Mensagem = null;
    }

    /// <summary>Resultado da baixa automática: feita, ou o motivo para a baixa manual.</summary>
    public void RegistrarBaixa(bool baixada, string? mensagem = null)
    {
        Validador.Garantir(Status == StatusCobranca.Paga, "A cobrança ainda não foi paga.");
        Baixada = baixada;
        Mensagem = Cortar(mensagem, 500);
    }

    public void Avisar(string? mensagem) => Mensagem = Cortar(mensagem, 500);

    public void Cancelar(DateTime agora)
    {
        Validador.Garantir(Pendente, "Só uma cobrança pendente pode ser cancelada.");
        Status = StatusCobranca.Cancelada;
        CanceladaEm = agora;
        Mensagem = null;
    }

    private static string? Cortar(string? texto, int tamanho) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(texto.Trim().Length, tamanho)];
}
