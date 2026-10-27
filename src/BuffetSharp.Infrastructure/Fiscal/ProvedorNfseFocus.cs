using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Fiscal;

namespace BuffetSharp.Infrastructure.Fiscal;

/// <summary>
/// NFS-e pelo Focus NFe (API v2): o Focus fala com a prefeitura de cada município. A emissão é assíncrona: o envio volta
/// "processando" e a situação é consultada depois pela referência. Autenticação: token como usuário do Basic, senha vazia.
/// </summary>
/// <param name="manipulador">Troca o HTTP nos testes.</param>
public sealed class ProvedorNfseFocus(NfseOptions opcoes, HttpMessageHandler? manipulador = null) : IProvedorNfse, IDisposable
{
    private static readonly Uri Homologacao = new("https://homologacao.focusnfe.com.br/");
    private static readonly Uri Producao = new("https://api.focusnfe.com.br/");

    private readonly HttpClient _http = new(manipulador ?? new HttpClientHandler())
    {
        Timeout = TimeSpan.FromSeconds(Math.Max(5, opcoes.TempoLimiteSegundos))
    };

    public async Task<RespostaNfse> EmitirAsync(PedidoNfse pedido, CancellationToken ct = default)
    {
        using var requisicao = Requisicao(HttpMethod.Post, pedido.Ambiente, $"v2/nfse?ref={Uri.EscapeDataString(pedido.Referencia)}");
        requisicao.Content = new StringContent(Corpo(pedido).ToJsonString(), Encoding.UTF8, "application/json");
        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(requisicao, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // O pedido pode ter chegado ao Focus: a nota fica em processamento e a consulta pela referência esclarece.
            return new RespostaNfse(StatusNfse.Processando,
                Mensagem: $"Sem resposta do Focus NFe ({ex.Message}). Atualize a situação em instantes.");
        }
        using (resposta)
        {
            var json = await LerAsync(resposta, ct);
            ConferirAcesso(resposta);
            if (resposta.IsSuccessStatusCode)
                return Situacao(json);

            // Pedido recusado pela validação do Focus (dados faltando, referência repetida...).
            return new RespostaNfse(StatusNfse.Erro, Mensagem: Erros(json) ?? $"Focus NFe recusou o envio (HTTP {(int)resposta.StatusCode}).");
        }
    }

    public async Task<RespostaNfse> ConsultarAsync(AmbienteNfse ambiente, string referencia, CancellationToken ct = default)
    {
        using var requisicao = Requisicao(HttpMethod.Get, ambiente, $"v2/nfse/{Uri.EscapeDataString(referencia)}");
        using var resposta = await EnviarAsync(requisicao, ct);
        var json = await LerAsync(resposta, ct);
        ConferirAcesso(resposta);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
            return new RespostaNfse(StatusNfse.Erro, Mensagem: "O Focus NFe não recebeu esta nota. Emita de novo.");
        Validador.Garantir(resposta.IsSuccessStatusCode,
            $"Focus NFe não respondeu à consulta: {Erros(json) ?? $"HTTP {(int)resposta.StatusCode}"}");
        return Situacao(json);
    }

    public async Task<RespostaNfse> CancelarAsync(AmbienteNfse ambiente, string referencia, string justificativa, CancellationToken ct = default)
    {
        using var requisicao = Requisicao(HttpMethod.Delete, ambiente, $"v2/nfse/{Uri.EscapeDataString(referencia)}");
        requisicao.Content = new StringContent(new JsonObject { ["justificativa"] = justificativa }.ToJsonString(), Encoding.UTF8, "application/json");
        using var resposta = await EnviarAsync(requisicao, ct);
        var json = await LerAsync(resposta, ct);
        ConferirAcesso(resposta);
        if (!resposta.IsSuccessStatusCode)
            return new RespostaNfse(StatusNfse.Erro, Mensagem: Erros(json) ?? $"HTTP {(int)resposta.StatusCode}");
        return Situacao(json);
    }

    public async Task<byte[]> BaixarXmlAsync(AmbienteNfse ambiente, string caminhoXml, CancellationToken ct = default)
    {
        using var requisicao = Requisicao(HttpMethod.Get, ambiente, caminhoXml.TrimStart('/'));
        using var resposta = await EnviarAsync(requisicao, ct);
        ConferirAcesso(resposta);
        Validador.Garantir(resposta.IsSuccessStatusCode, $"Não foi possível baixar o XML da nota (HTTP {(int)resposta.StatusCode}).");
        return await resposta.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>JSON do envio, no leiaute do Focus NFe.</summary>
    internal static JsonObject Corpo(PedidoNfse pedido)
    {
        var tomador = new JsonObject
        {
            [pedido.Tomador.Documento.Length == 14 ? "cnpj" : "cpf"] = pedido.Tomador.Documento,
            ["razao_social"] = pedido.Tomador.Nome
        };
        if (!string.IsNullOrWhiteSpace(pedido.Tomador.Email))
            tomador["email"] = pedido.Tomador.Email;
        var telefone = Documento.SomenteDigitos(pedido.Tomador.Telefone);
        if (telefone.Length > 0)
            tomador["telefone"] = telefone;
        var endereco = pedido.Tomador.Endereco;
        if (!string.IsNullOrWhiteSpace(endereco.Logradouro))
        {
            var cep = Documento.SomenteDigitos(endereco.Cep);
            tomador["endereco"] = new JsonObject
            {
                ["logradouro"] = endereco.Logradouro,
                ["numero"] = string.IsNullOrWhiteSpace(endereco.Numero) ? "S/N" : endereco.Numero,
                ["complemento"] = endereco.Complemento,
                ["bairro"] = endereco.Bairro,
                ["uf"] = endereco.Uf,
                ["cep"] = cep.Length > 0 ? cep : null
            };
        }

        var servico = new JsonObject
        {
            ["valor_servicos"] = pedido.Servico.Valor,
            ["aliquota"] = pedido.Servico.AliquotaIss,
            ["iss_retido"] = pedido.Servico.IssRetido,
            ["item_lista_servico"] = pedido.Servico.ItemListaServico,
            ["discriminacao"] = pedido.Servico.Discriminacao,
            ["codigo_municipio"] = pedido.Servico.CodigoMunicipio
        };
        if (!string.IsNullOrWhiteSpace(pedido.Servico.CodigoTributarioMunicipio))
            servico["codigo_tributario_municipio"] = pedido.Servico.CodigoTributarioMunicipio;
        if (!string.IsNullOrWhiteSpace(pedido.Servico.CodigoCnae))
            servico["codigo_cnae"] = Documento.SomenteDigitos(pedido.Servico.CodigoCnae);

        var corpo = new JsonObject
        {
            ["data_emissao"] = new DateTimeOffset(pedido.DataEmissao).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["natureza_operacao"] = "1",
            ["optante_simples_nacional"] = pedido.Regime is RegimeTributario.SimplesNacional or RegimeTributario.Mei,
            ["prestador"] = new JsonObject
            {
                ["cnpj"] = pedido.Prestador.Cnpj,
                ["inscricao_municipal"] = pedido.Prestador.InscricaoMunicipal,
                ["codigo_municipio"] = pedido.Prestador.CodigoMunicipio
            },
            ["tomador"] = tomador,
            ["servico"] = servico
        };
        // Regime especial: 5 = MEI, 6 = ME/EPP do Simples Nacional.
        if (pedido.Regime is RegimeTributario.Mei or RegimeTributario.SimplesNacional)
            corpo["regime_especial_tributacao"] = pedido.Regime == RegimeTributario.Mei ? 5 : 6;
        return corpo;
    }

    /// <summary>Lê a situação devolvida pelo Focus (envio, consulta e cancelamento usam o mesmo formato).</summary>
    internal static RespostaNfse Situacao(JsonNode? json)
    {
        var status = Texto(json, "status");
        return status switch
        {
            "autorizado" => new RespostaNfse(StatusNfse.Autorizada, Texto(json, "numero"), Texto(json, "codigo_verificacao"),
                Data(Texto(json, "data_emissao")), Texto(json, "url"), Texto(json, "caminho_xml_nota_fiscal")),
            "cancelado" => new RespostaNfse(StatusNfse.Cancelada, Texto(json, "numero"), Texto(json, "codigo_verificacao"),
                Data(Texto(json, "data_emissao")), Texto(json, "url"), Texto(json, "caminho_xml_nota_fiscal")),
            "erro_autorizacao" or "erro_cancelamento" => new RespostaNfse(StatusNfse.Erro,
                Mensagem: Erros(json) ?? "Recusada pela prefeitura (sem mensagem)."),
            _ => new RespostaNfse(StatusNfse.Processando, Mensagem: "Aguardando a prefeitura.")
        };
    }

    /// <summary>Mensagens de erro do Focus: "mensagem" e a lista "erros" (com a correção sugerida, quando vem).</summary>
    internal static string? Erros(JsonNode? json)
    {
        var mensagens = new List<string>();
        if (json?["erros"] is JsonArray erros)
            foreach (var erro in erros)
            {
                var mensagem = Texto(erro, "mensagem");
                var correcao = Texto(erro, "correcao");
                if (mensagem is not null)
                    mensagens.Add(correcao is null ? mensagem : $"{mensagem} ({correcao})");
            }
        if (mensagens.Count == 0 && Texto(json, "mensagem") is { } unica)
            mensagens.Add(unica);
        return mensagens.Count == 0 ? null : string.Join(" | ", mensagens);
    }

    private HttpRequestMessage Requisicao(HttpMethod metodo, AmbienteNfse ambiente, string caminho)
    {
        Validador.Garantir(ambiente != AmbienteNfse.Simulado, "O ambiente simulado não usa o Focus NFe.");
        var token = opcoes.Token(ambiente);
        Validador.Garantir(!string.IsNullOrWhiteSpace(token),
            $"Informe o token do Focus NFe de {(ambiente == AmbienteNfse.Producao ? "produção" : "homologação")} em appsettings.Local.json " +
            $"(seção \"{NfseOptions.Secao}\", chave \"{(ambiente == AmbienteNfse.Producao ? nameof(NfseOptions.TokenProducao) : nameof(NfseOptions.TokenHomologacao))}\").");
        var requisicao = new HttpRequestMessage(metodo, new Uri(ambiente == AmbienteNfse.Producao ? Producao : Homologacao, caminho));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(token!.Trim() + ":")));
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return requisicao;
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(requisicao, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new RegraNegocioException($"Não foi possível falar com o Focus NFe: {ex.Message}");
        }
    }

    private static void ConferirAcesso(HttpResponseMessage resposta) =>
        Validador.Garantir(resposta.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
            "O Focus NFe recusou o token (confira o token e o ambiente em appsettings.Local.json, e se o CNPJ está habilitado para NFS-e).");

    private static async Task<JsonNode?> LerAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        var texto = await resposta.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(texto))
            return null;
        try
        {
            return JsonNode.Parse(texto);
        }
        catch (JsonException)
        {
            return new JsonObject { ["mensagem"] = texto.Length > 300 ? texto[..300] : texto };
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

    private static DateTime? Data(string? texto) =>
        DateTimeOffset.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var data)
            ? data.LocalDateTime
            : null;

    public void Dispose() => _http.Dispose();
}
