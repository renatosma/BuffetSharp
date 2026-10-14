using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Formularios;
using BuffetSharp.WinForms.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace BuffetSharp.Tests.Telas.Infra;

/// <summary>Mensagem (TaskDialog) que a tela tentou exibir.</summary>
public sealed record MensagemExibida(string Titulo, string Texto, bool Erro);

/// <summary>
/// "Robô" que opera as telas reais do aplicativo dentro do processo de teste: localiza controles pelo texto ou
/// pelo rótulo do campo, clica, preenche, trata diálogos modais e espera as operações assíncronas terminarem.
/// <para>
/// As janelas são criadas em uma área de trabalho (desktop) oculta do Windows: nada aparece na tela de quem
/// roda os testes e o foco não é roubado. As TaskDialogs são interceptadas e registradas em <see cref="Mensagens"/>.
/// </para>
/// </summary>
internal sealed class Robo : IDisposable
{
    private static readonly IntPtr DesktopOculto = CriarDesktopOculto();
    private readonly List<(Type Tipo, Action<Form> Interagir)> _dialogosPendentes = [];
    private readonly HashSet<Form> _dialogosTratados = [];
    private readonly Dictionary<Form, DateTime> _dialogosSemTratamento = [];
    private readonly System.Windows.Forms.Timer _vigia = new() { Interval = 25 };

    private Robo(ServiceProvider servicos)
    {
        Servicos = servicos;
        _vigia.Tick += (_, _) => TratarDialogos();
        _vigia.Start();
        WinForms.Infra.Mensagens.Interceptador = pagina =>
        {
            var erro = pagina.Icon == TaskDialogIcon.Error || pagina.Heading is "Não foi possível concluir" or "Verifique os campos";
            Mensagens.Add(new MensagemExibida(pagina.Heading ?? "", pagina.Text ?? "", erro));
            return RespostaConfirmacao is { } escolha && pagina.Buttons.Count > 1
                ? pagina.Buttons.First(b => b.Text == escolha)
                : pagina.Buttons.First();
        };
    }

    public ServiceProvider Servicos { get; }

    public List<MensagemExibida> Mensagens { get; } = [];

    /// <summary>Diálogos modais que abriram sem o teste esperar por eles (foram fechados pelo robô).</summary>
    public List<string> DialogosInesperados { get; } = [];

    /// <summary>Texto do botão escolhido nas confirmações (padrão: o primeiro, "Sim").</summary>
    public string? RespostaConfirmacao { get; set; }

    public T Servico<T>() where T : notnull => Servicos.GetRequiredService<T>();

    // ============================== Execução ==============================

    /// <summary>
    /// Executa o teste em uma thread STA, no desktop oculto, dentro de um laço de mensagens do Windows Forms
    /// (<see cref="System.Windows.Forms.Application.Run(ApplicationContext)"/>), como o aplicativo real: assim as
    /// continuações assíncronas das telas voltam para a thread da interface mesmo depois de diálogos modais.
    /// Falha se alguma tela exibir uma mensagem de erro não esperada.
    /// </summary>
    public static void Executar(AmbienteTelas ambiente, Action<Robo> teste, bool permitirErros = false)
    {
        ExceptionDispatchInfo? falha = null;
        var thread = new Thread(() =>
        {
            SetThreadDesktop(DesktopOculto);
            using var laco = new ApplicationContext();
            using var gatilho = new Control();
            gatilho.CreateControl();
            gatilho.BeginInvoke(() => ExecutarNoLaco(ambiente, teste, permitirErros, laco, e => falha = e));
            System.Windows.Forms.Application.Run(laco);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(3)))
            throw new TimeoutException("O teste de tela não terminou em 3 minutos.");
        falha?.Throw();
    }

    private static void ExecutarNoLaco(AmbienteTelas ambiente, Action<Robo> teste, bool permitirErros, ApplicationContext laco,
        Action<ExceptionDispatchInfo> registrarFalha)
    {
            var servicos = ambiente.CriarServicos();
            var robo = new Robo(servicos);
            try
            {
                teste(robo);
                if (robo.DialogosInesperados.Count > 0)
                    throw new InvalidOperationException($"Diálogo(s) inesperado(s): {string.Join(", ", robo.DialogosInesperados)}");
                if (!permitirErros && robo.Mensagens.FirstOrDefault(m => m.Erro) is { } erro)
                    throw new InvalidOperationException($"A tela exibiu um erro: {erro.Titulo} - {erro.Texto}");
            }
            catch (Exception ex)
            {
                registrarFalha(ExceptionDispatchInfo.Capture(ex));
            }
            finally
            {
                foreach (var form in System.Windows.Forms.Application.OpenForms.Cast<Form>().ToList())
                    form.Dispose();
                robo.Dispose();
                servicos.Dispose();
                laco.ExitThread();
            }
    }

    public void Dispose()
    {
        _vigia.Dispose();
        WinForms.Infra.Mensagens.Interceptador = null;
    }

    /// <summary>Processa a fila de mensagens até a condição ser verdadeira (operações assíncronas das telas).</summary>
    public void Esperar(Func<bool> condicao, string descricao, int milissegundos = 20_000)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(milissegundos);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"Tempo esgotado esperando: {descricao}. Mensagens: " +
                                           string.Join(" | ", Mensagens.Select(m => $"{m.Titulo}: {m.Texto}")));
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
        System.Windows.Forms.Application.DoEvents();
    }

    /// <summary>
    /// Chama um serviço assíncrono a partir do teste (preparação ou conferência de dados). Roda fora da thread da
    /// interface e espera processando mensagens: bloquear a thread com .Result causaria deadlock.
    /// </summary>
    public T Rodar<T>(Func<Task<T>> operacao)
    {
        var tarefa = Task.Run(operacao);
        Esperar(() => tarefa.IsCompleted, "operação de serviço");
        return tarefa.GetAwaiter().GetResult();
    }

    public void Rodar(Func<Task> operacao) => Rodar(async () =>
    {
        await operacao();
        return true;
    });

    /// <summary>Deixa a fila andar por um tempo (cargas assíncronas sem condição observável simples).</summary>
    public void Aguardar(int milissegundos = 300)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(milissegundos);
        while (DateTime.UtcNow < limite)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    // ============================== Sessão e janela principal ==============================

    /// <summary>Faz login pela tela de login real e abre a janela principal.</summary>
    public FormPrincipal Entrar(string login = "admin", string senha = "Admin@123")
    {
        using var formLogin = ActivatorUtilities.CreateInstance<FormLogin>(Servicos);
        formLogin.StartPosition = FormStartPosition.Manual;
        formLogin.Show();
        Esperar(() => formLogin.Visible, "tela de login");
        Digitar(Encontrar<TextBox>(formLogin, t => !t.UseSystemPasswordChar), login);
        Digitar(Encontrar<TextBox>(formLogin, t => t.UseSystemPasswordChar), senha);
        Clicar(formLogin, "Entrar");
        Esperar(() => formLogin.DialogResult == DialogResult.OK, $"login de {login}");
        formLogin.Hide();

        var principal = ActivatorUtilities.CreateInstance<FormPrincipal>(Servicos);
        principal.StartPosition = FormStartPosition.Manual;
        principal.WindowState = FormWindowState.Normal;
        principal.Bounds = new Rectangle(0, 0, 1500, 950);
        principal.Show();
        Esperar(() => AbaAtual(principal) is { } aba && aba.Text == "Painel" && Carregado(aba), "painel inicial");
        return principal;
    }

    /// <summary>Formulário exibido na aba selecionada da janela principal.</summary>
    public static Form? AbaAtual(FormPrincipal principal) =>
        Encontrar<TabControl>(principal).SelectedTab?.Tag as Form;

    /// <summary>Clica no item do menu lateral e espera a tela abrir e carregar.</summary>
    public T AbrirMenu<T>(FormPrincipal principal, string item) where T : Form
    {
        var botao = Encontrar<Button>(principal, b => b.Text.Trim() == item);
        botao.PerformClick();
        Esperar(() => AbaAtual(principal) is T tela && Carregado(tela), $"tela {typeof(T).Name} pelo menu \"{item}\"");
        return (T)AbaAtual(principal)!;
    }

    /// <summary>Considera a tela carregada quando não há operação assíncrona em andamento (cursor de espera).</summary>
    public static bool Carregado(Form tela) => tela.IsHandleCreated && !tela.UseWaitCursor;

    // ============================== Localização de controles ==============================

    public static IEnumerable<Control> Descendentes(Control raiz)
    {
        foreach (Control filho in raiz.Controls)
        {
            yield return filho;
            foreach (var neto in Descendentes(filho))
                yield return neto;
        }
    }

    public static T Encontrar<T>(Control raiz, Func<T, bool>? filtro = null) where T : Control =>
        Descendentes(raiz).OfType<T>().FirstOrDefault(c => filtro?.Invoke(c) ?? true)
        ?? throw new InvalidOperationException($"{typeof(T).Name} não encontrado em {raiz.Text}.");

    /// <summary>Controle de um campo do <see cref="EditorCampos"/>, localizado pelo rótulo.</summary>
    public static T Campo<T>(Control raiz, string rotulo) where T : Control
    {
        foreach (var bloco in Descendentes(raiz).OfType<TableLayoutPanel>())
        {
            if (bloco.GetControlFromPosition(0, 0) is Label { } label && label.Text == rotulo &&
                bloco.GetControlFromPosition(0, 1) is { } controle)
                return controle as T ?? Descendentes(controle).OfType<T>().First();
        }
        if (typeof(T) == typeof(CheckBox) && Descendentes(raiz).OfType<CheckBox>().FirstOrDefault(c => c.Text == rotulo) is T marcador)
            return marcador;
        throw new InvalidOperationException($"Campo \"{rotulo}\" ({typeof(T).Name}) não encontrado em {raiz.Text}.");
    }

    /// <summary>Lê um campo privado da tela (ex.: o rótulo do total do orçamento), inclusive de classes base.</summary>
    public static T Interno<T>(object tela, string campo)
    {
        for (var tipo = tela.GetType(); tipo is not null; tipo = tipo.BaseType)
        {
            if (tipo.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) is { } info)
                return (T)info.GetValue(tela)!;
        }
        throw new InvalidOperationException($"Campo {campo} não encontrado em {tela.GetType().Name}.");
    }

    /// <summary>Grade principal de uma tela de listagem.</summary>
    public static DataGridView Grade(Form tela) => Interno<DataGridView>(tela, "GradeControle");

    /// <summary>Seleciona a aba interna (ex.: "Materiais" na central do evento).</summary>
    public void SelecionarAba(Control raiz, string titulo)
    {
        var abas = Descendentes(raiz).OfType<TabControl>().First(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == titulo));
        abas.SelectedTab = abas.TabPages.Cast<TabPage>().First(p => p.Text == titulo);
        System.Windows.Forms.Application.DoEvents();
    }

    // ============================== Ações ==============================

    /// <summary>
    /// Clica em um botão ou item de barra de ferramentas pelo texto exibido. Se ele estiver desabilitado por uma
    /// operação em andamento, espera alguns segundos (como o usuário faria) antes de falhar.
    /// </summary>
    public void Clicar(Control raiz, string texto)
    {
        var botao = Descendentes(raiz).OfType<Button>().FirstOrDefault(b => b.Visible && b.Text.Trim() == texto);
        if (botao is not null)
        {
            EsperarHabilitado(() => botao.Enabled, texto);
            botao.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            return;
        }

        var item = Descendentes(raiz).OfType<ToolStrip>().SelectMany(t => t.Items.Cast<ToolStripItem>())
            .FirstOrDefault(i => i.Text?.Trim() == texto)
            ?? throw new InvalidOperationException($"Botão \"{texto}\" não encontrado em {raiz.Text}.");
        EsperarHabilitado(() => item.Enabled, texto);
        item.PerformClick();
        System.Windows.Forms.Application.DoEvents();
    }

    private void EsperarHabilitado(Func<bool> habilitado, string texto)
    {
        try
        {
            Esperar(habilitado, $"botão \"{texto}\" habilitado", 5_000);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"O botão \"{texto}\" está desabilitado.");
        }
    }

    public void Digitar(Control controle, string texto)
    {
        controle.Focus();
        controle.Text = texto;
        System.Windows.Forms.Application.DoEvents();
    }

    public void Preencher(Control raiz, string rotulo, string texto) => Digitar(Campo<TextBoxBase>(raiz, rotulo), texto);

    public void Preencher(Control raiz, string rotulo, decimal valor)
    {
        var numero = Campo<NumericUpDown>(raiz, rotulo);
        numero.Value = valor;
        System.Windows.Forms.Application.DoEvents();
    }

    /// <summary>Seleciona o item do combo cujo texto contém <paramref name="texto"/> (ou o primeiro item válido).</summary>
    public void Escolher(ComboBox combo, string? texto = null)
    {
        var indice = Enumerable.Range(0, combo.Items.Count)
            .FirstOrDefault(i => (combo.GetItemText(combo.Items[i]) ?? "") is var nome && nome != "(nenhum)" && !nome.StartsWith('(') &&
                                 (texto is null || nome.Contains(texto, StringComparison.CurrentCultureIgnoreCase)), -1);
        Assert.True(indice >= 0, $"Opção \"{texto}\" não encontrada no combo.");
        combo.SelectedIndex = indice;
        System.Windows.Forms.Application.DoEvents();
    }

    public void Escolher(Control raiz, string rotulo, string? texto = null) => Escolher(Campo<ComboBox>(raiz, rotulo), texto);

    /// <summary>Seleciona a linha da grade cujo item satisfaz o critério.</summary>
    public void SelecionarLinha<T>(DataGridView grade, Func<T, bool> criterio) where T : class
    {
        var linha = grade.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag is T item && criterio(item))
                    ?? throw new InvalidOperationException($"Nenhuma linha de {typeof(T).Name} atende ao critério.");
        grade.CurrentCell = linha.Cells.Cast<DataGridViewCell>().First(c => c.Visible);
        System.Windows.Forms.Application.DoEvents();
    }

    public static IReadOnlyList<T> Linhas<T>(DataGridView grade) where T : class =>
        grade.Rows.Cast<DataGridViewRow>().Select(r => (T)r.Tag!).ToList();

    /// <summary>Desenha um traço (rabisco) no controle enviando mensagens de mouse à janela, sem mover o cursor real.</summary>
    public void Desenhar(Control controle)
    {
        const int botaoEsquerdo = 0x0201, mover = 0x0200, soltar = 0x0202, mkLButton = 1;
        static IntPtr Ponto(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));
        SendMessage(controle.Handle, botaoEsquerdo, mkLButton, Ponto(20, 60));
        for (var x = 20; x <= 220; x += 10)
            SendMessage(controle.Handle, mover, mkLButton, Ponto(x, 60 + (int)(25 * Math.Sin(x / 15.0))));
        SendMessage(controle.Handle, soltar, 0, Ponto(220, 60));
        System.Windows.Forms.Application.DoEvents();
    }

    // ============================== Diálogos modais ==============================

    /// <summary>
    /// Executa <paramref name="abrir"/> (que abre um diálogo modal do tipo <typeparamref name="T"/>), interage com o
    /// diálogo assim que ele aparece e espera ele fechar.
    /// </summary>
    public void Dialogo<T>(Action abrir, Action<T> interagir, string descricao) where T : Form
    {
        T? aberto = null;
        _dialogosPendentes.Add((typeof(T), form =>
        {
            aberto = (T)form;
            interagir(aberto);
        }));
        abrir();
        Esperar(() => aberto is not null, $"abrir o diálogo: {descricao}");
        Esperar(() => aberto!.IsDisposed || !aberto.Visible, $"fechar o diálogo: {descricao}");
    }

    private void TratarDialogos()
    {
        FecharDialogosEsquecidos();
        if (_dialogosPendentes.Count == 0)
            return;
        var (tipo, interagir) = _dialogosPendentes[0];
        var dialogo = System.Windows.Forms.Application.OpenForms.Cast<Form>()
            .LastOrDefault(f => tipo.IsInstanceOfType(f) && f.Visible && f.Modal && !_dialogosTratados.Contains(f));
        if (dialogo is null || !Carregado(dialogo))
            return;

        _dialogosPendentes.RemoveAt(0);
        _dialogosTratados.Add(dialogo);
        _vigia.Stop();
        try
        {
            interagir(dialogo);
        }
        finally
        {
            _vigia.Start();
        }
    }

    /// <summary>
    /// Um diálogo modal que ninguém trata travaria o teste para sempre: depois de alguns segundos ele é
    /// registrado em <see cref="DialogosInesperados"/> e fechado.
    /// </summary>
    private void FecharDialogosEsquecidos()
    {
        var abertos = System.Windows.Forms.Application.OpenForms.Cast<Form>()
            .Where(f => f.Visible && f.Modal && !_dialogosTratados.Contains(f) &&
                        !_dialogosPendentes.Any(p => p.Tipo.IsInstanceOfType(f)))
            .ToList();
        foreach (var form in abertos)
        {
            if (!_dialogosSemTratamento.TryGetValue(form, out var desde))
            {
                _dialogosSemTratamento[form] = DateTime.UtcNow;
                continue;
            }
            if (DateTime.UtcNow - desde < TimeSpan.FromSeconds(8))
                continue;
            DialogosInesperados.Add(form.Text);
            _dialogosTratados.Add(form);
            form.DialogResult = DialogResult.Cancel;
        }
    }

    // ============================== Win32 ==============================

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string nome, IntPtr dispositivo, IntPtr modo, int flags, uint acesso, IntPtr seguranca);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr desktop);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr janela, int mensagem, int wParam, IntPtr lParam);

    private static IntPtr CriarDesktopOculto()
    {
        const uint acessoTotal = 0x10000000; // GENERIC_ALL
        var desktop = CreateDesktop("BuffetSharp.TestesTelas", IntPtr.Zero, IntPtr.Zero, 0, acessoTotal, IntPtr.Zero);
        if (desktop == IntPtr.Zero)
            throw new InvalidOperationException($"Não foi possível criar o desktop oculto (erro {Marshal.GetLastWin32Error()}).");
        return desktop;
    }
}
