using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Carregamento do cardápio do evento para o <see cref="Domain.Cardapio.PlanoCardapio"/>.</summary>
internal static class Cardapios
{
    /// <summary>
    /// O pacote com os pratos, os insumos (com produto e unidade) e as seções, os substitutos até o segundo nível, os
    /// convidados e as restrições avulsas: tudo o que o plano das porções com restrições usa.
    /// </summary>
    public static IQueryable<Evento> ComCardapioCompleto(this IQueryable<Evento> eventos) => eventos
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.CategoriaCardapio)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Ingredientes)
        .ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Substituto!)
        .ThenInclude(s => s.CategoriaCardapio)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Substituto!)
        .ThenInclude(s => s.Ingredientes).ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Substituto!)
        .ThenInclude(s => s.Substituto!).ThenInclude(s => s.CategoriaCardapio)
        .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Substituto!)
        .ThenInclude(s => s.Substituto!).ThenInclude(s => s.Ingredientes).ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida)
        .Include(e => e.Convidados).ThenInclude(c => c.Mesa)
        .Include(e => e.RestricoesAvulsas);
}
