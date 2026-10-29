using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>Fornecedores do evento: a lista do dia para a coordenação e a mensagem de confirmação ao fornecedor.</summary>
internal static class ImpressaoFornecedores
{
    /// <summary>Contratações ativas na ordem de chegada (as sem horário por último).</summary>
    public static List<EventoFornecedor> Ativos(Evento e) =>
        e.Fornecedores.Where(f => f.Ativo).OrderBy(f => f.Chegada ?? DateTime.MaxValue).ThenBy(f => f.Servico).ToList();

    public static string Contato(EventoFornecedor f) =>
        string.Join(" - ", new[] { f.Responsavel, f.Telefone ?? f.Fornecedor?.Telefone }.Where(t => !string.IsNullOrWhiteSpace(t)));

    /// <summary>Endereço da festa: o do espaço ou, sem ele, o da empresa.</summary>
    public static string? Local(Evento e, BuffetOptions opcoes)
    {
        var endereco = e.Espaco?.Endereco is { Logradouro.Length: > 0 } doEspaco ? doEspaco.ToString() : opcoes.Endereco;
        return e.Espaco is null ? endereco : string.IsNullOrWhiteSpace(endereco) ? e.Espaco.Nome : $"{e.Espaco.Nome} - {endereco}";
    }

    public static DocumentoImpresso ListaDoDia(Evento e, BuffetOptions opcoes)
    {
        var doc = new DocumentoImpresso("Fornecedores do evento", opcoes.NomeEmpresa);
        doc.Subtitulo($"{e.Codigo} - {e.Titulo}  •  {e.Inicio.ToString("dddd, dd/MM/yyyy", Textos.PtBr)}, das {e.Inicio:HH:mm} às {e.Fim:HH:mm}");
        if (Local(e, opcoes) is { } local)
            doc.Paragrafo($"Local: {local}");
        doc.Tabela(["Chegada", "Serviço", "Fornecedor", "Contato no dia", "Situação", "Observação"],
            Ativos(e).Select(f => new[]
            {
                f.Chegada?.ToString("dd/MM HH:mm") ?? "-", f.Servico, f.NomeFornecedor, Contato(f), Textos.Exibir(f.Situacao), f.Observacao ?? ""
            }).ToList(),
            [1.1f, 2.2f, 2f, 2.2f, 1.1f, 2.4f]);
        return doc;
    }

    /// <summary>Dados da festa para o fornecedor confirmar (WhatsApp ou e-mail).</summary>
    public static string Mensagem(Evento e, EventoFornecedor f, BuffetOptions opcoes)
    {
        var nome = (f.Responsavel ?? f.NomeFornecedor).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var texto = new StringBuilder();
        texto.AppendLine($"Olá, {nome}! Aqui é do {opcoes.NomeEmpresa}. Seguem os dados do evento para confirmar o serviço de {f.Servico}:");
        texto.AppendLine();
        texto.AppendLine($"• Evento: {e.Titulo} ({e.Codigo})");
        texto.AppendLine($"• Data: {e.Inicio.ToString("dddd, dd/MM/yyyy", Textos.PtBr)}, das {e.Inicio:HH:mm} às {e.Fim:HH:mm}");
        if (f.Chegada is { } chegada)
            texto.AppendLine($"• Chegada: {chegada:dd/MM} às {chegada:HH:mm}");
        if (Local(e, opcoes) is { } local)
            texto.AppendLine($"• Local: {local}");
        texto.AppendLine($"• Convidados: {e.QuantidadeConvidados}");
        texto.AppendLine($"• Valor combinado: {f.Valor:C}");
        if (f.Observacao is { } observacao)
            texto.AppendLine($"• Observação: {observacao}");
        texto.AppendLine();
        texto.Append("Pode confirmar, por favor?");
        if (opcoes.Telefone is { Length: > 0 } telefone)
            texto.Append($" Qualquer dúvida, fale com a gente pelo {telefone}.");
        texto.AppendLine();
        texto.Append(opcoes.NomeEmpresa);
        return texto.ToString();
    }
}
