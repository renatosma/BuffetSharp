using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>Impressões do mapa de mesas: a lista da recepção, a planta com quem senta em cada mesa e os cartões de mesa.</summary>
internal static class ImpressaoMesas
{
    /// <summary>As mesas do evento como o controle do mapa as desenha (com a ocupação).</summary>
    public static List<MapaMesas.Mesa> Mesas(Evento e) =>
        e.Mesas.OrderBy(m => m.Numero)
            .Select(m => new MapaMesas.Mesa(m.Id, m.Numero, m.Nome, m.Lugares, m.Formato, m.X, m.Y, e.LugaresOcupados(m))).ToList();

    /// <summary>Nome do convidado com os acompanhantes: "Ana Souza (+2)".</summary>
    public static string Nome(Convidado c) => c.Acompanhantes > 0 ? $"{c.Nome} (+{c.Acompanhantes})" : c.Nome;

    /// <summary>Nome com a sigla da restrição alimentar, para o garçom: "Ana Souza (+2) [VGN+SG]" (* = outra restrição, ver a lista).</summary>
    public static string NomeComRestricao(Convidado c) =>
        c.Restricoes == RestricaoAlimentar.Nenhuma && c.ObservacaoRestricao is null ? Nome(c)
        : $"{Nome(c)} [{(c.Restricoes == RestricaoAlimentar.Nenhuma ? "*" : RestricoesAlimentares.Sigla(c.Restricoes) + (c.ObservacaoRestricao is null ? "" : "*"))}]";

    /// <summary>Legenda das siglas que aparecem entre os convidados.</summary>
    private static string? Legenda(IEnumerable<Convidado> convidados)
    {
        var usadas = convidados.Aggregate(RestricaoAlimentar.Nenhuma, (soma, c) => soma | c.Restricoes);
        var outras = convidados.Any(c => c.ObservacaoRestricao is not null);
        if (usadas == RestricaoAlimentar.Nenhuma && !outras)
            return null;
        return "Restrições alimentares: " + string.Join(", ", RestricoesAlimentares.Separar(usadas)
                   .Select(r => $"{RestricoesAlimentares.Sigla(r)} = {RestricoesAlimentares.Nome(r).ToLowerInvariant()}")
                   .Concat(outras ? new[] { "* = outra restrição (na lista da recepção)" } : []));
    }

    private static string Cabecalho(Evento e) => $"{e.Codigo} - {e.Titulo}  •  {e.Inicio:dd/MM/yyyy HH:mm}  •  {e.Espaco?.Nome}";

    /// <summary>Lista alfabética para a entrada: cada convidado com a mesa (quem avisou que não vai fica de fora).</summary>
    public static DocumentoImpresso ListaRecepcao(Evento e, string empresa)
    {
        var doc = new DocumentoImpresso("Lista da recepção", empresa);
        doc.Subtitulo(Cabecalho(e));
        var convidados = e.Convidados.Where(c => c.Rsvp != StatusRsvp.Recusado).OrderBy(c => c.Nome, StringComparer.CurrentCulture).ToList();
        doc.Paragrafo($"{convidados.Count} convidado(s), {convidados.Sum(c => c.Pessoas)} pessoa(s) com os acompanhantes.");
        doc.Tabela(["Convidado", "Acomp.", "Mesa", "Restrição alimentar", "Presença", "Entrada"],
            convidados.Select(c => new[]
            {
                c.Nome, c.Acompanhantes.ToString(), c.Mesa?.Descricao ?? "(sem mesa)", c.DescricaoRestricao ?? "", Textos.Exibir(c.Rsvp),
                c.CheckInEm?.ToString("HH:mm") ?? "[   ]"
            }).ToList(),
            [3.2f, 0.7f, 2f, 2.4f, 1.1f, 0.9f]);
        return doc;
    }

    /// <summary>A planta do salão (paisagem) e, em seguida, quem senta em cada mesa.</summary>
    public static DocumentoImpresso Mapa(Evento e, string empresa)
    {
        var doc = new DocumentoImpresso("Mapa de mesas", empresa);
        doc.Subtitulo($"{Cabecalho(e)}  •  {e.Mesas.Count} mesa(s), {e.LugaresNasMesas} lugares, {e.PessoasSentadas} pessoa(s) sentada(s)");
        var mesas = Mesas(e);
        doc.Desenho((g, area) => MapaMesas.Desenhar(g, area, mesas, null, Tema.Fonte));
        doc.Tabela(["Mesa", "Lugares", "Convidados"],
            e.Mesas.OrderBy(m => m.Numero).Select(m => new[]
            {
                m.Descricao, $"{e.LugaresOcupados(m)}/{m.Lugares}",
                string.Join(", ", e.ConvidadosDaMesa(m).OrderBy(c => c.Nome, StringComparer.CurrentCulture).Select(NomeComRestricao))
            }).ToList(),
            [1.6f, 0.8f, 7f]);
        if (Legenda(e.Convidados.Where(c => c.Rsvp != StatusRsvp.Recusado)) is { } legenda)
            doc.Paragrafo(legenda);
        var semMesa = e.ConvidadosSemMesa().OrderBy(c => c.Nome, StringComparer.CurrentCulture).ToList();
        if (semMesa.Count > 0)
        {
            doc.Secao($"Sem mesa ({semMesa.Sum(c => c.Pessoas)} pessoa(s))");
            doc.Paragrafo(string.Join(", ", semMesa.Select(NomeComRestricao)));
        }
        return doc;
    }

    /// <summary>Uma página por mesa, com o número em destaque e os nomes, para o centro da mesa.</summary>
    public static DocumentoImpresso Cartoes(Evento e, string empresa)
    {
        var doc = new DocumentoImpresso($"Cartões de mesa - {e.Titulo}", empresa);
        var primeira = true;
        foreach (var mesa in e.Mesas.OrderBy(m => m.Numero))
        {
            if (!primeira)
                doc.QuebraPagina();
            primeira = false;
            // No cartão a restrição vai por extenso, para o garçom servir certo sem consultar a legenda.
            var nomes = e.ConvidadosDaMesa(mesa).OrderBy(c => c.Nome, StringComparer.CurrentCulture)
                .Select(c => c.DescricaoRestricao is { } restricao ? $"{Nome(c)} — {restricao}" : Nome(c)).ToList();
            doc.Desenho((g, area) => DesenharCartao(g, area, mesa, nomes));
        }
        return doc;
    }

    private static void DesenharCartao(Graphics g, RectangleF area, EventoMesa mesa, IReadOnlyList<string> nomes)
    {
        var centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
        using var fonteRotulo = new Font("Segoe UI", 20f);
        using var fonteNumero = new Font("Segoe UI Semibold", 96f);
        using var fonteNome = new Font("Segoe UI Semibold", 18f);
        using var fonteConvidado = new Font("Segoe UI", 13f);
        using var cor = new SolidBrush(Tema.Primaria);

        var y = area.Top + 30;
        g.DrawString("Mesa", fonteRotulo, Brushes.DimGray, new RectangleF(area.Left, y, area.Width, fonteRotulo.GetHeight(g)), centro);
        y += fonteRotulo.GetHeight(g);
        g.DrawString(mesa.Numero.ToString(), fonteNumero, cor, new RectangleF(area.Left, y, area.Width, fonteNumero.GetHeight(g)), centro);
        y += fonteNumero.GetHeight(g);
        if (mesa.Nome is { } nome)
        {
            g.DrawString(nome, fonteNome, Brushes.Black, new RectangleF(area.Left, y, area.Width, fonteNome.GetHeight(g)), centro);
            y += fonteNome.GetHeight(g);
        }

        using var caneta = new Pen(Tema.Destaque, 2);
        g.DrawLine(caneta, area.Left + area.Width / 3, y + 12, area.Right - area.Width / 3, y + 12);
        y += 30;
        var alturaLinha = fonteConvidado.GetHeight(g) + 4;
        foreach (var convidado in nomes)
        {
            if (y + alturaLinha > area.Bottom)
                break;
            g.DrawString(convidado, fonteConvidado, Brushes.Black, new RectangleF(area.Left, y, area.Width, alturaLinha), centro);
            y += alturaLinha;
        }
    }
}
