using System.Globalization;
using System.Text;

namespace BuffetSharp.Domain.Cardapio;

/// <summary>O que um insumo (e, pela ficha técnica, um prato) contém e importa para as restrições alimentares.</summary>
[Flags]
public enum ConteudoAlimentar
{
    Nenhum = 0,
    Gluten = 1,
    Lactose = 2,
    Ovo = 4,

    /// <summary>Amendoim e castanhas.</summary>
    Oleaginosas = 8,

    /// <summary>Peixes e frutos do mar.</summary>
    FrutosDoMar = 16,

    Soja = 32,

    /// <summary>Carne de boi, porco, aves (e caldos e gorduras delas).</summary>
    Carne = 64,

    /// <summary>Outro ingrediente de origem animal (mel, gelatina...), que o vegano não come.</summary>
    OrigemAnimal = 128
}

/// <summary>Restrição alimentar de um convidado (pode combinar várias: vegano e sem glúten).</summary>
[Flags]
public enum RestricaoAlimentar
{
    Nenhuma = 0,
    Vegetariano = 1,
    Vegano = 2,
    SemGluten = 4,
    SemLactose = 8,
    SemOvo = 16,
    SemOleaginosas = 32,
    SemFrutosDoMar = 64,
    SemSoja = 128
}

/// <summary>Regras das restrições alimentares: o que cada uma não pode comer, nomes, siglas e leitura de texto livre.</summary>
public static class RestricoesAlimentares
{
    public const int TamanhoObservacao = 200;

    public static readonly RestricaoAlimentar[] Todas = Enum.GetValues<RestricaoAlimentar>().Where(r => r != RestricaoAlimentar.Nenhuma).ToArray();
    public static readonly ConteudoAlimentar[] Conteudos = Enum.GetValues<ConteudoAlimentar>().Where(c => c != ConteudoAlimentar.Nenhum).ToArray();

    /// <summary>Alérgenos que a proposta e o cardápio informam (carne e origem animal aparecem como vegetariano/vegano).</summary>
    public const ConteudoAlimentar Alergenos = ConteudoAlimentar.Gluten | ConteudoAlimentar.Lactose | ConteudoAlimentar.Ovo |
                                               ConteudoAlimentar.Oleaginosas | ConteudoAlimentar.FrutosDoMar | ConteudoAlimentar.Soja;

    private const ConteudoAlimentar NaoVegetariano = ConteudoAlimentar.Carne | ConteudoAlimentar.FrutosDoMar;
    private const ConteudoAlimentar NaoVegano = NaoVegetariano | ConteudoAlimentar.Lactose | ConteudoAlimentar.Ovo | ConteudoAlimentar.OrigemAnimal;

    /// <summary>O que quem tem as restrições não pode comer.</summary>
    public static ConteudoAlimentar Proibido(RestricaoAlimentar restricoes) =>
        Separar(restricoes).Aggregate(ConteudoAlimentar.Nenhum, (soma, r) => soma | r switch
        {
            RestricaoAlimentar.Vegetariano => NaoVegetariano,
            RestricaoAlimentar.Vegano => NaoVegano,
            RestricaoAlimentar.SemGluten => ConteudoAlimentar.Gluten,
            RestricaoAlimentar.SemLactose => ConteudoAlimentar.Lactose,
            RestricaoAlimentar.SemOvo => ConteudoAlimentar.Ovo,
            RestricaoAlimentar.SemOleaginosas => ConteudoAlimentar.Oleaginosas,
            RestricaoAlimentar.SemFrutosDoMar => ConteudoAlimentar.FrutosDoMar,
            RestricaoAlimentar.SemSoja => ConteudoAlimentar.Soja,
            _ => ConteudoAlimentar.Nenhum
        });

    public static bool Serve(ConteudoAlimentar contem, RestricaoAlimentar restricoes) => (contem & Proibido(restricoes)) == 0;

    public static IEnumerable<RestricaoAlimentar> Separar(RestricaoAlimentar restricoes) => Todas.Where(r => restricoes.HasFlag(r));

    public static IEnumerable<ConteudoAlimentar> Separar(ConteudoAlimentar contem) => Conteudos.Where(c => contem.HasFlag(c));

    public static string Nome(RestricaoAlimentar restricao) => restricao switch
    {
        RestricaoAlimentar.Vegetariano => "Vegetariano",
        RestricaoAlimentar.Vegano => "Vegano",
        RestricaoAlimentar.SemGluten => "Sem glúten",
        RestricaoAlimentar.SemLactose => "Sem lactose",
        RestricaoAlimentar.SemOvo => "Sem ovo",
        RestricaoAlimentar.SemOleaginosas => "Sem amendoim/castanhas",
        RestricaoAlimentar.SemFrutosDoMar => "Sem peixe/frutos do mar",
        RestricaoAlimentar.SemSoja => "Sem soja",
        RestricaoAlimentar.Nenhuma => "Sem restrição",
        _ => string.Join(" + ", Separar(restricao).Select(Nome))
    };

    public static string Nome(ConteudoAlimentar conteudo) => conteudo switch
    {
        ConteudoAlimentar.Gluten => "glúten",
        ConteudoAlimentar.Lactose => "lactose",
        ConteudoAlimentar.Ovo => "ovo",
        ConteudoAlimentar.Oleaginosas => "amendoim/castanhas",
        ConteudoAlimentar.FrutosDoMar => "peixe/frutos do mar",
        ConteudoAlimentar.Soja => "soja",
        ConteudoAlimentar.Carne => "carne",
        ConteudoAlimentar.OrigemAnimal => "origem animal",
        ConteudoAlimentar.Nenhum => "nada",
        _ => string.Join(", ", Separar(conteudo).Select(Nome))
    };

    /// <summary>Sigla curta para a mesa, o cartão e a lista da recepção (ex.: "VGN+SG").</summary>
    public static string Sigla(RestricaoAlimentar restricoes) => string.Join("+", Separar(restricoes).Select(r => r switch
    {
        RestricaoAlimentar.Vegetariano => "VEG",
        RestricaoAlimentar.Vegano => "VGN",
        RestricaoAlimentar.SemGluten => "SG",
        RestricaoAlimentar.SemLactose => "SL",
        RestricaoAlimentar.SemOvo => "S/OVO",
        RestricaoAlimentar.SemOleaginosas => "S/CAST",
        RestricaoAlimentar.SemFrutosDoMar => "S/MAR",
        _ => "S/SOJA"
    }));

    /// <summary>Texto curto da restrição de uma pessoa: as restrições marcadas e a observação ("Vegano; alergia a kiwi").</summary>
    public static string? Descrever(RestricaoAlimentar restricoes, string? observacao) =>
        (restricoes, string.IsNullOrWhiteSpace(observacao)) switch
        {
            (RestricaoAlimentar.Nenhuma, true) => null,
            (RestricaoAlimentar.Nenhuma, false) => observacao!.Trim(),
            (_, true) => Nome(restricoes),
            _ => $"{Nome(restricoes)}; {observacao!.Trim()}"
        };

    /// <summary>
    /// Selos do prato para o cliente: vegano ou vegetariano, sem glúten e sem lactose, e os alérgenos que ele contém
    /// (ex.: "vegetariano • sem glúten • contém: lactose, ovo").
    /// </summary>
    public static string Selos(ConteudoAlimentar contem)
    {
        var selos = new List<string>();
        if (Serve(contem, RestricaoAlimentar.Vegano))
            selos.Add("vegano");
        else if (Serve(contem, RestricaoAlimentar.Vegetariano))
            selos.Add("vegetariano");
        if (!contem.HasFlag(ConteudoAlimentar.Gluten))
            selos.Add("sem glúten");
        if (!contem.HasFlag(ConteudoAlimentar.Lactose))
            selos.Add("sem lactose");
        if ((contem & Alergenos) != 0)
            selos.Add("contém: " + Nome(contem & Alergenos));
        return string.Join(" • ", selos);
    }

    /// <summary>
    /// Lê restrições escritas à mão (importação da lista de convidados): "vegano; sem lactose", "celíaco", "alergia a camarão".
    /// O que não for reconhecido volta como observação.
    /// </summary>
    public static (RestricaoAlimentar Restricoes, string? Observacao) Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return (RestricaoAlimentar.Nenhuma, null);
        var restricoes = RestricaoAlimentar.Nenhuma;
        var resto = new List<string>();
        foreach (var parte in texto.Split([',', ';', '/', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var chave = SemAcento(parte).ToLowerInvariant();
            RestricaoAlimentar? lida = chave switch
            {
                _ when chave.StartsWith("vegan") || chave.StartsWith("vgn") => RestricaoAlimentar.Vegano,
                _ when chave.StartsWith("vegetarian") || chave == "veg" => RestricaoAlimentar.Vegetariano,
                _ when chave.Contains("gluten") || chave.StartsWith("celiac") || chave == "sg" => RestricaoAlimentar.SemGluten,
                _ when chave.Contains("lactose") || chave.Contains("leite") || chave == "sl" => RestricaoAlimentar.SemLactose,
                _ when chave.Contains("ovo") => RestricaoAlimentar.SemOvo,
                _ when chave.Contains("amendoim") || chave.Contains("castanha") || chave.Contains("nozes") => RestricaoAlimentar.SemOleaginosas,
                _ when chave.Contains("frutos do mar") || chave.Contains("camarao") || chave.Contains("peixe") || chave.Contains("marisco") =>
                    RestricaoAlimentar.SemFrutosDoMar,
                _ when chave.Contains("soja") => RestricaoAlimentar.SemSoja,
                _ => null
            };
            if (lida is { } r)
                restricoes |= r;
            else
                resto.Add(parte);
        }
        var observacao = resto.Count == 0 ? null : string.Join("; ", resto);
        return (restricoes, observacao is { Length: > TamanhoObservacao } ? observacao[..TamanhoObservacao] : observacao);
    }

    private static string SemAcento(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        return new string(normalizado.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
