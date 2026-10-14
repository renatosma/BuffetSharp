using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;

namespace BuffetSharp.WinForms.Infra;

/// <summary>Diálogos curtos reutilizáveis.</summary>
internal static class Dialogos
{
    /// <summary>Solicita um texto (ex.: motivo de cancelamento). Retorna nulo se o usuário cancelar.</summary>
    public static string? PedirTexto(IWin32Window dono, string titulo, string rotulo, string? valorInicial = null,
        bool obrigatorio = true, int linhas = 3, string? subtitulo = null)
    {
        var valor = valorInicial;
        var editor = new EditorCampos(1);
        editor.Texto(rotulo, () => valor, v => valor = v, "Valor", 500, linhas: linhas);
        using var dialogo = new FormEdicao(titulo, subtitulo, editor, () =>
        {
            if (obrigatorio && string.IsNullOrWhiteSpace(valor))
                throw new Domain.Comum.ValidacaoException(
                    [new System.ComponentModel.DataAnnotations.ValidationResult("Preencha este campo.", ["Valor"])]);
            return Task.CompletedTask;
        }, 520);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? valor : null;
    }

    /// <summary>Registro de pagamento/recebimento: data, valor e forma.</summary>
    public static PagamentoInput? PedirPagamento(IWin32Window dono, string titulo, string descricao, decimal valorSugerido,
        IReadOnlyList<ItemLookup> formas, int? formaSugerida)
    {
        var pagamento = new PagamentoInput
        {
            Data = DateOnly.FromDateTime(DateTime.Today),
            Valor = valorSugerido,
            FormaPagamentoId = formaSugerida
        };
        var editor = new EditorCampos();
        editor.Informacao("Título", () => descricao, span: 2);
        editor.Data("Data do pagamento", () => pagamento.Data, v => pagamento.Data = v ?? DateOnly.FromDateTime(DateTime.Today));
        editor.Decimal("Valor pago (R$)", () => pagamento.Valor, v => pagamento.Valor = v);
        editor.Lista("Forma de pagamento", formas, () => pagamento.FormaPagamentoId, v => pagamento.FormaPagamentoId = v,
            permitirVazio: true, span: 2);

        using var dialogo = new FormEdicao(titulo, "Informe juros/multa ou desconto ajustando o valor pago.", editor,
            () => Task.CompletedTask, 520);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? pagamento : null;
    }
}
