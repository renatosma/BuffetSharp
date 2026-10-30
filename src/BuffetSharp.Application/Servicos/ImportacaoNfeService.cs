using System.Globalization;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Importação do XML da NF-e. Com pedido, a nota é conferida com os recebimentos e lançada pela tela da nota de entrada.
/// Sem pedido, a importação dá entrada no estoque e lança a nota (com as contas) de uma vez, guardando o vínculo de cada
/// código do fornecedor com o produto do sistema para as próximas notas.
/// </summary>
public sealed class ImportacaoNfeService(IBuffetDbContextFactory fabrica, ISessaoUsuario sessao, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    /// <summary>Confronta a nota com os cadastros: fornecedor pelo CNPJ/CPF, produtos de cada item e recebimentos a faturar.</summary>
    public async Task<AnaliseNfe> AnalisarAsync(NotaFiscalEletronica nota, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        await GarantirNaoLancadaAsync(db, nota, ct);
        var fornecedor = await FornecedorAsync(db, nota, ct);
        var vinculos = fornecedor is null
            ? []
            : await db.CodigosProdutoFornecedor.AsNoTracking().Where(v => v.FornecedorId == fornecedor.Id).ToListAsync(ct);
        var produtos = await db.Produtos.AsNoTracking()
            .Include(p => p.UnidadeMedida)
            .Include(p => p.Embalagens).ThenInclude(e => e.UnidadeMedida)
            .Where(p => p.Ativo)
            .ToListAsync(ct);

        var recebimentos = new List<ProdutoRecebido>();
        var aFaturar = new List<int>();
        if (fornecedor is not null)
        {
            var pendentes = await db.RecebimentosCompra.AsNoTracking()
                .Include(r => r.Itens).ThenInclude(i => i.PedidoCompraItem!).ThenInclude(i => i.Embalagem)
                .Include(r => r.Itens).ThenInclude(i => i.PedidoCompraItem!).ThenInclude(i => i.Produto!).ThenInclude(p => p.UnidadeMedida)
                .Where(r => r.FornecedorId == fornecedor.Id && r.Status == StatusRecebimento.AFaturar)
                .OrderBy(r => r.Id)
                .ToListAsync(ct);
            aFaturar.AddRange(pendentes.Select(r => r.Id));
            recebimentos.AddRange(pendentes.SelectMany(r => r.Itens.Select(i => new ProdutoRecebido(r.Id, i.PedidoCompraItem!.ProdutoId,
                i.PedidoCompraItem.Produto?.Nome ?? "", i.PedidoCompraItem.Produto?.UnidadeMedida?.Sigla ?? "",
                Math.Round(i.Quantidade * (i.PedidoCompraItem.Embalagem?.Fator ?? 1), 3)))));
        }

        var forma = nota.MeioPagamento is { } meio
            ? await db.FormasPagamento.AsNoTracking().Where(f => f.Ativo && f.Tipo == meio).OrderBy(f => f.Id).FirstOrDefaultAsync(ct)
            : null;
        return new AnaliseNfe
        {
            Nota = nota,
            FormaPagamentoId = forma?.Id,
            FornecedorId = fornecedor?.Id,
            Fornecedor = fornecedor?.NomeExibicao ?? nota.Emitente.Nome,
            RecebimentosAFaturar = aFaturar,
            Recebido = recebimentos,
            Itens = nota.Itens.Select(i => Reconhecer(i, fornecedor?.Id, vinculos, produtos)).ToList()
        };
    }

    /// <summary>
    /// Compra sem pedido: cadastra o fornecedor se preciso, cria o pedido já recebido (os itens entram no estoque) e lança a
    /// nota com as contas a pagar, na mesma gravação. Os vínculos dos códigos do fornecedor ficam gravados.
    /// </summary>
    /// <returns>A nota fiscal de entrada lançada.</returns>
    public async Task<int> LancarSemPedidoAsync(NotaSemPedidoInput input, CancellationToken ct = default)
    {
        var nota = input.Nota;
        await using var db = fabrica.Criar();
        await GarantirNaoLancadaAsync(db, nota, ct);

        var escolhidos = input.Itens.Where(i => i.ProdutoId is > 0).ToList();
        Validador.Garantir(escolhidos.Count > 0,
            "Escolha o produto de ao menos um item. Uma nota sem nada para o estoque é lançada direto em contas a pagar.");
        Validador.Garantir(input.Itens.Count == nota.Itens.Count && nota.Itens.All(i => input.Itens.Any(x => x.Numero == i.Numero)),
            "Escolha o produto (ou \"não entra no estoque\") de cada item da nota.");
        var produtosIds = escolhidos.Select(i => i.ProdutoId!.Value).Distinct().ToList();
        var produtos = await db.Produtos.Include(p => p.Embalagens).ThenInclude(e => e.UnidadeMedida)
            .Where(p => produtosIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        Validador.Garantir(produtos.Count == produtosIds.Count, "Um dos produtos escolhidos não foi encontrado.");

        var itens = escolhidos.Select(escolha =>
        {
            var item = nota.Itens.First(i => i.Numero == escolha.Numero);
            var produto = produtos[escolha.ProdutoId!.Value];
            var embalagem = escolha.ProdutoEmbalagemId > 0
                ? produto.Embalagens.FirstOrDefault(e => e.Id == escolha.ProdutoEmbalagemId)
                  ?? throw new RegraNegocioException($"A unidade escolhida não é de '{produto.Nome}'.")
                : null;
            return (Item: item, Produto: produto, Embalagem: embalagem, Escolha: escolha);
        }).ToList();
        Validador.Garantir(itens.All(i => i.Item.Quantidade > 0), "A nota tem item sem quantidade.");

        var fornecedor = input.FornecedorId is { } id
            ? await db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw new RegraNegocioException("Fornecedor não encontrado.")
            : await FornecedorAsync(db, nota, ct);
        if (fornecedor is null)
        {
            // O pedido precisa do fornecedor gravado: o cadastro vai antes.
            fornecedor = NovoFornecedor(nota.Emitente);
            db.Fornecedores.Add(fornecedor);
            await db.SaveChangesAsync(ct);
        }
        var serie = string.IsNullOrWhiteSpace(nota.Serie) ? null : nota.Serie.Trim();
        var repetida = await db.NotasFiscaisEntrada.AnyAsync(n => n.FornecedorId == fornecedor.Id && n.Numero == nota.Numero &&
                                                                  n.Serie == serie && n.Status == StatusNotaEntrada.Lancada, ct);
        Validador.Garantir(!repetida, $"A nota {nota.Numero} deste fornecedor já foi lançada.");

        var pedido = new PedidoCompra(fornecedor.Id, nota.DataEmissao);
        pedido.Alterar(fornecedor.Id, nota.DataEmissao, null, input.Categoria, $"Compra sem pedido: {nota.Descricao} importada do XML.");
        pedido.DefinirItens(itens.Select(i => (i.Produto, i.Embalagem, i.Item.Quantidade, i.Item.CustoUnitario)));
        pedido.DefinirPagamento(null, input.FormaPagamentoId);
        // Lote e validade: os informados na importação ou, sem eles, os do grupo "rastro" da NF-e (convertidos para o estoque).
        var lotes = new Dictionary<PedidoCompraItem, IReadOnlyList<LoteInformado>>();
        foreach (var (doPedido, i) in pedido.Itens.Zip(itens))
        {
            var fator = i.Embalagem?.Fator ?? 1;
            IReadOnlyList<LoteInformado>? doItem = i.Escolha.Validade is { } validade
                ? [new LoteInformado(i.Escolha.Lote, validade)]
                : i.Item.Lotes.Count > 0 ? i.Item.Lotes.Select(r => new LoteInformado(r.Numero, r.Validade, r.Quantidade * fator)).ToList() : null;
            Validador.Garantir(!i.Produto.ControlaValidade || doItem is not null,
                $"'{i.Produto.Nome}' controla validade: informe a validade do item {i.Item.Numero} da nota.");
            if (doItem is not null)
                lotes[doPedido] = doItem;
        }
        var recebimento = pedido.Receber(input.DataRecebimento, pedido.Itens.ToDictionary(i => i, i => i.Quantidade), input.Observacoes,
            sessao.Usuario?.Login, Agora, lotes);
        var quando = input.DataRecebimento == Hoje ? Agora : input.DataRecebimento.ToDateTime(new TimeOnly(12, 0));
        db.PedidosCompra.Add(pedido);
        db.RecebimentosCompra.Add(recebimento);
        db.MovimentacoesEstoque.AddRange(recebimento.DarEntrada(quando));

        var notaEntrada = new NotaFiscalEntrada(fornecedor.Id, nota.Numero, nota.Serie, nota.DataEmissao, input.Categoria, [recebimento],
            nota.ValorTotal, input.Observacoes, Hoje);
        notaEntrada.DefinirChaveAcesso(nota.ChaveAcesso);
        var foraDoEstoque = nota.Itens.Count - itens.Count;
        var (conta, parcelamento) = notaEntrada.LancarContas(input.Vencimentos.Select(v => v.ParaPlanejado()).ToList(),
            fornecedor.NomeExibicao,
            $"NF-e importada (compra sem pedido): {itens.Count} item(ns) no estoque" + (foraDoEstoque > 0 ? $", {foraDoEstoque} fora do estoque." : "."),
            input.FormaPagamentoId);
        if (conta is not null)
            db.ContasPagar.Add(conta);
        if (parcelamento is not null)
            db.ParcelamentosPagar.Add(parcelamento);
        db.NotasFiscaisEntrada.Add(notaEntrada);

        // Vínculos: na próxima nota deste fornecedor os itens são reconhecidos sozinhos.
        var codigos = itens.Select(i => i.Item.Codigo).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var existentes = await db.CodigosProdutoFornecedor
            .Where(v => v.FornecedorId == fornecedor.Id && codigos.Contains(v.Codigo))
            .ToDictionaryAsync(v => v.Codigo, ct);
        foreach (var (item, produto, embalagem, _) in itens.Where(i => !string.IsNullOrWhiteSpace(i.Item.Codigo)).DistinctBy(i => i.Item.Codigo))
        {
            if (existentes.TryGetValue(item.Codigo, out var vinculo))
                vinculo.Vincular(item.Descricao, produto.Id, embalagem?.Id);
            else
                db.CodigosProdutoFornecedor.Add(new CodigoProdutoFornecedor(fornecedor.Id, item.Codigo, item.Descricao, produto.Id, embalagem?.Id));
        }

        await db.SaveChangesAsync(ct);
        return notaEntrada.Id;
    }

    private static async Task GarantirNaoLancadaAsync(IBuffetDbContext db, NotaFiscalEletronica nota, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(nota.ChaveAcesso))
            return;
        var lancada = await db.NotasFiscaisEntrada.AsNoTracking()
            .FirstOrDefaultAsync(n => n.ChaveAcesso == nota.ChaveAcesso && n.Status == StatusNotaEntrada.Lancada, ct);
        Validador.Garantir(lancada is null, $"Esta NF-e ({nota.Descricao}) já foi lançada.");
    }

    private static async Task<Fornecedor?> FornecedorAsync(IBuffetDbContext db, NotaFiscalEletronica nota, CancellationToken ct) =>
        string.IsNullOrEmpty(nota.Emitente.Documento)
            ? null
            : await db.Fornecedores.FirstOrDefaultAsync(f => f.Documento == nota.Emitente.Documento, ct);

    private static Fornecedor NovoFornecedor(EmitenteNfe emitente)
    {
        static string? Cortar(string? texto, int tamanho) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(texto.Trim().Length, tamanho)];
        Validador.Garantir(!string.IsNullOrWhiteSpace(emitente.RazaoSocial), "O XML não traz o nome do fornecedor.");
        var e = emitente.Endereco;
        return new Fornecedor
        {
            RazaoSocial = Cortar(emitente.RazaoSocial, 150)!,
            NomeFantasia = Cortar(emitente.NomeFantasia, 150),
            Documento = Cortar(emitente.Documento, 14),
            Telefone = Cortar(emitente.Telefone, 20),
            Observacoes = "Cadastrado pela importação do XML da NF-e.",
            Endereco = new Endereco
            {
                Cep = Cortar(e.Cep, 9), Logradouro = Cortar(e.Logradouro, 150), Numero = Cortar(e.Numero, 20), Complemento = Cortar(e.Complemento, 80),
                Bairro = Cortar(e.Bairro, 80), Cidade = Cortar(e.Cidade, 80), Uf = Cortar(e.Uf, 2)
            }
        };
    }

    /// <summary>
    /// Produto do item: o vínculo gravado; senão, a embalagem com o código do fornecedor; senão, o produto cujo nome está na
    /// descrição do item (o mais longo; é só sugestão). A unidade é a da embalagem com a sigla da nota, se houver.
    /// </summary>
    internal static ItemNfeAnalisado Reconhecer(ItemNfe item, int? fornecedorId, IReadOnlyList<CodigoProdutoFornecedor> vinculos,
        IReadOnlyList<Produto> produtos)
    {
        ItemNfeAnalisado Resultado(Produto produto, ProdutoEmbalagem? embalagem, OrigemVinculo origem) => new()
        {
            Item = item, ProdutoId = produto.Id, Produto = produto.Nome, ProdutoEmbalagemId = embalagem?.Id ?? 0, Fator = embalagem?.Fator ?? 1,
            UnidadeEstoque = produto.UnidadeMedida?.Sigla ?? "", Origem = origem
        };

        if (vinculos.FirstOrDefault(v => v.Codigo == item.Codigo) is { } vinculo && produtos.FirstOrDefault(p => p.Id == vinculo.ProdutoId) is { } vinculado)
            return Resultado(vinculado, vinculado.Embalagens.FirstOrDefault(e => e.Id == vinculo.ProdutoEmbalagemId), OrigemVinculo.Vinculo);

        foreach (var produto in produtos)
        {
            var porCodigo = produto.Embalagens.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.CodigoFornecedor) &&
                                                                   e.CodigoFornecedor.Trim() == item.Codigo &&
                                                                   (e.FornecedorId is null || e.FornecedorId == fornecedorId));
            if (porCodigo is not null)
                return Resultado(produto, porCodigo, OrigemVinculo.CodigoFornecedor);
        }

        var descricao = $" {Normalizar(item.Descricao)} ";
        var pelaDescricao = produtos
            .Select(p => (Produto: p, Nome: Normalizar(p.Nome)))
            .Where(p => p.Nome.Length >= 3 && descricao.Contains($" {p.Nome} "))
            .OrderByDescending(p => p.Nome.Length)
            .Select(p => p.Produto)
            .FirstOrDefault();
        if (pelaDescricao is null)
            return new ItemNfeAnalisado { Item = item };

        var unidade = string.Equals(pelaDescricao.UnidadeMedida?.Sigla, item.Unidade, StringComparison.OrdinalIgnoreCase)
            ? null
            : pelaDescricao.Embalagens.Where(e => string.Equals(e.UnidadeMedida?.Sigla, item.Unidade, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FornecedorId == fornecedorId && fornecedorId is not null ? 0 : e.FornecedorId is null ? 1 : 2)
                .FirstOrDefault(e => e.FornecedorId is null || e.FornecedorId == fornecedorId);
        return Resultado(pelaDescricao, unidade, OrigemVinculo.Nome);
    }

    /// <summary>Minúsculas, sem acento e só letras e números separados por um espaço.</summary>
    internal static string Normalizar(string texto)
    {
        var sem = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sem.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sem.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
