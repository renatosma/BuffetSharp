using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Infrastructure.WhatsApp;

namespace BuffetSharp.Tests.Dominio;

/// <summary>WhatsApp do buffet: envio pela Z-API (HTTP simulado), gravação em pasta e o modo Link.</summary>
public class EnvioWhatsAppTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class HttpFalso(params (HttpStatusCode Status, string Corpo)[] respostas) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode, string)> _respostas = new(respostas);
        public List<(HttpMethod Metodo, string Endereco, string? ClientToken, string? Corpo)> Pedidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add((request.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("Client-Token", out var token) ? token.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            var (status, corpo) = _respostas.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static WhatsAppOptions ZApi(string? clientToken = "seguranca") =>
        new() { Modo = ModoEnvioWhatsApp.ZApi, Instancia = "3C2INSTANCIA", Token = "TOKEN123", ClientToken = clientToken };

    [Fact]
    public async Task ZApi_envia_o_texto_e_o_pdf_para_o_celular_com_ddi()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"zaapId":"3999984263738042930CD6ECDE9VDWSA","messageId":"D241XXXX732339502B68","id":"D241XXXX732339502B68"}"""),
            (HttpStatusCode.OK, """{"zaapId":"3999984263738042930CD6ECDE9VDWSB","messageId":"D241XXXX732339502B69"}"""));
        using var envio = new EnvioWhatsApp(ZApi(), http);
        Assert.True(envio.Direto);
        Assert.StartsWith("Z-API (instância 3C2INS...", envio.Destino);

        await envio.EnviarAsync("(11) 98888-7777", "Olá, Ana!", new AnexoEmail("Proposta ORC-00012.pdf", [1, 2, 3]), Ct);

        Assert.Equal(2, http.Pedidos.Count);
        var (metodo, endereco, token, corpo) = http.Pedidos[0];
        Assert.Equal((HttpMethod.Post, "https://api.z-api.io/instances/3C2INSTANCIA/token/TOKEN123/send-text", "seguranca"), (metodo, endereco, token));
        var texto = JsonNode.Parse(corpo!)!;
        Assert.Equal(("5511988887777", "Olá, Ana!"), (texto["phone"]!.GetValue<string>(), texto["message"]!.GetValue<string>()));

        Assert.EndsWith("/send-document/pdf", http.Pedidos[1].Endereco);
        var documento = JsonNode.Parse(http.Pedidos[1].Corpo!)!;
        Assert.Equal("data:application/pdf;base64,AQID", documento["document"]!.GetValue<string>());
        Assert.Equal("Proposta ORC-00012", documento["fileName"]!.GetValue<string>());
    }

    [Fact]
    public async Task ZApi_informa_a_conexao_e_traduz_os_erros()
    {
        var http = new HttpFalso(
            (HttpStatusCode.OK, """{"connected":true,"error":null,"smartphoneConnected":true}"""),
            (HttpStatusCode.OK, """{"connected":false,"error":"You are not connected.","smartphoneConnected":false}"""),
            (HttpStatusCode.BadRequest, """{"error":"Phone number does not exist"}"""),
            (HttpStatusCode.OK, """{"value":false}"""),
            (HttpStatusCode.Unauthorized, """{"error":"Client-Token not allowed"}"""));
        using var envio = new EnvioWhatsApp(ZApi(clientToken: null), http);

        Assert.True((await envio.SituacaoAsync(Ct)).Conectado);
        var desconectado = await envio.SituacaoAsync(Ct);
        Assert.False(desconectado.Conectado);
        Assert.Contains("QR Code", desconectado.Descricao);
        Assert.EndsWith("/status", http.Pedidos[0].Endereco);
        Assert.Null(http.Pedidos[0].ClientToken);

        Assert.Contains("Phone number does not exist", (await Assert.ThrowsAsync<RegraNegocioException>(() => envio.EnviarAsync("11988887777", "Oi", ct: Ct))).Message);
        Assert.Contains("não confirmou", (await Assert.ThrowsAsync<RegraNegocioException>(() => envio.EnviarAsync("11988887777", "Oi", ct: Ct))).Message);
        Assert.Contains("Client-Token", (await Assert.ThrowsAsync<RegraNegocioException>(() => envio.EnviarAsync("11988887777", "Oi", ct: Ct))).Message);
    }

    [Fact]
    public async Task Sem_instancia_ou_celular_valido_nao_chama_a_zapi()
    {
        var http = new HttpFalso();
        using var semInstancia = new EnvioWhatsApp(new WhatsAppOptions { Modo = ModoEnvioWhatsApp.ZApi }, http);
        Assert.Contains("appsettings.Local.json", (await Assert.ThrowsAsync<RegraNegocioException>(() =>
            semInstancia.EnviarAsync("11988887777", "Oi", ct: Ct))).Message);

        using var envio = new EnvioWhatsApp(ZApi(), http);
        Assert.Contains("DDD", (await Assert.ThrowsAsync<RegraNegocioException>(() => envio.EnviarAsync("98888-7777", "Oi", ct: Ct))).Message);
        Assert.Empty(http.Pedidos);
    }

    [Fact]
    public async Task Modo_pasta_grava_a_mensagem_e_o_anexo_e_o_link_nao_envia()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"BuffetSharp_WhatsApp_{Guid.NewGuid():N}");
        try
        {
            using var envio = new EnvioWhatsApp(new WhatsAppOptions { Modo = ModoEnvioWhatsApp.Pasta, Pasta = pasta });
            Assert.True(envio.Direto);
            Assert.True((await envio.SituacaoAsync(Ct)).Conectado);

            await envio.EnviarAsync("11988887777", "Parcela vencendo", new AnexoEmail("boleto.pdf", [7]), Ct);

            var texto = Assert.Single(Directory.GetFiles(pasta, "*.txt"));
            Assert.Contains("5511988887777", Path.GetFileName(texto));
            Assert.Equal("Parcela vencendo", await File.ReadAllTextAsync(texto, Ct));
            Assert.Single(Directory.GetFiles(pasta, "*_boleto.pdf"));
        }
        finally
        {
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }

        using var link = new EnvioWhatsApp(new WhatsAppOptions());
        Assert.False(link.Direto);
        Assert.False((await link.SituacaoAsync(Ct)).Conectado);
        await Assert.ThrowsAsync<RegraNegocioException>(() => link.EnviarAsync("11988887777", "Oi", ct: Ct));
    }
}
