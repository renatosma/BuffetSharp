using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Cotações de compra: os produtos orçados com vários participantes (fornecedores cadastrados ou só um nome e contato), o
/// mapa de preços com o vencedor de cada produto e, ao concluir, um pedido de compra por vencedor.
/// </summary>
public sealed class CotacaoService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    public CotacaoInput Nova() => new() { Data = Hoje, PrazoResposta = Hoje.AddDays(3) };

    public async Task<CotacaoInput> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var c = await CarregarAsync(db, id, ct, rastrear: false);
        return new CotacaoInput
        {
            Id = c.Id,
            Versao = c.Versao,
            Descricao = c.Descricao,
            Data = c.Data,
            PrazoResposta = c.PrazoResposta,
            EventoId = c.EventoId,
            Observacoes = c.Observacoes,
            Codigo = c.Codigo,
            Status = c.Status,
            Itens = c.Itens.OrderBy(i => i.Id).Select(i => new CotacaoItemInput
            {
                ProdutoId = i.ProdutoId, Quantidade = i.Quantidade, Especificacao = i.Especificacao
            }).ToList(),
            Participantes = c.Participantes.OrderBy(p => p.Id).Select(p => new CotacaoParticipanteInput
            {
                Id = p.Id,
                Nome = p.Nome,
                FornecedorId = p.FornecedorId,
                Contato = p.Contato,
                Telefone = p.Telefone,
                Email = p.Email,
                PrevisaoEntrega = p.PrevisaoEntrega,
                CondicaoPagamento = p.CondicaoPagamento,
                Observacoes = p.Observacoes
            }).ToList()
        };
    }

    /// <summary>Grava o cabeçalho, os produtos e os participantes. Os preços já informados de quem continua são mantidos.</summary>
    public async Task<int> SalvarAsync(CotacaoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        CotacaoCompra cotacao;
        if (input.Id == 0)
        {
            cotacao = new CotacaoCompra(input.Descricao, input.Data);
            db.CotacoesCompra.Add(cotacao);
        }
        else
        {
            cotacao = await CarregarAsync(db, input.Id, ct);
            Concorrencia.Conferir(cotacao, input.Versao);
        }

        cotacao.Alterar(input.Descricao, input.Data, input.PrazoResposta, input.EventoId, input.Observacoes);
        var produtosIds = input.Itens.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.Where(p => produtosIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        cotacao.DefinirItens(input.Itens.Select(i => (
            produtos.GetValueOrDefault(i.ProdutoId) ?? throw new RegraNegocioException("Selecione o produto de cada item da cotação."),
            i.Quantidade,
            i.Especificacao)));

        // Primeiro saem os retirados (assim um nome pode passar de um participante para outro).
        foreach (var retirado in cotacao.Participantes.Where(p => input.Participantes.All(i => i.Id != p.Id)).ToList())
            cotacao.RemoverParticipante(retirado);
        foreach (var dados in input.Participantes)
        {
            CotacaoParticipante participante;
            if (dados.Id == 0)
                participante = cotacao.AdicionarParticipante(dados.Nome, dados.Contato, dados.Telefone, dados.Email, dados.FornecedorId);
            else
            {
                participante = cotacao.Participantes.FirstOrDefault(p => p.Id == dados.Id)
                               ?? throw new RegraNegocioException("Participante não encontrado na cotação.");
                cotacao.AlterarParticipante(participante, dados.Nome, dados.Contato, dados.Telefone, dados.Email, dados.FornecedorId);
            }
            cotacao.DefinirCondicoes(participante, dados.PrevisaoEntrega, dados.CondicaoPagamento, dados.Observacoes);
        }

        cotacao.AtualizarVencedores();
        await db.SaveChangesAsync(ct);
        input.Versao = cotacao.Versao;
        return cotacao.Id;
    }

    public async Task<MapaCotacao> ObterMapaAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var c = await CarregarAsync(db, id, ct, rastrear: false);
        return new MapaCotacao
        {
            CotacaoId = c.Id,
            Versao = c.Versao,
            Codigo = c.Codigo,
            Descricao = c.Descricao,
            Status = c.Status,
            Itens = c.Itens.OrderBy(i => i.Id).Select(i => new MapaCotacaoItem
            {
                ItemId = i.Id,
                Produto = i.Produto?.Nome ?? "",
                Unidade = i.Produto?.UnidadeMedida?.Sigla ?? "",
                Quantidade = i.Quantidade,
                Especificacao = i.Especificacao,
                Escolhido = i.EscolhaManual ? i.VencedorId : null,
                Motivo = i.MotivoEscolha
            }).ToList(),
            Participantes = c.Participantes.OrderBy(p => p.Id).Select(p => new MapaCotacaoParticipante
            {
                Id = p.Id,
                Nome = p.Nome,
                FornecedorId = p.FornecedorId,
                PrevisaoEntrega = p.PrevisaoEntrega,
                CondicaoPagamento = p.CondicaoPagamento
            }).ToList(),
            Precos = c.Precos.Select(p => new MapaCotacaoPreco
            {
                ItemId = p.CotacaoItemId, ParticipanteId = p.CotacaoParticipanteId, PrecoUnitario = p.PrecoUnitario, Observacao = p.Observacao
            }).ToList()
        };
    }

    /// <summary>
    /// Grava os preços do mapa (sem preço = não cotou) e as escolhas: sem escolha, vence o menor preço; escolher outro exige
    /// o motivo.
    /// </summary>
    public async Task SalvarMapaAsync(MapaCotacao mapa, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var cotacao = await CarregarAsync(db, mapa.CotacaoId, ct);
        Concorrencia.Conferir(cotacao, mapa.Versao);
        foreach (var item in cotacao.Itens)
        {
            foreach (var participante in cotacao.Participantes)
            {
                var preco = mapa.Preco(item.Id, participante.Id);
                cotacao.DefinirPreco(item, participante, preco is { PrecoUnitario: > 0 } ? preco.PrecoUnitario : null, preco?.Observacao);
            }
        }
        foreach (var item in cotacao.Itens)
        {
            var escolha = mapa.Itens.FirstOrDefault(i => i.ItemId == item.Id);
            var participante = escolha?.Escolhido is { } id ? cotacao.Participantes.FirstOrDefault(p => p.Id == id) : null;
            cotacao.EscolherVencedor(item, participante, escolha?.Motivo);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Conclui a cotação: um pedido de compra por vencedor. Vencedores ainda sem cadastro são cadastrados como fornecedores
    /// com os dados da cotação quando <paramref name="cadastrarFornecedores"/> é verdadeiro (senão a operação é recusada).
    /// </summary>
    /// <returns>Os pedidos gerados.</returns>
    public async Task<IReadOnlyList<int>> GerarPedidosAsync(int cotacaoId, bool cadastrarFornecedores, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var cotacao = await CarregarAsync(db, cotacaoId, ct);
        var semCadastro = cotacao.VencedoresSemCadastro();
        if (cadastrarFornecedores && semCadastro.Count > 0)
        {
            foreach (var participante in semCadastro)
                db.Fornecedores.Add(participante.CadastrarFornecedor());
            await db.SaveChangesAsync(ct);
        }

        var pedidos = cotacao.GerarPedidos(Hoje, Agora);
        db.PedidosCompra.AddRange(pedidos);
        await db.SaveChangesAsync(ct);
        return pedidos.Select(p => p.Id).ToList();
    }

    public async Task CancelarAsync(int cotacaoId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var cotacao = await db.CotacoesCompra.FirstOrDefaultAsync(c => c.Id == cotacaoId, ct)
                      ?? throw new RegraNegocioException("Cotação não encontrada.");
        cotacao.Cancelar(motivo);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Cotação aberta a partir da lista de compras: todos os produtos (na unidade do estoque) e, como participantes, os
    /// fornecedores padrão deles. Outros participantes podem ser incluídos depois.
    /// </summary>
    public async Task<int> GerarDaListaAsync(string descricao, IEnumerable<ItemListaCompras> itens, CancellationToken ct = default)
    {
        var selecionados = itens.Where(i => i.AComprar > 0).ToList();
        Validador.Garantir(selecionados.Count > 0, "Não há itens a comprar na lista.");
        var input = Nova();
        input.Descricao = descricao;
        input.Observacoes = "Gerada pela lista de compras.";
        input.Itens = selecionados.GroupBy(i => i.ProdutoId)
            .Select(g => new CotacaoItemInput { ProdutoId = g.Key, Quantidade = g.Sum(i => i.AComprar) })
            .ToList();

        var fornecedoresIds = selecionados.Where(i => i.FornecedorId is not null).Select(i => i.FornecedorId!.Value).Distinct().ToList();
        await using (var db = fabrica.Criar())
        {
            var fornecedores = await db.Fornecedores.AsNoTracking().Where(f => fornecedoresIds.Contains(f.Id)).ToListAsync(ct);
            input.Participantes = fornecedores.OrderBy(f => f.NomeExibicao).Select(f => new CotacaoParticipanteInput
            {
                Nome = f.NomeExibicao, FornecedorId = f.Id, Contato = f.Contato, Telefone = f.Telefone, Email = f.Email
            }).ToList();
        }
        return await SalvarAsync(input, ct);
    }

    private static async Task<CotacaoCompra> CarregarAsync(IBuffetDbContext db, int id, CancellationToken ct, bool rastrear = true)
    {
        IQueryable<CotacaoCompra> consulta = db.CotacoesCompra
            .Include(c => c.Itens).ThenInclude(i => i.Produto!).ThenInclude(p => p.UnidadeMedida)
            .Include(c => c.Itens).ThenInclude(i => i.Produto!).ThenInclude(p => p.Embalagens).ThenInclude(e => e.UnidadeMedida)
            .Include(c => c.Participantes)
            .Include(c => c.Precos);
        return await (rastrear ? consulta : consulta.AsNoTrackingWithIdentityResolution()).FirstOrDefaultAsync(c => c.Id == id, ct)
               ?? throw new RegraNegocioException("Cotação não encontrada.");
    }
}
