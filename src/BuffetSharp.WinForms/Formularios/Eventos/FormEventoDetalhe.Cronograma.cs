using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>Aba Cronograma: o minuto a minuto da festa, com o painel do dia (agora / próxima), impressão e envio ao cliente.</summary>
internal sealed partial class FormEventoDetalhe
{
    private readonly Grade<EventoEtapa> _etapas = new();
    private readonly Label _agora = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), Margin = new Padding(0, 4, 0, 2) };
    private readonly Label _proxima = new() { AutoSize = true, Font = Tema.FonteNegrito, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 0, 0, 6) };
    private readonly System.Windows.Forms.Timer _relogioCronograma = new() { Interval = 30_000 };

    private TabPage CriarAbaCronograma()
    {
        _etapas.Texto("Horário", t => ImpressaoCronograma.Horario(t), 16)
            .Numero("Min.", t => t.DuracaoMinutos, peso: 8)
            .Texto("Atividade", t => t.Atividade, 45)
            .Texto("Local", t => t.Local, 18)
            .Texto("Responsável", t => t.Responsavel, 20)
            .Texto("Observação", t => t.Observacao, 35)
            .CorDaLinha(t => _detalhe is null || Evento.DataEvento != DateOnly.FromDateTime(DateTime.Now) ? null
                : t.EmAndamento(DateTime.Now) || ReferenceEquals(Evento.AndamentoCronograma(DateTime.Now).Atual, t) ? Tema.Sucesso
                : t.Fim < DateTime.Now ? Tema.TextoSuave : null);

        var barra = BarraAba();
        BotaoAba(barra, "Nova etapa", Icones.Adicionar, () => EditarEtapaAsync(null), primario: true);
        BotaoAba(barra, "Editar", Icones.Editar, () => _etapas.Selecionado is { } t ? EditarEtapaAsync(t) : Task.CompletedTask);
        BotaoAba(barra, "Remover", Icones.Excluir, async () =>
        {
            if (_etapas.Selecionado is { } t && Mensagens.Confirmar(this, $"Remover a etapa \"{t.Atividade}\" ({t.Horario:HH:mm})?"))
                await Recarregar(_eventos.RemoverEtapaAsync(_eventoId, t.Id));
        });
        BotaoAba(barra, "Aplicar modelo", Icones.Lista, AplicarModeloCronogramaAsync);
        BotaoAba(barra, "Imprimir", Icones.Imprimir, () => ImprimirCronograma(porResponsavel: false), edicao: false);
        BotaoAba(barra, "Por responsável", Icones.Pessoas, () => ImprimirCronograma(porResponsavel: true), edicao: false);
        BotaoAba(barra, "E-mail ao cliente", Icones.Email, EnviarCronogramaEmailAsync, edicao: false);
        BotaoAba(barra, "WhatsApp ao cliente", Icones.Mensagem, EnviarCronogramaWhatsAppAsync, edicao: false);
        _etapas.DuploClique += async t => await ExecutarAsync(() => EditarEtapaAsync(t));

        var painelDia = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        painelDia.Controls.Add(_agora);
        painelDia.Controls.Add(_proxima);

        _relogioCronograma.Tick += (_, _) => AtualizarPainelDia();
        _relogioCronograma.Start();
        HandleDestroyed += (_, _) => _relogioCronograma.Dispose();

        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        painel.Controls.Add(_etapas.Controle);
        painel.Controls.Add(barra);
        painel.Controls.Add(painelDia);
        return new TabPage("Cronograma") { Controls = { painel } };
    }

    private void CarregarCronograma(Evento e)
    {
        var selecionada = _etapas.Selecionado?.Id;
        _etapas.Carregar(e.Etapas.OrderBy(t => t.Horario).ThenBy(t => t.Ordem));
        if (selecionada is { } id)
            _etapas.Selecionar(t => t.Id == id);
        AtualizarPainelDia();
    }

    /// <summary>No dia da festa: a etapa em andamento e a próxima. Nos outros dias, o resumo do cronograma.</summary>
    private void AtualizarPainelDia()
    {
        if (_detalhe is null || IsDisposed)
            return;
        var e = Evento;
        var agora = DateTime.Now;
        if (e.Etapas.Count == 0)
        {
            _agora.Text = "Cronograma ainda não definido.";
            _proxima.Text = "Use \"Aplicar modelo\" para começar do cronograma padrão do tipo de evento, ou \"Nova etapa\".";
            _agora.ForeColor = Tema.TextoSuave;
            return;
        }

        var primeira = e.Etapas.Min(t => t.Horario);
        var ultima = e.Etapas.Max(t => t.Fim);
        if (agora < primeira.AddHours(-2) || agora > ultima.AddHours(1))
        {
            _agora.Text = agora < primeira
                ? $"{e.Etapas.Count} etapa(s), das {primeira:HH:mm} às {ultima:HH:mm} de {e.Inicio:dd/MM/yyyy}"
                : $"Cronograma encerrado ({e.Etapas.Count} etapa(s), das {primeira:HH:mm} às {ultima:HH:mm} de {e.Inicio:dd/MM/yyyy})";
            _proxima.Text = "No dia da festa, este painel mostra a etapa em andamento e a próxima.";
            _agora.ForeColor = Tema.Texto;
        }
        else
        {
            var (atual, proxima) = e.AndamentoCronograma(agora);
            _agora.Text = atual is null
                ? "Agora: intervalo entre etapas"
                : $"Agora: {atual.Atividade} ({ImpressaoCronograma.Horario(atual)})" + (atual.Responsavel is { } r ? $"  •  {r}" : "");
            _agora.ForeColor = atual is null ? Tema.TextoSuave : Tema.Sucesso;
            _proxima.Text = proxima is null
                ? "Não há mais etapas depois desta."
                : $"Próxima: {proxima.Atividade} às {proxima.Horario:HH:mm} (em {Tempo(proxima.Horario - agora)})" +
                  (proxima.Responsavel is { } p ? $"  •  {p}" : "");
        }
        _etapas.Controle.Invalidate();
    }

    private static string Tempo(TimeSpan falta) =>
        falta.TotalMinutes < 1 ? "menos de 1 min" : falta.TotalHours >= 1 ? $"{(int)falta.TotalHours}h{falta.Minutes:00}" : $"{(int)Math.Ceiling(falta.TotalMinutes)} min";

    private async Task EditarEtapaAsync(EventoEtapa? etapa)
    {
        var e = Evento;
        var ultima = e.Etapas.OrderBy(t => t.Horario).LastOrDefault();
        var input = etapa is null
            ? new EtapaInput { Horario = ultima?.Fim ?? e.Inicio, DuracaoMinutos = 30 }
            : new EtapaInput
            {
                Id = etapa.Id, Horario = etapa.Horario, DuracaoMinutos = etapa.DuracaoMinutos, Atividade = etapa.Atividade, Local = etapa.Local,
                Responsavel = etapa.FuncionarioId is null ? etapa.Responsavel : null, FuncionarioId = etapa.FuncionarioId, Observacao = etapa.Observacao
            };
        var data = DateOnly.FromDateTime(input.Horario);
        var hora = TimeOnly.FromDateTime(input.Horario);
        var escalados = e.Equipe.Where(f => f.Funcionario is not null).OrderBy(f => f.Funcionario!.Nome)
            .Select(f => new ItemLookup { Id = f.FuncionarioId, Nome = $"{f.Funcionario!.Nome} ({f.Funcao})" }).ToList();

        var editor = new EditorCampos();
        editor.Data("Dia", () => data, v => data = v ?? data, nameof(input.Horario));
        editor.Hora("Horário", () => hora, v => hora = v);
        editor.Inteiro("Duração (minutos)", () => input.DuracaoMinutos, v => input.DuracaoMinutos = v, nameof(input.DuracaoMinutos), 0,
            EventoEtapa.DuracaoMaxima);
        editor.Texto("Local", () => input.Local, v => input.Local = v, nameof(input.Local), EventoEtapa.TamanhoLocal, dica: "Salão, pista, jardim...");
        editor.Texto("Atividade", () => input.Atividade, v => input.Atividade = v ?? "", nameof(input.Atividade), EventoEtapa.TamanhoAtividade, span: 2,
            dica: "Ex.: entrada dos noivos; jantar; valsa; corte do bolo...");
        editor.Lista("Responsável da equipe", escalados, () => input.FuncionarioId, v => input.FuncionarioId = v is > 0 ? v : null,
            nameof(input.FuncionarioId), permitirVazio: true);
        editor.Texto("Ou responsável (texto)", () => input.Responsavel, v => input.Responsavel = v, nameof(input.Responsavel),
            EventoEtapa.TamanhoResponsavel, dica: "DJ, cerimonial, fotógrafo...");
        editor.Texto("Observação", () => input.Observacao, v => input.Observacao = v, nameof(input.Observacao), EventoEtapa.TamanhoObservacao,
            span: 2, linhas: 3);

        using var dialogo = new FormEdicao(etapa is null ? "Nova etapa do cronograma" : "Editar etapa do cronograma",
            $"{e.Codigo} - {e.Titulo}: festa das {e.Inicio:HH:mm} às {e.Fim:HH:mm} de {e.Inicio:dd/MM/yyyy}. Duração 0 = um marco (ex.: entrada dos noivos).",
            editor, () =>
            {
                input.Horario = data.ToDateTime(hora);
                return _eventos.SalvarEtapaAsync(_eventoId, input);
            }, 680);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private async Task AplicarModeloCronogramaAsync()
    {
        var modelos = (await _navegador.Servico<ICadastroService<ModeloCronograma>>().ListarAsync(q => q.Where(m => m.Ativo).OrderBy(m => m.Nome)))
            .Select(m => new ItemLookup { Id = m.Id, Nome = m.Nome, Numero = m.TipoEventoId ?? 0 }).ToList();
        if (modelos.Count == 0)
        {
            Mensagens.Aviso(this, "Não há modelo de cronograma ativo. Cadastre um em Cadastros › Modelos de cronograma.");
            return;
        }
        int? modeloId = (modelos.FirstOrDefault(m => m.Numero == Evento.TipoEventoId) ?? modelos.FirstOrDefault(m => m.Numero == 0) ?? modelos[0]).Id;
        var e = new EditorCampos(1);
        e.Lista("Modelo", modelos, () => modeloId, v => modeloId = v);
        var incluidas = 0;
        using var dialogo = new FormEdicao("Aplicar modelo de cronograma",
            $"As etapas do modelo entram com o horário contado a partir do início do evento ({Evento.Inicio:HH:mm}). Etapas iguais não se repetem.", e,
            async () => incluidas = await _eventos.AplicarCronogramaAsync(_eventoId, modeloId), 520);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        await CarregarAsync();
        Mensagens.Info(this, incluidas == 0 ? "Todas as etapas do modelo já estavam no cronograma." : $"{incluidas} etapa(s) incluída(s).");
    }

    private Task ImprimirCronograma(bool porResponsavel)
    {
        ImpressaoCronograma.Montar(Evento, _opcoes.NomeEmpresa, porResponsavel: porResponsavel).Visualizar(this);
        return Task.CompletedTask;
    }

    private Task EnviarCronogramaEmailAsync()
    {
        var e = Evento;
        if (!TemProgramacao(e))
            return Task.CompletedTask;
        var nome = (e.Cliente?.Nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var envio = new EmailInput
        {
            Para = e.Cliente?.Email ?? "",
            Assunto = $"Programação do seu evento {e.Titulo} | {_opcoes.NomeEmpresa}",
            Mensagem = $"Olá, {nome}!{Environment.NewLine}{Environment.NewLine}Segue em anexo a programação do seu evento, " +
                       $"{e.Inicio.ToString("dddd, dd/MM/yyyy", Textos.PtBr)}, das {e.Inicio:HH:mm} às {e.Fim:HH:mm}. " +
                       $"Se quiser ajustar algum horário, é só responder este e-mail.{Environment.NewLine}{Environment.NewLine}{_opcoes.NomeEmpresa}"
        };
        DialogosEnvio.Email(this, "Enviar a programação ao cliente", envio, "a programação",
            () => DialogosEnvio.Pdf($"Programação {e.Codigo}", ImpressaoCronograma.Montar(e, _opcoes.NomeEmpresa, cliente: true).GerarPdf()),
            m => _email.EnviarAsync(new MensagemEmail(m.Para.Trim(), m.Assunto.Trim(),
                "<p>" + System.Net.WebUtility.HtmlEncode(m.Mensagem ?? "").Replace("\r\n", "<br>").Replace("\n", "<br>") + "</p>",
                m.Anexo is null ? [] : [m.Anexo])), _email.Destino);
        return Task.CompletedTask;
    }

    private Task EnviarCronogramaWhatsAppAsync()
    {
        var e = Evento;
        if (!TemProgramacao(e))
            return Task.CompletedTask;
        var mensagem = new MensagemWhatsApp(e.Cliente?.Celular ?? e.Cliente?.Telefone ?? "", ImpressaoCronograma.TextoCliente(e, _opcoes.NomeEmpresa));
        DialogosEnvio.WhatsApp(this, mensagem, "a programação", "a programação",
            () => DialogosEnvio.Pdf($"Programação {e.Codigo}", ImpressaoCronograma.Montar(e, _opcoes.NomeEmpresa, cliente: true).GerarPdf()));
        return Task.CompletedTask;
    }

    private bool TemProgramacao(Evento e)
    {
        if (ImpressaoCronograma.Etapas(e, cliente: true).Count > 0)
            return true;
        Mensagens.Aviso(this, "O cronograma não tem etapas entre o início e o fim da festa para enviar ao cliente.");
        return false;
    }
}
