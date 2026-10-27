using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Infrastructure.Cobranca;

/// <summary>
/// Boleto e PIX registrados pelo Asaas (API v3). O cliente (pagador) é procurado pelo CPF/CNPJ e cadastrado se ainda não
/// existir; depois a cobrança é criada e a linha digitável e o PIX "copia e cola" são lidos. Autenticação pelo cabeçalho
/// access_token.
/// </summary>
/// <param name="manipulador">Troca o HTTP nos testes.</param>
public sealed class ProvedorCobrancaAsaas(AsaasOptions opcoes, HttpMessageHandler? manipulador = null) : IProvedorCobranca, IDisposable
{
    private static readonly Uri Sandbox = new("https://api-sandbox.asaas.com/v3/");
    private static readonly Uri Producao = new("https://api.asaas.com/v3/");

    private readonly HttpClient _http = new(manipulador ?? new HttpClientHandler())
    {
        Timeout = TimeSpan.FromSeconds(Math.Max(5, opcoes.TempoLimiteSegundos))
    };

    public async Task<CobrancaEmitida> EmitirAsync(PedidoCobranca pedido, CancellationToken ct = default)
    {
        var cliente = await ClienteAsync(pedido.Ambiente, pedido.Pagador, ct);

        var cobranca = await EnviarAsync(HttpMethod.Post, pedido.Ambiente, "payments", Corpo(pedido, cliente), ct);
        var id = Texto(cobranca, "id") ?? throw new RegraNegocioException("O Asaas não devolveu o identificador da cobrança.");

        // A cobrança já existe no Asaas: daqui em diante uma falha não pode perder o identificador (a página da fatura
        // continua mostrando o boleto e o PIX).
        string? linha = null, nossoNumero = Texto(cobranca, "nossoNumero"), pix = null;
        if (pedido.Tipo is TipoCobranca.Boleto or TipoCobranca.ClienteEscolhe)
        {
            try
            {
                var identificacao = await EnviarAsync(HttpMethod.Get, pedido.Ambiente, $"payments/{id}/identificationField", null, ct);
                linha = Texto(identificacao, "identificationField");
                nossoNumero = Texto(identificacao, "nossoNumero") ?? nossoNumero;
            }
            catch (RegraNegocioException)
            {
            }
        }
        try
        {
            pix = Texto(await EnviarAsync(HttpMethod.Get, pedido.Ambiente, $"payments/{id}/pixQrCode", null, ct), "payload");
        }
        catch (RegraNegocioException)
        {
            // Conta sem chave PIX cadastrada no Asaas: o boleto continua valendo sem o QR Code.
        }
        return new CobrancaEmitida(id, cliente, linha, nossoNumero, Texto(cobranca, "bankSlipUrl"), Texto(cobranca, "invoiceUrl"), pix);
    }

    public async Task<SituacaoCobranca> ConsultarAsync(AmbienteCobranca ambiente, string id, CancellationToken ct = default) =>
        Situacao(await EnviarAsync(HttpMethod.Get, ambiente, $"payments/{Uri.EscapeDataString(id)}", null, ct));

    public async Task CancelarAsync(AmbienteCobranca ambiente, string id, CancellationToken ct = default)
    {
        var resposta = await EnviarAsync(HttpMethod.Delete, ambiente, $"payments/{Uri.EscapeDataString(id)}", null, ct);
        Validador.Garantir(resposta?["deleted"]?.GetValueKind() == JsonValueKind.True, "O Asaas não confirmou o cancelamento da cobrança.");
    }

    /// <summary>Cliente do Asaas com o CPF/CNPJ do pagador (cadastra se ainda não existir).</summary>
    private async Task<string> ClienteAsync(AmbienteCobranca ambiente, PagadorCobranca pagador, CancellationToken ct)
    {
        var busca = await EnviarAsync(HttpMethod.Get, ambiente, $"customers?cpfCnpj={pagador.Documento}", null, ct);
        if (busca?["data"] is JsonArray { Count: > 0 } encontrados && Texto(encontrados[0], "id") is { } existente)
            return existente;

        var novo = new JsonObject
        {
            ["name"] = pagador.Nome,
            ["cpfCnpj"] = pagador.Documento,
            ["externalReference"] = pagador.Referencia
        };
        if (!string.IsNullOrWhiteSpace(pagador.Email))
            novo["email"] = pagador.Email.Trim();
        if (Documento.SomenteDigitos(pagador.Celular) is { Length: >= 10 } celular)
            novo["mobilePhone"] = celular;
        if (Documento.SomenteDigitos(pagador.Cep) is { Length: 8 } cep)
        {
            novo["postalCode"] = cep;
            novo["addressNumber"] = string.IsNullOrWhiteSpace(pagador.Numero) ? "S/N" : pagador.Numero.Trim();
        }
        var criado = await EnviarAsync(HttpMethod.Post, ambiente, "customers", novo, ct);
        return Texto(criado, "id") ?? throw new RegraNegocioException("O Asaas não devolveu o cliente cadastrado.");
    }

    /// <summary>JSON da cobrança, no leiaute do Asaas.</summary>
    internal static JsonObject Corpo(PedidoCobranca pedido, string cliente)
    {
        var corpo = new JsonObject
        {
            ["customer"] = cliente,
            ["billingType"] = pedido.Tipo switch
            {
                TipoCobranca.Pix => "PIX",
                TipoCobranca.Boleto => "BOLETO",
                _ => "UNDEFINED"
            },
            ["value"] = pedido.Valor,
            ["dueDate"] = pedido.Vencimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["description"] = pedido.Descricao.Length > 500 ? pedido.Descricao[..500] : pedido.Descricao,
            ["externalReference"] = pedido.Referencia
        };
        if (pedido.MultaPercentual > 0)
            corpo["fine"] = new JsonObject { ["value"] = pedido.MultaPercentual };
        if (pedido.JurosMensalPercentual > 0)
            corpo["interest"] = new JsonObject { ["value"] = pedido.JurosMensalPercentual };
        return corpo;
    }

    /// <summary>Situação da cobrança: recebida (boleto/PIX), confirmada (cartão) ou em dinheiro contam como pagas.</summary>
    internal static SituacaoCobranca Situacao(JsonNode? json)
    {
        if (json?["deleted"]?.GetValueKind() == JsonValueKind.True)
            return new SituacaoCobranca(StatusCobranca.Cancelada);
        return Texto(json, "status") switch
        {
            "RECEIVED" or "CONFIRMED" or "RECEIVED_IN_CASH" or "DUNNING_RECEIVED" => new SituacaoCobranca(StatusCobranca.Paga,
                Data(Texto(json, "paymentDate") ?? Texto(json, "clientPaymentDate") ?? Texto(json, "confirmedDate")),
                Numero(json, "value"), Numero(json, "netValue"),
                Texto(json, "billingType") switch
                {
                    "PIX" => TipoFormaPagamento.Pix,
                    "CREDIT_CARD" => TipoFormaPagamento.CartaoCredito,
                    "DEBIT_CARD" => TipoFormaPagamento.CartaoDebito,
                    "BOLETO" => TipoFormaPagamento.Boleto,
                    _ => null
                }),
            _ => new SituacaoCobranca(StatusCobranca.Pendente)
        };
    }

    /// <summary>Mensagens de erro do Asaas ("errors": [{ "code", "description" }]).</summary>
    internal static string? Erros(JsonNode? json) =>
        json?["errors"] is JsonArray erros && erros.Select(e => Texto(e, "description")).Where(d => d is not null).ToList() is { Count: > 0 } lista
            ? string.Join(" | ", lista)
            : null;

    private async Task<JsonNode?> EnviarAsync(HttpMethod metodo, AmbienteCobranca ambiente, string caminho, JsonObject? corpo, CancellationToken ct)
    {
        Validador.Garantir(ambiente != AmbienteCobranca.Simulado, "O ambiente simulado não usa o Asaas.");
        var chave = opcoes.Chave(ambiente);
        Validador.Garantir(!string.IsNullOrWhiteSpace(chave),
            $"Informe a chave da API do Asaas de {(ambiente == AmbienteCobranca.Producao ? "produção" : "sandbox")} em appsettings.Local.json " +
            $"(seção \"{AsaasOptions.Secao}\", chave \"{(ambiente == AmbienteCobranca.Producao ? nameof(AsaasOptions.ChaveProducao) : nameof(AsaasOptions.ChaveSandbox))}\").");

        using var requisicao = new HttpRequestMessage(metodo, new Uri(ambiente == AmbienteCobranca.Producao ? Producao : Sandbox, caminho));
        requisicao.Headers.Add("access_token", chave!.Trim());
        requisicao.Headers.UserAgent.ParseAdd("BuffetSharp/1.0");
        if (corpo is not null)
            requisicao.Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(requisicao, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new RegraNegocioException($"Não foi possível falar com o Asaas: {ex.Message}");
        }
        using (resposta)
        {
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            JsonNode? json = null;
            try
            {
                json = string.IsNullOrWhiteSpace(texto) ? null : JsonNode.Parse(texto);
            }
            catch (JsonException)
            {
            }
            Validador.Garantir(resposta.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                "O Asaas recusou a chave da API (confira a chave e o ambiente em appsettings.Local.json).");
            Validador.Garantir(resposta.StatusCode != HttpStatusCode.NotFound, "Cobrança não encontrada no Asaas.");
            Validador.Garantir(resposta.IsSuccessStatusCode,
                $"O Asaas recusou o pedido: {Erros(json) ?? $"HTTP {(int)resposta.StatusCode}"}");
            return json;
        }
    }

    private static string? Texto(JsonNode? json, string campo) =>
        json is JsonObject objeto && objeto[campo] is JsonValue valor
            ? valor.GetValueKind() switch
            {
                JsonValueKind.String => valor.GetValue<string>() is { Length: > 0 } s ? s : null,
                JsonValueKind.Number => valor.ToJsonString(),
                _ => null
            }
            : null;

    private static decimal? Numero(JsonNode? json, string campo) =>
        json is JsonObject objeto && objeto[campo] is JsonValue valor && valor.GetValueKind() == JsonValueKind.Number
            ? valor.GetValue<decimal>()
            : null;

    private static DateOnly? Data(string? texto) =>
        DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;

    public void Dispose() => _http.Dispose();
}
