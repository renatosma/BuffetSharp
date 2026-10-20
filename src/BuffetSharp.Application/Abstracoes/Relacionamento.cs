namespace BuffetSharp.Application.Abstracoes;

/// <summary>Lembretes do dia (seção "Lembretes" do appsettings.json).</summary>
public sealed class LembretesOptions
{
    public const string Secao = "Lembretes";

    /// <summary>Avisar das propostas enviadas que perdem a validade nos próximos N dias.</summary>
    public int DiasOrcamento { get; set; } = 3;

    /// <summary>Avisar das parcelas a receber que vencem nos próximos N dias.</summary>
    public int DiasParcela { get; set; } = 3;

    /// <summary>Avisar das contas a pagar que vencem nos próximos N dias (e das vencidas).</summary>
    public int DiasContaPagar { get; set; } = 3;

    /// <summary>
    /// Envia sozinho, depois do login, os e-mails de lembrete ao cliente (parcela a vencer, degustação, aniversário) para
    /// quem tem e-mail. Desligado por padrão: com ele desligado, o envio é feito pela tela de lembretes.
    /// </summary>
    public bool EmailAutomatico { get; set; }
}
