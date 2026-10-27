namespace BuffetSharp.Application.Abstracoes;

public enum ModoEnvioWhatsApp
{
    /// <summary>Abre o WhatsApp (aplicativo ou WhatsApp Web) com a mensagem pronta; quem envia é o usuário.</summary>
    Link,

    /// <summary>Grava cada mensagem como arquivo .txt numa pasta (para treinar e testar, sem enviar nada).</summary>
    Pasta,

    /// <summary>Envia direto pelo número do buffet conectado na Z-API.</summary>
    ZApi
}

/// <summary>
/// WhatsApp (seção "WhatsApp" do appsettings). A instância e os tokens da Z-API ficam na máquina (appsettings.Local.json),
/// fora do banco, como a senha do e-mail.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string Secao = "WhatsApp";

    public ModoEnvioWhatsApp Modo { get; set; } = ModoEnvioWhatsApp.Link;

    /// <summary>Pasta de saída no modo <see cref="ModoEnvioWhatsApp.Pasta"/> (relativa à pasta do sistema).</summary>
    public string Pasta { get; set; } = "WhatsAppEnviados";

    /// <summary>Id da instância na Z-API.</summary>
    public string? Instancia { get; set; }

    /// <summary>Token da instância na Z-API.</summary>
    public string? Token { get; set; }

    /// <summary>Token de segurança da conta Z-API (cabeçalho Client-Token), quando ativado no painel.</summary>
    public string? ClientToken { get; set; }

    /// <summary>Os envios automáticos só saem a partir desta hora...</summary>
    public int HorarioInicio { get; set; } = 8;

    /// <summary>...e até esta hora (exclusive), para não incomodar o cliente à noite.</summary>
    public int HorarioFim { get; set; } = 20;

    public int TempoLimiteSegundos { get; set; } = 30;

    public bool DentroDoHorario(DateTime agora) => agora.Hour >= HorarioInicio && agora.Hour < HorarioFim;
}

/// <summary>Situação da conexão do WhatsApp do buffet.</summary>
public sealed record SituacaoWhatsApp(bool Conectado, string Descricao);

/// <summary>Envio de WhatsApp pelo número do buffet (Z-API) ou para uma pasta local, conforme <see cref="WhatsAppOptions"/>.</summary>
public interface IEnvioWhatsApp
{
    /// <summary>Envia sem abrir o WhatsApp (Z-API ou pasta). Falso no modo Link, em que o usuário envia pelo aplicativo.</summary>
    bool Direto { get; }

    /// <summary>Descreve o destino atual (ex.: "Z-API (instância 3C2...)" ou a pasta de saída).</summary>
    string Destino { get; }

    /// <param name="telefone">Celular com DDD (com ou sem o 55 e a pontuação).</param>
    /// <param name="anexo">Documento enviado depois do texto (ex.: a proposta em PDF).</param>
    Task EnviarAsync(string telefone, string texto, AnexoEmail? anexo = null, CancellationToken ct = default);

    Task<SituacaoWhatsApp> SituacaoAsync(CancellationToken ct = default);
}
