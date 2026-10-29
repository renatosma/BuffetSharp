using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

public enum FormatoMesa
{
    Redonda,
    Quadrada,
    Retangular
}

/// <summary>Mesa desenhada na planta do salão (a padrão do espaço ou a do evento).</summary>
public interface IMesaPlanta
{
    int Numero { get; }
    string? Nome { get; }
    int Lugares { get; }
    FormatoMesa Formato { get; }

    /// <summary>Centro da mesa, em pontos da planta (<see cref="PlantaSalao.Largura"/> × <see cref="PlantaSalao.Altura"/>).</summary>
    int X { get; }

    int Y { get; }
}

/// <summary>Medidas da planta do salão e as regras comuns às mesas do espaço e do evento.</summary>
public static class PlantaSalao
{
    public const int Largura = 1200;
    public const int Altura = 800;
    public const int NumeroMaximo = 999;
    public const int LugaresMaximo = 30;
    public const int MesasMaximo = 200;
    public const int TamanhoNome = 40;
    public const int LugaresPadrao = 10;

    private const int Passo = 150;

    public static string Descricao(int numero, string? nome) => nome is null ? $"Mesa {numero}" : $"Mesa {numero} - {nome}";

    /// <summary>Largura e altura da mesa na planta: cresce com os lugares, para o desenho dar ideia do tamanho real.</summary>
    public static (int Largura, int Altura) Tamanho(FormatoMesa formato, int lugares) => formato switch
    {
        FormatoMesa.Retangular => (Math.Min(60 + lugares * 11, 300), 64),
        _ => (Math.Min(64 + lugares * 4, 150), Math.Min(64 + lugares * 4, 150))
    };

    public static (int X, int Y) Limitar(int x, int y) => (Math.Clamp(x, 0, Largura), Math.Clamp(y, 0, Altura));

    /// <summary>Primeira posição livre numa grade, da esquerda para a direita e de cima para baixo.</summary>
    public static (int X, int Y) ProximaPosicao(IEnumerable<IMesaPlanta> mesas)
    {
        var ocupadas = mesas.Select(m => (m.X, m.Y)).ToList();
        for (var y = Passo / 2 + 25; y < Altura; y += Passo)
            for (var x = Passo / 2 + 25; x < Largura; x += Passo)
                if (ocupadas.All(o => Math.Abs(o.X - x) >= Passo / 2 || Math.Abs(o.Y - y) >= Passo / 2))
                    return (x, y);
        return (Largura / 2, Altura / 2);
    }

    internal static void ValidarMesa(int numero, string? nome, int lugares)
    {
        Validador.Garantir(numero is >= 1 and <= NumeroMaximo, $"O número da mesa vai de 1 a {NumeroMaximo}.");
        Validador.Garantir((nome?.Trim().Length ?? 0) <= TamanhoNome, $"O nome da mesa pode ter no máximo {TamanhoNome} caracteres.");
        Validador.Garantir(lugares is >= 1 and <= LugaresMaximo, $"A mesa tem de 1 a {LugaresMaximo} lugares.");
    }

    /// <summary>Confere um mapa inteiro: cada mesa válida, sem número repetido e dentro do limite de mesas.</summary>
    public static void Validar(IReadOnlyCollection<IMesaPlanta> mesas)
    {
        Validador.Garantir(mesas.Count <= MesasMaximo, $"O mapa pode ter no máximo {MesasMaximo} mesas.");
        foreach (var mesa in mesas)
            ValidarMesa(mesa.Numero, mesa.Nome, mesa.Lugares);
        var repetida = mesas.GroupBy(m => m.Numero).FirstOrDefault(g => g.Count() > 1);
        Validador.Garantir(repetida is null, $"A mesa {repetida?.Key} aparece mais de uma vez no mapa.");
    }

    internal static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>Mesa do evento, com os convidados sentados nela (<see cref="Convidado.Mesa"/>).</summary>
public class EventoMesa : IMesaPlanta
{
    private EventoMesa()
    {
    }

    internal EventoMesa(int numero, string? nome, int lugares, FormatoMesa formato, int x, int y)
    {
        Alterar(numero, nome, lugares, formato);
        Mover(x, y);
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public int Numero { get; private set; }
    public string? Nome { get; private set; }
    public int Lugares { get; private set; }
    public FormatoMesa Formato { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }

    public string Descricao => PlantaSalao.Descricao(Numero, Nome);

    internal void Alterar(int numero, string? nome, int lugares, FormatoMesa formato)
    {
        Numero = numero;
        Nome = nome;
        Lugares = lugares;
        Formato = formato;
    }

    internal void Mover(int x, int y) => (X, Y) = PlantaSalao.Limitar(x, y);

    public override string ToString() => Descricao;
}
