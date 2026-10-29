using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>
/// Aba Mesas: a planta do salão com as mesas (arrastáveis), os convidados sem mesa e os da mesa selecionada. O convidado é
/// sentado arrastando-o até a mesa ou pelo botão; há distribuição automática, o mapa padrão do salão e as impressões.
/// </summary>
internal sealed partial class FormEventoDetalhe
{
    private readonly MapaMesas _mapa = new() { Dock = DockStyle.Fill };
    private readonly Grade<Convidado> _semMesa = new();
    private readonly Grade<Convidado> _naMesa = new();
    private readonly Label _resumoMesas = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(8, 8, 16, 0) };
    private readonly Label _tituloMesa = new() { Dock = DockStyle.Top, Height = 28, Font = Tema.FonteNegrito, TextAlign = ContentAlignment.MiddleLeft };
    private Convidado? _convidadoArrastado;

    private TabPage CriarAbaMesas()
    {
        _semMesa.Texto("Convidado sem mesa", c => ImpressaoMesas.Nome(c), 60)
            .Status("Presença", c => c.Rsvp, 28);
        _naMesa.Texto("Convidado", c => ImpressaoMesas.Nome(c), 60)
            .Numero("Lugares", c => c.Pessoas, peso: 20);

        var barra = BarraAba();
        BotaoAba(barra, "Nova mesa", Icones.Adicionar, () => EditarMesaAsync(null), primario: true);
        BotaoAba(barra, "Editar mesa", Icones.Editar, () => MesaSelecionada() is { } m ? EditarMesaAsync(m) : AvisarSemMesa());
        BotaoAba(barra, "Remover mesa", Icones.Excluir, RemoverMesaAsync);
        BotaoAba(barra, "Sentar na mesa", Icones.Confirmar, () =>
            _semMesa.Selecionado is { } c ? SentarAsync(c, MesaSelecionada()?.Id ?? 0) : Avisar("Selecione na lista o convidado sem mesa."));
        BotaoAba(barra, "Tirar da mesa", Icones.Desfazer, () =>
            _naMesa.Selecionado is { } c ? Recarregar(_eventos.SentarConvidadoAsync(_eventoId, c.Id, null)) : Avisar("Selecione o convidado da mesa."));
        BotaoAba(barra, "Distribuir automaticamente", Icones.Pessoas, DistribuirConvidadosAsync);
        BotaoAba(barra, "Usar o mapa do salão", Icones.Loja, UsarMapaDoSalaoAsync);
        BotaoAba(barra, "Salvar como padrão do salão", Icones.Salvar, SalvarMapaComoPadraoAsync, edicao: false);

        var impressao = BarraAba();
        BotaoAba(impressao, "Lista da recepção", Icones.Lista, () => Imprimir(ImpressaoMesas.ListaRecepcao(Evento, _opcoes.NomeEmpresa)), edicao: false);
        BotaoAba(impressao, "Mapa impresso", Icones.Imprimir, () => TemMesas() ? Imprimir(ImpressaoMesas.Mapa(Evento, _opcoes.NomeEmpresa), paisagem: true)
            : Task.CompletedTask, edicao: false);
        BotaoAba(impressao, "Cartões de mesa", Icones.Pagina, () => TemMesas() ? Imprimir(ImpressaoMesas.Cartoes(Evento, _opcoes.NomeEmpresa))
            : Task.CompletedTask, edicao: false);
        impressao.Controls.Add(_resumoMesas);

        _mapa.SelecaoAlterada += _ => AtualizarMesaSelecionada();
        _mapa.MesaAberta += async m => await ExecutarAsync(() => Evento.Mesas.FirstOrDefault(x => x.Id == m.Id) is { } mesa
            ? EditarMesaAsync(mesa) : Task.CompletedTask);
        _mapa.MesaMovida += async (m, x, y) => await ExecutarAsync(() => Recarregar(_eventos.MoverMesaAsync(_eventoId, m.Id, x, y)));
        _mapa.Solto += async (m, _) =>
        {
            if (_convidadoArrastado is { } c)
                await ExecutarAsync(() => SentarAsync(c, m.Id));
        };
        _mapa.TextoDica = m => Evento.Mesas.FirstOrDefault(x => x.Id == m.Id) is { } mesa
            ? $"{mesa.Descricao}: {Evento.LugaresOcupados(mesa)}/{mesa.Lugares} lugares" + string.Concat(
                Evento.ConvidadosDaMesa(mesa).OrderBy(c => c.Nome).Select(c => Environment.NewLine + ImpressaoMesas.Nome(c)))
            : m.Descricao;

        PermitirArrastar(_semMesa);
        PermitirArrastar(_naMesa);
        _semMesa.DuploClique += async c => await ExecutarAsync(() => SentarAsync(c, MesaSelecionada()?.Id ?? 0));

        // Soltar um convidado na lista "sem mesa" tira ele da mesa.
        _semMesa.Controle.AllowDrop = true;
        _semMesa.Controle.DragOver += (_, e) => e.Effect = _convidadoArrastado?.Mesa is not null ? DragDropEffects.Move : DragDropEffects.None;
        _semMesa.Controle.DragDrop += async (_, _) =>
        {
            if (_convidadoArrastado is { Mesa: not null } c)
                await ExecutarAsync(() => Recarregar(_eventos.SentarConvidadoAsync(_eventoId, c.Id, null)));
        };

        var lateral = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8, 0, 0, 0) };
        lateral.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        lateral.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        lateral.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        _semMesa.Controle.Dock = DockStyle.Fill;
        _naMesa.Controle.Dock = DockStyle.Fill;
        _tituloMesa.Dock = DockStyle.Fill;
        lateral.Controls.Add(_semMesa.Controle, 0, 0);
        lateral.Controls.Add(_tituloMesa, 0, 1);
        lateral.Controls.Add(_naMesa.Controle, 0, 2);

        var divisao = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        divisao.Panel1.Controls.Add(_mapa);
        divisao.Panel2.Controls.Add(lateral);
        divisao.HandleCreated += (_, _) => divisao.SplitterDistance = Math.Max(divisao.Panel1MinSize, divisao.Width - LogicalToDeviceUnits(360));

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(divisao);
        painel.Controls.Add(impressao);
        painel.Controls.Add(barra);
        return new TabPage("Mesas") { Controls = { painel } };
    }

    /// <summary>Arrastar a linha do convidado até uma mesa do mapa (ou de volta à lista "sem mesa").</summary>
    private void PermitirArrastar(Grade<Convidado> grade)
    {
        var inicio = Rectangle.Empty;
        grade.Controle.MouseDown += (_, e) =>
        {
            var linha = grade.Controle.HitTest(e.X, e.Y).RowIndex;
            inicio = e.Button == MouseButtons.Left && linha >= 0
                ? new Rectangle(new Point(e.X - SystemInformation.DragSize.Width / 2, e.Y - SystemInformation.DragSize.Height / 2), SystemInformation.DragSize)
                : Rectangle.Empty;
        };
        grade.Controle.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || inicio == Rectangle.Empty || inicio.Contains(e.Location) || grade.Selecionado is not { } c ||
                !_mapa.PermiteMover)
                return;
            inicio = Rectangle.Empty;
            _convidadoArrastado = c;
            try
            {
                grade.Controle.DoDragDrop(c.Nome, DragDropEffects.Move);
            }
            finally
            {
                _convidadoArrastado = null;
            }
        };
    }

    private void CarregarMesas(Evento e)
    {
        var selecionada = _mapa.Selecionada?.Id;
        _mapa.PermiteMover = e.Status == StatusEvento.Confirmado;
        _mapa.Definir(ImpressaoMesas.Mesas(e));
        _semMesa.Carregar(e.ConvidadosSemMesa().OrderBy(c => c.Nome));
        _mapa.Selecionar(selecionada);

        var semMesa = e.ConvidadosSemMesa().Sum(c => c.Pessoas);
        _resumoMesas.Text = e.Mesas.Count == 0
            ? "Sem mapa de mesas."
            : $"{e.Mesas.Count} mesa(s)  •  {e.LugaresNasMesas} lugares  •  sentadas: {e.PessoasSentadas} pessoa(s)" +
              (semMesa > 0 ? $"  •  ⚠ {semMesa} sem mesa" : "") +
              (e.PessoasSentadas + semMesa > e.LugaresNasMesas ? "  •  faltam lugares" : "");
        _resumoMesas.ForeColor = semMesa > 0 ? Tema.Alerta : Tema.Texto;
    }

    private void AtualizarMesaSelecionada()
    {
        if (_detalhe is null)
            return;
        var mesa = MesaSelecionada();
        _tituloMesa.Text = mesa is null
            ? "Clique numa mesa do mapa para ver quem senta nela."
            : $"{mesa.Descricao}  •  {Evento.LugaresOcupados(mesa)}/{mesa.Lugares} lugares";
        _naMesa.Carregar(mesa is null ? [] : Evento.ConvidadosDaMesa(mesa).OrderBy(c => c.Nome));
    }

    private EventoMesa? MesaSelecionada() => _mapa.Selecionada is { } m ? Evento.Mesas.FirstOrDefault(x => x.Id == m.Id) : null;

    private Task SentarAsync(Convidado convidado, int mesaId) =>
        mesaId == 0 ? AvisarSemMesa() : Recarregar(_eventos.SentarConvidadoAsync(_eventoId, convidado.Id, mesaId));

    private Task AvisarSemMesa() => Avisar("Selecione uma mesa no mapa.");

    private Task Avisar(string mensagem)
    {
        Mensagens.Aviso(this, mensagem);
        return Task.CompletedTask;
    }

    private async Task EditarMesaAsync(EventoMesa? mesa)
    {
        var input = mesa is null ? new MesaInput { Numero = Evento.ProximoNumeroMesa } : MesaInput.De(mesa, mesa.Id);
        int? formato = (int)input.Formato;
        var formatos = Enum.GetValues<FormatoMesa>().Select(f => new ItemLookup { Id = (int)f, Nome = Textos.Exibir(f) }).ToList();

        var editor = new EditorCampos();
        editor.Inteiro("Número", () => input.Numero, v => input.Numero = v, nameof(input.Numero), 1, PlantaSalao.NumeroMaximo);
        editor.Inteiro("Lugares", () => input.Lugares, v => input.Lugares = v, nameof(input.Lugares), 1, PlantaSalao.LugaresMaximo);
        editor.Lista("Formato", formatos, () => formato, v => formato = v);
        editor.Texto("Nome (opcional)", () => input.Nome, v => input.Nome = v, nameof(input.Nome), PlantaSalao.TamanhoNome,
            dica: "Noivos, família da noiva, crianças...");

        using var dialogo = new FormEdicao(mesa is null ? "Nova mesa" : $"Editar {mesa.Descricao}",
            mesa is null ? "A mesa entra no primeiro lugar livre do mapa; depois é só arrastá-la." : null, editor, () =>
            {
                input.Formato = (FormatoMesa)(formato ?? 0);
                return _eventos.SalvarMesaAsync(_eventoId, input);
            }, 560);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private async Task RemoverMesaAsync()
    {
        if (MesaSelecionada() is not { } mesa)
        {
            await AvisarSemMesa();
            return;
        }
        var sentados = Evento.ConvidadosDaMesa(mesa).Count();
        if (Mensagens.Confirmar(this, $"Remover a {mesa.Descricao} do mapa?" +
                                      (sentados > 0 ? $" Os {sentados} convidado(s) sentados nela ficam sem mesa." : "")))
            await Recarregar(_eventos.RemoverMesaAsync(_eventoId, mesa.Id));
    }

    private async Task DistribuirConvidadosAsync()
    {
        var sentados = await _eventos.DistribuirConvidadosAsync(_eventoId);
        await CarregarAsync();
        var semMesa = Evento.ConvidadosSemMesa().ToList();
        Mensagens.Info(this, $"{sentados} convidado(s) sentado(s)." + (semMesa.Count == 0 ? ""
            : $" {semMesa.Count} ficaram sem mesa ({semMesa.Sum(c => c.Pessoas)} pessoa(s)): faltam lugares para eles juntos com os acompanhantes."));
    }

    private async Task UsarMapaDoSalaoAsync()
    {
        if (Evento.Mesas.Count > 0 && !Mensagens.Confirmar(this,
                $"Trocar o mapa deste evento pelo mapa padrão do {Evento.Espaco?.Nome}? As mesas de mesmo número continuam com quem está sentado nelas."))
            return;
        var semMesa = await _eventos.UsarMapaDoEspacoAsync(_eventoId);
        await CarregarAsync();
        if (semMesa > 0)
            Mensagens.Aviso(this, $"{semMesa} convidado(s) ficaram sem mesa: a mesa deles não existe no mapa do salão ou ficou menor.");
    }

    private async Task SalvarMapaComoPadraoAsync()
    {
        if (!TemMesas() || !Mensagens.Confirmar(this,
                $"O mapa deste evento (sem os convidados) passa a ser o padrão do {Evento.Espaco?.Nome}, usado nos próximos eventos do salão. Continuar?"))
            return;
        var espaco = await _navegador.Servico<MapaMesasService>().SalvarDoEventoComoPadraoAsync(_eventoId);
        Mensagens.Info(this, $"Mapa padrão do {espaco} atualizado.");
    }

    private bool TemMesas()
    {
        if (Evento.Mesas.Count > 0)
            return true;
        Mensagens.Aviso(this, "O mapa deste evento ainda não tem mesas.");
        return false;
    }

    private Task Imprimir(DocumentoImpresso documento, bool paisagem = false)
    {
        documento.Visualizar(this, paisagem);
        return Task.CompletedTask;
    }
}
