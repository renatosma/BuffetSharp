using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>
/// Aba Fornecedores: os terceiros contratados para a festa, com o custo (contas a pagar), a margem sobre o serviço vendido ao
/// cliente, a chegada no cronograma, a confirmação do fornecedor e a avaliação depois do evento.
/// </summary>
internal sealed partial class FormEventoDetalhe
{
    private readonly Grade<EventoFornecedor> _contratacoes = new();
    private readonly Label _resumoFornecedores = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(0, 8, 16, 0) };

    private TabPage CriarAbaFornecedores()
    {
        _contratacoes.Data("Chegada", f => f.Chegada, "dd/MM HH:mm", 16)
            .Texto("Serviço", f => f.Servico, 30)
            .Texto("Fornecedor", f => f.NomeFornecedor, 26)
            .Moeda("Custo", f => f.Valor, 16)
            .Moeda("Pago", f => f.ValorPago, 14)
            .Moeda("Vendido", f => ServicoVendido(f)?.Total, 16)
            .Moeda("Margem", f => ServicoVendido(f) is { } s && f.Ativo ? s.Total - Evento.CustoFornecedoresDoServico(s) : null, 16,
                f => ServicoVendido(f) is { } s && s.Total < Evento.CustoFornecedoresDoServico(s) ? Tema.Perigo : null)
            .Status("Situação", f => f.Situacao, 16)
            .Texto("Contato no dia", f => ImpressaoFornecedores.Contato(f), 24)
            .Texto("Nota", f => f.Nota is { } n ? new string('★', n) + new string('☆', 5 - n) : null, 12)
            .CorDaLinha(f => f.Ativo ? null : Tema.Neutro);

        var barra = BarraAba();
        BotaoAba(barra, "Contratar", Icones.Adicionar, () => EditarContratacaoAsync(null), primario: true);
        BotaoAba(barra, "Editar", Icones.Editar, () => ContratacaoSelecionada() is { } f ? EditarContratacaoAsync(f) : Task.CompletedTask);
        BotaoAba(barra, "Refazer pagamento", Icones.Calculadora, () => ContratacaoSelecionada() is { } f ? RefazerPagamentoAsync(f) : Task.CompletedTask);
        BotaoAba(barra, "Enviar confirmação", Icones.Mensagem, () => ContratacaoSelecionada() is { } f ? EnviarConfirmacaoAsync(f) : Task.CompletedTask,
            edicao: false);
        BotaoAba(barra, "Confirmou", Icones.Aceitar, async () =>
        {
            if (ContratacaoSelecionada() is { } f)
                await Recarregar(_eventos.ConfirmarFornecedorAsync(_eventoId, f.Id));
        });
        BotaoAba(barra, "Cancelar contratação", Icones.Cancelar, CancelarContratacaoAsync);
        BotaoAba(barra, "Avaliar", Icones.Estrela, () => ContratacaoSelecionada() is { } f ? AvaliarFornecedorAsync(f) : Task.CompletedTask, edicao: false);
        BotaoAba(barra, "Lista do dia", Icones.Imprimir, () =>
        {
            ImpressaoFornecedores.ListaDoDia(Evento, _opcoes).Visualizar(this, paisagem: true);
            return Task.CompletedTask;
        }, edicao: false);
        _contratacoes.DuploClique += async f => await ExecutarAsync(() => f.Ativo ? EditarContratacaoAsync(f) : Task.CompletedTask);

        var resumo = BarraAba();
        resumo.Controls.Add(_resumoFornecedores);

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(_contratacoes.Controle);
        painel.Controls.Add(resumo);
        painel.Controls.Add(barra);
        return new TabPage("Fornecedores") { Controls = { painel } };
    }

    private void CarregarFornecedores(Evento e)
    {
        var selecionada = _contratacoes.Selecionado?.Id;
        _contratacoes.Carregar(e.Fornecedores.OrderBy(f => f.Ativo ? 0 : 1).ThenBy(f => f.Chegada ?? DateTime.MaxValue).ThenBy(f => f.Servico));
        if (selecionada is { } id)
            _contratacoes.Selecionar(f => f.Id == id);

        var vendidos = e.Servicos.Where(s => e.Fornecedores.Any(f => f.Ativo && f.EventoServicoId == s.Id)).ToList();
        var semFornecedor = e.ServicosSemFornecedor().Select(s => s.Descricao).ToList();
        _resumoFornecedores.Text = $"Custo dos fornecedores: {e.CustoFornecedores:C}" +
                                   (vendidos.Count > 0 ? $"  •  margem nos serviços vendidos: {vendidos.Sum(s => s.Total - e.CustoFornecedoresDoServico(s)):C}" : "") +
                                   (semFornecedor.Count > 0 ? $"  •  ⚠ vendidos sem fornecedor: {string.Join(", ", semFornecedor)}" : "");
        _resumoFornecedores.ForeColor = semFornecedor.Count > 0 && e.Status == StatusEvento.Confirmado ? Tema.Alerta : Tema.Texto;
    }

    private EventoServico? ServicoVendido(EventoFornecedor f) =>
        f.EventoServicoId is { } id ? Evento.Servicos.FirstOrDefault(s => s.Id == id) : null;

    private EventoFornecedor? ContratacaoSelecionada() => _contratacoes.Selecionado;

    private async Task EditarContratacaoAsync(EventoFornecedor? contratacao)
    {
        var e = Evento;
        var nova = contratacao is null;
        var medias = await _eventos.MediasAvaliacaoAsync();
        var lookupFornecedores = await _lookups.ListarAsync(TipoLookup.Fornecedores);
        var fornecedores = lookupFornecedores
            .Select(f => new ItemLookup { Id = f.Id, Nome = medias.TryGetValue(f.Id, out var media) ? $"{f.Nome}   {media}" : f.Nome }).ToList();
        var formas = await _lookups.ListarAsync(TipoLookup.FormasPagamento);
        var servicos = e.Servicos.Select(s => new ItemLookup { Id = s.Id, Nome = $"{s.Descricao} (vendido por {s.Total:C})" }).ToList();

        var input = contratacao is null
            ? new ContratacaoFornecedorInput
            {
                Plano = new PlanoPagamentoInput { Quantidade = 1, PrimeiroVencimento = Hoje() > e.DataEvento ? Hoje() : e.DataEvento }
            }
            : new ContratacaoFornecedorInput
            {
                Id = contratacao.Id, FornecedorId = contratacao.FornecedorId, EventoServicoId = contratacao.EventoServicoId, Servico = contratacao.Servico,
                Valor = contratacao.Valor, Chegada = contratacao.Chegada, Responsavel = contratacao.Responsavel, Telefone = contratacao.Telefone,
                Observacao = contratacao.Observacao
            };
        int? fornecedorId = nova ? null : input.FornecedorId;
        var temChegada = input.Chegada is not null;
        var chegada = input.Chegada ?? e.Inicio.AddHours(-2);
        var dia = DateOnly.FromDateTime(chegada);
        var hora = TimeOnly.FromDateTime(chegada);

        var editor = new EditorCampos();
        if (nova)
            editor.Lista("Fornecedor", fornecedores, () => fornecedorId, v => fornecedorId = v, nameof(input.FornecedorId));
        editor.Lista("Serviço vendido ao cliente", servicos, () => input.EventoServicoId, v => input.EventoServicoId = v is > 0 ? v : null,
            nameof(input.EventoServicoId), permitirVazio: true);
        editor.Texto("Serviço", () => input.Servico, v => input.Servico = v ?? "", nameof(input.Servico), EventoFornecedor.TamanhoServico, span: 2,
            dica: "DJ, decoração, fotografia, bolo... (vazio: o nome do serviço vendido)");
        if (nova)
            CamposPagamento(editor, () => input.Valor, v => input.Valor = v, input.Plano, formas);
        editor.Marcador("Tem horário de chegada (entra no cronograma)", () => temChegada, v => temChegada = v, span: 2);
        editor.Data("Dia da chegada", () => dia, v => dia = v ?? dia, nameof(input.Chegada));
        editor.Hora("Horário da chegada", () => hora, v => hora = v);
        editor.Texto("Responsável no dia", () => input.Responsavel, v => input.Responsavel = v, nameof(input.Responsavel), EventoFornecedor.TamanhoResponsavel);
        editor.Texto("Telefone", () => input.Telefone, v => input.Telefone = v, nameof(input.Telefone), EventoFornecedor.TamanhoTelefone,
            dica: "Vazio: o telefone do cadastro do fornecedor");
        editor.Texto("Observação", () => input.Observacao, v => input.Observacao = v, nameof(input.Observacao), EventoFornecedor.TamanhoObservacao,
            span: 2, linhas: 2);

        using var dialogo = new FormEdicao(nova ? "Contratar fornecedor" : $"Editar contratação - {contratacao!.NomeFornecedor}",
            nova ? $"{e.Codigo} - {e.Titulo}. O valor vira contas a pagar (Serviços) ligadas ao evento." : null, editor, () =>
            {
                input.FornecedorId = fornecedorId ?? 0;
                input.Chegada = temChegada ? dia.ToDateTime(hora) : null;
                if (string.IsNullOrWhiteSpace(input.Servico) && input.EventoServicoId is { } servicoId)
                    input.Servico = e.Servicos.First(s => s.Id == servicoId).Descricao;
                return _eventos.SalvarFornecedorAsync(_eventoId, input);
            }, 720);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    /// <summary>Valor combinado e como ele vai ser pago (parcelas, vencimento, intervalo e forma).</summary>
    private static void CamposPagamento(EditorCampos editor, Func<decimal> valor, Action<decimal> definirValor, PlanoPagamentoInput plano,
        IReadOnlyList<ItemLookup> formas)
    {
        editor.Decimal("Valor combinado (R$)", valor, definirValor, nameof(ContratacaoFornecedorInput.Valor));
        editor.Inteiro("Parcelas", () => plano.Quantidade, v => plano.Quantidade = v, nameof(plano.Quantidade), 1, PlanoParcelas.MaximoParcelas);
        editor.Data("Primeiro vencimento", () => plano.PrimeiroVencimento, v => plano.PrimeiroVencimento = v ?? plano.PrimeiroVencimento,
            nameof(plano.PrimeiroVencimento));
        editor.Inteiro("Intervalo (dias; 0 = mensal)", () => plano.IntervaloDias ?? 0, v => plano.IntervaloDias = v, nameof(plano.IntervaloDias), 0, 365);
        editor.Lista("Forma de pagamento", formas, () => plano.FormaPagamentoId, v => plano.FormaPagamentoId = v is > 0 ? v : null,
            nameof(plano.FormaPagamentoId), permitirVazio: true);
    }

    private static DateOnly Hoje() => DateOnly.FromDateTime(DateTime.Today);

    private async Task RefazerPagamentoAsync(EventoFornecedor contratacao)
    {
        var formas = await _lookups.ListarAsync(TipoLookup.FormasPagamento);
        var valor = contratacao.Valor;
        var plano = new PlanoPagamentoInput
        {
            Quantidade = 1, PrimeiroVencimento = Hoje() > Evento.DataEvento ? Hoje() : Evento.DataEvento,
            FormaPagamentoId = contratacao.Pagamento?.Parcelas.FirstOrDefault()?.FormaPagamentoId
        };
        var editor = new EditorCampos();
        CamposPagamento(editor, () => valor, v => valor = v, plano, formas);
        using var dialogo = new FormEdicao($"Refazer pagamento - {contratacao.NomeFornecedor}",
            "As contas a pagar desta contratação são refeitas com o novo valor e plano. Só é possível enquanto nada foi pago.", editor,
            () => _eventos.RefazerPagamentoFornecedorAsync(_eventoId, contratacao.Id, valor, plano), 640);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private async Task CancelarContratacaoAsync()
    {
        if (ContratacaoSelecionada() is not { } f ||
            !Mensagens.Confirmar(this, $"Cancelar a contratação de {f.NomeFornecedor} ({f.Servico})? As contas a pagar em aberto são canceladas" +
                                       (f.Chegada is null ? "." : " e a chegada sai do cronograma."), "Cancelar contratação", perigoso: true))
            return;
        var pago = await _eventos.CancelarFornecedorAsync(_eventoId, f.Id);
        await CarregarAsync();
        if (pago > 0)
            Mensagens.Aviso(this, $"Já tinham sido pagos {pago:C} a {f.NomeFornecedor}: o pagamento continua registrado. Se houver devolução, lance no financeiro.");
    }

    /// <summary>A confirmação vai com o convite de agenda (.ics) do horário de chegada, registrado para as atualizações.</summary>
    private async Task EnviarConfirmacaoAsync(EventoFornecedor f)
    {
        if (!f.Ativo)
        {
            await Avisar("A contratação foi cancelada.");
            return;
        }
        var convites = _navegador.Servico<ConviteService>();
        var convite = (await convites.DoEventoAsync(_eventoId)).First(c => c.Uid == ConviteService.UidFornecedor(_eventoId, f.Id));
        var texto = ImpressaoFornecedores.Mensagem(Evento, f, _opcoes);
        var telefone = f.Telefone ?? f.Fornecedor?.Telefone ?? "";
        if (string.IsNullOrWhiteSpace(telefone) && f.Fornecedor?.Email is { Length: > 0 } email)
        {
            var envio = new EmailInput { Para = email, Assunto = $"Confirmação de serviço - {Evento.Titulo} ({Evento.Inicio:dd/MM/yyyy})", Mensagem = texto };
            DialogosEnvio.Email(this, "Enviar a confirmação ao fornecedor (com o convite de agenda)", envio, null, null,
                m => convites.EnviarEmailAsync(convite, m.Para, m.Assunto, m.Mensagem), _email.Destino);
            return;
        }
        if (DialogosEnvio.WhatsApp(this, new MensagemWhatsApp(telefone, texto), "a confirmação", "o convite", () => convites.Arquivo(convite),
                "agenda .ics") is { } enviado)
            await convites.RegistrarAsync(convite, Domain.Comercial.CanalConvite.WhatsApp, enviado);
    }

    private async Task AvaliarFornecedorAsync(EventoFornecedor contratacao)
    {
        var nota = contratacao.Nota ?? 5;
        var comentario = contratacao.Comentario;
        var editor = new EditorCampos(1);
        editor.Inteiro("Nota (1 a 5)", () => nota, v => nota = v, null, 1, 5);
        editor.Texto("Comentário", () => comentario, v => comentario = v, null, EventoFornecedor.TamanhoComentario, linhas: 3,
            dica: "Pontualidade, qualidade, atendimento...");
        using var dialogo = new FormEdicao($"Avaliar {contratacao.NomeFornecedor}", $"{contratacao.Servico} em {Evento.Codigo} - {Evento.Titulo}", editor,
            () => _eventos.AvaliarFornecedorAsync(_eventoId, contratacao.Id, nota, comentario), 520);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }
}
