using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>
/// Central do evento: resumo e resultado estimado, equipe, convidados (RSVP e check-in), materiais (reserva,
/// locação e devolução), contrato com assinatura, financeiro e encerramento com baixa automática de estoque.
/// </summary>
internal sealed class FormEventoDetalhe : FormBase
{
    private sealed record InsumoPrevisto(string Produto, decimal Quantidade, string Unidade, decimal Custo);

    private readonly EventoService _eventos;
    private readonly FinanceiroService _financeiro;
    private readonly IConsultasLookup _lookups;
    private readonly IConsultasEstoqueCardapio _cardapio;
    private readonly BuffetOptions _opcoes;
    private readonly ContratoService _contratos;
    private readonly IEnvioEmail _email;
    private readonly ISessaoUsuario _sessao;
    private readonly Navegador _navegador;
    private readonly int _eventoId;
    private EventoDetalhe? _detalhe;
    private Evento Evento => _detalhe!.Evento;

    // Cabeçalho
    private readonly Label _titulo = Tema.Titulo("Evento");
    private readonly Label _status = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(12, 8, 0, 0) };
    private readonly Label _subtitulo = Tema.Subtitulo("");
    private readonly FlowLayoutPanel _botoes = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 0, 16, 4) };
    private readonly CartaoIndicador _valor = new("Valor do contrato", "-", Tema.Primaria);
    private readonly CartaoIndicador _recebido = new("Recebido", "-", Tema.Sucesso);
    private readonly CartaoIndicador _saldo = new("Saldo a receber", "-", Tema.Alerta);
    private readonly CartaoIndicador _insumos = new("Custo de insumos (est.)", "-", Tema.Info);
    private readonly CartaoIndicador _equipeCusto = new("Custos operacionais", "-", Tema.Info);
    private readonly CartaoIndicador _resultado = new("Resultado estimado", "-", Tema.Sucesso);

    // Abas
    private readonly TableLayoutPanel _dados = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(4) };
    private readonly ListBox _cardapioLista = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly Grade<EventoServico> _servicos = new();
    private readonly Grade<InsumoPrevisto> _insumosPrevistos = new();
    private readonly Grade<EventoFuncionario> _equipe = new();
    private readonly Grade<Convidado> _convidados = new();
    private readonly Grade<Parcela> _parcelas = new();
    private readonly Label _resumoConvidados = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 8, 16, 0) };
    private readonly Label _resumoFinanceiro = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 8, 16, 0) };
    private readonly TextBox _buscaConvidado = new() { Width = 220, PlaceholderText = "Localizar convidado...", Margin = new Padding(0, 4, 8, 0) };
    private readonly List<Control> _controlesEdicao = [];
    private readonly List<Control> _acoesCabecalho = [];

    public FormEventoDetalhe(EventoService eventos, FinanceiroService financeiro, IConsultasLookup lookups,
        IConsultasEstoqueCardapio cardapio, BuffetOptions opcoes, ContratoService contratos, IEnvioEmail email,
        ISessaoUsuario sessao, Navegador navegador, int eventoId)
    {
        _contratos = contratos;
        _email = email;
        _sessao = sessao;
        _navegador = navegador;
        _eventos = eventos;
        _financeiro = financeiro;
        _lookups = lookups;
        _cardapio = cardapio;
        _opcoes = opcoes;
        _eventoId = eventoId;
        Text = $"EVT-{eventoId:D5}";

        var cabecalho = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20, 14, 20, 4), ColumnCount = 1 };
        var linhaTitulo = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        linhaTitulo.Controls.Add(_titulo);
        linhaTitulo.Controls.Add(_status);
        cabecalho.Controls.Add(linhaTitulo);
        cabecalho.Controls.Add(_subtitulo);

        AdicionarBotao("Editar dados", Icones.Editar, EditarAsync, edicao: true);
        AdicionarBotao("Plano de pagamento", Icones.Calculadora, PlanoPagamentoAsync, edicao: true);
        AdicionarBotao("Realizar evento", Icones.Confirmar, RealizarAsync, edicao: true);
        AdicionarBotao("Cancelar evento", Icones.Cancelar, CancelarAsync, edicao: true, perigo: true);
        AdicionarBotao("Ficha do evento", Icones.Imprimir, ImprimirFichaAsync);
        AdicionarBotao("Atualizar", Icones.Atualizar, CarregarAsync);
        if (sessao.Usuario?.EhAdministrador == true)
            AdicionarBotao("Alterações", Icones.Escudo, () =>
            {
                var codigo = $"EVT-{_eventoId:D5}";
                _navegador.Abrir($"auditoria:{codigo}", () => _navegador.Criar<Seguranca.FormAuditoria>(new Seguranca.FiltroAuditoriaInicial($"Alterações - {codigo}", Texto: codigo)));
                return Task.CompletedTask;
            });

        var indicadores = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 0, 14, 4) };
        indicadores.Controls.AddRange([_valor, _recebido, _saldo, _insumos, _equipeCusto, _resultado]);

        var abas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
        abas.TabPages.Add(CriarAbaResumo());
        abas.TabPages.Add(CriarAbaEquipe());
        abas.TabPages.Add(CriarAbaConvidados());
        abas.TabPages.Add(CriarAbaMateriais());
        abas.TabPages.Add(CriarAbaContrato());
        abas.TabPages.Add(CriarAbaFinanceiro());
        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 14) };
        corpo.Controls.Add(abas);

        Controls.Add(corpo);
        Controls.Add(indicadores);
        Controls.Add(_botoes);
        Controls.Add(cabecalho);
    }

    private void AdicionarBotao(string texto, char icone, Func<Task> acao, bool edicao = false, bool perigo = false)
    {
        var botao = perigo ? Tema.BotaoPerigo(texto, icone) : Tema.BotaoSecundario(texto, icone);
        botao.Click += async (_, _) => await ExecutarAsync(acao, botao, cursorEspera: false);
        _botoes.Controls.Add(botao);
        if (edicao)
            _acoesCabecalho.Add(botao);
    }

    private static FlowLayoutPanel BarraAba() => new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 4, 0, 6), WrapContents = true };

    private Button BotaoAba(FlowLayoutPanel barra, string texto, char icone, Func<Task> acao, bool primario = false, bool edicao = true)
    {
        var botao = primario ? Tema.BotaoPrimario(texto, icone) : Tema.BotaoSecundario(texto, icone);
        botao.Click += async (_, _) => await ExecutarAsync(acao, botao, cursorEspera: false);
        barra.Controls.Add(botao);
        if (edicao)
            _controlesEdicao.Add(botao);
        return botao;
    }

    // =========================== Aba Resumo ===========================

    private TabPage CriarAbaResumo()
    {
        _servicos.Texto("Serviço", s => s.Descricao, 60).Numero("Qtd.", s => s.Quantidade, peso: 15)
            .Moeda("Unitário", s => s.ValorUnitario, 22).Moeda("Total", s => s.Total, 22);
        _insumosPrevistos.Texto("Insumo", i => i.Produto, 50).Numero("Quantidade", i => i.Quantidade, "N3", 22)
            .Texto("Un.", i => i.Unidade, 10).Moeda("Custo estimado", i => i.Custo, 25);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(6) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(Bloco("Dados do evento", _dados, autoSize: true), 0, 0);
        layout.Controls.Add(Bloco("Cardápio contratado", _cardapioLista), 1, 0);
        layout.SetRowSpan(layout.GetControlFromPosition(1, 0)!, 2);

        var inferior = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        inferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inferior.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        inferior.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        inferior.Controls.Add(Bloco("Serviços adicionais", _servicos.Controle), 0, 0);
        inferior.Controls.Add(Bloco("Insumos previstos (ficha técnica × convidados) - baixados do estoque ao realizar", _insumosPrevistos.Controle), 0, 1);
        layout.Controls.Add(inferior, 0, 1);

        return new TabPage("Resumo") { Controls = { layout }, BackColor = Tema.Fundo };
    }

    private static Control Bloco(string titulo, Control conteudo, bool autoSize = false)
    {
        var cartao = Tema.Cartao(new Padding(12));
        cartao.Dock = DockStyle.Fill;
        if (autoSize)
        {
            cartao.AutoSize = true;
            cartao.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }
        conteudo.Dock = autoSize ? DockStyle.Top : DockStyle.Fill;
        cartao.Controls.Add(conteudo);
        cartao.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Top, Font = Tema.FonteNegrito, Height = 26 });
        return cartao;
    }

    // =========================== Aba Equipe ===========================

    private readonly ComboBox _funcionario = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 6, 0) };
    private readonly TextBox _funcao = new() { Width = 150, PlaceholderText = "Função (opcional)", Margin = new Padding(0, 5, 6, 0) };
    private readonly NumericUpDown _diaria = new() { Width = 100, DecimalPlaces = 2, Maximum = 100_000, Margin = new Padding(0, 5, 6, 0), ThousandsSeparator = true };

    private TabPage CriarAbaEquipe()
    {
        _equipe.Texto("Funcionário", f => f.Funcionario?.Nome, 50)
            .Texto("Função", f => f.Funcao, 30)
            .Moeda("Diária", f => f.ValorDiaria, 20)
            .SimNao("Presente", f => f.Presente, 15);

        _funcionario.SelectedIndexChanged += (_, _) =>
        {
            if (_funcionario.SelectedItem is ItemLookup f)
            {
                _diaria.Value = f.Valor;
                _funcao.PlaceholderText = f.Info ?? "Função";
            }
        };

        var barra = BarraAba();
        barra.Controls.AddRange([_funcionario, _funcao, new Label { Text = "R$", AutoSize = true, Margin = new Padding(0, 9, 2, 0) }, _diaria]);
        _controlesEdicao.AddRange([_funcionario, _funcao, _diaria]);
        BotaoAba(barra, "Escalar", Icones.Adicionar, EscalarAsync, primario: true);
        BotaoAba(barra, "Remover", Icones.Excluir, async () =>
        {
            if (_equipe.Selecionado is { } f && Mensagens.Confirmar(this, $"Remover {f.Funcionario?.Nome} da equipe?"))
                await Recarregar(_eventos.RemoverDaEquipeAsync(_eventoId, f.FuncionarioId));
        });
        BotaoAba(barra, "Presente / ausente", Icones.Confirmar, async () =>
        {
            if (_equipe.Selecionado is { } f)
                await Recarregar(_eventos.RegistrarPresencaAsync(_eventoId, f.FuncionarioId, !f.Presente));
        }, edicao: false);

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(_equipe.Controle);
        painel.Controls.Add(barra);
        return new TabPage("Equipe") { Controls = { painel } };
    }

    private async Task EscalarAsync()
    {
        if (_funcionario.SelectedItem is not ItemLookup funcionario)
            return;
        await Recarregar(_eventos.EscalarFuncionarioAsync(_eventoId, funcionario.Id,
            string.IsNullOrWhiteSpace(_funcao.Text) ? null : _funcao.Text.Trim(), _diaria.Value));
        _funcao.Clear();
    }

    // =========================== Aba Convidados ===========================

    private readonly TextBox _nomeConvidado = new() { Width = 220, PlaceholderText = "Nome do convidado", Margin = new Padding(0, 5, 6, 0) };
    private readonly MaskedTextBox _telefoneConvidado = new("(00) 00000-0000") { Width = 120, Margin = new Padding(0, 5, 6, 0) };
    private readonly NumericUpDown _acompanhantes = new() { Width = 50, Maximum = 20, Margin = new Padding(0, 5, 6, 0) };
    private readonly TextBox _mesa = new() { Width = 80, PlaceholderText = "Mesa", Margin = new Padding(0, 5, 6, 0) };

    private TabPage CriarAbaConvidados()
    {
        _convidados.Texto("Nome", c => c.Nome, 50)
            .Texto("Telefone", c => c.Telefone, 25)
            .Numero("Acomp.", c => c.Acompanhantes, peso: 12)
            .Texto("Mesa", c => c.Mesa, 15)
            .Status("Presença (RSVP)", c => c.Rsvp, 20)
            .Data("Check-in", c => c.CheckInEm, "dd/MM HH:mm", 20, _ => Tema.Sucesso)
            .Busca(c => $"{c.Nome} {c.Telefone} {c.Mesa}");
        _buscaConvidado.TextChanged += (_, _) => _convidados.Filtrar(_buscaConvidado.Text);

        var cadastro = BarraAba();
        cadastro.Controls.AddRange([_nomeConvidado, _telefoneConvidado,
            new Label { Text = "Acomp.", AutoSize = true, Margin = new Padding(0, 9, 2, 0) }, _acompanhantes, _mesa]);
        _controlesEdicao.AddRange([_nomeConvidado, _telefoneConvidado, _acompanhantes, _mesa]);
        var adicionar = BotaoAba(cadastro, "Adicionar", Icones.Adicionar, AdicionarConvidadoAsync, primario: true);
        _nomeConvidado.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                adicionar.PerformClick();
            }
        };
        BotaoAba(cadastro, "Importar lista...", Icones.Lista, async () =>
        {
            if (await DialogosEvento.ImportarConvidadosAsync(this, _eventos, _eventoId) is { } quantidade)
            {
                await CarregarAsync();
                Mensagens.Info(this, $"{quantidade} convidado(s) importado(s). Nomes repetidos foram ignorados.");
            }
        });

        var acoes = BarraAba();
        acoes.Controls.Add(_buscaConvidado);
        BotaoAba(acoes, "Check-in", Icones.Confirmar, async () =>
        {
            if (_convidados.Selecionado is { } c)
                await Recarregar(_eventos.RegistrarCheckInAsync(_eventoId, c.Id));
        }, primario: true, edicao: false);
        BotaoAba(acoes, "Desfazer check-in", Icones.Desfazer, async () =>
        {
            if (_convidados.Selecionado is { } c)
                await Recarregar(_eventos.DesfazerCheckInAsync(_eventoId, c.Id));
        }, edicao: false);
        BotaoAba(acoes, "Confirmou presença", Icones.Aceitar, () => DefinirRsvpAsync(StatusRsvp.Confirmado));
        BotaoAba(acoes, "Não vai", Icones.Cancelar, () => DefinirRsvpAsync(StatusRsvp.Recusado));
        BotaoAba(acoes, "Remover", Icones.Excluir, async () =>
        {
            if (_convidados.Selecionado is { } c && Mensagens.Confirmar(this, $"Remover {c.Nome} da lista?"))
                await Recarregar(_eventos.RemoverConvidadoAsync(_eventoId, c.Id));
        });
        acoes.Controls.Add(_resumoConvidados);

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(_convidados.Controle);
        painel.Controls.Add(acoes);
        painel.Controls.Add(cadastro);
        return new TabPage("Convidados") { Controls = { painel } };
    }

    private async Task AdicionarConvidadoAsync()
    {
        var telefone = Domain.Comum.Documento.SomenteDigitos(_telefoneConvidado.Text).Length == 0 ? null : _telefoneConvidado.Text;
        await Recarregar(_eventos.AdicionarConvidadoAsync(_eventoId, _nomeConvidado.Text, telefone, null, (int)_acompanhantes.Value,
            string.IsNullOrWhiteSpace(_mesa.Text) ? null : _mesa.Text.Trim()));
        _nomeConvidado.Clear();
        _telefoneConvidado.Clear();
        _acompanhantes.Value = 0;
        _nomeConvidado.Focus();
    }

    private async Task DefinirRsvpAsync(StatusRsvp rsvp)
    {
        if (_convidados.Selecionado is { } c)
            await Recarregar(_eventos.DefinirRsvpAsync(_eventoId, c.Id, rsvp));
    }

    // =========================== Aba Materiais ===========================

    private readonly Grade<EventoMaterial> _materiais = new();
    private readonly ComboBox _material = new() { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 6, 0) };
    private readonly NumericUpDown _quantidadeMaterial = new() { Width = 80, Maximum = 100_000, Margin = new Padding(0, 5, 6, 0), ThousandsSeparator = true };
    private readonly NumericUpDown _alugadas = new() { Width = 80, Maximum = 100_000, Margin = new Padding(0, 5, 6, 0), ThousandsSeparator = true };
    private readonly Label _disponibilidade = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 9, 12, 0) };
    private readonly Label _resumoMateriais = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 8, 16, 0) };
    private IReadOnlyList<DisponibilidadeMaterial> _disponiveis = [];

    private TabPage CriarAbaMateriais()
    {
        _materiais.Texto("Material", m => m.Material?.Nome, 45)
            .Texto("Categoria", m => m.Material is null ? "" : Textos.Exibir(m.Material.Categoria), 22)
            .Numero("Qtd.", m => m.Quantidade, peso: 12)
            .Numero("Próprios", m => m.QuantidadePropria, peso: 14)
            .Numero("Alugados", m => m.QuantidadeAlugada, peso: 14, cor: m => m.QuantidadeAlugada > 0 ? Tema.Alerta : null)
            .Moeda("Locação", m => m.CustoLocacao, 18)
            .Numero("Devolvidos", m => m.QuantidadeDevolvida, peso: 15)
            .Numero("Perdas", m => m.Devolvido ? m.QuantidadePerdida : null, peso: 12, cor: m => m.QuantidadePerdida > 0 ? Tema.Perigo : null)
            .Moeda("Custo perdas", m => m.Devolvido ? m.CustoPerdas : null, 18)
            .Texto("Situação", m => m.DevolvidoEm is { } d ? $"Devolvido {d:dd/MM}" : "Pendente", 20,
                m => m.Devolvido ? Tema.Sucesso : Tema.Alerta);

        _material.SelectedIndexChanged += (_, _) => MaterialSelecionado();
        _quantidadeMaterial.ValueChanged += (_, _) => SugerirAlugadas();

        var barra = BarraAba();
        barra.Controls.AddRange([_material, new Label { Text = "Qtd.", AutoSize = true, Margin = new Padding(0, 9, 2, 0) }, _quantidadeMaterial,
            new Label { Text = "Alugadas", AutoSize = true, Margin = new Padding(0, 9, 2, 0) }, _alugadas]);
        _controlesEdicao.AddRange([_material, _quantidadeMaterial, _alugadas]);
        BotaoAba(barra, "Reservar / atualizar", Icones.Adicionar, async () =>
        {
            if (_material.SelectedItem is ItemLookup material)
                await Recarregar(_eventos.ReservarMaterialAsync(_eventoId, material.Id, (int)_quantidadeMaterial.Value, (int)_alugadas.Value));
        }, primario: true);
        barra.Controls.Add(_disponibilidade);

        var acoes = BarraAba();
        BotaoAba(acoes, "Remover", Icones.Excluir, async () =>
        {
            if (_materiais.Selecionado is { } m && Mensagens.Confirmar(this, $"Remover {m.Material?.Nome} da reserva?"))
                await Recarregar(_eventos.RemoverMaterialAsync(_eventoId, m.MaterialId));
        });
        BotaoAba(acoes, "Registrar devolução", Icones.Desfazer, RegistrarDevolucaoAsync, edicao: false);
        BotaoAba(acoes, "Lançar conta da locação", Icones.Banco, async () =>
        {
            var total = await _eventos.LancarLocacaoMateriaisAsync(_eventoId);
            Mensagens.Info(this, $"Conta(s) a pagar de {Textos.Moeda(total)} lançada(s) para o(s) locador(es), com vencimento na data do evento.");
        });
        acoes.Controls.Add(_resumoMateriais);

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(_materiais.Controle);
        painel.Controls.Add(acoes);
        painel.Controls.Add(barra);
        return new TabPage("Materiais") { Controls = { painel } };
    }

    /// <summary>Sugere a quantidade (a já reservada ou uma por convidado) para o material escolhido.</summary>
    private void MaterialSelecionado()
    {
        if (_material.SelectedItem is not ItemLookup material || _detalhe is null)
            return;
        var existente = Evento.Materiais.FirstOrDefault(m => m.MaterialId == material.Id);
        _quantidadeMaterial.Value = Math.Min(_quantidadeMaterial.Maximum, existente?.Quantidade ?? Evento.QuantidadeConvidados);
        SugerirAlugadas();
    }

    /// <summary>Mostra o patrimônio livre na data e sugere alugar o que faltar.</summary>
    private void SugerirAlugadas()
    {
        if (_material.SelectedItem is not ItemLookup material || _detalhe is null)
            return;
        var situacao = _disponiveis.FirstOrDefault(d => d.Id == material.Id);
        var disponivel = situacao?.Disponivel ?? material.Numero;
        _alugadas.Value = Math.Max(0, _quantidadeMaterial.Value - disponivel);
        _disponibilidade.Text = $"Patrimônio: {material.Numero}  •  em outros eventos (±1 dia): {situacao?.Reservada ?? 0}  •  disponível: {disponivel}" +
                                (material.Valor > 0 ? $"  •  locação {Textos.Moeda(material.Valor)}/un." : "");
        _disponibilidade.ForeColor = _quantidadeMaterial.Value > disponivel ? Tema.Alerta : Tema.TextoSuave;
    }

    private async Task RegistrarDevolucaoAsync()
    {
        if (_materiais.Selecionado is not { } item)
        {
            Mensagens.Aviso(this, "Selecione um material.");
            return;
        }
        var devolvidas = item.Quantidade;
        var perdidas = 0;
        var e = new EditorCampos();
        e.Informacao("Material", () => $"{item.Material?.Nome} - enviados: {item.Quantidade} ({item.QuantidadeAlugada} alugados)", span: 2);
        var campoDevolvidas = e.Inteiro("Devolvidos em bom estado", () => devolvidas, v => devolvidas = v, maximo: item.Quantidade);
        var campoPerdidas = e.Inteiro("Quebrados / extraviados", () => perdidas, v => perdidas = v, maximo: item.Quantidade);
        campoPerdidas.ValueChanged += (_, _) => campoDevolvidas.Value = Math.Max(0, item.Quantidade - campoPerdidas.Value);
        e.Informacao("Valor de reposição", () => $"{Textos.Moeda(item.ValorReposicaoUnitario)} por unidade (entra no custo do evento)", span: 2);

        using var dialogo = new FormEdicao("Devolução de material",
            "As perdas de itens próprios são baixadas do patrimônio; as de itens alugados são indenizadas ao locador.", e,
            () => _eventos.RegistrarDevolucaoMaterialAsync(_eventoId, item.MaterialId, devolvidas, perdidas), 560);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    // =========================== Aba Contrato ===========================

    private readonly Label _situacaoContrato = new() { AutoSize = true, Font = Tema.FonteSubtitulo, Margin = new Padding(0, 6, 16, 0) };
    private readonly TextBox _textoContrato = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Tema.Superficie,
        Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.None
    };
    private readonly Grade<Contrato> _historicoContratos = new();
    private readonly List<(Button Botao, Func<Contrato?, bool> Habilitar)> _botoesContrato = [];
    private List<Contrato> _contratosEvento = [];
    private Contrato? ContratoAtual => _contratosEvento.FirstOrDefault(c => c.Status != StatusContrato.Cancelado);

    private TabPage CriarAbaContrato()
    {
        _historicoContratos.Texto("Contrato", c => c.Codigo, 18)
            .Status("Situação", c => c.Status, 25)
            .Data("Emitido em", c => c.EmitidoEm, "dd/MM/yyyy HH:mm", 22)
            .Data("Assinado em", c => c.AssinadoEm, "dd/MM/yyyy HH:mm", 22)
            .Texto("Signatário", c => c.NomeSignatario, 35)
            .Texto("Cancelamento", c => c.MotivoCancelamento, 40);
        _historicoContratos.Controle.Height = 120;
        _historicoContratos.Controle.Dock = DockStyle.Bottom;
        _historicoContratos.Controle.SelectionChanged += (_, _) => ExibirContrato(_historicoContratos.Selecionado);

        var barra = BarraAba();
        BotaoContrato(barra, "Gerar contrato", Icones.Pagina, async () =>
        {
            if (await DialogosContrato.EmitirAsync(this, _contratos, _lookups, _eventoId) is not null)
                await CarregarContratosAsync();
        }, c => c is null && Evento.Status == StatusEvento.Confirmado, primario: true);
        BotaoContrato(barra, "Coletar assinaturas", Icones.Caneta, async () =>
        {
            var contrato = await _contratos.ObterAsync(ContratoAtual!.Id);
            if (DialogosContrato.Assinar(this, _contratos, contrato, _sessao.Usuario?.Nome))
            {
                await CarregarContratosAsync();
                Mensagens.Info(this, $"Contrato {contrato.Codigo} assinado.");
            }
        }, c => c?.Status == StatusContrato.AguardandoAssinatura);
        BotaoContrato(barra, "Imprimir / PDF", Icones.Imprimir,
            () => DialogosContrato.ImprimirAsync(this, _contratos, _opcoes, ContratoSelecionado()!.Id), c => ContratoSelecionado() is not null);
        BotaoContrato(barra, "Enviar por e-mail", Icones.Email, async () =>
        {
            if (await DialogosContrato.EnviarEmailAsync(this, _contratos, _email, _opcoes, ContratoAtual!.Id))
                Mensagens.Info(this, "Contrato enviado por e-mail.");
        }, c => c is not null);
        BotaoContrato(barra, "Cancelar contrato", Icones.Cancelar, async () =>
        {
            if (await DialogosContrato.CancelarAsync(this, _contratos, ContratoAtual!))
                await CarregarContratosAsync();
        }, c => c is not null);
        barra.Controls.Add(_situacaoContrato);

        var cartao = Tema.Cartao(new Padding(16));
        cartao.Dock = DockStyle.Fill;
        cartao.Controls.Add(_textoContrato);

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(cartao);
        painel.Controls.Add(_historicoContratos.Controle);
        painel.Controls.Add(barra);
        return new TabPage("Contrato") { Controls = { painel } };
    }

    private void BotaoContrato(FlowLayoutPanel barra, string texto, char icone, Func<Task> acao, Func<Contrato?, bool> habilitar,
        bool primario = false)
    {
        var botao = BotaoAba(barra, texto, icone, acao, primario, edicao: false);
        _botoesContrato.Add((botao, habilitar));
    }

    private Contrato? ContratoSelecionado() => _historicoContratos.Selecionado ?? ContratoAtual;

    private async Task CarregarContratosAsync()
    {
        _contratosEvento = await _contratos.ListarDoEventoAsync(_eventoId);
        _historicoContratos.Carregar(_contratosEvento);
        var exibir = ContratoAtual ?? _contratosEvento.FirstOrDefault();
        if (exibir is not null)
            _historicoContratos.Selecionar(c => c.Id == exibir.Id);
        ExibirContrato(exibir);
    }

    private void ExibirContrato(Contrato? contrato)
    {
        if (contrato is null)
        {
            _situacaoContrato.Text = "Nenhum contrato emitido";
            _situacaoContrato.ForeColor = Tema.TextoSuave;
            _textoContrato.Text = Evento.Status == StatusEvento.Confirmado
                ? "Use \"Gerar contrato\" para montar o contrato com os dados do evento, o cardápio e o plano de pagamento." +
                  Environment.NewLine + Environment.NewLine + "Dica: gere o plano de pagamento antes, para que as parcelas constem no contrato."
                : "Evento encerrado sem contrato emitido.";
        }
        else
        {
            var integridade = contrato.ConteudoIntegro ? "integridade OK ✔" : "⚠ TEXTO ALTERADO APÓS A EMISSÃO";
            _situacaoContrato.Text = $"{contrato.Codigo}  •  {Textos.Exibir(contrato.Status)}" +
                                     (contrato.AssinadoEm is { } assinado ? $" em {assinado:dd/MM/yyyy HH:mm} por {contrato.NomeSignatario}" : "") +
                                     $"  •  {integridade}";
            _situacaoContrato.ForeColor = contrato.ConteudoIntegro ? Textos.Cor(contrato.Status) : Tema.Perigo;
            _textoContrato.Text = contrato.Conteudo.ReplaceLineEndings();
        }

        var atual = ContratoAtual;
        foreach (var (botao, habilitar) in _botoesContrato)
            botao.Enabled = habilitar(atual);
    }

    // =========================== Aba Financeiro ===========================

    private TabPage CriarAbaFinanceiro()
    {
        _parcelas.Numero("Nº", p => p.Numero, peso: 8)
            .Texto("Descrição", p => p.Descricao, 35)
            .Data("Vencimento", p => p.Vencimento.ToDateTime(TimeOnly.MinValue), peso: 20,
                cor: p => p.EstaVencido(DateOnly.FromDateTime(DateTime.Today)) ? Tema.Perigo : null)
            .Moeda("Valor", p => p.Valor, 20)
            .Status("Situação", p => p.Status, 18)
            .Data("Pago em", p => p.DataPagamento?.ToDateTime(TimeOnly.MinValue), peso: 20)
            .Moeda("Valor pago", p => p.ValorPago, 20)
            .Texto("Forma", p => p.FormaPagamento?.Nome, 25);

        var barra = BarraAba();
        BotaoAba(barra, "Gerar / refazer plano", Icones.Calculadora, PlanoPagamentoAsync, primario: true);
        BotaoAba(barra, "Registrar pagamento", Icones.Cartao, PagarParcelaAsync, edicao: false);
        BotaoAba(barra, "Estornar pagamento", Icones.Desfazer, async () =>
        {
            if (_parcelas.Selecionado is { } p && Mensagens.Confirmar(this, $"Estornar o pagamento da parcela {p.Numero}?", perigoso: true))
                await Recarregar(_financeiro.EstornarParcelaAsync(p.Id));
        }, edicao: false);
        barra.Controls.Add(_resumoFinanceiro);

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(_parcelas.Controle);
        painel.Controls.Add(barra);
        return new TabPage("Financeiro") { Controls = { painel } };
    }

    private async Task PagarParcelaAsync()
    {
        if (_parcelas.Selecionado is not { } parcela)
        {
            Mensagens.Aviso(this, "Selecione uma parcela.");
            return;
        }
        var formas = await _lookups.ListarAsync(TipoLookup.FormasPagamento);
        var pagamento = Dialogos.PedirPagamento(this, "Registrar recebimento", $"{parcela.Descricao} - vencimento {parcela.Vencimento:dd/MM/yyyy}",
            parcela.Valor, formas, parcela.FormaPagamentoId);
        if (pagamento is not null)
            await Recarregar(_financeiro.PagarParcelaAsync(parcela.Id, pagamento));
    }

    // =========================== Ações do evento ===========================

    private async Task EditarAsync()
    {
        if (await DialogosEvento.EditarAsync(this, _eventos, _lookups, _eventoId) is not null)
            await CarregarAsync();
    }

    private async Task PlanoPagamentoAsync()
    {
        var saldo = Evento.ValorTotal - Evento.Parcelas.Where(p => p.Status == StatusTitulo.Pago).Sum(p => p.Valor);
        if (await DialogosEvento.PlanoPagamentoAsync(this, _eventos, _lookups, _eventoId, saldo, Evento.DataEvento,
                Evento.CondicaoPagamentoId))
            await CarregarAsync();
    }

    private async Task RealizarAsync()
    {
        if (!Mensagens.Confirmar(this,
                "Marcar o evento como realizado? Os insumos do cardápio serão baixados do estoque (ficha técnica × convidados) e o evento não poderá mais ser alterado.",
                "Realizar evento"))
            return;
        var resultado = await _eventos.RealizarAsync(_eventoId);
        await CarregarAsync();
        Mensagens.Info(this, resultado.Avisos.Count == 0
            ? "Evento encerrado e estoque baixado com sucesso."
            : "Evento encerrado. Avisos do estoque:" + Environment.NewLine + string.Join(Environment.NewLine, resultado.Avisos.Select(a => "• " + a)));
    }

    private async Task CancelarAsync()
    {
        var motivo = Dialogos.PedirTexto(this, "Cancelar evento", "Motivo do cancelamento",
            subtitulo: "As parcelas em aberto serão canceladas. Valores já recebidos permanecem registrados.");
        if (motivo is null)
            return;
        await _eventos.CancelarAsync(_eventoId, motivo);
        await CarregarAsync();
    }

    private async Task ImprimirFichaAsync()
    {
        var e = Evento;
        var doc = new DocumentoImpresso($"Ficha do evento {e.Codigo}", _opcoes.NomeEmpresa);
        doc.Secao(e.Titulo);
        doc.Campos(3,
            ("Cliente", e.Cliente?.Nome ?? "-"),
            ("Contato", e.Cliente?.Celular ?? e.Cliente?.Telefone ?? "-"),
            ("Tipo", e.TipoEvento?.Nome ?? "-"),
            ("Data", e.Inicio.ToString("dddd, dd/MM/yyyy", Textos.PtBr)),
            ("Horário", $"{e.Inicio:HH:mm} às {e.Fim:HH:mm}"),
            ("Espaço", e.Espaco?.Nome ?? "-"),
            ("Convidados contratados", e.QuantidadeConvidados.ToString()),
            ("Pessoas na lista", e.TotalPessoasNaLista.ToString()),
            ("Situação", Textos.Exibir(e.Status)));

        if (e.PacoteId is { } pacoteId)
        {
            doc.Secao($"Cardápio - {e.Pacote?.Nome}");
            foreach (var grupo in (await _cardapio.ComposicaoPacoteAsync(pacoteId)).GroupBy(c => c.Categoria))
            {
                doc.Paragrafo(grupo.Key, negrito: true);
                doc.Paragrafo(string.Join("  •  ", grupo.Select(i => i.Item)));
            }
        }
        if (e.Servicos.Count > 0)
        {
            doc.Secao("Serviços contratados");
            doc.Tabela(["Serviço", "Quantidade"], e.Servicos.Select(s => new[] { s.Descricao, s.Quantidade.ToString("N0") }).ToList(), [4, 1], [false, true]);
        }
        if (e.Equipe.Count > 0)
        {
            doc.Secao("Equipe escalada");
            doc.Tabela(["Nome", "Função", "Telefone"],
                e.Equipe.OrderBy(f => f.Funcao).Select(f => new[] { f.Funcionario?.Nome ?? "", f.Funcao, f.Funcionario?.Telefone ?? "" }).ToList(), [3, 2, 2]);
        }
        if (e.Materiais.Count > 0)
        {
            doc.Secao("Materiais (louça, enxoval e mobiliário)");
            doc.Tabela(["Material", "Quantidade", "Alugados", "Devolução"],
                e.Materiais.OrderBy(m => m.Material?.Categoria).Select(m => new[]
                {
                    m.Material?.Nome ?? "", m.Quantidade.ToString("N0"), m.QuantidadeAlugada.ToString("N0"),
                    m.Devolvido ? $"{m.QuantidadeDevolvida} ok / {m.QuantidadePerdida} perdas" : "[   ]"
                }).ToList(), [4, 1.2f, 1.2f, 2], [false, true, true, false]);
        }
        if (_insumosPrevistos.Itens.Count > 0)
        {
            doc.Secao("Insumos previstos");
            doc.Tabela(["Insumo", "Quantidade", "Unidade"],
                _insumosPrevistos.Itens.Select(i => new[] { i.Produto, i.Quantidade.ToString("N3"), i.Unidade }).ToList(), [4, 1.5f, 1], [false, true, false]);
        }
        if (e.Convidados.Count > 0)
        {
            doc.Secao("Lista de convidados (recepção)");
            doc.Tabela(["Nome", "Acomp.", "Mesa", "Presença", "Check-in"],
                e.Convidados.OrderBy(c => c.Nome).Select(c => new[]
                {
                    c.Nome, c.Acompanhantes.ToString(), c.Mesa ?? "", Textos.Exibir(c.Rsvp), c.CheckInEm?.ToString("HH:mm") ?? "[   ]"
                }).ToList(), [4, 1, 1.2f, 1.5f, 1.2f]);
        }
        if (!string.IsNullOrWhiteSpace(e.Observacoes))
        {
            doc.Secao("Observações");
            doc.Paragrafo(e.Observacoes);
        }
        doc.Visualizar(this);
    }

    // =========================== Carga ===========================

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(async () =>
        {
            _funcionario.DataSource = (await _lookups.ListarAsync(TipoLookup.Funcionarios)).ToList();
            _material.DataSource = (await _lookups.ListarAsync(TipoLookup.Materiais)).ToList();
            await CarregarAsync();
        });
    }

    private async Task Recarregar(Task operacao)
    {
        await operacao;
        await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        var selecionadoConvidado = _convidados.Selecionado?.Id;
        var selecionadaParcela = _parcelas.Selecionado?.Id;

        _detalhe = await _eventos.ObterDetalheAsync(_eventoId);
        var e = Evento;

        Text = $"{e.Codigo} - {e.Titulo}";
        _titulo.Text = $"{e.Codigo}  {e.Titulo}";
        _status.Text = Textos.Exibir(e.Status).ToUpperInvariant() + (e.Status == StatusEvento.Realizado && e.EstoqueBaixado ? " • estoque baixado" : "");
        _status.ForeColor = Textos.Cor(e.Status);
        _subtitulo.Text = $"{e.Cliente?.Nome}  •  {e.Inicio:dddd, dd/MM/yyyy HH:mm} às {e.Fim:HH:mm}  •  {e.Espaco?.Nome}  •  {e.QuantidadeConvidados} convidados"
                          + (e.Orcamento is not null ? $"  •  origem: {e.Orcamento.Codigo}" : "");

        var custosEquipe = e.TotalEquipe + e.CustoMateriais + _detalhe.DespesasVinculadas;
        _valor.Atualizar(Textos.Moeda(e.ValorTotal), e.ValorNaoParcelado > 0 ? $"{Textos.Moeda(e.ValorNaoParcelado)} sem parcela" : "100% parcelado",
            e.ValorNaoParcelado > 0 ? Tema.Alerta : Tema.Primaria);
        _recebido.Atualizar(Textos.Moeda(e.TotalRecebido));
        var vencidas = e.Parcelas.Count(p => p.EstaVencido(DateOnly.FromDateTime(DateTime.Today)));
        _saldo.Atualizar(Textos.Moeda(e.ValorTotal - e.Parcelas.Where(p => p.Status == StatusTitulo.Pago).Sum(p => p.Valor)),
            vencidas > 0 ? $"{vencidas} parcela(s) vencida(s)" : null, vencidas > 0 ? Tema.Perigo : Tema.Alerta);
        _insumos.Atualizar(Textos.Moeda(_detalhe.CustoInsumosEstimado), e.Pacote?.Nome);
        _equipeCusto.Atualizar(Textos.Moeda(custosEquipe), $"{e.Equipe.Count} pessoa(s) na escala" +
                                                           (e.CustoMateriais > 0 ? $" • materiais {Textos.Moeda(e.CustoMateriais)}" : ""));
        _resultado.Atualizar(Textos.Moeda(_detalhe.ResultadoEstimado), $"margem {_detalhe.MargemEstimada:0.#}%",
            _detalhe.ResultadoEstimado >= 0 ? Tema.Sucesso : Tema.Perigo);

        PreencherDados(e);

        _cardapioLista.Items.Clear();
        if (e.Pacote is null)
            _cardapioLista.Items.Add("(evento sem pacote de cardápio)");
        else
            foreach (var item in e.Pacote.Itens.Select(i => i.ItemCardapio!).OrderBy(i => i.CategoriaCardapioId).ThenBy(i => i.Nome))
                _cardapioLista.Items.Add("•  " + item.Nome);

        _servicos.Carregar(e.Servicos);
        _insumosPrevistos.Carregar(e.Pacote is null
            ? []
            : e.Pacote.CalcularConsumo(e.QuantidadeConvidados).Select(c =>
            {
                var produto = e.Pacote.Itens.SelectMany(i => i.ItemCardapio!.Ingredientes).First(f => f.ProdutoId == c.Key).Produto!;
                return new InsumoPrevisto(produto.Nome, c.Value, produto.UnidadeMedida?.Sigla ?? "", Math.Round(c.Value * produto.CustoMedio, 2));
            }).OrderByDescending(i => i.Custo));

        _equipe.Carregar(e.Equipe.OrderBy(f => f.Funcao).ThenBy(f => f.Funcionario?.Nome));
        _convidados.Carregar(e.Convidados.OrderBy(c => c.Nome), _buscaConvidado.Text);
        if (selecionadoConvidado is { } idConvidado)
            _convidados.Selecionar(c => c.Id == idConvidado);
        _resumoConvidados.Text = $"Na lista: {e.TotalPessoasNaLista} pessoas  •  confirmadas: {e.PessoasConfirmadas}  •  presentes: {e.PessoasPresentes}" +
                                 (e.TotalPessoasNaLista > e.QuantidadeConvidados ? $"  ⚠ acima do contratado ({e.QuantidadeConvidados})" : "");
        _resumoConvidados.ForeColor = e.TotalPessoasNaLista > e.QuantidadeConvidados ? Tema.Alerta : Tema.Texto;

        var selecionadoMaterial = _materiais.Selecionado?.MaterialId;
        _materiais.Carregar(e.Materiais.OrderBy(m => m.Material?.Categoria).ThenBy(m => m.Material?.Nome));
        if (selecionadoMaterial is { } idMaterial)
            _materiais.Selecionar(m => m.MaterialId == idMaterial);
        _disponiveis = await _cardapio.DisponibilidadeMateriaisAsync(e.DataEvento, _eventoId);
        if (_quantidadeMaterial.Value == 0)
            MaterialSelecionado();
        else
            SugerirAlugadas();
        _resumoMateriais.Text = $"Locação de terceiros: {Textos.Moeda(e.Materiais.Sum(m => m.CustoLocacao))}  •  " +
                                $"perdas: {Textos.Moeda(e.Materiais.Sum(m => m.CustoPerdas))}  •  " +
                                $"pendentes de devolução: {e.MateriaisPendentesDeDevolucao}";
        await CarregarContratosAsync();

        _parcelas.Carregar(e.Parcelas.OrderBy(p => p.Numero));
        if (selecionadaParcela is { } idParcela)
            _parcelas.Selecionar(p => p.Id == idParcela);
        _resumoFinanceiro.Text = $"Contrato: {Textos.Moeda(e.ValorTotal)}  •  recebido: {Textos.Moeda(e.TotalRecebido)}  •  " +
                                 $"em aberto: {Textos.Moeda(e.SaldoAReceber)}  •  sem parcela: {Textos.Moeda(e.ValorNaoParcelado)}";

        var editavel = e.Status == StatusEvento.Confirmado;
        foreach (var controle in _controlesEdicao)
            controle.Enabled = editavel;
        foreach (var botao in _acoesCabecalho)
            botao.Visible = editavel;
    }

    private void PreencherDados(Evento e)
    {
        _dados.SuspendLayout();
        _dados.Controls.Clear();
        _dados.ColumnStyles.Clear();
        _dados.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _dados.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _dados.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _dados.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        void Par(string rotulo, string? valor)
        {
            _dados.Controls.Add(new Label { Text = rotulo, AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 4, 8, 4) });
            _dados.Controls.Add(new Label { Text = valor ?? "-", AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 4, 16, 4) });
        }

        Par("Cliente", e.Cliente?.Nome);
        Par("Contato", e.Cliente?.Celular ?? e.Cliente?.Telefone ?? e.Cliente?.Email);
        Par("Tipo", e.TipoEvento?.Nome);
        Par("Espaço", e.Espaco is null ? null : $"{e.Espaco.Nome} (até {e.Espaco.CapacidadeMaxima})");
        Par("Data", e.Inicio.ToString("dd/MM/yyyy (dddd)", Textos.PtBr));
        Par("Horário", $"{e.Inicio:HH:mm} às {e.Fim:HH:mm} ({(e.Fim - e.Inicio).TotalHours:0.#} h)");
        Par("Pacote", e.Pacote is null ? null : $"{e.Pacote.Nome} - {Textos.Moeda(e.ValorPorPessoa)}/pessoa");
        Par("Convidados", e.QuantidadeConvidados.ToString());
        Par("Vendedor", e.Vendedor is null ? null : $"{e.Vendedor.Nome} ({e.PercentualComissao:0.##}% de comissão)");
        if (e.Orcamento is not null)
            Par("Orçamento", e.Orcamento.Codigo);
        if (e.RealizadoEm is { } realizado)
            Par("Realizado em", realizado.ToString("dd/MM/yyyy HH:mm"));
        if (e.MotivoCancelamento is not null)
            Par("Cancelamento", e.MotivoCancelamento);
        if (!string.IsNullOrWhiteSpace(e.Observacoes))
            Par("Observações", e.Observacoes);
        _dados.ResumeLayout();
    }
}
