using Microsoft.Extensions.DependencyInjection;

namespace BuffetSharp.WinForms.Infra;

/// <summary>
/// Abre telas na área de abas da janela principal (uma aba por tela/registro) e cria formulários
/// via injeção de dependência, permitindo parâmetros extras (ex.: Id do evento).
/// </summary>
internal sealed class Navegador(IServiceProvider servicos)
{
    private readonly Dictionary<string, TabPage> _abas = [];
    private TabControl? _area;
    private TabPage? _abrindo;

    public void Configurar(TabControl area)
    {
        _area = area;

        // Voltar para uma lista aberta antes (pelo menu ou clicando na aba) traz os dados de agora: o que foi feito em
        // outra tela (ex.: o recebimento de um pedido) aparece sem precisar apertar F5.
        area.Selected += async (_, e) =>
        {
            if (e.TabPage is { } aba && aba != _abrindo && aba.Tag is FormListagem lista)
                await lista.AtualizarAoVoltarAsync();
        };
    }

    /// <summary>Resolve um serviço da aplicação (para telas auxiliares sem construtor injetado).</summary>
    public T Servico<T>() where T : notnull => servicos.GetRequiredService<T>();

    /// <summary>Cria um formulário resolvendo dependências do construtor (parâmetros extras por posição/tipo).</summary>
    public TForm Criar<TForm>(params object[] parametros) where TForm : Form =>
        ActivatorUtilities.CreateInstance<TForm>(servicos, parametros);

    public void Abrir<TForm>(string? chave = null, params object[] parametros) where TForm : Form =>
        Abrir(chave ?? typeof(TForm).FullName!, () => Criar<TForm>(parametros));

    public void Abrir(string chave, Func<Form> criar)
    {
        if (_area is null)
            throw new InvalidOperationException("A área de navegação não foi configurada.");

        if (_abas.TryGetValue(chave, out var existente) && !existente.IsDisposed)
        {
            _area.SelectedTab = existente;
            return;
        }

        var formulario = criar();
        formulario.TopLevel = false;
        formulario.FormBorderStyle = FormBorderStyle.None;
        formulario.Dock = DockStyle.Fill;

        var aba = new TabPage(formulario.Text + "      ") { BackColor = Tema.Fundo, Tag = formulario };
        aba.Controls.Add(formulario);
        _area.TabPages.Add(aba);
        _abas[chave] = aba;
        formulario.TextChanged += (_, _) => aba.Text = formulario.Text + "      ";
        formulario.FormClosed += (_, _) => Fechar(aba);
        formulario.Show();
        _abrindo = aba; // acabou de carregar: não recarrega ao selecionar
        _area.SelectedTab = aba;
        _abrindo = null;
    }

    public void Fechar(TabPage aba)
    {
        if (_area is null)
            return;
        var chave = _abas.FirstOrDefault(p => p.Value == aba).Key;
        if (chave is not null)
            _abas.Remove(chave);

        var indice = _area.TabPages.IndexOf(aba);
        _area.TabPages.Remove(aba);
        if (aba.Tag is Form { IsDisposed: false } formulario)
            formulario.Dispose();
        aba.Dispose();
        if (_area.TabPages.Count > 0)
            _area.SelectedIndex = Math.Max(0, indice - 1);
    }

    public void FecharTodas()
    {
        if (_area is null)
            return;
        foreach (var aba in _area.TabPages.Cast<TabPage>().ToList())
            Fechar(aba);
    }
}
