using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Fiscal;

namespace BuffetSharp.Application.Abstracoes;

/// <summary>
/// Credenciais do provedor da NFS-e (seção "Nfse" do appsettings). Ficam na máquina (appsettings.Local.json), fora do banco,
/// como a senha do e-mail.
/// </summary>
public sealed class NfseOptions
{
    public const string Secao = "Nfse";

    /// <summary>Token do Focus NFe para o ambiente de homologação (testes).</summary>
    public string? TokenHomologacao { get; set; }

    /// <summary>Token do Focus NFe para produção (notas com valor fiscal).</summary>
    public string? TokenProducao { get; set; }

    public int TempoLimiteSegundos { get; set; } = 30;

    public string? Token(AmbienteNfse ambiente) => ambiente switch
    {
        AmbienteNfse.Homologacao => TokenHomologacao,
        AmbienteNfse.Producao => TokenProducao,
        _ => null
    };
}

public sealed record PrestadorNfse(string Cnpj, string InscricaoMunicipal, string CodigoMunicipio);

public sealed record TomadorNfse(string Documento, string Nome, string? Email, string? Telefone, Endereco Endereco);

/// <param name="CodigoMunicipio">Município onde o serviço é prestado (IBGE).</param>
public sealed record ServicoNfse(decimal Valor, decimal AliquotaIss, bool IssRetido, string ItemListaServico, string? CodigoTributarioMunicipio,
    string? CodigoCnae, string Discriminacao, string CodigoMunicipio);

/// <param name="Referencia">Identificador do pedido no provedor (consultar e cancelar usam ele).</param>
public sealed record PedidoNfse(string Referencia, AmbienteNfse Ambiente, DateTime DataEmissao, RegimeTributario Regime, PrestadorNfse Prestador,
    TomadorNfse Tomador, ServicoNfse Servico);

/// <summary>Situação da NFS-e devolvida pelo provedor.</summary>
public sealed record RespostaNfse(StatusNfse Status, string? Numero = null, string? CodigoVerificacao = null, DateTime? DataEmissao = null,
    string? Url = null, string? CaminhoXml = null, string? Mensagem = null);

/// <summary>Provedor que fala com a prefeitura (Focus NFe). O ambiente simulado não passa por aqui.</summary>
public interface IProvedorNfse
{
    Task<RespostaNfse> EmitirAsync(PedidoNfse pedido, CancellationToken ct = default);

    Task<RespostaNfse> ConsultarAsync(AmbienteNfse ambiente, string referencia, CancellationToken ct = default);

    Task<RespostaNfse> CancelarAsync(AmbienteNfse ambiente, string referencia, string justificativa, CancellationToken ct = default);

    Task<byte[]> BaixarXmlAsync(AmbienteNfse ambiente, string caminhoXml, CancellationToken ct = default);
}
