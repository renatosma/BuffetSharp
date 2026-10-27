using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Abstracoes;

/// <summary>Recebimento por PIX (seção "Pix" do appsettings.json). Sem chave, a cobrança por PIX fica desligada.</summary>
public sealed class PixOptions
{
    public const string Secao = "Pix";

    /// <summary>Chave PIX do buffet: CPF/CNPJ, e-mail, celular ou chave aleatória.</summary>
    public string? Chave { get; set; }

    /// <summary>Nome do recebedor como aparece no banco (até 25 caracteres; sem o nome, usa o da empresa).</summary>
    public string? NomeRecebedor { get; set; }

    /// <summary>Cidade do recebedor (até 15 caracteres).</summary>
    public string Cidade { get; set; } = "SAO PAULO";
}

/// <summary>Imagem do QR Code (PNG) de um texto.</summary>
public interface IGeradorQrCode
{
    byte[] Png(string texto, int pixelsPorModulo = 8);
}

/// <summary>
/// Cobrança registrada pelo Asaas (seção "Asaas" do appsettings). As chaves da API ficam na máquina (appsettings.Local.json),
/// fora do banco, como a senha do e-mail.
/// </summary>
public sealed class AsaasOptions
{
    public const string Secao = "Asaas";

    /// <summary>Chave da API da conta de testes (sandbox.asaas.com).</summary>
    public string? ChaveSandbox { get; set; }

    /// <summary>Chave da API da conta de produção.</summary>
    public string? ChaveProducao { get; set; }

    /// <summary>De quanto em quanto tempo o sistema pergunta ao Asaas se as cobranças foram pagas (0 = só pelo botão).</summary>
    public int IntervaloConsultaMinutos { get; set; } = 10;

    public int TempoLimiteSegundos { get; set; } = 30;

    public string? Chave(AmbienteCobranca ambiente) => ambiente switch
    {
        AmbienteCobranca.Sandbox => ChaveSandbox,
        AmbienteCobranca.Producao => ChaveProducao,
        _ => null
    };
}

/// <summary>Pagador da cobrança (o cliente do evento).</summary>
public sealed record PagadorCobranca(string Documento, string Nome, string? Email, string? Celular, string? Cep, string? Numero, string Referencia);

public sealed record PedidoCobranca(AmbienteCobranca Ambiente, PagadorCobranca Pagador, TipoCobranca Tipo, decimal Valor, DateOnly Vencimento,
    string Descricao, string Referencia, decimal MultaPercentual, decimal JurosMensalPercentual);

/// <summary>Cobrança criada no provedor: o que vai para o cliente pagar.</summary>
public sealed record CobrancaEmitida(string Id, string? ClienteId, string? LinhaDigitavel, string? NossoNumero, string? UrlBoleto,
    string? UrlFatura, string? PixCopiaECola);

/// <summary>Situação da cobrança no provedor.</summary>
public sealed record SituacaoCobranca(StatusCobranca Status, DateOnly? DataPagamento = null, decimal? ValorPago = null,
    decimal? ValorLiquido = null, TipoFormaPagamento? PagaCom = null);

/// <summary>Provedor da cobrança registrada (Asaas). O ambiente simulado não passa por aqui.</summary>
public interface IProvedorCobranca
{
    Task<CobrancaEmitida> EmitirAsync(PedidoCobranca pedido, CancellationToken ct = default);

    Task<SituacaoCobranca> ConsultarAsync(AmbienteCobranca ambiente, string id, CancellationToken ct = default);

    Task CancelarAsync(AmbienteCobranca ambiente, string id, CancellationToken ct = default);
}
