using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>
/// Funil de vendas: interessados (leads) desde o primeiro contato até virarem clientes com orçamento, com os retornos
/// combinados, o histórico das conversas e o motivo das perdas.
/// </summary>
internal sealed class FormLeads : FormListagem<LeadResumo>
{
    private const string EmAberto = "Em negociação";
    private readonly IConsultasLeads _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly LeadService _servico;
    private readonly Navegador _navegador;
    private readonly ComboBox _situacao = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _retornosHoje = new() { Text = "Só retornos até hoje", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    public FormLeads(IConsultasLeads consultas, IConsultasLookup lookups, LeadService servico, Navegador navegador)
        : base("Leads (funil de vendas)", "Interessados que ainda não são clientes. Registre cada contato, combine o próximo retorno e converta em cliente para fazer o orçamento.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;
        _navegador = navegador;

        _situacao.DataSource = new List<object> { EmAberto, "(todos)" }.Concat(Textos.Opcoes<StatusLead>()).ToList();
        Filtro("Situação", _situacao);
        Filtros.Controls.Add(_retornosHoje);
        _situacao.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();
        _retornosHoje.CheckedChanged += async (_, _) => await RecarregarAsync();

        Botao("Novo lead", Icones.Adicionar, () => EditarAsync(0), "Registrar um interessado", destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } l ? EditarAsync(l.Id) : Task.CompletedTask);
        Separador();
        Botao("Registrar contato", Icones.Mensagem, RegistrarContatoAsync, "Anota a conversa e combina o próximo retorno");
        Botao("WhatsApp", Icones.Enviar, AbrirWhatsAppAsync, "Abre uma conversa no WhatsApp com o lead");
        Botao("Histórico", Icones.Historico, HistoricoAsync, "Conversas registradas com o lead");
        Separador();
        Botao("Converter em cliente", Icones.Aceitar, ConverterAsync, "Cadastra o cliente e abre um orçamento para ele");
        Botao("Perdido", Icones.Cancelar, PerderAsync, "Encerra a negociação informando o motivo");
        Botao("Reabrir", Icones.Desfazer, () => AlterarAsync(l => _servico.ReabrirAsync(l.Id)), "Volta um lead perdido para a negociação");
        Botao("Excluir", Icones.Excluir, ExcluirAsync);

        var hoje = DateTime.Today;
        Grade.Texto("Código", l => l.Codigo, 20)
            .Data("Retorno", l => l.ProximoContato, peso: 20, cor: l => l.EmAberto && l.ProximoContato < hoje ? Tema.Perigo
                : l.ProximoContato == hoje ? Tema.Alerta : null)
            .Texto("Nome", l => l.Nome, 45)
            .Texto("Telefone", l => l.Telefone ?? l.Email, 28)
            .Texto("Origem", l => Textos.Exibir(l.Origem) + (l.DetalheOrigem is { } d ? $" ({d})" : ""), 30)
            .Texto("Evento", l => l.TipoEvento, 25)
            .Data("Data prevista", l => l.DataPrevista, peso: 20)
            .Numero("Conv.", l => l.Convidados, peso: 12)
            .Status("Situação", l => l.Status, 20)
            .Texto("Responsável", l => l.Responsavel, 25)
            .Data("Último contato", l => l.UltimoContato, "dd/MM HH:mm", 20)
            .Numero("Contatos", l => l.Contatos, peso: 13)
            .Texto("Cliente / motivo", l => l.Cliente ?? l.MotivoPerda, 35)
            .Busca(l => $"{l.Codigo} {l.Nome} {l.Telefone} {l.Email} {l.DetalheOrigem} {l.TipoEvento} {l.Responsavel} {l.Cliente}");
    }

    protected override async Task CarregarAsync()
    {
        var situacao = _situacao.SelectedItem;
        Exibir(await _consultas.ListarAsync(new FiltroLeads
        {
            SomenteEmAberto = situacao is EmAberto,
            Status = (situacao as OpcaoEnum<StatusLead>)?.Valor,
            RetornoAte = _retornosHoje.Checked ? DateTime.Today : null
        }));
    }

    protected override string TextoRodape(IReadOnlyList<LeadResumo> visiveis)
    {
        var decididos = visiveis.Count(l => !l.EmAberto);
        var convertidos = visiveis.Count(l => l.Status == StatusLead.Convertido);
        return $"{visiveis.Count} lead(s)  •  em negociação: {visiveis.Count(l => l.EmAberto)}  •  " +
               $"retornos atrasados: {visiveis.Count(l => l.EmAberto && l.ProximoContato < DateTime.Today)}  •  " +
               $"convertidos: {convertidos}  •  perdidos: {visiveis.Count(l => l.Status == StatusLead.Perdido)}" +
               (decididos > 0 ? $"  •  conversão: {convertidos * 100m / decididos:0.#}%" : "");
    }

    protected override Task AoAbrirAsync(LeadResumo item) => EditarAsync(item.Id);

    private async Task EditarAsync(int id)
    {
        var input = id == 0 ? _servico.Novo() : await _servico.ObterAsync(id);
        var tipos = await _lookups.ListarAsync(TipoLookup.TiposEvento);
        var responsaveis = await _lookups.ListarAsync(TipoLookup.Vendedores);

        var e = new EditorCampos();
        e.Texto("Nome", () => input.Nome, v => input.Nome = v ?? "", nameof(input.Nome), Lead.TamanhoNome, span: 2);
        e.Mascara("Telefone / WhatsApp", "(00) 00000-0000", () => input.Telefone, v => input.Telefone = v, nameof(input.Telefone));
        e.Texto("E-mail", () => input.Email, v => input.Email = v, nameof(input.Email), 150);
        e.Opcoes("Origem", () => input.Origem, v => input.Origem = v, nameof(input.Origem));
        e.Texto("Detalhe da origem", () => input.DetalheOrigem, v => input.DetalheOrigem = v, nameof(input.DetalheOrigem), 150,
            dica: "Quem indicou, qual anúncio, qual feira...");
        e.Lista("Tipo de evento", tipos, () => input.TipoEventoId, v => input.TipoEventoId = v, nameof(input.TipoEventoId), permitirVazio: true);
        e.Data("Data prevista", () => input.DataPrevista, v => input.DataPrevista = v, nameof(input.DataPrevista), opcional: true);
        e.Inteiro("Convidados (aprox.)", () => input.Convidados ?? 0, v => input.Convidados = v > 0 ? v : null, nameof(input.Convidados));
        e.Lista("Responsável", responsaveis, () => input.ResponsavelId, v => input.ResponsavelId = v, nameof(input.ResponsavelId),
            permitirVazio: true);
        e.Data("Próximo retorno", () => input.ProximoContato, v => input.ProximoContato = v, nameof(input.ProximoContato), opcional: true);
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), 2000, span: 2, linhas: 3,
            dica: "O que o interessado procura, orçamento esperado, preferências...");

        using var dialogo = new FormEdicao(id == 0 ? "Novo lead" : $"Editar LEAD-{id:D5}",
            input.Status is StatusLead.Novo or StatusLead.EmContato ? "Informe ao menos um telefone ou e-mail para o retorno." : $"Situação: {Textos.Exibir(input.Status)}",
            e, async () => id = await _servico.SalvarAsync(input), 680);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        await CarregarAsync();
        Grade.Selecionar(l => l.Id == id);
    }

    private async Task RegistrarContatoAsync()
    {
        if (Selecionado() is not { } lead)
            return;
        string? descricao = null;
        DateOnly? retorno = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var e = new EditorCampos(1);
        e.Texto("O que foi conversado", () => descricao, v => descricao = v, "Descricao", Domain.Comercial.LeadInteracao.TamanhoDescricao,
            linhas: 5, dica: "Ex.: enviou fotos do salão; quer degustação; vai conversar com o noivo...");
        e.Data("Próximo retorno", () => retorno, v => retorno = v, opcional: true);
        using var dialogo = new FormEdicao("Registrar contato", $"{lead.Codigo} - {lead.Nome}", e,
            () => _servico.RegistrarContatoAsync(lead.Id, descricao ?? "", retorno), 560);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await RecarregarSelecionandoAsync(lead.Id);
    }

    private Task AbrirWhatsAppAsync()
    {
        if (Selecionado() is not { } lead)
            return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(lead.Telefone))
        {
            Mensagens.Aviso(this, "Este lead não tem telefone cadastrado.");
            return Task.CompletedTask;
        }
        var mensagem = new MensagemWhatsApp(lead.Telefone, $"Olá, {lead.Nome.Split(' ')[0]}! Tudo bem?");
        DialogosEnvio.WhatsApp(this, mensagem, "mensagem");
        return Task.CompletedTask;
    }

    private async Task HistoricoAsync()
    {
        if (Selecionado() is not { } lead)
            return;
        var interacoes = await _consultas.InteracoesAsync(lead.Id);
        var grade = new Grade<InteracaoLeadResumo>();
        grade.Data("Data", i => i.Data, "dd/MM/yyyy HH:mm", 22).Texto("Usuário", i => i.Usuario, 16).Texto("Contato", i => i.Descricao, 80);
        grade.Carregar(interacoes);
        grade.Controle.Dock = DockStyle.Fill;

        using var janela = new FormBase { Text = $"Histórico - {lead.Codigo} {lead.Nome}", ClientSize = new Size(Px(760), Px(420)) };
        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        painel.Controls.Add(grade.Controle);
        janela.Controls.Add(painel);
        janela.ShowDialog(this);
    }

    private async Task ConverterAsync()
    {
        if (Selecionado() is not { } lead)
            return;
        if (!lead.EmAberto)
        {
            Mensagens.Aviso(this, "Somente leads em negociação podem ser convertidos.");
            return;
        }

        var input = new ConversaoLeadInput { LeadId = lead.Id };
        var clientes = await _lookups.ListarAsync(TipoLookup.Clientes);
        var e = new EditorCampos(1);
        e.Informacao("Lead", () => $"{lead.Nome} ({lead.Telefone ?? lead.Email})");
        e.Lista("Cliente", clientes, () => input.ClienteExistenteId, v => input.ClienteExistenteId = v, permitirVazio: true);
        int? clienteId = null;
        using (var dialogo = new FormEdicao("Converter em cliente",
                   "Deixe \"(nenhum)\" para cadastrar um cliente novo com os dados do lead, ou escolha um cliente já cadastrado.", e,
                   async () => clienteId = await _servico.ConverterAsync(input), 560))
        {
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;
        }

        await RecarregarSelecionandoAsync(lead.Id);
        if (clienteId is { } id && Mensagens.Confirmar(this, $"{lead.Nome} agora é cliente. Abrir um orçamento para ele?", "Lead convertido"))
        {
            var dataPrevista = lead.DataPrevista is { } data ? DateOnly.FromDateTime(data) : (DateOnly?)null;
            using var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(0, id, lead.TipoEventoId, dataPrevista, lead.Convidados));
            editor.ShowDialog(this);
        }
    }

    private async Task PerderAsync()
    {
        if (Selecionado() is not { } lead)
            return;
        var motivo = Dialogos.PedirTexto(this, "Lead perdido", "Motivo da perda", subtitulo: $"{lead.Codigo} - {lead.Nome}. Ex.: preço, data indisponível, fechou com outro buffet.");
        if (motivo is not null)
            await AlterarAsync(l => _servico.MarcarPerdidoAsync(l.Id, motivo));
    }

    private async Task ExcluirAsync()
    {
        if (Selecionado() is not { } lead)
            return;
        if (Mensagens.Confirmar(this, $"Excluir o lead {lead.Codigo} - {lead.Nome} e o histórico de contatos?", "Excluir", perigoso: true))
        {
            await _servico.ExcluirAsync(lead.Id);
            await CarregarAsync();
        }
    }

    private async Task AlterarAsync(Func<LeadResumo, Task> acao)
    {
        if (Selecionado() is not { } lead)
            return;
        await acao(lead);
        await RecarregarSelecionandoAsync(lead.Id);
    }

    private async Task RecarregarSelecionandoAsync(int id)
    {
        await CarregarAsync();
        Grade.Selecionar(l => l.Id == id);
    }
}
