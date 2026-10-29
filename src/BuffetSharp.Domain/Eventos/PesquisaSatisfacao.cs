using System.Text.RegularExpressions;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

public enum CategoriaNps
{
    /// <summary>Notas 0 a 6.</summary>
    Detrator,

    /// <summary>Notas 7 e 8.</summary>
    Neutro,

    /// <summary>Notas 9 e 10.</summary>
    Promotor
}

/// <summary>
/// Pesquisa de satisfação do evento realizado: enviada ao cliente pelo WhatsApp ou e-mail, com a resposta registrada pelo
/// comercial. Mede o NPS ("de 0 a 10, quanto indicaria o buffet") e dá notas de 1 a 5 para comida, atendimento, espaço e
/// organização. Detratores viram um alerta para o administrador; promotores recebem o pedido de avaliação no Google.
/// </summary>
public class PesquisaSatisfacao : Entidade
{
    public const int TamanhoComentario = 1000;
    public const int TamanhoTratativa = 500;

    private PesquisaSatisfacao()
    {
    }

    public PesquisaSatisfacao(int eventoId)
    {
        Validador.Garantir(eventoId > 0, "Evento inválido.");
        EventoId = eventoId;
    }

    public int EventoId { get; private set; }
    public Evento? Evento { get; private set; }

    public DateTime? EnviadaEm { get; private set; }
    public DateTime? RespondidaEm { get; private set; }
    public string? RegistradaPor { get; private set; }

    /// <summary>De 0 a 10: quanto o cliente indicaria o buffet.</summary>
    public int? Nps { get; private set; }

    /// <summary>Notas de 1 a 5 por área.</summary>
    public int? NotaComida { get; private set; }

    public int? NotaAtendimento { get; private set; }
    public int? NotaEspaco { get; private set; }
    public int? NotaOrganizacao { get; private set; }
    public string? Comentario { get; private set; }

    /// <summary>Pedido de avaliação no Google enviado ao promotor.</summary>
    public DateTime? AvaliacaoGooglePedidaEm { get; private set; }

    /// <summary>O que foi feito com o detrator (ligação, desconto, retorno...).</summary>
    public string? TratativaDetrator { get; private set; }

    public DateTime? DetratorTratadoEm { get; private set; }

    public bool Respondida => RespondidaEm is not null;
    public CategoriaNps? Categoria => Nps is { } nota ? Classificar(nota) : null;

    public static CategoriaNps Classificar(int nota) => nota >= 9 ? CategoriaNps.Promotor : nota >= 7 ? CategoriaNps.Neutro : CategoriaNps.Detrator;

    public void RegistrarEnvio(DateTime agora) => EnviadaEm ??= agora;

    public void RegistrarResposta(int nps, int? comida, int? atendimento, int? espaco, int? organizacao, string? comentario, string? usuario,
        DateTime agora)
    {
        Validador.Garantir(nps is >= 0 and <= 10, "A nota de indicação vai de 0 a 10.");
        foreach (var nota in new[] { comida, atendimento, espaco, organizacao })
            Validador.Garantir(nota is null or >= 1 and <= 5, "As notas por área vão de 1 a 5.");
        Validador.Garantir((comentario?.Trim().Length ?? 0) <= TamanhoComentario, $"O comentário pode ter no máximo {TamanhoComentario} caracteres.");
        Nps = nps;
        (NotaComida, NotaAtendimento, NotaEspaco, NotaOrganizacao) = (comida, atendimento, espaco, organizacao);
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        RegistradaPor = usuario;
        RespondidaEm = agora;
    }

    public void RegistrarPedidoAvaliacao(DateTime agora)
    {
        Validador.Garantir(Categoria == CategoriaNps.Promotor, "O pedido de avaliação no Google vai só para quem deu nota 9 ou 10.");
        AvaliacaoGooglePedidaEm ??= agora;
    }

    public void TratarDetrator(string tratativa, DateTime agora)
    {
        Validador.Garantir(Categoria == CategoriaNps.Detrator, "Somente notas de 0 a 6 (detratores) têm tratativa.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(tratativa), "Descreva o que foi feito com o cliente.");
        Validador.Garantir(tratativa.Trim().Length <= TamanhoTratativa, $"A tratativa pode ter no máximo {TamanhoTratativa} caracteres.");
        TratativaDetrator = tratativa.Trim();
        DetratorTratadoEm = agora;
    }

    /// <summary>NPS: % de promotores menos % de detratores (de -100 a 100); nulo sem respostas.</summary>
    public static decimal? CalcularNps(IEnumerable<int> notas)
    {
        var lista = notas.ToList();
        if (lista.Count == 0)
            return null;
        var promotores = lista.Count(n => Classificar(n) == CategoriaNps.Promotor);
        var detratores = lista.Count(n => Classificar(n) == CategoriaNps.Detrator);
        return Math.Round((promotores - detratores) * 100m / lista.Count, 1);
    }
}

/// <summary>Resposta que o cliente mandou pelo WhatsApp, interpretada: "10, 5, 5, 4, 5 adoramos a festa".</summary>
public sealed record RespostaPesquisa(int? Nps, int? Comida, int? Atendimento, int? Espaco, int? Organizacao, string? Comentario)
{
    /// <summary>
    /// A primeira nota (0 a 10) é a de indicação; as quatro seguintes (1 a 5) são comida, atendimento, espaço e organização, na
    /// ordem das perguntas. O texto que sobra é o comentário.
    /// </summary>
    public static RespostaPesquisa Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new RespostaPesquisa(null, null, null, null, null, null);
        var numeros = Regex.Matches(texto, @"(?<![\d,.])\d{1,2}(?![\d,.]\d)").Take(5).ToList();
        var notas = numeros.Select(m => int.Parse(m.Value)).ToList();
        int? Nota(int i, int maximo) => i < notas.Count && notas[i] >= (i == 0 ? 0 : 1) && notas[i] <= maximo ? notas[i] : null;
        var restante = numeros.Count == 0 ? texto : texto[(numeros[^1].Index + numeros[^1].Length)..];
        var comentario = restante.Trim(' ', ',', ';', '.', '-', '\r', '\n', '\t');
        return new RespostaPesquisa(Nota(0, 10), Nota(1, 5), Nota(2, 5), Nota(3, 5), Nota(4, 5), comentario.Length == 0 ? null : comentario);
    }
}
