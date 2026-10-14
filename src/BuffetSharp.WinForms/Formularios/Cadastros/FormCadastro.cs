using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>
/// Descreve um cadastro simples/intermediário: colunas da listagem, campos do formulário e ações extras.
/// Uma única tela genérica (<see cref="FormCadastro{T}"/>) atende todos os cadastros declarados.
/// </summary>
internal sealed class DefinicaoCadastro<T> where T : Entidade, new()
{
    public required string Titulo { get; init; }
    public required string Singular { get; init; }
    public string? Descricao { get; init; }
    public Func<IQueryable<T>, IQueryable<T>>? Consulta { get; init; }
    public required Action<Grade<T>> Colunas { get; init; }
    public required Func<T, string> TextoBusca { get; init; }
    public required Func<EditorCampos, T, IConsultasLookup, Task> Campos { get; init; }
    public required Func<T, string> Nome { get; init; }
    public int ColunasEditor { get; init; } = 2;
    public int LarguraEditor { get; init; } = 680;
    public IReadOnlyList<AcaoCadastro<T>> Acoes { get; init; } = [];
    public Func<IReadOnlyList<T>, string>? Rodape { get; init; }
}

internal sealed record AcaoCadastro<T>(string Texto, char Icone, Func<T, ContextoCadastro, Task> Executar);

internal sealed record ContextoCadastro(IWin32Window Dono, Navegador Navegador, Func<Task> Recarregar);

internal sealed class FormCadastro<T> : FormListagem<T> where T : Entidade, new()
{
    private readonly DefinicaoCadastro<T> _definicao;
    private readonly ICadastroService<T> _servico;
    private readonly IConsultasLookup _lookups;
    private readonly Navegador _navegador;
    private readonly CheckBox? _mostrarInativos;

    public FormCadastro(DefinicaoCadastro<T> definicao, ICadastroService<T> servico, IConsultasLookup lookups, Navegador navegador)
        : base(definicao.Titulo, definicao.Descricao)
    {
        _definicao = definicao;
        _servico = servico;
        _lookups = lookups;
        _navegador = navegador;

        definicao.Colunas(Grade);
        Grade.Busca(definicao.TextoBusca);
        if (typeof(IAtivavel).IsAssignableFrom(typeof(T)))
        {
            Grade.CorDaLinha(item => ((IAtivavel)item).Ativo ? null : Tema.Neutro);
            _mostrarInativos = new CheckBox { Text = "Mostrar inativos", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            _mostrarInativos.CheckedChanged += async (_, _) => await RecarregarAsync();
            Filtros.Controls.Add(_mostrarInativos);
        }

        Botao("Novo", Icones.Adicionar, NovoAsync, $"Cadastrar {definicao.Singular}", destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } item ? EditarAsync(item) : Task.CompletedTask);
        Botao("Excluir", Icones.Excluir, ExcluirAsync);

        if (navegador.Servico<ISessaoUsuario>().Usuario?.EhAdministrador == true)
        {
            Botao("Alterações", Icones.Escudo, () =>
            {
                if (Selecionado() is { } item)
                    navegador.Abrir($"auditoria:{typeof(T).Name}:{item.Id}", () => navegador.Criar<Seguranca.FormAuditoria>(
                        new Seguranca.FiltroAuditoriaInicial($"Alterações - {definicao.Nome(item)}", Entidade: typeof(T).Name,
                            RegistroId: item.Id.ToString())));
                return Task.CompletedTask;
            }, "Histórico de alterações do registro selecionado (auditoria)");
        }

        if (definicao.Acoes.Count > 0)
        {
            Separador();
            foreach (var acao in definicao.Acoes)
            {
                Botao(acao.Texto, acao.Icone, () => Selecionado() is { } item
                    ? acao.Executar(item, new ContextoCadastro(this, _navegador, RecarregarAsync))
                    : Task.CompletedTask);
            }
        }
    }

    protected override async Task CarregarAsync()
    {
        var itens = await _servico.ListarAsync(_definicao.Consulta);
        if (_mostrarInativos is { Checked: false })
            itens = itens.Where(i => ((IAtivavel)i).Ativo).ToList();
        Exibir(itens);
    }

    protected override string TextoRodape(IReadOnlyList<T> visiveis) =>
        _definicao.Rodape?.Invoke(visiveis) ?? base.TextoRodape(visiveis);

    protected override Task AoAbrirAsync(T item) => EditarAsync(item);

    private Task NovoAsync() => AbrirEditorAsync(new T(), $"Novo(a) {_definicao.Singular}");

    private async Task EditarAsync(T item)
    {
        // Recarrega do banco sem navegações: somente o cadastro em edição é gravado.
        var entidade = await _servico.ObterAsync(item.Id)
                       ?? throw new RegraNegocioException("O registro não existe mais. Atualize a lista.");
        await AbrirEditorAsync(entidade, $"Editar {_definicao.Singular}");
    }

    private async Task AbrirEditorAsync(T entidade, string titulo)
    {
        var editor = new EditorCampos(_definicao.ColunasEditor);
        await _definicao.Campos(editor, entidade, _lookups);

        var subtitulo = entidade.EhNovo ? null : $"{_definicao.Nome(entidade)}  •  código {entidade.Id}";
        using var dialogo = new FormEdicao(titulo, subtitulo, editor, () => _servico.SalvarAsync(entidade), _definicao.LarguraEditor);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;

        await CarregarAsync();
        Grade.Selecionar(i => i.Id == entidade.Id);
    }

    private async Task ExcluirAsync()
    {
        if (Selecionado() is not { } item)
            return;
        if (!Mensagens.Confirmar(this, $"Excluir \"{_definicao.Nome(item)}\"? Esta operação não pode ser desfeita.", "Excluir", perigoso: true))
            return;
        await _servico.ExcluirAsync(item.Id);
        await CarregarAsync();
    }
}
