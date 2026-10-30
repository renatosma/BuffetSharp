using System.Net;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using static BuffetSharp.Application.Servicos.FormatoDevolucao;

namespace BuffetSharp.Application.Servicos;

/// <summary>Devolução de itens de um recebimento: quanto de cada item (na unidade de compra) e o motivo.</summary>
public sealed class DevolucaoInput
{
    public int RecebimentoId { get; set; }
    public DateOnly Data { get; set; }
    public MotivoDevolucao Motivo { get; set; } = MotivoDevolucao.Avariado;
    public bool FornecedorRepoe { get; set; } = true;
    public string? NotaDevolucao { get; set; }
    public string? Observacoes { get; set; }
    public List<DevolucaoItemInput> Itens { get; set; } = [];
}

public sealed record DevolucaoItemInput(int RecebimentoItemId, decimal Quantidade);

/// <summary>Item do recebimento com o que ainda pode ser devolvido (unidade de compra).</summary>
public sealed record ItemDevolvivel(int RecebimentoItemId, string Produto, string Unidade, decimal Recebido, decimal Devolvido, decimal CustoUnitario,
    bool ControlaValidade)
{
    public decimal Disponivel => Recebido - Devolvido;
}

/// <summary>O que a devolução de um recebimento vai fazer com o valor: fora da nota, abatido das contas ou crédito.</summary>
public sealed record SituacaoDevolucao(string Recebimento, string Pedido, int FornecedorId, string Fornecedor, StatusRecebimento Status,
    string? NotaFiscal, decimal ContasEmAberto, IReadOnlyList<ItemDevolvivel> Itens)
{
    /// <summary>Texto que explica para onde vai o valor de uma devolução de <paramref name="valor"/>.</summary>
    public string Destino(decimal valor) => Status switch
    {
        StatusRecebimento.AFaturar => "O recebimento ainda não tem nota: a nota já virá sem o devolvido.",
        _ when valor <= ContasEmAberto => $"Abate {Moeda(valor)} das contas em aberto da {NotaFiscal}.",
        _ when ContasEmAberto > 0 => $"Abate {Moeda(ContasEmAberto)} das contas em aberto da {NotaFiscal}; {Moeda(valor - ContasEmAberto)} viram crédito com o fornecedor.",
        _ => $"A {NotaFiscal} já foi paga: {Moeda(valor)} viram crédito com o fornecedor, descontado na próxima nota dele."
    };
}

internal static class FormatoDevolucao
{
    public static string Moeda(decimal valor) => valor.ToString("C2", PrecoCompraService.PtBr);
}

/// <summary>Devolução registrada.</summary>
public sealed record DevolucaoResumo(int Id, DateOnly Data, int FornecedorId, string Fornecedor, string? TelefoneFornecedor, string? EmailFornecedor,
    int RecebimentoCompraId, int PedidoCompraId, string? NotaFiscal, MotivoDevolucao Motivo, bool FornecedorRepoe, string? NotaDevolucao,
    string? Observacoes, string? Usuario, decimal Valor, decimal ValorAbatido, decimal ValorCredito, decimal CreditoUtilizado, int Itens)
{
    public string Codigo => $"DEV-{Id:D5}";
    public string Recebimento => $"REC-{RecebimentoCompraId:D5}";
    public string Pedido => $"PC-{PedidoCompraId:D5}";
    public decimal CreditoDisponivel => ValorCredito - CreditoUtilizado;

    public string Destino => NotaFiscal is null ? "Antes da nota"
        : ValorCredito == 0 ? $"Abatido da {NotaFiscal}"
        : ValorAbatido == 0 ? "Crédito" : $"Abatido + crédito";
}

/// <summary>Item devolvido (unidade de compra), para o comprovante.</summary>
public sealed record DevolucaoItemResumo(string Produto, decimal Quantidade, string Unidade, decimal CustoUnitario)
{
    public decimal Total => Math.Round(Quantidade * CustoUnitario, 2);
}

/// <summary>Devoluções de um fornecedor no período, comparadas com o que foi recebido dele.</summary>
public sealed record DevolucoesFornecedor(int FornecedorId, string Fornecedor, int Devolucoes, decimal Valor, decimal Recebido,
    MotivoDevolucao MotivoPrincipal, decimal CreditoDisponivel)
{
    /// <summary>Quanto do recebido no período voltou, em %.</summary>
    public decimal Percentual => Recebido == 0 ? 0 : Math.Round(Valor / Recebido * 100, 1);
}

/// <summary>
/// Devolução parcial ao fornecedor: tira do estoque só o que voltou (pelo custo da entrada; nos perecíveis, do lote que veio
/// naquele recebimento) e abate o valor da nota. Também o comprovante para o fornecedor, a lista de devoluções, o resumo por
/// fornecedor e o crédito de devoluções usado nas notas seguintes.
/// </summary>
public sealed class DevolucaoService(IBuffetDbContextFactory fabrica, ISessaoUsuario sessao, IEnvioEmail email, BuffetOptions empresa,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    public DevolucaoInput Nova(int recebimentoId) => new() { RecebimentoId = recebimentoId, Data = Hoje };

    /// <summary>Itens do recebimento e o que a devolução vai fazer com o valor.</summary>
    public async Task<SituacaoDevolucao> SituacaoAsync(int recebimentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var recebimento = await db.RecebimentosCompra.AsNoTracking()
                              .Include(r => r.PedidoCompra)
                              .Include(r => r.NotaFiscalEntrada!).ThenInclude(n => n.ContaPagar)
                              .Include(r => r.NotaFiscalEntrada!).ThenInclude(n => n.ParcelamentoPagar!).ThenInclude(p => p.Parcelas)
                              .Include(r => r.Itens).ThenInclude(i => i.PedidoCompraItem!).ThenInclude(i => i.Produto!).ThenInclude(p => p.UnidadeMedida)
                              .Include(r => r.Itens).ThenInclude(i => i.PedidoCompraItem!).ThenInclude(i => i.Embalagem!).ThenInclude(e => e.UnidadeMedida)
                              .FirstOrDefaultAsync(r => r.Id == recebimentoId, ct)
                          ?? throw new RegraNegocioException("Recebimento não encontrado.");
        Validador.Garantir(recebimento.Status != StatusRecebimento.Estornado, "Recebimento estornado não tem o que devolver.");
        var fornecedor = await db.Fornecedores.AsNoTracking().Where(f => f.Id == recebimento.FornecedorId)
            .Select(f => f.NomeFantasia ?? f.RazaoSocial).FirstAsync(ct);
        var nota = recebimento.NotaFiscalEntrada;
        var itens = recebimento.Itens.OrderBy(i => i.Id).Select(i =>
        {
            var doPedido = i.PedidoCompraItem!;
            return new ItemDevolvivel(i.Id, doPedido.Produto!.Nome, doPedido.Embalagem?.UnidadeMedida?.Sigla ?? doPedido.Produto.UnidadeMedida?.Sigla ?? "",
                i.Quantidade, i.QuantidadeDevolvida, i.CustoUnitario, doPedido.Produto.ControlaValidade);
        }).ToList();
        return new SituacaoDevolucao(recebimento.Codigo, recebimento.PedidoCompra?.Codigo ?? $"PC-{recebimento.PedidoCompraId:D5}",
            recebimento.FornecedorId, fornecedor, recebimento.Status, nota?.Descricao,
            nota?.Contas().Where(c => c.Status == StatusTitulo.Aberto).Sum(c => c.Valor) ?? 0, itens);
    }

    /// <summary>Registra a devolução: saída do estoque, saldo do pedido (se o fornecedor repõe) e abatimento da nota.</summary>
    public async Task<int> DevolverAsync(DevolucaoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var recebimento = await db.RecebimentosCompra
                              .Include(r => r.PedidoCompra!).ThenInclude(p => p.Itens)
                              .Include(r => r.NotaFiscalEntrada!).ThenInclude(n => n.ContaPagar)
                              .Include(r => r.NotaFiscalEntrada!).ThenInclude(n => n.ParcelamentoPagar!).ThenInclude(p => p.Parcelas)
                              .Include(r => r.Itens).ThenInclude(i => i.PedidoCompraItem!).ThenInclude(i => i.Produto)
                              .Include(r => r.Itens).ThenInclude(i => i.Entrada!).ThenInclude(m => m.Produto)
                              .Include(r => r.Itens).ThenInclude(i => i.Entrada!).ThenInclude(m => m.Lotes).ThenInclude(l => l.Lote)
                              .FirstOrDefaultAsync(r => r.Id == input.RecebimentoId, ct)
                          ?? throw new RegraNegocioException("Recebimento não encontrado.");
        var quantidades = new Dictionary<RecebimentoItem, decimal>();
        foreach (var item in input.Itens.Where(i => i.Quantidade != 0))
        {
            var doRecebimento = recebimento.Itens.FirstOrDefault(i => i.Id == item.RecebimentoItemId)
                                ?? throw new RegraNegocioException("Um dos itens não é deste recebimento.");
            quantidades[doRecebimento] = quantidades.GetValueOrDefault(doRecebimento) + item.Quantidade;
        }

        var devolucao = recebimento.Devolver(quantidades, input.Data, input.Motivo, input.FornecedorRepoe, input.NotaDevolucao, input.Observacoes,
            sessao.Usuario?.Login, Agora);
        db.DevolucoesCompra.Add(devolucao);
        db.MovimentacoesEstoque.AddRange(devolucao.Itens.Select(i => i.Saida!));
        await db.SaveChangesAsync(ct);
        return devolucao.Id;
    }

    public async Task<IReadOnlyList<DevolucaoResumo>> ListarAsync(DateOnly de, DateOnly ate, int? fornecedorId = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var consulta = db.DevolucoesCompra.AsNoTracking().Where(d => d.Data >= de && d.Data <= ate);
        if (fornecedorId is { } id)
            consulta = consulta.Where(d => d.FornecedorId == id);
        return await Resumos(db, consulta).ToListAsync(ct);
    }

    public async Task<DevolucaoResumo> ObterAsync(int devolucaoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await Resumos(db, db.DevolucoesCompra.AsNoTracking().Where(d => d.Id == devolucaoId)).FirstOrDefaultAsync(ct)
               ?? throw new RegraNegocioException("Devolução não encontrada.");
    }

    private static IQueryable<DevolucaoResumo> Resumos(IBuffetDbContext db, IQueryable<DevolucaoCompra> consulta) =>
        from d in consulta
        join f in db.Fornecedores on d.FornecedorId equals f.Id
        orderby d.Data descending, d.Id descending
        select new DevolucaoResumo(d.Id, d.Data, d.FornecedorId, f.NomeFantasia ?? f.RazaoSocial, f.Telefone, f.Email, d.RecebimentoCompraId,
            d.PedidoCompraId, d.NotaFiscalEntrada == null ? null : "NF " + d.NotaFiscalEntrada.Numero, d.Motivo, d.FornecedorRepoe, d.NotaDevolucao,
            d.Observacoes, d.Usuario, d.Valor, d.ValorAbatido, d.ValorCredito, d.CreditoUtilizado, d.Itens.Count);

    public async Task<IReadOnlyList<DevolucaoItemResumo>> ItensAsync(int devolucaoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.DevolucoesCompra.AsNoTracking().Where(d => d.Id == devolucaoId)
            .SelectMany(d => d.Itens)
            .OrderBy(i => i.Id)
            .Select(i => new DevolucaoItemResumo(i.RecebimentoItem!.PedidoCompraItem!.Produto!.Nome, i.Quantidade,
                i.RecebimentoItem.PedidoCompraItem.Embalagem != null
                    ? i.RecebimentoItem.PedidoCompraItem.Embalagem.UnidadeMedida!.Sigla
                    : i.RecebimentoItem.PedidoCompraItem.Produto.UnidadeMedida!.Sigla,
                i.CustoUnitario))
            .ToListAsync(ct);
    }

    /// <summary>Devoluções por fornecedor no período, com a parte do recebido que voltou e o motivo mais comum.</summary>
    public async Task<IReadOnlyList<DevolucoesFornecedor>> PorFornecedorAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var devolucoes = await ListarAsync(de, ate, null, ct);
        await using var db = fabrica.Criar();
        var recebido = (await db.RecebimentosCompra.AsNoTracking()
                .Where(r => r.Status != StatusRecebimento.Estornado && r.Data >= de && r.Data <= ate)
                .SelectMany(r => r.Itens.Select(i => new { r.FornecedorId, i.Quantidade, i.CustoUnitario }))
                .ToListAsync(ct))
            .GroupBy(r => r.FornecedorId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Round(i.Quantidade * i.CustoUnitario, 2)));
        var creditos = await CreditosAsync(db, ct);
        return devolucoes.GroupBy(d => d.FornecedorId).Select(g => new DevolucoesFornecedor(g.Key, g.First().Fornecedor, g.Count(),
                g.Sum(d => d.Valor), recebido.GetValueOrDefault(g.Key),
                g.GroupBy(d => d.Motivo).OrderByDescending(m => m.Sum(d => d.Valor)).First().Key, creditos.GetValueOrDefault(g.Key)))
            .OrderByDescending(f => f.Valor)
            .ToList();
    }

    private static async Task<Dictionary<int, decimal>> CreditosAsync(IBuffetDbContext db, CancellationToken ct) =>
        (await db.DevolucoesCompra.AsNoTracking().Where(d => d.ValorCredito > d.CreditoUtilizado)
            .Select(d => new { d.FornecedorId, Disponivel = d.ValorCredito - d.CreditoUtilizado }).ToListAsync(ct))
        .GroupBy(d => d.FornecedorId).ToDictionary(g => g.Key, g => g.Sum(d => d.Disponivel));

    /// <summary>Crédito de devoluções que o fornecedor ainda deve (descontado na próxima nota dele).</summary>
    public async Task<decimal> CreditoDisponivelAsync(int fornecedorId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return (await CreditosAsync(db, ct)).GetValueOrDefault(fornecedorId);
    }

    /// <summary>Usa o crédito do fornecedor na nota (antes de lançar as contas).</summary>
    internal static async Task UsarCreditoAsync(IBuffetDbContext db, NotaFiscalEntrada nota, decimal valor, CancellationToken ct)
    {
        if (valor <= 0)
            return;
        var devolucoes = await db.DevolucoesCompra.Where(d => d.FornecedorId == nota.FornecedorId && d.ValorCredito > d.CreditoUtilizado).ToListAsync(ct);
        nota.UsarCredito(devolucoes, valor);
    }

    /// <summary>Texto da devolução para o fornecedor (WhatsApp e e-mail).</summary>
    public string Mensagem(DevolucaoResumo devolucao, IReadOnlyList<DevolucaoItemResumo> itens) =>
        $"Olá! Aqui é do {empresa.NomeEmpresa}. Registramos a devolução {devolucao.Codigo} do recebimento {devolucao.Recebimento} " +
        $"(pedido {devolucao.Pedido}), motivo: {MotivoTexto(devolucao.Motivo)}.\n\n" +
        string.Join("\n", itens.Select(i => $"• {i.Produto}: {i.Quantidade.ToString("0.###", PrecoCompraService.PtBr)} {i.Unidade} ({Moeda(i.Total)})")) +
        $"\n\nTotal devolvido: {Moeda(devolucao.Valor)}." +
        (devolucao.NotaDevolucao is null ? "" : $"\nNota fiscal de devolução: {devolucao.NotaDevolucao}.") +
        (devolucao.FornecedorRepoe ? "\nAguardamos a reposição desses itens." : "") +
        (devolucao.ValorAbatido > 0 ? $"\nAbatemos {Moeda(devolucao.ValorAbatido)} da nota {devolucao.NotaFiscal}." : "") +
        (devolucao.ValorCredito > 0 ? $"\nFicam {Moeda(devolucao.ValorCredito)} de crédito para a próxima compra." : "") +
        (devolucao.Observacoes is null ? "" : $"\n\n{devolucao.Observacoes}") +
        "\n\nObrigado!";

    public static string MotivoTexto(MotivoDevolucao motivo) => motivo switch
    {
        MotivoDevolucao.Avariado => "produto avariado",
        MotivoDevolucao.Vencido => "produto vencido",
        MotivoDevolucao.ProdutoErrado => "produto errado",
        MotivoDevolucao.ForaDoPedido => "fora do pedido",
        MotivoDevolucao.Qualidade => "qualidade",
        _ => "outro motivo"
    };

    public async Task EnviarEmailAsync(EmailInput input, CancellationToken ct = default)
    {
        Validador.Garantir(System.Net.Mail.MailAddress.TryCreate(input.Para?.Trim(), out _), "Informe um e-mail válido para o fornecedor.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(input.Assunto), "Informe o assunto do e-mail.");
        var corpo = new StringBuilder("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2b2b2b\">");
        foreach (var paragrafo in (input.Mensagem ?? "").Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            corpo.Append("<p>").Append(WebUtility.HtmlEncode(paragrafo).Replace("\n", "<br>")).Append("</p>");
        corpo.Append($"<p style=\"color:#6e6a66\">{WebUtility.HtmlEncode(empresa.NomeEmpresa)}</p></div>");
        await email.EnviarAsync(new MensagemEmail(input.Para!.Trim(), input.Assunto.Trim(), corpo.ToString(), input.Anexo is null ? [] : [input.Anexo]), ct);
    }
}
