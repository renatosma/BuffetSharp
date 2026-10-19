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
}
