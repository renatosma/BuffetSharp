using BuffetSharp.Domain.Comum;

namespace BuffetSharp.WinForms.Infra;

/// <summary>Diálogos padronizados (TaskDialog do Windows).</summary>
internal static class Mensagens
{
    private const string Aplicacao = "BuffetSharp";

    /// <summary>
    /// Quando definido, recebe as mensagens no lugar do TaskDialog e devolve o botão "clicado".
    /// Usado pelos testes automatizados das telas, que não podem depender de diálogos nativos modais.
    /// </summary>
    internal static Func<TaskDialogPage, TaskDialogButton>? Interceptador { get; set; }

    public static void Info(IWin32Window? dono, string texto, string? titulo = null) =>
        Mostrar(dono, titulo ?? "Tudo certo", texto, TaskDialogIcon.Information);

    public static void Aviso(IWin32Window? dono, string texto, string? titulo = null) =>
        Mostrar(dono, titulo ?? "Atenção", texto, TaskDialogIcon.Warning);

    public static bool Confirmar(IWin32Window? dono, string texto, string titulo = "Confirmação", bool perigoso = false)
    {
        var sim = new TaskDialogButton(perigoso ? "Sim, continuar" : "Sim");
        var nao = new TaskDialogButton("Não");
        var pagina = new TaskDialogPage
        {
            Caption = Aplicacao,
            Heading = titulo,
            Text = texto,
            Icon = perigoso ? TaskDialogIcon.Warning : TaskDialogIcon.Information,
            Buttons = { sim, nao },
            DefaultButton = perigoso ? nao : sim,
            AllowCancel = true
        };
        return Exibir(dono, pagina) == sim;
    }

    public static void Erro(IWin32Window? dono, Exception ex)
    {
        switch (ex)
        {
            case ValidacaoException validacao:
                Mostrar(dono, "Verifique os campos", string.Join(Environment.NewLine,
                    validacao.Erros.Select(e => "• " + e.ErrorMessage)), TaskDialogIcon.Warning);
                break;
            case RegraNegocioException regra:
                Mostrar(dono, "Não foi possível concluir", regra.Message, TaskDialogIcon.Warning);
                break;
            case OperationCanceledException:
                break;
            default:
                var pagina = new TaskDialogPage
                {
                    Caption = Aplicacao,
                    Heading = "Ocorreu um erro inesperado",
                    Text = ex.GetBaseException().Message,
                    Icon = TaskDialogIcon.Error,
                    Buttons = { TaskDialogButton.OK },
                    Expander = new TaskDialogExpander
                    {
                        Text = ex.ToString(),
                        CollapsedButtonText = "Detalhes técnicos",
                        ExpandedButtonText = "Ocultar detalhes",
                        Position = TaskDialogExpanderPosition.AfterFootnote
                    }
                };
                Exibir(dono, pagina);
                break;
        }
    }

    private static void Mostrar(IWin32Window? dono, string titulo, string texto, TaskDialogIcon icone) =>
        Exibir(dono, new TaskDialogPage
        {
            Caption = Aplicacao,
            Heading = titulo,
            Text = texto,
            Icon = icone,
            Buttons = { TaskDialogButton.OK }
        });

    private static TaskDialogButton Exibir(IWin32Window? dono, TaskDialogPage pagina) =>
        Interceptador?.Invoke(pagina) ?? (dono is null ? TaskDialog.ShowDialog(pagina) : TaskDialog.ShowDialog(dono, pagina));
}
