using System.Text;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>Cronograma do evento impresso: o da equipe (completo, também por responsável) e o do cliente (só a festa).</summary>
internal static class ImpressaoCronograma
{
    /// <param name="cliente">Versão do cliente: só as etapas entre o início e o fim da festa, sem responsáveis e observações internas.</param>
    /// <param name="porResponsavel">Uma seção por responsável (cada um vê só o que conduz).</param>
    public static DocumentoImpresso Montar(Evento e, string empresa, bool cliente = false, bool porResponsavel = false)
    {
        var doc = new DocumentoImpresso($"Cronograma {e.Codigo}", empresa);
        doc.Secao(e.Titulo);
        doc.Campos(3,
            ("Cliente", e.Cliente?.Nome ?? "-"),
            ("Data", e.Inicio.ToString("dddd, dd/MM/yyyy", Textos.PtBr)),
            ("Horário", $"{e.Inicio:HH:mm} às {e.Fim:HH:mm}"),
            ("Espaço", e.Espaco?.Nome ?? "-"),
            ("Convidados", e.QuantidadeConvidados.ToString()),
            ("Tipo", e.TipoEvento?.Nome ?? "-"));

        var etapas = Etapas(e, cliente);
        if (etapas.Count == 0)
        {
            doc.Paragrafo("Cronograma ainda não definido.");
            return doc;
        }

        if (cliente)
        {
            doc.Secao("Programação da festa");
            doc.Tabela(["Horário", "Atividade", "Local"],
                etapas.Select(t => new[] { Horario(t), t.Atividade, t.Local ?? "" }).ToList(), [1.4f, 4, 2]);
        }
        else if (porResponsavel)
        {
            foreach (var grupo in etapas.GroupBy(t => t.Responsavel ?? "(sem responsável)").OrderBy(g => g.Key))
            {
                doc.Secao(grupo.Key);
                doc.Tabela(["Horário", "Atividade", "Local", "Observação"],
                    grupo.Select(t => new[] { Horario(t), t.Atividade, t.Local ?? "", t.Observacao ?? "" }).ToList(), [1.4f, 3.5f, 1.6f, 3]);
            }
        }
        else
        {
            doc.Secao("Minuto a minuto");
            doc.Tabela(["Horário", "Atividade", "Local", "Responsável", "Observação"],
                etapas.Select(t => new[] { Horario(t), t.Atividade, t.Local ?? "", t.Responsavel ?? "", t.Observacao ?? "" }).ToList(),
                [1.4f, 3.2f, 1.5f, 1.7f, 2.6f]);
        }
        return doc;
    }

    /// <summary>Programação da festa em texto, para o WhatsApp do cliente.</summary>
    public static string TextoCliente(Evento e, string empresa)
    {
        var nome = (e.Cliente?.Nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var texto = new StringBuilder($"Olá, {nome}! Segue a programação do seu evento *{e.Titulo}*, ")
            .Append($"{e.Inicio.ToString("dddd, dd/MM", Textos.PtBr)}, das {e.Inicio:HH:mm} às {e.Fim:HH:mm}:\n\n");
        foreach (var etapa in Etapas(e, cliente: true))
            texto.Append($"*{Horario(etapa)}* - {etapa.Atividade}{(etapa.Local is { } local ? $" ({local})" : "")}\n");
        texto.Append("\nSe quiser ajustar algum horário, é só nos avisar.\n\n").Append(empresa);
        return texto.ToString();
    }

    /// <summary>Etapas em ordem; na versão do cliente, só as que acontecem durante a festa.</summary>
    public static List<EventoEtapa> Etapas(Evento e, bool cliente) =>
        e.Etapas.Where(t => !cliente || t.Horario >= e.Inicio && t.Horario < e.Fim)
            .OrderBy(t => t.Horario).ThenBy(t => t.Ordem).ToList();

    public static string Horario(EventoEtapa etapa) =>
        etapa.DuracaoMinutos == 0 ? $"{etapa.Horario:HH:mm}" : $"{etapa.Horario:HH:mm} - {etapa.Fim:HH:mm}";
}
