namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Base de todas as telas: aparência padrão e execução assíncrona com cursor de espera
/// e tratamento de erros centralizado.
/// </summary>
internal class FormBase : Form
{
    private int _operacoesEmAndamento;

    public FormBase()
    {
        Font = Tema.Fonte;
        BackColor = Tema.Fundo;
        ForeColor = Tema.Texto;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        KeyPreview = true;
        ShowIcon = false;
    }

    /// <summary>Verdadeiro enquanto houver operação assíncrona em execução.</summary>
    protected bool Ocupado => _operacoesEmAndamento > 0;

    /// <param name="cursorEspera">Falso para ações que abrem diálogos (o cursor de espera ficaria sobre a tela durante o diálogo).</param>
    protected async Task<bool> ExecutarAsync(Func<Task> acao, Control? origem = null, bool cursorEspera = true)
    {
        _operacoesEmAndamento++;
        if (cursorEspera)
            UseWaitCursor = true;
        if (origem is not null)
            origem.Enabled = false;
        try
        {
            // Ações de botão cedem o controle antes de abrir diálogos: o clique retorna imediatamente
            // (melhor para leitores de tela e automação de interface, que aguardam o fim do clique).
            if (!cursorEspera)
                await Task.Yield();
            await acao();
            return true;
        }
        catch (Exception ex)
        {
            Mensagens.Erro(this, ex);
            return false;
        }
        finally
        {
            _operacoesEmAndamento--;
            if (_operacoesEmAndamento == 0)
                UseWaitCursor = false;
            if (origem is not null && !origem.IsDisposed)
                origem.Enabled = true;
        }
    }

    /// <summary>Converte pixels lógicos (96 DPI) para o DPI atual da tela.</summary>
    protected int Px(int valor) => LogicalToDeviceUnits(valor);
}
