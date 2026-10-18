using System.Globalization;
using System.Net;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Envio da proposta comercial ao cliente por e-mail (HTML + PDF anexo) ou WhatsApp (link wa.me com a
/// mensagem pronta). Cada envio fica registrado no orçamento, que passa de rascunho para "Enviado".
/// </summary>
public sealed class PropostaService(
    IBuffetDbContextFactory fabrica,
    IConsultasEstoqueCardapio cardapio,
    IEnvioEmail email,
    BuffetOptions opcoes,
    ISessaoUsuario sessao,
    TimeProvider relogio)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    private static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    // ------------------------- E-mail -------------------------

    public async Task<EmailInput> PrepararEmailAsync(int orcamentoId, CancellationToken ct = default)
    {
        var o = await CarregarAsync(orcamentoId, ct);
        return new EmailInput
        {
            Para = o.Cliente!.Email ?? ValidacaoContatos.Primeiro(o.Cliente, TipoContato.Email) ?? string.Empty,
            Assunto = $"Proposta {o.Codigo} - {o.TipoEvento?.Nome} em {o.DataEvento:dd/MM/yyyy} | {opcoes.NomeEmpresa}",
            Mensagem = $"Olá, {o.Cliente.Nome.Split(' ')[0]}!\n\n" +
                       "Obrigado pelo interesse. Segue a nossa proposta para o seu evento, com o cardápio, os serviços e os valores. " +
                       "A proposta completa também vai em anexo (PDF).\n\n" +
                       $"Ficamos à disposição para ajustar o que for preciso ou agendar uma degustação.\n\n{opcoes.NomeEmpresa}"
        };
    }

    public async Task EnviarEmailAsync(int orcamentoId, EmailInput input, CancellationToken ct = default)
    {
        Validador.Garantir(System.Net.Mail.MailAddress.TryCreate(input.Para?.Trim(), out _), "Informe um e-mail válido para o destinatário.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(input.Assunto), "Informe o assunto do e-mail.");

        await using var db = fabrica.Criar();
        var orcamento = await IncluirDetalhes(db.Orcamentos).Include(o => o.Envios).FirstOrDefaultAsync(o => o.Id == orcamentoId, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");

        // Valida o registro antes de enviar: um orçamento aprovado/recusado não deve sair de novo.
        orcamento.RegistrarEnvio(CanalEnvio.Email, input.Para!.Trim(), sessao.Usuario?.Login, Agora);

        var corpo = await MontarHtmlAsync(orcamento, input.Mensagem, ct);
        await email.EnviarAsync(new MensagemEmail(input.Para.Trim(), input.Assunto.Trim(), corpo,
            input.Anexo is null ? [] : [input.Anexo]), ct);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------- WhatsApp -------------------------

    public async Task<MensagemWhatsApp> PrepararWhatsAppAsync(int orcamentoId, CancellationToken ct = default)
    {
        var o = await CarregarAsync(orcamentoId, ct);
        var resumo = o.Resumo;
        var texto = new StringBuilder()
            .Append($"Olá, {o.Cliente!.Nome.Split(' ')[0]}! Aqui é do *{opcoes.NomeEmpresa}*.\n\n")
            .Append($"Segue a proposta *{o.Codigo}* para o seu evento:\n")
            .Append($"📅 {o.Inicio.ToString("dddd, dd/MM/yyyy", PtBr)}, das {o.Inicio:HH:mm} às {o.Fim:HH:mm}\n")
            .Append($"👥 {o.QuantidadeConvidados} convidados\n");
        if (o.Espaco is not null)
            texto.Append($"📍 {o.Espaco.Nome}\n");
        if (o.Pacote is not null)
            texto.Append($"🍽️ Pacote {o.Pacote.Nome} ({Moeda(o.ValorPorPessoa)} por pessoa)\n");
        if (o.Servicos.Count > 0)
            texto.Append($"✨ Serviços: {string.Join(", ", o.Servicos.Select(s => s.Descricao))}\n");
        if (resumo.Desconto > 0)
            texto.Append($"🎁 Desconto de {o.PercentualDesconto:0.##}%\n");
        texto.Append($"\n*Total: {Moeda(resumo.Total)}* ({Moeda(o.QuantidadeConvidados == 0 ? 0 : resumo.Total / o.QuantidadeConvidados)} por convidado)\n");
        if (o.CondicaoPagamento is { } condicao)
            texto.Append($"💳 {TextosPagamento.Completo(condicao, resumo.Total, DateOnly.FromDateTime(Agora), o.DataEvento)}\n");
        texto
            .Append($"Proposta válida até {o.ValidoAte:dd/MM/yyyy}.\n\n")
            .Append("Posso te enviar a proposta completa por e-mail ou agendar uma degustação? 😊");

        var telefone = o.Cliente.Celular ?? ValidacaoContatos.Primeiro(o.Cliente, TipoContato.WhatsApp, TipoContato.Celular) ?? o.Cliente.Telefone;
        return new MensagemWhatsApp(telefone ?? string.Empty, texto.ToString());
    }

    /// <summary>Registra o envio após o usuário abrir o WhatsApp com a mensagem.</summary>
    public async Task RegistrarWhatsAppAsync(int orcamentoId, string telefone, CancellationToken ct = default)
    {
        var digitos = Documento.SomenteDigitos(telefone);
        Validador.Garantir(digitos.Length is >= 10 and <= 13, "Informe um telefone celular válido, com DDD.");

        await using var db = fabrica.Criar();
        var orcamento = await db.Orcamentos.Include(o => o.Envios).FirstOrDefaultAsync(o => o.Id == orcamentoId, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");
        orcamento.RegistrarEnvio(CanalEnvio.WhatsApp, telefone, sessao.Usuario?.Login, Agora);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------- Infra -------------------------

    private static IQueryable<Orcamento> IncluirDetalhes(IQueryable<Orcamento> consulta) => consulta
        .Include(o => o.Cliente!).ThenInclude(c => c.Contatos)
        .Include(o => o.TipoEvento)
        .Include(o => o.Espaco)
        .Include(o => o.Pacote)
        .Include(o => o.CondicaoPagamento)
        .Include(o => o.Servicos);

    private async Task<Orcamento> CarregarAsync(int orcamentoId, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        return await IncluirDetalhes(db.Orcamentos.AsNoTracking()).FirstOrDefaultAsync(o => o.Id == orcamentoId, ct)
               ?? throw new RegraNegocioException("Orçamento não encontrado.");
    }

    /// <summary>Proposta em HTML simples (tabelas e estilos inline, compatíveis com os clientes de e-mail).</summary>
    private async Task<string> MontarHtmlAsync(Orcamento o, string? mensagem, CancellationToken ct)
    {
        static string H(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);
        const string Celula = "padding:6px 10px;border-bottom:1px solid #eee";

        var resumo = o.Resumo;
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2b2b2b;max-width:680px\">");
        foreach (var paragrafo in (mensagem ?? string.Empty).ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            html.Append("<p>").Append(H(paragrafo).Replace("\n", "<br>")).Append("</p>");

        html.Append($"<h2 style=\"color:#8b1e3f;border-bottom:2px solid #c9a227;padding-bottom:4px\">Proposta {H(o.Codigo)}</h2>");
        html.Append("<table style=\"border-collapse:collapse;width:100%\">");
        void Linha(string rotulo, string valor) =>
            html.Append($"<tr><td style=\"{Celula};color:#6e6a66;width:35%\">{H(rotulo)}</td><td style=\"{Celula}\"><b>{H(valor)}</b></td></tr>");
        Linha("Evento", o.TipoEvento?.Nome ?? "-");
        Linha("Data", o.Inicio.ToString("dddd, dd/MM/yyyy", PtBr));
        Linha("Horário", $"{o.Inicio:HH:mm} às {o.Fim:HH:mm}");
        Linha("Convidados", $"{o.QuantidadeConvidados} pessoas");
        if (o.Espaco is not null)
            Linha("Espaço", o.Espaco.Nome);
        if (o.Pacote is not null)
            Linha("Pacote", $"{o.Pacote.Nome} ({Moeda(o.ValorPorPessoa)} por pessoa)");
        html.Append("</table>");

        if (o.PacoteId is { } pacoteId)
        {
            var composicao = await cardapio.ComposicaoPacoteAsync(pacoteId, ct);
            if (composicao.Count > 0)
            {
                html.Append("<h3 style=\"color:#8b1e3f\">Cardápio</h3>");
                foreach (var grupo in composicao.GroupBy(c => c.Categoria))
                    html.Append($"<p><b>{H(grupo.Key)}</b><br>{H(string.Join(" • ", grupo.Select(i => i.Item)))}</p>");
            }
        }

        if (o.Servicos.Count > 0)
        {
            html.Append("<h3 style=\"color:#8b1e3f\">Serviços adicionais</h3><table style=\"border-collapse:collapse;width:100%\">");
            foreach (var s in o.Servicos)
                html.Append($"<tr><td style=\"{Celula}\">{H(s.Descricao)}</td><td style=\"{Celula};text-align:right\">{s.Quantidade:N0} × {H(Moeda(s.ValorUnitario))}</td>" +
                            $"<td style=\"{Celula};text-align:right\">{H(Moeda(s.Total))}</td></tr>");
            html.Append("</table>");
        }

        html.Append("<h3 style=\"color:#8b1e3f\">Investimento</h3><table style=\"border-collapse:collapse;width:100%\">");
        void Valor(string rotulo, string valor) =>
            html.Append($"<tr><td style=\"{Celula}\">{H(rotulo)}</td><td style=\"{Celula};text-align:right\">{H(valor)}</td></tr>");
        Valor($"Alimentação ({o.QuantidadeConvidados} × {Moeda(o.ValorPorPessoa)})", Moeda(resumo.Alimentacao));
        Valor("Locação do espaço", Moeda(resumo.Espaco));
        Valor("Serviços adicionais", Moeda(resumo.Servicos));
        if (resumo.Desconto > 0)
            Valor($"Desconto ({o.PercentualDesconto:0.##}%)", "- " + Moeda(resumo.Desconto));
        html.Append($"<tr><td style=\"padding:10px;font-size:16px\"><b>Total</b></td><td style=\"padding:10px;text-align:right;font-size:18px;color:#8b1e3f\"><b>{H(Moeda(resumo.Total))}</b></td></tr>");
        html.Append("</table>");

        if (o.CondicaoPagamento is { } condicao)
            html.Append($"<p><b>Condição de pagamento:</b> {H(condicao.Texto)}<br>" +
                        $"{H(TextosPagamento.Simulacao(condicao, resumo.Total, DateOnly.FromDateTime(Agora), o.DataEvento))}</p>");

        html.Append($"<p style=\"color:#6e6a66\">Proposta válida até {o.ValidoAte:dd/MM/yyyy}. A reserva da data é confirmada mediante assinatura do contrato e pagamento da entrada.</p>");
        if (!string.IsNullOrWhiteSpace(o.Observacoes))
            html.Append($"<p><b>Observações:</b> {H(o.Observacoes)}</p>");

        var contatos = new[] { opcoes.Telefone, opcoes.Email, opcoes.Endereco }.Where(s => !string.IsNullOrWhiteSpace(s));
        html.Append($"<p style=\"margin-top:24px;color:#6e6a66;font-size:12px\"><b>{H(opcoes.NomeEmpresa)}</b><br>{H(string.Join(" • ", contatos))}</p>");
        html.Append("</div>");
        return html.ToString();
    }
}
