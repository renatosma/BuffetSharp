using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Pedidos de compra: vários produtos de um fornecedor. No recebimento informa-se só a nota fiscal, a data da compra e os
/// vencimentos; todos os itens entram no estoque e a nota vira uma conta a pagar (ou parcelas), tudo na mesma gravação.
/// </summary>
public sealed class CompraService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    public PedidoCompraInput Novo(int fornecedorId = 0) => new() { FornecedorId = fornecedorId, Data = Hoje };

    public async Task<PedidoCompraInput> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var p = await db.PedidosCompra.AsNoTracking().Include(x => x.Itens).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
        return new PedidoCompraInput
        {
            Id = p.Id,
            Versao = p.Versao,
            FornecedorId = p.FornecedorId,
            Data = p.Data,
            PrevisaoEntrega = p.PrevisaoEntrega,
            Categoria = p.Categoria,
            Observacoes = p.Observacoes,
            Codigo = p.Codigo,
            Status = p.Status,
            NotaFiscal = p.NotaFiscal,
            DataCompra = p.DataCompra,
            Itens = p.Itens.OrderBy(i => i.Id).Select(i => new PedidoCompraItemInput
            {
                ProdutoId = i.ProdutoId,
                ProdutoEmbalagemId = i.ProdutoEmbalagemId ?? 0,
                Quantidade = i.Quantidade,
                CustoUnitario = i.CustoUnitario
            }).ToList()
        };
    }

    /// <summary>
    /// Produtos ativos com as unidades de compra que valem para o fornecedor (a do estoque e as embalagens dele ou de qualquer
    /// fornecedor), para a tela do pedido.
    /// </summary>
    public async Task<IReadOnlyList<ProdutoCompra>> ProdutosAsync(int fornecedorId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var produtos = await db.Produtos.AsNoTracking()
            .Include(p => p.UnidadeMedida)
            .Include(p => p.Embalagens).ThenInclude(e => e.UnidadeMedida)
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .ToListAsync(ct);
        return produtos.Select(p =>
        {
            var unidade = p.UnidadeMedida?.Sigla ?? "";
            var unidades = new List<UnidadeCompra> { new(0, unidade, 1) };
            unidades.AddRange(p.Embalagens
                .Where(e => e.FornecedorId is null || e.FornecedorId == fornecedorId)
                .OrderBy(e => e.Fator)
                .Select(e => new UnidadeCompra(e.Id, $"{e.UnidadeMedida?.Sigla} com {e.Fator:0.###} {unidade}", e.Fator)));
            return new ProdutoCompra(p.Id, p.Nome, unidade, p.CustoMedio, p.FornecedorId, unidades);
        }).ToList();
    }

    public async Task<int> SalvarAsync(PedidoCompraInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        PedidoCompra pedido;
        if (input.Id == 0)
        {
            pedido = new PedidoCompra(input.FornecedorId, input.Data);
            db.PedidosCompra.Add(pedido);
        }
        else
        {
            pedido = await db.PedidosCompra.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == input.Id, ct)
                     ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
            Concorrencia.Conferir(pedido, input.Versao);
        }

        pedido.Alterar(input.FornecedorId, input.Data, input.PrevisaoEntrega, input.Categoria, input.Observacoes);
        pedido.DefinirItens(await ItensAsync(db, input.Itens, ct));
        await db.SaveChangesAsync(ct);
        input.Versao = pedido.Versao;
        return pedido.Id;
    }

    /// <summary>
    /// Recebe a mercadoria: cada item entra no estoque (convertido pela embalagem, com o fornecedor e a nota) e a nota vira uma
    /// conta a pagar com vencimento único ou uma compra parcelada com os vencimentos informados.
    /// </summary>
    public async Task ReceberAsync(RecebimentoPedidoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pedido = await db.PedidosCompra
                         .Include(p => p.Fornecedor)
                         .Include(p => p.Itens).ThenInclude(i => i.Produto)
                         .Include(p => p.Itens).ThenInclude(i => i.Embalagem!).ThenInclude(e => e.UnidadeMedida)
                         .FirstOrDefaultAsync(p => p.Id == input.PedidoId, ct)
                     ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
        var vencimentos = input.Vencimentos.Select(v => (v.Vencimento, v.Valor)).ToList();
        pedido.ConferirRecebimento(input.NotaFiscal, input.DataCompra, vencimentos, Hoje);

        var nota = input.NotaFiscal.Trim();
        var quando = input.DataCompra == Hoje ? Agora : input.DataCompra.ToDateTime(new TimeOnly(12, 0));
        var entradas = new List<MovimentacaoEstoque>();
        foreach (var item in pedido.Itens.OrderBy(i => i.Id))
        {
            var entrada = item.Produto!.RegistrarCompra(item.Quantidade, item.CustoUnitario, quando, $"{pedido.Codigo} - NF {nota}",
                pedido.FornecedorId, item.Embalagem);
            db.MovimentacoesEstoque.Add(entrada);
            entradas.Add(entrada);
        }

        var fornecedor = pedido.Fornecedor?.NomeFantasia ?? pedido.Fornecedor?.RazaoSocial;
        var descricao = $"Compra {pedido.Codigo} - NF {nota}" + (fornecedor is null ? "" : $" - {fornecedor}");
        descricao = descricao[..Math.Min(descricao.Length, 120)];
        var observacao = $"{pedido.Itens.Count} item(ns) recebido(s) pelo pedido {pedido.Codigo}.";
        ContaPagar? conta = null;
        ParcelamentoPagar? parcelamento = null;
        if (vencimentos.Count == 1)
        {
            conta = new ContaPagar(descricao, pedido.Categoria, pedido.Total, vencimentos[0].Vencimento, pedido.FornecedorId)
            {
                Observacao = observacao
            };
            conta.DefinirDocumento(nota);
            db.ContasPagar.Add(conta);
        }
        else
        {
            parcelamento = new ParcelamentoPagar(descricao, pedido.Categoria, pedido.Total, pedido.FornecedorId, null, nota, observacao);
            parcelamento.GerarParcelas(vencimentos);
            db.ParcelamentosPagar.Add(parcelamento);
        }

        pedido.VincularRecebimento(nota, input.DataCompra, conta, parcelamento, entradas, Agora);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Desfaz o recebimento (nota lançada errada): as entradas são estornadas, as contas da nota canceladas e o pedido volta a
    /// ficar aberto para corrigir e receber de novo. Recusado se a nota já foi paga ou se um produto já foi usado.
    /// </summary>
    public async Task EstornarRecebimentoAsync(int pedidoId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pedido = await db.PedidosCompra
                         .Include(p => p.ContaPagar)
                         .Include(p => p.ParcelamentoPagar!).ThenInclude(p => p.Parcelas)
                         .Include(p => p.Itens).ThenInclude(i => i.Entrada!).ThenInclude(m => m.Produto)
                         .FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do estorno.");

        var agora = Agora;
        foreach (var entrada in pedido.Itens.Select(i => i.Entrada).OfType<MovimentacaoEstoque>().Where(e => !e.Estornada))
            db.MovimentacoesEstoque.Add(entrada.Produto!.EstornarEntrada(entrada, agora, $"{pedido.Codigo}: {motivo.Trim()}", permitirPedido: true));
        pedido.EstornarRecebimento();
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelarAsync(int pedidoId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pedido = await db.PedidosCompra.FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
        pedido.Cancelar(motivo);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Pedido aberto a partir da lista de compras: os itens de um fornecedor, na embalagem sugerida.</summary>
    public async Task<int> GerarDaListaAsync(int fornecedorId, IEnumerable<ItemListaCompras> itens, CancellationToken ct = default)
    {
        var selecionados = itens.Where(i => i.FornecedorId == fornecedorId).ToList();
        Validador.Garantir(selecionados.Count > 0, "Não há itens deste fornecedor na lista.");
        var input = Novo(fornecedorId);
        input.Observacoes = "Gerado pela lista de compras.";
        input.Itens = selecionados.Select(i => new PedidoCompraItemInput
        {
            ProdutoId = i.ProdutoId,
            ProdutoEmbalagemId = i.EmbalagemId ?? 0,
            Quantidade = i.EmbalagemId is null ? i.AComprar : i.QuantidadeEmbalagens ?? i.AComprar,
            CustoUnitario = Math.Round(i.CustoUnitario * (i.FatorEmbalagem ?? 1), 4)
        }).ToList();
        return await SalvarAsync(input, ct);
    }

    private static async Task<List<(Produto, ProdutoEmbalagem?, decimal, decimal)>> ItensAsync(IBuffetDbContext db,
        IReadOnlyList<PedidoCompraItemInput> itens, CancellationToken ct)
    {
        var produtosIds = itens.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.Where(p => produtosIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var embalagensIds = itens.Where(i => i.ProdutoEmbalagemId > 0).Select(i => i.ProdutoEmbalagemId).Distinct().ToList();
        var embalagens = await db.ProdutoEmbalagens.Where(e => embalagensIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        return itens.Select(i => (
            produtos.GetValueOrDefault(i.ProdutoId) ?? throw new RegraNegocioException("Selecione o produto de cada item do pedido."),
            i.ProdutoEmbalagemId > 0 ? embalagens.GetValueOrDefault(i.ProdutoEmbalagemId) : null,
            i.Quantidade,
            i.CustoUnitario)).ToList();
    }
}
