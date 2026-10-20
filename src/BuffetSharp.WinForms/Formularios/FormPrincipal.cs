using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Seguranca;
using BuffetSharp.WinForms.Formularios.Cadastros;
using BuffetSharp.WinForms.Formularios.Cardapio;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Formularios.Estoque;
using BuffetSharp.WinForms.Formularios.Financeiro;
using BuffetSharp.WinForms.Formularios.Relatorios;
using BuffetSharp.WinForms.Formularios.Seguranca;
using BuffetSharp.WinForms.Infra;
using BuffetSharp.Infrastructure.Persistencia;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.WinForms.Formularios;

/// <summary>
/// Janela principal: menu lateral (filtrado pelo perfil do usuário), barra superior e telas em abas.
/// </summary>
internal sealed class FormPrincipal : FormBase
{
    private const PerfilUsuario Adm = PerfilUsuario.Administrador;
    private const PerfilUsuario Com = PerfilUsuario.Comercial;
    private const PerfilUsuario Fin = PerfilUsuario.Financeiro;
    private const PerfilUsuario Ope = PerfilUsuario.Operacional;

    private sealed record ItemMenu(string Secao, string Texto, char Icone, PerfilUsuario[] Perfis, Action Abrir);

    private readonly Navegador _navegador;
    private readonly ISessaoUsuario _sessao;
    private readonly IBackupBanco _backup;
    private readonly ILogger<FormPrincipal> _log;
    private readonly ToolStripStatusLabel _statusBackup = new() { ForeColor = Tema.TextoSuave };
    private readonly TabControl _abas;
    private Button? _itemAtivo;

    public bool TrocarUsuario { get; private set; }

    public FormPrincipal(Navegador navegador, ISessaoUsuario sessao, BuffetOptions opcoes, FabricaConexao banco, IBackupBanco backup,
        ILogger<FormPrincipal> log)
    {
        _navegador = navegador;
        _sessao = sessao;
        _backup = backup;
        _log = log;
        var usuario = sessao.Usuario ?? throw new InvalidOperationException("Usuário não autenticado.");

        Text = $"{opcoes.NomeEmpresa} - BuffetSharp";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1366, 820);
        MinimumSize = new Size(1100, 680);
        WindowState = FormWindowState.Maximized;
        ShowIcon = true;
        Icon = SystemIcons.Application;

        _abas = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Padding = new Point(14, 6),
            Font = Tema.Fonte
        };
        _abas.DrawItem += DesenharAba;
        _abas.MouseDown += CliqueNaAba;

        var conteudo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };
        conteudo.Controls.Add(_abas);

        Controls.Add(conteudo);
        Controls.Add(CriarBarraSuperior(usuario));
        Controls.Add(CriarMenuLateral(usuario));
        Controls.Add(CriarBarraStatus(usuario, banco, _statusBackup));
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _navegador.Configurar(_abas);
        _navegador.Abrir<FormDashboard>();
        _ = BackupAutomaticoAsync();
        _ = RotinasDiariasAsync();
    }

    /// <summary>
    /// Depois do login, em segundo plano: aplica os reajustes de preço cuja vigência chegou e, se configurado, envia os
    /// lembretes automáticos por e-mail. Falhas ficam no log, sem interromper o usuário.
    /// </summary>
    private async Task RotinasDiariasAsync()
    {
        try
        {
            await _navegador.Servico<ReajusteService>().AplicarPendentesAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Os reajustes de preço agendados não foram aplicados");
        }

        try
        {
            if (_sessao.Usuario?.PossuiPerfil(PerfilUsuario.Comercial, PerfilUsuario.Financeiro) == true)
                await Task.Run(() => _navegador.Servico<LembreteService>().EnviarEmailsAutomaticosAsync());
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Os lembretes automáticos por e-mail não foram enviados");
        }
    }

    /// <summary>Cópia de segurança diária em segundo plano; uma falha fica no log e na barra de status, sem interromper o usuário.</summary>
    private async Task BackupAutomaticoAsync()
    {
        if (_backup.Indisponivel is not null)
            return;
        MostrarUltimoBackup();
        try
        {
            if (await Task.Run(() => _backup.FazerSeNecessarioAsync()) is not null && !IsDisposed)
                MostrarUltimoBackup();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "A cópia de segurança automática falhou");
            if (IsDisposed)
                return;
            _statusBackup.Text = "Cópia de segurança automática falhou (veja Administração › Backup e logs)";
            _statusBackup.ForeColor = Tema.Perigo;
        }
    }

    private void MostrarUltimoBackup()
    {
        var ultima = _backup.Listar().FirstOrDefault();
        _statusBackup.Text = ultima is null ? "Nenhuma cópia de segurança" : $"Último backup: {ultima.CriadoEm:dd/MM HH:mm}";
        _statusBackup.ForeColor = ultima is null || ultima.CriadoEm < DateTime.Now.AddDays(-7) ? Tema.Alerta : Tema.TextoSuave;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!TrocarUsuario && e.CloseReason == CloseReason.UserClosing &&
            !Mensagens.Confirmar(this, "Deseja realmente sair do BuffetSharp?", "Sair"))
        {
            e.Cancel = true;
            return;
        }
        _navegador.FecharTodas();
        base.OnFormClosing(e);
    }

    // ============================== Menu ==============================

    private IEnumerable<ItemMenu> Itens()
    {
        PerfilUsuario[] todos = [Adm, Com, Fin, Ope];
        var n = _navegador;
        return
        [
            new("", "Painel", Icones.Inicio, todos, () => n.Abrir<FormDashboard>()),
            new("", "Agenda", Icones.Calendario, todos, () => n.Abrir<FormAgenda>()),
            new("", "Lembretes", Icones.Relogio, todos, () => n.Abrir<FormLembretes>()),

            new("COMERCIAL", "Leads (funil)", Icones.Filtro, [Adm, Com], () => n.Abrir<FormLeads>()),
            new("COMERCIAL", "Orçamentos", Icones.Documento, [Adm, Com], () => n.Abrir<FormOrcamentos>()),
            new("COMERCIAL", "Clientes", Icones.Contato, [Adm, Com, Fin], () => Cadastro(DefinicoesCadastros.Clientes)),
            new("COMERCIAL", "Degustações", Icones.Estrela, [Adm, Com], () => n.Abrir<FormDegustacoes>()),
            new("COMERCIAL", "Contratos", Icones.Pagina, [Adm, Com, Fin], () => n.Abrir<FormContratos>()),
            new("COMERCIAL", "Modelos de contrato", Icones.Documento, [Adm, Com], () => Cadastro(DefinicoesCadastros.ModelosContrato)),

            new("EVENTOS", "Eventos", Icones.Bandeira, todos, () => n.Abrir<FormEventos>()),
            new("EVENTOS", "Produção da cozinha", Icones.Lista, [Adm, Ope], () => n.Abrir<FormProducao>()),

            new("CARDÁPIO", "Itens do cardápio", Icones.Lista, [Adm, Com, Ope], () => n.Abrir<FormItensCardapio>()),
            new("CARDÁPIO", "Pacotes", Icones.Biblioteca, [Adm, Com, Ope], () => n.Abrir<FormPacotes>()),
            new("CARDÁPIO", "Reajuste de preços", Icones.Calculadora, [Adm, Com], () => n.Abrir<FormReajustes>()),

            new("ESTOQUE", "Produtos", Icones.Carrinho, [Adm, Ope], () => Cadastro(DefinicoesCadastros.Produtos)),
            new("ESTOQUE", "Movimentações", Icones.Historico, [Adm, Ope], () => n.Abrir<FormMovimentacoes>()),
            new("ESTOQUE", "Lista de compras", Icones.Carrinho, [Adm, Ope], () => n.Abrir<FormListaCompras>()),
            new("ESTOQUE", "Materiais e enxoval", Icones.Pacote, [Adm, Ope], () => Cadastro(DefinicoesCadastros.Materiais)),

            new("FINANCEIRO", "Contas a receber", Icones.Cartao, [Adm, Fin], () => n.Abrir<FormContasReceber>()),
            new("FINANCEIRO", "Contas a pagar", Icones.Banco, [Adm, Fin], () => n.Abrir<FormContasPagar>()),
            new("FINANCEIRO", "Caixa e bancos", Icones.Dinheiro, [Adm, Fin], () => n.Abrir<FormContasFinanceiras>()),
            new("FINANCEIRO", "Comissões", Icones.Dinheiro, [Adm, Fin], () => n.Abrir<FormComissoes>()),
            new("FINANCEIRO", "Pagamento da equipe", Icones.Pessoas, [Adm, Fin], () => n.Abrir<FormPagamentoEquipe>()),
            new("FINANCEIRO", "Relatórios", Icones.Calculadora, [Adm, Fin], () => n.Abrir<FormRelatorios>()),

            new("CADASTROS", "Tipos de evento", Icones.Etiqueta, [Adm, Com], () => Cadastro(DefinicoesCadastros.TiposEvento)),
            new("CADASTROS", "Espaços", Icones.Loja, [Adm, Com], () => Cadastro(DefinicoesCadastros.Espacos)),
            new("CADASTROS", "Serviços adicionais", Icones.Etiqueta, [Adm, Com], () => Cadastro(DefinicoesCadastros.ServicosAdicionais)),
            new("CADASTROS", "Funcionários", Icones.Pessoas, [Adm, Ope], () => Cadastro(DefinicoesCadastros.Funcionarios)),
            new("CADASTROS", "Cargos", Icones.Etiqueta, [Adm, Ope], () => Cadastro(DefinicoesCadastros.Cargos)),
            new("CADASTROS", "Modelos de checklist", Icones.Confirmar, [Adm, Com, Ope], () => Cadastro(DefinicoesCadastros.ModelosChecklist)),
            new("CADASTROS", "Fornecedores", Icones.Contato, [Adm, Fin, Ope], () => Cadastro(DefinicoesCadastros.Fornecedores)),
            new("CADASTROS", "Categorias de produto", Icones.Etiqueta, [Adm, Ope], () => Cadastro(DefinicoesCadastros.CategoriasProduto)),
            new("CADASTROS", "Seções do cardápio", Icones.Etiqueta, [Adm, Ope], () => Cadastro(DefinicoesCadastros.CategoriasCardapio)),
            new("CADASTROS", "Unidades de medida", Icones.Etiqueta, [Adm, Ope], () => Cadastro(DefinicoesCadastros.UnidadesMedida)),
            new("CADASTROS", "Formas de pagamento", Icones.Cartao, [Adm, Fin], () => Cadastro(DefinicoesCadastros.FormasPagamento)),
            new("CADASTROS", "Cartões de crédito", Icones.Cartao, [Adm, Fin], () => Cadastro(DefinicoesCadastros.CartoesCredito)),
            new("CADASTROS", "Condições de pagamento", Icones.Calendario, [Adm, Com, Fin], () => Cadastro(DefinicoesCadastros.CondicoesPagamento)),

            new("ADMINISTRAÇÃO", "Usuários", Icones.Cadeado, [Adm], () => n.Abrir<FormUsuarios>()),
            new("ADMINISTRAÇÃO", "Auditoria", Icones.Escudo, [Adm], () => n.Abrir<FormAuditoria>()),
            new("ADMINISTRAÇÃO", "Backup e logs", Icones.Salvar, [Adm], () => n.Abrir<FormManutencao>())
        ];
    }

    private void Cadastro<T>(DefinicaoCadastro<T> definicao) where T : Domain.Comum.Entidade, new() =>
        _navegador.Abrir($"cadastro:{typeof(T).Name}", () => _navegador.Criar<FormCadastro<T>>(definicao));

    private Control CriarMenuLateral(UsuarioLogado usuario)
    {
        var lateral = new Panel { Dock = DockStyle.Left, Width = LogicalToDeviceUnits(232), BackColor = Tema.Lateral };
        var lista = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 8, 0, 16)
        };

        var marca = new Label
        {
            Text = "BuffetSharp",
            Font = new Font("Segoe UI Semibold", 17f),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(8, 14, 0, 0)
        };
        var slogan = new Label
        {
            Text = "Gestão de buffets e eventos",
            ForeColor = Tema.Destaque,
            AutoSize = true,
            Margin = new Padding(9, 0, 0, 14)
        };
        lista.Controls.Add(marca);
        lista.Controls.Add(slogan);

        var largura = LogicalToDeviceUnits(206);
        string? secaoAtual = null;
        foreach (var item in Itens().Where(i => usuario.PossuiPerfil(i.Perfis)))
        {
            if (item.Secao != secaoAtual && item.Secao.Length > 0)
            {
                lista.Controls.Add(new Label
                {
                    Text = item.Secao,
                    ForeColor = Color.FromArgb(170, 150, 158),
                    Font = new Font("Segoe UI Semibold", 8f),
                    AutoSize = true,
                    Margin = new Padding(10, 14, 0, 4)
                });
            }
            secaoAtual = item.Secao;

            var botao = new Button
            {
                Text = "   " + item.Texto,
                Image = Icones.Imagem(item.Icone, Tema.LateralTexto),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Tema.LateralTexto,
                BackColor = Tema.Lateral,
                Width = largura,
                Height = LogicalToDeviceUnits(36),
                Margin = new Padding(0, 1, 0, 1),
                Padding = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            botao.FlatAppearance.BorderSize = 0;
            botao.FlatAppearance.MouseOverBackColor = Tema.LateralHover;
            botao.Click += (_, _) =>
            {
                MarcarAtivo(botao);
                try
                {
                    item.Abrir();
                }
                catch (Exception ex)
                {
                    Mensagens.Erro(this, ex);
                }
            };
            lista.Controls.Add(botao);
        }

        lateral.Controls.Add(lista);
        return lateral;
    }

    private void MarcarAtivo(Button botao)
    {
        if (_itemAtivo is not null)
        {
            _itemAtivo.BackColor = Tema.Lateral;
            _itemAtivo.Font = Tema.Fonte;
        }
        _itemAtivo = botao;
        botao.BackColor = Tema.Primaria;
        botao.Font = Tema.FonteNegrito;
    }

    private Control CriarBarraSuperior(UsuarioLogado usuario)
    {
        var barra = new Panel { Dock = DockStyle.Top, Height = LogicalToDeviceUnits(54), BackColor = Tema.Superficie };
        barra.Paint += (_, e) =>
        {
            using var caneta = new Pen(Tema.Borda);
            e.Graphics.DrawLine(caneta, 0, barra.Height - 1, barra.Width, barra.Height - 1);
        };

        var saudacao = DateTime.Now.Hour switch { < 12 => "Bom dia", < 18 => "Boa tarde", _ => "Boa noite" };
        var esquerda = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, Padding = new Padding(18, 8, 0, 0), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        esquerda.Controls.Add(new Label { Text = $"{saudacao}, {usuario.Nome.Split(' ')[0]}!", Font = Tema.FonteSubtitulo, AutoSize = true });
        esquerda.Controls.Add(new Label
        {
            Text = DateTime.Today.ToString("dddd, dd 'de' MMMM 'de' yyyy", Textos.PtBr),
            ForeColor = Tema.TextoSuave,
            AutoSize = true
        });

        var direita = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, Padding = new Padding(0, 10, 14, 0), WrapContents = false };
        var perfil = new Label
        {
            Text = $"{usuario.Nome}  •  {Textos.Exibir(usuario.Perfil)}",
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Margin = new Padding(0, 8, 12, 0)
        };
        var senha = Tema.BotaoSecundario("Alterar senha", Icones.Cadeado);
        senha.Click += (_, _) => AlteracaoSenha.Exibir(this, _navegador.Servico<UsuarioService>());
        var trocar = Tema.BotaoSecundario("Trocar usuário", Icones.Pessoas);
        trocar.Click += (_, _) =>
        {
            TrocarUsuario = true;
            Close();
        };
        var sair = Tema.BotaoSecundario("Sair", Icones.Sair);
        sair.Click += (_, _) => Close();
        direita.Controls.AddRange([perfil, senha, trocar, sair]);

        barra.Controls.Add(direita);
        barra.Controls.Add(esquerda);
        return barra;
    }

    private static Control CriarBarraStatus(UsuarioLogado usuario, FabricaConexao conexoes, ToolStripStatusLabel backup)
    {
        var banco = conexoes.Provedor.DescreverConexao(conexoes.ConnectionString);

        var status = new StatusStrip { BackColor = Tema.Fundo, SizingGrip = false };
        status.Items.Add(new ToolStripStatusLabel($"Banco: {banco}") { ForeColor = Tema.TextoSuave });
        status.Items.Add(backup);
        status.Items.Add(new ToolStripStatusLabel { Spring = true });
        status.Items.Add(new ToolStripStatusLabel($"Usuário: {usuario.Login} ({Textos.Exibir(usuario.Perfil)})") { ForeColor = Tema.TextoSuave });
        status.Items.Add(new ToolStripStatusLabel($"v{typeof(FormPrincipal).Assembly.GetName().Version?.ToString(3)}  •  .NET {Environment.Version.ToString(2)}")
            { ForeColor = Tema.TextoSuave });
        return status;
    }

    // ============================== Abas ==============================

    private Rectangle AreaFechar(int indice)
    {
        var aba = _abas.GetTabRect(indice);
        var lado = LogicalToDeviceUnits(16);
        return new Rectangle(aba.Right - lado - 6, aba.Top + (aba.Height - lado) / 2, lado, lado);
    }

    private void DesenharAba(object? sender, DrawItemEventArgs e)
    {
        var aba = _abas.TabPages[e.Index];
        var area = _abas.GetTabRect(e.Index);
        var selecionada = e.Index == _abas.SelectedIndex;

        using (var fundo = new SolidBrush(selecionada ? Tema.Superficie : Tema.Fundo))
            e.Graphics.FillRectangle(fundo, area);
        if (selecionada)
        {
            using var destaque = new SolidBrush(Tema.Primaria);
            e.Graphics.FillRectangle(destaque, area.Left, area.Top, area.Width, 3);
        }

        var texto = aba.Text.TrimEnd();
        TextRenderer.DrawText(e.Graphics, texto, selecionada ? Tema.FonteNegrito : Tema.Fonte,
            new Rectangle(area.Left + 8, area.Top, area.Width - 30, area.Height),
            selecionada ? Tema.Texto : Tema.TextoSuave, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        var fechar = AreaFechar(e.Index);
        TextRenderer.DrawText(e.Graphics, "✕", Tema.FontePequena, fechar, Tema.TextoSuave,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void CliqueNaAba(object? sender, MouseEventArgs e)
    {
        for (var i = 0; i < _abas.TabPages.Count; i++)
        {
            if (!_abas.GetTabRect(i).Contains(e.Location))
                continue;
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && AreaFechar(i).Contains(e.Location)))
                _navegador.Fechar(_abas.TabPages[i]);
            return;
        }
    }
}
