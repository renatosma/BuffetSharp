using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Infrastructure.WhatsApp;

/// <summary>
/// WhatsApp do buffet: pela Z-API (o número do buffet conectado por QR Code no painel da Z-API) ou, no modo "Pasta", cada
/// mensagem gravada como arquivo .txt (com o anexo ao lado), sem enviar nada. No modo "Link" o envio é do usuário.
/// </summary>
/// <param name="manipulador">Troca o HTTP nos testes.</param>
public sealed class EnvioWhatsApp(WhatsAppOptions opcoes, HttpMessageHandler? manipulador = null) : IEnvioWhatsApp, IDisposable
{
    private static readonly Uri Base = new("https://api.z-api.io/");

    private readonly HttpClient _http = new(manipulador ?? new HttpClientHandler())
    {
        Timeout = TimeSpan.FromSeconds(Math.Max(5, opcoes.TempoLimiteSegundos))
    };

    public bool Direto => opcoes.Modo != ModoEnvioWhatsApp.Link;

    public string Destino => opcoes.Modo switch
    {
        ModoEnvioWhatsApp.ZApi => $"Z-API (instância {Abreviar(opcoes.Instancia)})",
        ModoEnvioWhatsApp.Pasta => $"arquivo .txt na pasta \"{opcoes.Pasta}\" (modo de teste - configure a Z-API no appsettings)",
        _ => "WhatsApp aberto no computador (envio pelo usuário)"
    };

    /// <summary>Pasta onde as mensagens são gravadas no modo <see cref="ModoEnvioWhatsApp.Pasta"/>.</summary>
    public string PastaSaida => Path.IsPathRooted(opcoes.Pasta) ? opcoes.Pasta : Path.Combine(AppContext.BaseDirectory, opcoes.Pasta);

    public async Task EnviarAsync(string telefone, string texto, AnexoEmail? anexo = null, CancellationToken ct = default)
    {
        var numero = new MensagemWhatsApp(telefone, "").TelefoneInternacional;
        Validador.Garantir(numero.Length is 12 or 13 && numero.StartsWith("55"), "Informe o celular do cliente com DDD.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(texto), "A mensagem está vazia.");
        Validador.Garantir(Direto, "O envio direto do WhatsApp não está configurado (seção \"WhatsApp\" do appsettings).");

        if (opcoes.Modo == ModoEnvioWhatsApp.Pasta)
        {
            Directory.CreateDirectory(PastaSaida);
            var prefixo = Path.Combine(PastaSaida, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{numero}");
            await File.WriteAllTextAsync(prefixo + ".txt", texto, Encoding.UTF8, ct);
            if (anexo is not null)
                await File.WriteAllBytesAsync($"{prefixo}_{string.Concat(anexo.NomeArquivo.Split(Path.GetInvalidFileNameChars()))}", anexo.Conteudo, ct);
            return;
        }

        await EnviarZApiAsync("send-text", new JsonObject { ["phone"] = numero, ["message"] = texto }, ct);
        if (anexo is not null)
        {
            var extensao = Path.GetExtension(anexo.NomeArquivo).TrimStart('.').ToLowerInvariant() is { Length: > 0 } e ? e : "pdf";
            await EnviarZApiAsync($"send-document/{extensao}", new JsonObject
            {
                ["phone"] = numero,
                ["document"] = $"data:{anexo.TipoMime};base64,{Convert.ToBase64String(anexo.Conteudo)}",
                ["fileName"] = Path.GetFileNameWithoutExtension(anexo.NomeArquivo)
            }, ct);
        }
    }

    public async Task<SituacaoWhatsApp> SituacaoAsync(CancellationToken ct = default)
    {
        switch (opcoes.Modo)
        {
            case ModoEnvioWhatsApp.Link:
                return new SituacaoWhatsApp(false, "Modo \"Link\": as mensagens abrem no WhatsApp do computador e são enviadas pelo usuário. " +
                                                   "Para enviar direto, configure a Z-API (seção \"WhatsApp\" do appsettings).");
            case ModoEnvioWhatsApp.Pasta:
                return new SituacaoWhatsApp(true, $"Modo de teste: as mensagens são gravadas em {PastaSaida}.");
        }
        var json = await ChamarAsync(HttpMethod.Get, "status", null, ct);
        var conectado = json?["connected"]?.GetValueKind() == JsonValueKind.True;
        return new SituacaoWhatsApp(conectado, conectado
            ? "WhatsApp do buffet conectado na Z-API."
            : $"WhatsApp desconectado: abra o painel da Z-API e leia o QR Code com o celular do buffet. {Texto(json, "error")}".Trim());
    }

    private async Task EnviarZApiAsync(string caminho, JsonObject corpo, CancellationToken ct)
    {
        var json = await ChamarAsync(HttpMethod.Post, caminho, corpo, ct);
        Validador.Garantir(Texto(json, "zaapId") is not null || Texto(json, "messageId") is not null || Texto(json, "id") is not null,
            $"A Z-API não confirmou o envio: {Texto(json, "error") ?? Texto(json, "message") ?? "sem detalhes"}");
    }

    private async Task<JsonNode?> ChamarAsync(HttpMethod metodo, string caminho, JsonObject? corpo, CancellationToken ct)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(opcoes.Instancia) && !string.IsNullOrWhiteSpace(opcoes.Token),
            $"Informe a instância e o token da Z-API em appsettings.Local.json (seção \"{WhatsAppOptions.Secao}\", chaves " +
            $"\"{nameof(WhatsAppOptions.Instancia)}\" e \"{nameof(WhatsAppOptions.Token)}\").");
        using var requisicao = new HttpRequestMessage(metodo,
            new Uri(Base, $"instances/{Uri.EscapeDataString(opcoes.Instancia!.Trim())}/token/{Uri.EscapeDataString(opcoes.Token!.Trim())}/{caminho}"));
        if (!string.IsNullOrWhiteSpace(opcoes.ClientToken))
            requisicao.Headers.Add("Client-Token", opcoes.ClientToken.Trim());
        if (corpo is not null)
            requisicao.Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(requisicao, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new RegraNegocioException($"Não foi possível falar com a Z-API: {ex.Message}");
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
                "A Z-API recusou o acesso (confira a instância, o token e o Client-Token em appsettings.Local.json).");
            Validador.Garantir(resposta.IsSuccessStatusCode,
                $"A Z-API recusou o envio: {Texto(json, "error") ?? Texto(json, "message") ?? $"HTTP {(int)resposta.StatusCode}"}");
            return json;
        }
    }

    private static string? Texto(JsonNode? json, string campo) =>
        json is JsonObject objeto && objeto[campo] is JsonValue valor && valor.GetValueKind() == JsonValueKind.String &&
        valor.GetValue<string>() is { Length: > 0 } s
            ? s
            : null;

    private static string Abreviar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? "não configurada" : texto.Length <= 6 ? texto : texto[..6] + "...";

    public void Dispose() => _http.Dispose();
}
