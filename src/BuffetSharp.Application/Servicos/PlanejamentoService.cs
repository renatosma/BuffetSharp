using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Planejamento da operação a partir da ficha técnica: lista de compras dos eventos de um período (descontando o estoque)
/// e ordem de produção da cozinha para cada evento.
/// </summary>
public sealed class PlanejamentoService(IBuffetDbContextFactory fabrica)
{
    // ------------------------- Lista de compras -------------------------

    /// <summary>
    /// Insumos para os eventos confirmados do período: consumo previsto (ficha técnica × pessoas) menos o saldo em estoque.
    /// O estoque só é baixado quando o evento é realizado, então o saldo atual ainda cobre eventos passados não encerrados.
    /// </summary>
    /// <param name="reporMinimo">Compra também o necessário para o estoque terminar no mínimo cadastrado.</param>
    public async Task<ListaCompras> ListaComprasAsync(DateOnly de, DateOnly ate, bool reporMinimo, CancellationToken ct = default)
    {
        Validador.Garantir(de <= ate, "O início do período deve ser anterior ao fim.");
        Validador.Garantir(ate.DayNumber - de.DayNumber <= 92, "Planeje as compras de no máximo três meses por vez.");

        await using var db = fabrica.Criar();
        var eventos = await EventosAsync(db, de, ate, ct);

        var consumo = new Dictionary<int, decimal>();
        var porProduto = new Dictionary<int, List<string>>();
        foreach (var evento in eventos.Where(e => e.Pacote is not null))
        {
            foreach (var (produtoId, quantidade) in evento.Pacote!.CalcularConsumo(evento.QuantidadeConvidados))
            {
                consumo[produtoId] = consumo.GetValueOrDefault(produtoId) + quantidade;
                if (!porProduto.TryGetValue(produtoId, out var eventosDoProduto))
                    porProduto[produtoId] = eventosDoProduto = [];
                eventosDoProduto.Add(evento.Codigo);
            }
        }

        var ids = consumo.Keys.ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Include(p => p.UnidadeMedida).Include(p => p.CategoriaProduto).Include(p => p.Fornecedor)
            .Include(p => p.Embalagens).ThenInclude(e => e.UnidadeMedida)
            .Where(p => ids.Contains(p.Id) || (reporMinimo && p.Ativo && p.EstoqueAtual < p.EstoqueMinimo))
            .ToListAsync(ct);

        var itens = new List<ItemListaCompras>();
        foreach (var produto in produtos)
        {
            var necessidade = consumo.GetValueOrDefault(produto.Id);
            var aComprar = Math.Round(necessidade + (reporMinimo ? produto.EstoqueMinimo : 0) - Math.Max(produto.EstoqueAtual, 0), 3);
            if (aComprar <= 0)
                continue;
            var embalagem = produto.EmbalagemDeCompra(produto.FornecedorId, aComprar);
            itens.Add(new ItemListaCompras
            {
                ProdutoId = produto.Id,
                Produto = produto.Nome,
                Unidade = produto.UnidadeMedida?.Sigla ?? "",
                Categoria = produto.CategoriaProduto?.Nome,
                FornecedorId = produto.FornecedorId,
                Fornecedor = produto.Fornecedor?.NomeFantasia ?? produto.Fornecedor?.RazaoSocial ?? "(sem fornecedor padrão)",
                TelefoneFornecedor = produto.Fornecedor?.Telefone,
                EmailFornecedor = produto.Fornecedor?.Email,
                Necessidade = necessidade,
                Estoque = produto.EstoqueAtual,
                EstoqueMinimo = produto.EstoqueMinimo,
                AComprar = aComprar,
                CustoUnitario = produto.CustoMedio,
                Embalagem = SugerirEmbalagem(produto, aComprar),
                EmbalagemId = embalagem?.Id,
                QuantidadeEmbalagens = embalagem is null ? null : Math.Ceiling(aComprar / embalagem.Fator),
                FatorEmbalagem = embalagem?.Fator,
                Eventos = porProduto.GetValueOrDefault(produto.Id) ?? []
            });
        }

        return new ListaCompras(de, ate, reporMinimo,
            eventos.Select(e => new EventoPlanejado(e.Id, e.Titulo, e.Inicio, e.QuantidadeConvidados, e.Pacote?.Nome)).ToList(),
            itens.OrderBy(i => i.Fornecedor).ThenBy(i => i.Categoria).ThenBy(i => i.Produto).ToList());
    }

    /// <summary>Embalagem do fornecedor padrão (ou a genérica) para comprar a quantidade, ex.: "3 CX de 12 UN".</summary>
    internal static string? SugerirEmbalagem(Produto produto, decimal aComprar)
    {
        if (produto.EmbalagemDeCompra(produto.FornecedorId, aComprar) is not { } embalagem)
            return null;
        var quantidade = Math.Ceiling(aComprar / embalagem.Fator);
        return $"{quantidade:0} {embalagem.UnidadeMedida?.Sigla ?? "emb."} de {embalagem.Fator:0.###} {produto.UnidadeMedida?.Sigla}".TrimEnd();
    }

    // ------------------------- Ordem de produção -------------------------

    /// <summary>Ordens de produção dos eventos confirmados de um dia.</summary>
    public async Task<IReadOnlyList<OrdemProducao>> OrdensDoDiaAsync(DateOnly data, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return (await EventosAsync(db, data, data, ct)).Select(Ordem).ToList();
    }

    public async Task<OrdemProducao> OrdemDoEventoAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await ComCardapio(db.Eventos).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        return Ordem(evento);
    }

    private static OrdemProducao Ordem(Evento e) => new()
    {
        EventoId = e.Id,
        Titulo = e.Titulo,
        Cliente = e.Cliente?.Nome ?? "",
        Inicio = e.Inicio,
        Fim = e.Fim,
        Espaco = e.Espaco?.Nome ?? "",
        Pessoas = e.QuantidadeConvidados,
        Convidados = TextosFaixas.Resumo(e.QuantidadesPorFaixa),
        Pacote = e.Pacote?.Nome,
        Observacoes = e.Observacoes,
        Pratos = e.Pacote is null
            ? []
            : e.Pacote.Itens.Select(i => i.ItemCardapio!)
                .OrderBy(i => i.CategoriaCardapio?.Ordem ?? 99).ThenBy(i => i.Nome)
                .Select(i => new PratoProducao(i.CategoriaCardapio?.Nome ?? "Outros", i.CategoriaCardapio?.Ordem ?? 99, i.Nome,
                    e.QuantidadeConvidados,
                    i.Ingredientes.OrderBy(f => f.Produto?.Nome)
                        .Select(f => new IngredienteProducao(f.Produto?.Nome ?? "?", Math.Round(f.QuantidadePorPessoa * e.QuantidadeConvidados, 3),
                            f.Produto?.UnidadeMedida?.Sigla ?? ""))
                        .ToList()))
                .ToList()
    };

    // ------------------------- Infra -------------------------

    private static IQueryable<Evento> ComCardapio(IQueryable<Evento> eventos) => eventos.AsNoTracking()
        .Include(e => e.Cliente)
        .Include(e => e.Espaco)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.CategoriaCardapio)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Ingredientes)
        .ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida);

    private static async Task<List<Evento>> EventosAsync(IBuffetDbContext db, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await ComCardapio(db.Eventos)
            .Where(e => e.Status == StatusEvento.Confirmado && e.Inicio >= inicio && e.Inicio < fim)
            .OrderBy(e => e.Inicio)
            .ToListAsync(ct);
    }
}
