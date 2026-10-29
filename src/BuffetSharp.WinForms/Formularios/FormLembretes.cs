using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Formularios.Cadastros;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Formularios.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios;

/// <summary>
/// O que pede atenção hoje, conforme o perfil: propostas vencendo, degustações, aniversários, retornos de leads,
/// parcelas a cobrar, contas a pagar, materiais não devolvidos e tarefas dos eventos. Mensagens ao cliente saem prontas
/// para o WhatsApp ou o e-mail; depois de tratado, o lembrete some da lista.
/// </summary>
internal sealed class FormLembretes : FormListagem<Lembrete>
{
    private readonly LembreteService _servico;
    private readonly IEnvioEmail _email;
    private readonly Navegador _navegador;
    private readonly ComboBox _tipo = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private IReadOnlyList<Lembrete> _todos = [];

    public FormLembretes(LembreteService servico, IEnvioEmail email, Navegador navegador)
        : base("Lembretes", "O que precisa de atenção hoje. Envie a mensagem pronta pelo WhatsApp ou e-mail, ou marque como resolvido: o lembrete sai da lista.")
    {
        _servico = servico;
        _email = email;
        _navegador = navegador;

        _tipo.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<TipoLembrete>()).ToList();
        Filtro("Tipo", _tipo);
        _tipo.SelectionChangeCommitted += (_, _) => Filtrar();

        Botao("WhatsApp", Icones.Mensagem, WhatsAppAsync, "Envia a mensagem pronta para o cliente pelo WhatsApp", destaque: true);
        Botao("E-mail", Icones.Email, EmailAsync, "Envia a mensagem por e-mail");
        Botao("Resolvido", Icones.Confirmar, ResolverAsync, "Tira o lembrete da lista (já tratado de outra forma); numa tarefa de evento, conclui a tarefa");
        Separador();
        Botao("Abrir", Icones.Editar, AbrirAsync, "Abre o evento, orçamento, lead ou conta do lembrete");
        Botao("WhatsApp do buffet", Icones.Configuracoes, SituacaoWhatsAppAsync, "Conexão do WhatsApp (Z-API) e mensagem de teste");

        var hoje = DateTime.Today;
        Grade.Texto("Tipo", l => Textos.Exibir(l.Tipo), 26, l => Cor(l.Tipo))
            .Data("Data", l => l.Data, "dd/MM/yy HH:mm", 20, l => l.Data.Date < hoje ? Tema.Perigo : l.Data.Date == hoje ? Tema.Alerta : null)
            .Texto("Lembrete", l => l.Titulo, 45)
            .Texto("Detalhe", l => l.Detalhe, 60)
            .Texto("Com quem", l => l.Pessoa, 35)
            .Texto("Contato", l => l.Telefone ?? l.Email, 25)
            .Moeda("Valor", l => l.Valor, 20)
            .Busca(l => $"{l.Titulo} {l.Detalhe} {l.Pessoa} {l.Telefone} {l.Email}");
    }

    private static Color? Cor(TipoLembrete tipo) => tipo switch
    {
        TipoLembrete.ParcelaVencida or TipoLembrete.MaterialNaoDevolvido or TipoLembrete.Detrator => Tema.Perigo,
        TipoLembrete.Aniversario => Tema.Destaque,
        TipoLembrete.EsperaData or TipoLembrete.AvaliacaoGoogle => Tema.Sucesso,
        TipoLembrete.ContaPagarAVencer or TipoLembrete.ParcelaAVencer => Tema.Alerta,
        _ => Tema.Info
    };

    protected override async Task CarregarAsync()
    {
        _todos = await _servico.ListarAsync();
        Filtrar();
    }

    private void Filtrar()
    {
        var tipo = (_tipo.SelectedItem as OpcaoEnum<TipoLembrete>)?.Valor;
        Exibir(tipo is null ? _todos : _todos.Where(l => l.Tipo == tipo));
    }

    protected override string TextoRodape(IReadOnlyList<Lembrete> visiveis) =>
        visiveis.Count == 0
            ? "Nada pendente por hoje."
            : $"{visiveis.Count} lembrete(s)  •  " + string.Join("  •  ", visiveis.GroupBy(l => l.Tipo).OrderBy(g => g.Key)
                .Select(g => $"{Textos.Exibir(g.Key).ToLowerInvariant()}: {g.Count()}"));

    protected override Task AoAbrirAsync(Lembrete item) => AbrirAsync(item);

    private async Task WhatsAppAsync()
    {
        if (Selecionado() is not { } lembrete)
            return;
        if (await _servico.PrepararWhatsAppAsync(lembrete) is not { } mensagem)
        {
            Mensagens.Aviso(this, "Este lembrete é uma tarefa interna, sem mensagem para o cliente. Use \"Abrir\" ou \"Resolvido\".");
            return;
        }
        if (DialogosEnvio.WhatsApp(this, mensagem, "lembrete") is null)
            return;
        await _servico.MarcarTratadoAsync(lembrete, CanalLembrete.WhatsApp);
        await CarregarAsync();
    }

    /// <summary>Mostra se o WhatsApp do buffet está conectado e, se estiver, envia uma mensagem de teste.</summary>
    private async Task SituacaoWhatsAppAsync()
    {
        var envio = _navegador.Servico<IEnvioWhatsApp>();
        var situacao = await envio.SituacaoAsync();
        if (!situacao.Conectado || !envio.Direto)
        {
            Mensagens.Aviso(this, situacao.Descricao, "WhatsApp do buffet");
            return;
        }
        if (!Mensagens.Confirmar(this, situacao.Descricao + "\n\nEnviar uma mensagem de teste?", "WhatsApp do buffet"))
            return;
        var telefone = Dialogos.PedirTexto(this, "Mensagem de teste", "Celular com DDD", linhas: 1);
        if (telefone is null)
            return;
        await envio.EnviarAsync(telefone, $"Mensagem de teste do BuffetSharp ({DateTime.Now:dd/MM/yyyy HH:mm}).");
        Mensagens.Info(this, "Mensagem de teste enviada.", "WhatsApp do buffet");
    }

    private async Task EmailAsync()
    {
        if (Selecionado() is not { } lembrete)
            return;
        if (await _servico.PrepararEmailAsync(lembrete) is not { } email)
        {
            Mensagens.Aviso(this, "Este lembrete é uma tarefa interna, sem mensagem para o cliente. Use \"Abrir\" ou \"Resolvido\".");
            return;
        }
        if (!DialogosEnvio.Email(this, "Enviar lembrete por e-mail", email, null, null, e => _servico.EnviarEmailAsync(lembrete, e), _email.Destino))
            return;
        await CarregarAsync();
    }

    private async Task ResolverAsync()
    {
        if (Selecionado() is not { } lembrete)
            return;
        await _servico.MarcarTratadoAsync(lembrete, CanalLembrete.Manual);
        await CarregarAsync();
    }

    private Task AbrirAsync() => Selecionado() is { } lembrete ? AbrirAsync(lembrete) : Task.CompletedTask;

    private Task AbrirAsync(Lembrete lembrete)
    {
        switch (lembrete.Tipo)
        {
            case TipoLembrete.OrcamentoVencendo:
                using (var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(lembrete.ReferenciaId)))
                    editor.ShowDialog(this);
                break;
            case TipoLembrete.Degustacao:
                _navegador.Abrir<FormDegustacoes>();
                break;
            case TipoLembrete.Aniversario when lembrete.ClienteId is { } clienteId:
                using (var historico = _navegador.Criar<FormHistoricoCliente>(clienteId, lembrete.Pessoa ?? ""))
                    historico.ShowDialog(this);
                break;
            case TipoLembrete.RetornoLead:
                _navegador.Abrir<FormLeads>();
                break;
            case TipoLembrete.ContaPagarAVencer:
                _navegador.Abrir<FormContasPagar>();
                break;
            case TipoLembrete.EsperaData:
                _navegador.Abrir<FormListaEspera>();
                break;
            case TipoLembrete.PesquisaSatisfacao or TipoLembrete.Detrator or TipoLembrete.AvaliacaoGoogle:
                _navegador.Abrir<FormPesquisasSatisfacao>();
                break;
            default:
                if (lembrete.EventoId is { } eventoId)
                    _navegador.Abrir($"evento:{eventoId}", () => _navegador.Criar<FormEventoDetalhe>(eventoId));
                break;
        }
        return Task.CompletedTask;
    }
}
