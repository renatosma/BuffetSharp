namespace BuffetSharp.Domain.Cardapio;

/// <summary>Pessoas com a mesma combinação de restrições (ex.: 6 vegetarianos, 1 vegano e sem glúten).</summary>
public sealed record GrupoRestricao(RestricaoAlimentar Restricoes, int Pessoas);

/// <summary>Prato do cardápio que um grupo com restrição não come, e o substituto que vai no lugar (nulo: a cozinha resolve).</summary>
public sealed record TrocaPrato(GrupoRestricao Grupo, ItemCardapio Prato, ItemCardapio? Substituto);

/// <summary>
/// Quantas porções de cada prato produzir para o evento, considerando as restrições: quem não pode comer um prato recebe o
/// substituto dele (ou o substituto do substituto, se o primeiro também não servir). Sem substituto que sirva, a porção sai
/// do prato e a ordem de produção avisa a cozinha.
/// </summary>
public sealed class PlanoCardapio
{
    /// <summary>Até onde segue a cadeia de substitutos (prato → substituto → substituto do substituto).</summary>
    public const int ProfundidadeSubstitutos = 2;

    private readonly Dictionary<ItemCardapio, int> _porcoes = new(ReferenceEqualityComparer.Instance);

    private PlanoCardapio(int pessoas) => Pessoas = pessoas;

    public int Pessoas { get; }

    /// <summary>Porções de cada prato, incluindo os substitutos (que podem não estar no pacote).</summary>
    public IReadOnlyDictionary<ItemCardapio, int> Porcoes => _porcoes;

    public IReadOnlyList<TrocaPrato> Trocas { get; private set; } = [];

    /// <param name="pratos">Os pratos do pacote, com os insumos (e os produtos deles) e os substitutos carregados.</param>
    /// <param name="restricoes">Grupos com restrição; somam no máximo as pessoas do evento.</param>
    public static PlanoCardapio Calcular(IEnumerable<ItemCardapio> pratos, int pessoas, IEnumerable<GrupoRestricao> restricoes)
    {
        var plano = new PlanoCardapio(pessoas);
        var cardapio = pratos.Distinct(ReferenceEqualityComparer.Instance).Cast<ItemCardapio>().ToList();
        foreach (var prato in cardapio)
            plano._porcoes[prato] = pessoas;

        var trocas = new List<TrocaPrato>();
        var restantes = pessoas;
        foreach (var grupo in restricoes.Where(g => g.Pessoas > 0 && g.Restricoes != RestricaoAlimentar.Nenhuma)
                     .GroupBy(g => g.Restricoes).Select(g => new GrupoRestricao(g.Key, g.Sum(x => x.Pessoas))).OrderBy(g => g.Restricoes))
        {
            var atendidos = new GrupoRestricao(grupo.Restricoes, Math.Min(grupo.Pessoas, restantes));
            restantes -= atendidos.Pessoas;
            if (atendidos.Pessoas == 0)
                continue;
            foreach (var prato in cardapio.Where(p => !p.Serve(atendidos.Restricoes)))
            {
                var substituto = SubstitutoPara(prato, atendidos.Restricoes);
                plano._porcoes[prato] -= atendidos.Pessoas;
                if (substituto is not null)
                    plano._porcoes[substituto] = plano._porcoes.GetValueOrDefault(substituto) + atendidos.Pessoas;
                trocas.Add(new TrocaPrato(atendidos, prato, substituto));
            }
        }
        plano.Trocas = trocas;
        return plano;
    }

    /// <summary>O substituto do prato que serve para as restrições (nulo se nenhum da cadeia servir).</summary>
    public static ItemCardapio? SubstitutoPara(ItemCardapio prato, RestricaoAlimentar restricoes)
    {
        var substituto = prato.Substituto;
        for (var nivel = 0; nivel < ProfundidadeSubstitutos && substituto is not null; nivel++)
        {
            if (substituto.Serve(restricoes))
                return substituto;
            substituto = substituto.Substituto;
        }
        return null;
    }

    /// <summary>Insumos a usar (Id do produto → quantidade), pelas porções de cada prato.</summary>
    /// <param name="contexto">Duração e adultos (bebidas por hora e alcoólicas); nulo usa a referência, com todos adultos.</param>
    public Dictionary<int, decimal> Consumo(ContextoConsumo? contexto = null) =>
        _porcoes.Where(p => p.Value > 0)
            .SelectMany(p => p.Key.Ingredientes.Select(f => (f.ProdutoId, Quantidade: f.Quantidade(p.Value, contexto ?? ContextoConsumo.Padrao(Pessoas)))))
            .GroupBy(f => f.ProdutoId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => f.Quantidade), 3));
}
