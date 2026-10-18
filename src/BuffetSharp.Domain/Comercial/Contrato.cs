using System.Security.Cryptography;
using System.Text;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Domain.Comercial;

public enum StatusContrato
{
    AguardandoAssinatura,
    Assinado,
    Cancelado
}

/// <summary>
/// Contrato de prestação de serviços do evento. O texto é "congelado" na emissão e protegido por um
/// hash SHA-256: qualquer alteração posterior no banco é detectada. A assinatura do contratante (e,
/// opcionalmente, do representante do buffet) é capturada na tela e guardada como imagem PNG.
/// </summary>
public class Contrato : Entidade
{
    private Contrato()
    {
    }

    /// <param name="modeloContratoId">Modelo usado para gerar o texto (nulo quando escrito do zero ou pelo modelo do sistema).</param>
    public Contrato(int eventoId, string conteudo, DateTime agora, int? modeloContratoId = null)
    {
        Validador.Garantir(eventoId > 0, "Evento inválido.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(conteudo) && conteudo.Trim().Length >= 100,
            "O texto do contrato está vazio ou incompleto.");
        EventoId = eventoId;
        Conteudo = conteudo.ReplaceLineEndings("\n").Trim();
        HashConteudo = CalcularHash(Conteudo);
        EmitidoEm = agora;
        ModeloContratoId = modeloContratoId;
    }

    public string Codigo => EhNovo ? "(novo)" : $"CTR-{Id:D5}";

    public int EventoId { get; private set; }
    public Evento? Evento { get; private set; }

    /// <summary>Modelo de contrato que originou o texto (somente para consulta: o texto vale como foi emitido).</summary>
    public int? ModeloContratoId { get; private set; }

    public string Conteudo { get; private set; } = string.Empty;

    /// <summary>SHA-256 (hexadecimal) do conteúdo no momento da emissão.</summary>
    public string HashConteudo { get; private set; } = string.Empty;

    public DateTime EmitidoEm { get; private set; }
    public StatusContrato Status { get; private set; } = StatusContrato.AguardandoAssinatura;

    public DateTime? AssinadoEm { get; private set; }
    public string? NomeSignatario { get; private set; }

    /// <summary>CPF ou CNPJ do signatário (somente dígitos).</summary>
    public string? DocumentoSignatario { get; private set; }

    /// <summary>Imagem PNG da assinatura do contratante.</summary>
    public byte[]? AssinaturaCliente { get; private set; }

    public string? NomeRepresentante { get; private set; }

    /// <summary>Imagem PNG da assinatura do representante do buffet (opcional).</summary>
    public byte[]? AssinaturaEmpresa { get; private set; }

    public DateTime? CanceladoEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Falso quando o texto gravado não corresponde mais ao hash da emissão.</summary>
    public bool ConteudoIntegro => CalcularHash(Conteudo) == HashConteudo;

    public void Assinar(string nomeSignatario, string documentoSignatario, byte[] assinaturaCliente,
        string? nomeRepresentante, byte[]? assinaturaEmpresa, DateTime agora)
    {
        Validador.Garantir(Status == StatusContrato.AguardandoAssinatura, "Este contrato não está aguardando assinatura.");
        Validador.Garantir(ConteudoIntegro, "O texto do contrato foi alterado após a emissão. Emita um novo contrato.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(nomeSignatario), "Informe o nome de quem assina.");

        var documento = Documento.SomenteDigitos(documentoSignatario);
        Validador.Garantir(documento.Length == 11 ? Documento.CpfValido(documento) : Documento.CnpjValido(documento),
            "Informe um CPF ou CNPJ válido para o signatário.");
        Validador.Garantir(assinaturaCliente is { Length: > 0 }, "A assinatura do contratante é obrigatória.");
        Validador.Garantir(assinaturaEmpresa is null || !string.IsNullOrWhiteSpace(nomeRepresentante),
            "Informe o nome do representante que assinou pelo buffet.");

        NomeSignatario = nomeSignatario.Trim();
        DocumentoSignatario = documento;
        AssinaturaCliente = assinaturaCliente;
        NomeRepresentante = string.IsNullOrWhiteSpace(nomeRepresentante) ? null : nomeRepresentante.Trim();
        AssinaturaEmpresa = assinaturaEmpresa is { Length: > 0 } ? assinaturaEmpresa : null;
        AssinadoEm = agora;
        Status = StatusContrato.Assinado;
    }

    /// <summary>Cancela a minuta ou registra o distrato de um contrato assinado.</summary>
    public void Cancelar(string motivo, DateTime agora)
    {
        Validador.Garantir(Status != StatusContrato.Cancelado, "O contrato já está cancelado.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        Status = StatusContrato.Cancelado;
        MotivoCancelamento = motivo.Trim();
        CanceladoEm = agora;
    }

    public static string CalcularHash(string conteudo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo.ReplaceLineEndings("\n"))));
}
