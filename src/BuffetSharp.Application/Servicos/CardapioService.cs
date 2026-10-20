using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class CardapioService(IBuffetDbContextFactory fabrica)
{
    // ------------------------- Itens do cardápio -------------------------

    public async Task<ItemCardapioInput> ObterItemAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var item = await db.ItensCardapio.AsNoTracking()
                       .Include(i => i.Ingredientes).ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida)
                       .FirstOrDefaultAsync(i => i.Id == id, ct)
                   ?? throw new RegraNegocioException("Item do cardápio não encontrado.");

        return new ItemCardapioInput
        {
            Id = item.Id,
            Versao = item.Versao,
            Nome = item.Nome,
            Descricao = item.Descricao,
            CategoriaCardapioId = item.CategoriaCardapioId,
            Ativo = item.Ativo,
            Ingredientes = item.Ingredientes
                .OrderBy(f => f.Produto!.Nome)
                .Select(f => new IngredienteInput
                {
                    ProdutoId = f.ProdutoId,
                    Produto = f.Produto!.Nome,
                    Unidade = f.Produto.UnidadeMedida?.Sigla ?? string.Empty,
                    CustoUnitario = f.Produto.CustoMedio,
                    QuantidadePorPessoa = f.QuantidadePorPessoa
                }).ToList()
        };
    }

    public async Task<int> SalvarItemAsync(ItemCardapioInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var item = input.Id == 0
            ? db.ItensCardapio.Add(new ItemCardapio()).Entity
            : await db.ItensCardapio.Include(i => i.Ingredientes).FirstOrDefaultAsync(i => i.Id == input.Id, ct)
              ?? throw new RegraNegocioException("Item do cardápio não encontrado.");
        Concorrencia.Conferir(item, input.Versao);

        item.Nome = input.Nome.Trim();
        item.Descricao = input.Descricao;
        item.CategoriaCardapioId = input.CategoriaCardapioId;
        item.Ativo = input.Ativo;
        Validador.Validar(item);
        item.DefinirFichaTecnica(input.Ingredientes.Select(i => (i.ProdutoId, i.QuantidadePorPessoa)));

        await db.SaveChangesAsync(ct);
        input.Versao = item.Versao;
        return item.Id;
    }

    public async Task ExcluirItemAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var item = await db.ItensCardapio.FirstOrDefaultAsync(i => i.Id == id, ct)
                   ?? throw new RegraNegocioException("Item do cardápio não encontrado.");
        db.ItensCardapio.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------- Pacotes -------------------------

    public async Task<PacoteInput> ObterPacoteAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pacote = await db.Pacotes.AsNoTracking().Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new RegraNegocioException("Pacote não encontrado.");
        return new PacoteInput
        {
            Id = pacote.Id,
            Versao = pacote.Versao,
            Nome = pacote.Nome,
            Descricao = pacote.Descricao,
            PrecoPorPessoa = pacote.PrecoPorPessoa,
            MinimoConvidados = pacote.MinimoConvidados,
            IdadeMaximaIsento = pacote.IdadeMaximaIsento,
            IdadeMaximaMeia = pacote.IdadeMaximaMeia,
            PercentualMeia = pacote.PercentualMeia,
            Ativo = pacote.Ativo,
            ItensCardapioIds = pacote.Itens.Select(i => i.ItemCardapioId).ToList()
        };
    }

    public async Task<int> SalvarPacoteAsync(PacoteInput input, CancellationToken ct = default)
    {
        Validador.Garantir(input.ItensCardapioIds.Count > 0, "Selecione ao menos um item do cardápio.");

        await using var db = fabrica.Criar();
        var pacote = input.Id == 0
            ? db.Pacotes.Add(new Pacote()).Entity
            : await db.Pacotes.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == input.Id, ct)
              ?? throw new RegraNegocioException("Pacote não encontrado.");
        Concorrencia.Conferir(pacote, input.Versao);

        pacote.Nome = input.Nome.Trim();
        pacote.Descricao = input.Descricao;
        pacote.PrecoPorPessoa = input.PrecoPorPessoa;
        pacote.MinimoConvidados = input.MinimoConvidados;
        pacote.IdadeMaximaIsento = input.IdadeMaximaIsento;
        pacote.IdadeMaximaMeia = input.IdadeMaximaMeia;
        pacote.PercentualMeia = input.PercentualMeia;
        pacote.Ativo = input.Ativo;
        Validador.Validar(pacote);
        pacote.DefinirItens(input.ItensCardapioIds);

        await db.SaveChangesAsync(ct);
        input.Versao = pacote.Versao;
        return pacote.Id;
    }

    public async Task ExcluirPacoteAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pacote = await db.Pacotes.FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new RegraNegocioException("Pacote não encontrado.");
        db.Pacotes.Remove(pacote);
        await db.SaveChangesAsync(ct);
    }
}
