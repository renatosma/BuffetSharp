using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Seguranca;

/// <summary>
/// Abre a auditoria já filtrada: por texto (ex.: tudo que mudou em um evento e nos itens dele, pelo código
/// "EVT-00012") ou por um registro específico (entidade + chave).
/// </summary>
internal sealed record FiltroAuditoriaInicial(string Titulo, string? Texto = null, string? Entidade = null, string? RegistroId = null);

/// <summary>
/// Log de auditoria: quem incluiu, alterou ou excluiu cada registro e o valor anterior/novo de cada campo.
/// Os registros são gerados automaticamente na gravação; esta tela é somente consulta.
/// </summary>
internal sealed class FormAuditoria : FormListagem<RegistroAuditoriaResumo>
{
    private readonly IConsultasAuditoria _consultas;
    private readonly FiltroAuditoriaInicial? _filtroInicial;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _usuario = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _entidade = new() { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _acao = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Grade<AlteracaoCampo> _campos = new();
    private readonly Label _tituloDetalhe = new() { Dock = DockStyle.Top, Height = 26, Font = Tema.FonteNegrito };

    private sealed record OpcaoTexto(string? Valor, string Texto)
    {
        public override string ToString() => Texto;
    }

    public FormAuditoria(IConsultasAuditoria consultas)
        : this(consultas, null)
    {
    }

    public FormAuditoria(IConsultasAuditoria consultas, FiltroAuditoriaInicial? filtroInicial)
        : base(filtroInicial?.Titulo ?? "Auditoria",
            filtroInicial is null
                ? "Histórico automático de inclusões, alterações e exclusões: quem fez, quando e o valor anterior e o novo de cada campo."
                : filtroInicial.Texto is not null
                    ? $"Registros relacionados a {filtroInicial.Texto} (o próprio registro e os itens vinculados a ele)."
                    : "Histórico de alterações do registro selecionado.")
    {
        _consultas = consultas;
        _filtroInicial = filtroInicial;

        _de = FiltroData("De", DateTime.Today.AddDays(filtroInicial is null ? -30 : -3650), opcional: false);
        _ate = FiltroData("Até", DateTime.Today, opcional: false);
        Filtro("Usuário", _usuario);
        Filtro("Registro", _entidade);
        Filtro("Ação", _acao);
        _acao.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<AcaoAuditoria>()).ToList();
        foreach (var data in new[] { _de, _ate })
            data.ValueChanged += async (_, _) => await RecarregarAsync();
        foreach (var combo in new[] { _usuario, _entidade, _acao })
            combo.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Grade.Data("Data/hora", r => r.DataHora, "dd/MM/yyyy HH:mm:ss", 26)
            .Texto("Usuário", r => r.Usuario, 18)
            .Status("Ação", r => r.Acao, 16)
            .Texto("Registro", r => Textos.Entidade(r.Entidade), 26)
            .Texto("Identificação", r => r.Descricao ?? $"#{r.RegistroId}", 55)
            .Texto("Campos", r => Resumo(r), 70)
            .Busca(r => $"{r.Usuario} {Textos.Entidade(r.Entidade)} {r.Descricao} {r.Alteracoes}");
        GradeControle.SelectionChanged += (_, _) => ExibirDetalhe(Grade.Selecionado);

        _campos.Texto("Campo", c => Textos.Campo(c.Campo), 30)
            .Texto("Valor anterior", c => c.Anterior ?? "—", 50, c => c.Anterior is null ? Tema.TextoSuave : Tema.Perigo)
            .Texto("Valor novo", c => c.Novo ?? "—", 50, c => c.Novo is null ? Tema.TextoSuave : Tema.Sucesso);

        var cartao = Tema.Cartao(new Padding(10));
        cartao.Dock = DockStyle.Fill;
        cartao.Controls.Add(_campos.Controle);
        cartao.Controls.Add(_tituloDetalhe);
        var detalhe = new Panel { Dock = DockStyle.Bottom, Height = 230, Padding = new Padding(20, 4, 20, 4) };
        detalhe.Controls.Add(cartao);
        Controls.Add(detalhe);
        Controls.SetChildIndex(detalhe, 1); // acima do rodapé, abaixo da grade
    }

    private static string Resumo(RegistroAuditoriaResumo registro)
    {
        var campos = registro.Campos;
        if (campos.Count == 0)
            return "";
        var nomes = string.Join(", ", campos.Take(4).Select(c => Textos.Campo(c.Campo)));
        return $"{campos.Count} campo(s): {nomes}{(campos.Count > 4 ? "…" : "")}";
    }

    private void ExibirDetalhe(RegistroAuditoriaResumo? registro)
    {
        if (registro is null)
        {
            _tituloDetalhe.Text = "Selecione um registro para ver os campos alterados";
            _campos.Carregar([]);
            return;
        }
        _tituloDetalhe.Text = $"{Textos.Exibir(registro.Acao)} de {Textos.Entidade(registro.Entidade).ToLowerInvariant()} " +
                              $"{registro.Descricao ?? "#" + registro.RegistroId} por {registro.Usuario} em {registro.DataHora:dd/MM/yyyy HH:mm:ss}";
        _campos.Carregar(registro.Campos);
    }

    protected override async Task CarregarAsync()
    {
        if (_usuario.DataSource is null)
        {
            var (entidades, usuarios) = await _consultas.OpcoesFiltroAsync();
            _usuario.DataSource = new[] { new OpcaoTexto(null, "(todos)") }.Concat(usuarios.Select(u => new OpcaoTexto(u, u))).ToList();
            _entidade.DataSource = new[] { new OpcaoTexto(null, "(todos)") }
                .Concat(entidades.Select(e => new OpcaoTexto(e, Textos.Entidade(e))).OrderBy(o => o.Texto)).ToList();
        }

        Exibir(await _consultas.ListarAsync(new FiltroAuditoria
        {
            De = _de.Value.Date,
            Ate = _ate.Value.Date,
            Usuario = (_usuario.SelectedItem as OpcaoTexto)?.Valor,
            Acao = (_acao.SelectedItem as OpcaoEnum<AcaoAuditoria>)?.Valor,
            Texto = _filtroInicial?.Texto,
            RegistroId = _filtroInicial?.RegistroId,
            Entidade = _filtroInicial?.Entidade ?? (_entidade.SelectedItem as OpcaoTexto)?.Valor
        }));
        ExibirDetalhe(Grade.Selecionado);
    }

    protected override string TextoRodape(IReadOnlyList<RegistroAuditoriaResumo> visiveis) =>
        $"{visiveis.Count} registro(s)  •  inclusões: {visiveis.Count(r => r.Acao == AcaoAuditoria.Inclusao)}  •  " +
        $"alterações: {visiveis.Count(r => r.Acao == AcaoAuditoria.Alteracao)}  •  exclusões: {visiveis.Count(r => r.Acao == AcaoAuditoria.Exclusao)}" +
        (visiveis.Count >= 2000 ? "  •  exibindo os 2.000 mais recentes: refine os filtros" : "");
}
