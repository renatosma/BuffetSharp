using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Pedidos de compra e o que vem depois deles: os recebimentos (a mercadoria entra no estoque, inteira ou em partes) e a
/// nota fiscal de entrada, que fatura os recebimentos e gera as contas a pagar.
/// </summary>
public sealed class CompraService(IBuffetDbContextFactory fabrica, ISessaoUsuario sessao, TimeProvider relogio)
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
    /// Registra o que chegou do pedido (pode ser só parte): cada item entra no estoque na hora, convertido pela embalagem e
    /// com o fornecedor. A conta a pagar vem depois, com a nota fiscal de entrada.
    /// </summary>
    public async Task<int> ReceberAsync(RecebimentoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pedido = await db.PedidosCompra
                         .Include(p => p.Itens).ThenInclude(i => i.Produto)
                         .Include(p => p.Itens).ThenInclude(i => i.Embalagem!).ThenInclude(e => e.UnidadeMedida)
                         .FirstOrDefaultAsync(p => p.Id == input.PedidoId, ct)
                     ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
        var quantidades = new Dictionary<PedidoCompraItem, decimal>();
        foreach (var item in input.Itens.Where(i => i.Quantidade != 0))
        {
            var doPedido = pedido.Itens.FirstOrDefault(i => i.Id == item.PedidoCompraItemId)
                           ?? throw new RegraNegocioException("Um dos itens não é deste pedido.");
            Validador.Garantir(item.Quantidade > 0, $"A quantidade recebida de '{doPedido.Produto?.Nome}' não pode ser negativa.");
            quantidades[doPedido] = quantidades.GetValueOrDefault(doPedido) + item.Quantidade;
        }

        var recebimento = pedido.Receber(input.Data, quantidades, input.Observacoes, sessao.Usuario?.Login, Agora);
        db.RecebimentosCompra.Add(recebimento);
        var quando = input.Data == Hoje ? Agora : input.Data.ToDateTime(new TimeOnly(12, 0));
        db.MovimentacoesEstoque.AddRange(recebimento.DarEntrada(quando));
        await db.SaveChangesAsync(ct);
        return recebimento.Id;
    }

    /// <summary>
    /// Desfaz um recebimento (mercadoria lançada errada ou devolvida): as entradas são estornadas e as quantidades voltam ao
    /// saldo do pedido. Recusado se o recebimento já está numa nota ou se um produto já foi usado.
    /// </summary>
    public async Task EstornarRecebimentoAsync(int recebimentoId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var recebimento = await db.RecebimentosCompra
                              .Include(r => r.PedidoCompra!).ThenInclude(p => p.Itens)
                              .Include(r => r.NotaFiscalEntrada)
                              .Include(r => r.Itens).ThenInclude(i => i.Entrada!).ThenInclude(m => m.Produto)
                              .FirstOrDefaultAsync(r => r.Id == recebimentoId, ct)
                          ?? throw new RegraNegocioException("Recebimento não encontrado.");
        db.MovimentacoesEstoque.AddRange(recebimento.Estornar(motivo, Agora));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>O saldo que não veio é dispensado: o pedido fica encerrado com o que foi recebido.</summary>
    public async Task EncerrarSaldoAsync(int pedidoId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var pedido = await db.PedidosCompra.FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new RegraNegocioException("Pedido de compra não encontrado.");
        pedido.EncerrarSaldo(motivo);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lança a nota fiscal de entrada: fatura os recebimentos escolhidos (do mesmo fornecedor) e gera a conta a pagar com
    /// vencimento único ou uma compra parcelada com os vencimentos informados.
    /// </summary>
    public async Task<int> LancarNotaAsync(NotaEntradaInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var ids = input.Recebimentos.Distinct().ToList();
        var recebimentos = await db.RecebimentosCompra
            .Include(r => r.PedidoCompra)
            .Include(r => r.Itens)
            .Where(r => ids.Contains(r.Id))
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
        Validador.Garantir(recebimentos.Count == ids.Count, "Um dos recebimentos não foi encontrado.");

        var numero = input.Numero?.Trim() ?? "";
        var serie = string.IsNullOrWhiteSpace(input.Serie) ? null : input.Serie.Trim();
        var repetida = await db.NotasFiscaisEntrada.AnyAsync(n => n.FornecedorId == input.FornecedorId && n.Numero == numero &&
                                                                  n.Serie == serie && n.Status == StatusNotaEntrada.Lancada, ct);
        Validador.Garantir(!repetida, $"A nota {numero} deste fornecedor já foi lançada.");

        var fornecedor = await db.Fornecedores.AsNoTracking().FirstOrDefaultAsync(f => f.Id == input.FornecedorId, ct);
        var nota = new NotaFiscalEntrada(input.FornecedorId, numero, serie, input.DataEmissao, input.Categoria, recebimentos,
            input.ValorTotal, input.Observacoes, Hoje);
        var (conta, parcelamento) = nota.LancarContas(input.Vencimentos.Select(v => (v.Vencimento, v.Valor)).ToList(), fornecedor?.NomeExibicao);
        if (conta is not null)
            db.ContasPagar.Add(conta);
        if (parcelamento is not null)
            db.ParcelamentosPagar.Add(parcelamento);
        db.NotasFiscaisEntrada.Add(nota);
        await db.SaveChangesAsync(ct);
        return nota.Id;
    }

    /// <summary>
    /// Cancela a nota lançada errada: as contas são canceladas e os recebimentos voltam a aguardar nota (a mercadoria
    /// continua no estoque). Recusado se algo já foi pago.
    /// </summary>
    public async Task CancelarNotaAsync(int notaId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var nota = await db.NotasFiscaisEntrada
                       .Include(n => n.ContaPagar)
                       .Include(n => n.ParcelamentoPagar!).ThenInclude(p => p.Parcelas)
                       .Include(n => n.Recebimentos)
                       .FirstOrDefaultAsync(n => n.Id == notaId, ct)
                   ?? throw new RegraNegocioException("Nota fiscal de entrada não encontrada.");
        nota.Cancelar(motivo, Agora);
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
