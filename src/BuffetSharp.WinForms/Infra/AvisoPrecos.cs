using BuffetSharp.Application.Servicos;

namespace BuffetSharp.WinForms.Infra;

/// <summary>Confirmação antes de gravar uma compra com preço acima do último pago além do limite (Buffet:LimiteAltaPrecoCompra).</summary>
internal static class AvisoPrecos
{
    /// <summary>Sem avisos, segue; com avisos, pergunta e, se o usuário desistir, cancela a gravação (o diálogo continua aberto).</summary>
    public static async Task ConfirmarAsync(Task<IReadOnlyList<AvisoPreco>> consulta)
    {
        var avisos = await consulta;
        if (avisos.Count == 0)
            return;
        var texto = (avisos.Count == 1 ? "Este preço subiu" : $"Estes {avisos.Count} preços subiram") +
                    " acima do limite sobre o último pago:\n\n" + string.Join("\n", avisos.Select(a => "• " + a.Texto)) +
                    "\n\nGravar a compra mesmo assim?";
        if (!Mensagens.Confirmar(Form.ActiveForm, texto, "Preço acima do último pago"))
            throw new OperationCanceledException("Compra não gravada: preço acima do último pago.");
    }
}
