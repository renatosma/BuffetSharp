using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Domain.Fiscal;

public enum StatusNfse
{
    /// <summary>Enviada; a prefeitura ainda não respondeu (atualize a situação em alguns segundos).</summary>
    Processando,

    Autorizada,

    /// <summary>Recusada pela prefeitura ou pelo provedor: corrija e emita de novo.</summary>
    Erro,

    Cancelada
}

/// <summary>
/// NFS-e do evento (uma ativa por evento): o pedido enviado ao provedor e a resposta da prefeitura. Os dados do tomador
/// e do serviço ficam gravados como foram enviados.
/// </summary>
public class NotaFiscalServico : Entidade
{
    public const int TamanhoDiscriminacao = 2000;
    public const int TamanhoMensagem = 2000;

    private NotaFiscalServico()
    {
    }

    public NotaFiscalServico(Evento evento, AmbienteNfse ambiente, decimal valorServicos, decimal aliquotaIss, bool issRetido,
        string discriminacao, string tomadorDocumento, string tomadorNome, string? tomadorEmail, string? usuario, DateTime agora)
    {
        Validador.Garantir(evento.Status != StatusEvento.Cancelado, "Evento cancelado não tem nota fiscal.");
        Validador.Garantir(valorServicos > 0, "Informe o valor dos serviços.");
        Validador.Garantir(valorServicos <= evento.ValorTotal, $"O valor da nota não pode passar do valor do evento ({evento.ValorTotal:C}).");
        Validador.Garantir(aliquotaIss is >= 0 and <= 5, "A alíquota do ISS deve estar entre 0% e 5%.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(discriminacao), "Informe a descrição do serviço.");
        Validador.Garantir(discriminacao.Trim().Length <= TamanhoDiscriminacao,
            $"A descrição do serviço pode ter no máximo {TamanhoDiscriminacao} caracteres.");
        var documento = new string(tomadorDocumento.Where(char.IsDigit).ToArray());
        Validador.Garantir(documento.Length is 11 or 14 && (Documento.CpfValido(documento) || Documento.CnpjValido(documento)),
            "O cliente precisa de CPF ou CNPJ válido no cadastro para receber a nota fiscal.");

        Evento = evento;
        EventoId = evento.Id;
        Ambiente = ambiente;
        ValorServicos = valorServicos;
        AliquotaIss = aliquotaIss;
        IssRetido = issRetido;
        Discriminacao = discriminacao.Trim();
        TomadorDocumento = documento;
        TomadorNome = tomadorNome.Trim()[..Math.Min(tomadorNome.Trim().Length, 150)];
        TomadorEmail = string.IsNullOrWhiteSpace(tomadorEmail) ? null : tomadorEmail.Trim();
        Usuario = usuario;
        SolicitadaEm = agora;
        Referencia = $"evt{evento.Id}-{Guid.NewGuid():N}"[..24];
    }

    /// <summary>Identificador do pedido no provedor (a "ref" do Focus NFe).</summary>
    public string Referencia { get; private set; } = string.Empty;

    public int EventoId { get; private set; }
    public Evento? Evento { get; private set; }
    public AmbienteNfse Ambiente { get; private set; }
    public StatusNfse Status { get; private set; } = StatusNfse.Processando;

    public decimal ValorServicos { get; private set; }
    public decimal AliquotaIss { get; private set; }
    public bool IssRetido { get; private set; }
    public decimal ValorIss => Math.Round(ValorServicos * AliquotaIss / 100, 2, MidpointRounding.AwayFromZero);
    public string Discriminacao { get; private set; } = string.Empty;

    public string TomadorDocumento { get; private set; } = string.Empty;
    public string TomadorNome { get; private set; } = string.Empty;
    public string? TomadorEmail { get; private set; }

    /// <summary>Número da NFS-e dado pela prefeitura.</summary>
    public string? Numero { get; private set; }
    public string? CodigoVerificacao { get; private set; }
    public DateTime? DataEmissao { get; private set; }

    /// <summary>Endereço da nota no site da prefeitura (para abrir ou imprimir).</summary>
    public string? Url { get; private set; }

    /// <summary>Caminho do XML no provedor.</summary>
    public string? CaminhoXml { get; private set; }

    public string? Mensagem { get; private set; }
    public string? Usuario { get; private set; }
    public DateTime SolicitadaEm { get; private set; }
    public DateTime? CanceladaEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public bool Ativa => Status is StatusNfse.Processando or StatusNfse.Autorizada;

    public void RegistrarAutorizacao(string? numero, string? codigoVerificacao, DateTime? dataEmissao, string? url, string? caminhoXml)
    {
        Validador.Garantir(Status != StatusNfse.Cancelada, "A nota está cancelada.");
        Status = StatusNfse.Autorizada;
        Numero = Cortar(numero, 20);
        CodigoVerificacao = Cortar(codigoVerificacao, 60);
        DataEmissao = dataEmissao;
        Url = Cortar(url, 500);
        CaminhoXml = Cortar(caminhoXml, 500);
        Mensagem = null;
    }

    /// <summary>Ainda na fila da prefeitura.</summary>
    public void RegistrarProcessamento(string? mensagem = null)
    {
        Validador.Garantir(Status is StatusNfse.Processando, "A nota já teve resposta da prefeitura.");
        Mensagem = Cortar(mensagem, TamanhoMensagem);
    }

    public void RegistrarErro(string mensagem)
    {
        Validador.Garantir(Status is StatusNfse.Processando, "Só uma nota em processamento pode ser recusada.");
        Status = StatusNfse.Erro;
        Mensagem = Cortar(string.IsNullOrWhiteSpace(mensagem) ? "Recusada pela prefeitura (sem mensagem)." : mensagem, TamanhoMensagem);
    }

    public void ConferirCancelamento(string motivo)
    {
        Validador.Garantir(Status == StatusNfse.Autorizada, "Só uma nota autorizada pode ser cancelada.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo) && motivo.Trim().Length >= 15,
            "Informe o motivo do cancelamento (a prefeitura exige ao menos 15 caracteres).");
    }

    public void RegistrarCancelamento(string motivo, DateTime agora)
    {
        ConferirCancelamento(motivo);
        Status = StatusNfse.Cancelada;
        CanceladaEm = agora;
        MotivoCancelamento = Cortar(motivo, 255);
    }

    private static string? Cortar(string? texto, int tamanho) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(texto.Trim().Length, tamanho)];
}
