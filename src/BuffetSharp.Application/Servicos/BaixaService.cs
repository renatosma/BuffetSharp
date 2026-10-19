using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Recebimentos e pagamentos (baixas): várias contas da mesma pessoa de uma vez, pagamento parcial, juros e desconto,
/// e várias formas no mesmo pagamento (dinheiro no caixa, PIX/transferência/cartão na conta bancária, cartão de
/// crédito da empresa gerando a fatura). O estorno desfaz tudo o que a baixa gerou.
/// </summary>
public sealed class BaixaService(IBuffetDbContextFactory fabrica, TimeProvider relogio, ISessaoUsuario sessao)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    /// <summary>Registra a baixa e retorna o Id.</summary>
    public async Task<int> RegistrarAsync(BaixaInput input, CancellationToken ct = default)
    {
        Validador.Garantir(input.Titulos.Count > 0, "Selecione ao menos uma conta.");
        var ids = input.Titulos.Select(t => t.TituloId).ToList();
        Validador.Garantir(ids.Distinct().Count() == ids.Count, "Uma conta foi informada duas vezes.");

        await using var db = fabrica.Criar();
        var (titulos, descricao) = input.Tipo == TipoBaixa.Recebimento
            ? await ParcelasAsync(db, ids, ct)
            : await ContasPagarAsync(db, ids, ct);

        var baixa = new Baixa(input.Tipo, input.Data, Hoje, input.Observacao, sessao.Usuario?.Login);
        foreach (var item in input.Titulos)
            baixa.AdicionarTitulo(titulos.Single(t => t.Id == item.TituloId), item.Valor, item.Juros, item.Desconto);

        var formaIds = input.Formas.Select(f => f.FormaPagamentoId).Distinct().ToList();
        var contaIds = input.Formas.Where(f => f.ContaFinanceiraId is not null).Select(f => f.ContaFinanceiraId!.Value).Distinct().ToList();
        var cartaoIds = input.Formas.Where(f => f.CartaoCreditoId is not null).Select(f => f.CartaoCreditoId!.Value).Distinct().ToList();
        var formas = await db.FormasPagamento.Where(f => formaIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        var contas = await db.ContasFinanceiras.Where(c => contaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var cartoes = await db.CartoesCredito.Where(c => cartaoIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        foreach (var item in input.Formas)
        {
            var forma = formas.GetValueOrDefault(item.FormaPagamentoId) ?? throw new RegraNegocioException("Escolha a forma de pagamento.");
            Validador.Garantir(forma.Ativo, $"A forma de pagamento '{forma.Nome}' está inativa.");
            baixa.AdicionarForma(forma, item.Valor,
                item.ContaFinanceiraId is { } conta ? contas.GetValueOrDefault(conta) ?? throw new RegraNegocioException("Conta não encontrada.") : null,
                item.CartaoCreditoId is { } cartao ? cartoes.GetValueOrDefault(cartao) ?? throw new RegraNegocioException("Cartão não encontrado.") : null,
                Math.Max(item.Parcelas, 1));
        }

        baixa.Concluir(descricao);
        db.Baixas.Add(baixa);
        await db.SaveChangesAsync(ct);
        return baixa.Id;
    }

    private static async Task<(List<TituloFinanceiro>, string)> ParcelasAsync(IBuffetDbContext db, List<int> ids, CancellationToken ct)
    {
        var parcelas = await db.Parcelas.Include(p => p.Evento!).ThenInclude(e => e.Cliente)
            .Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        Validador.Garantir(parcelas.Count == ids.Count, "Uma das parcelas não existe mais. Atualize a lista.");
        Validador.Garantir(parcelas.Select(p => p.Evento!.ClienteId).Distinct().Count() == 1,
            "Só é possível receber juntas parcelas do mesmo cliente.");

        var cliente = parcelas[0].Evento!.Cliente?.Nome ?? "Cliente";
        var descricao = parcelas.Count == 1
            ? $"{cliente} - {parcelas[0].Evento!.Codigo} {parcelas[0].Descricao}"
            : $"{cliente} - {parcelas.Count} parcelas ({string.Join(", ", parcelas.Select(p => p.Evento!.Codigo).Distinct())})";
        return (parcelas.Cast<TituloFinanceiro>().ToList(), descricao);
    }

    private static async Task<(List<TituloFinanceiro>, string)> ContasPagarAsync(IBuffetDbContext db, List<int> ids, CancellationToken ct)
    {
        var contas = await db.ContasPagar.Include(c => c.Fornecedor).Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        Validador.Garantir(contas.Count == ids.Count, "Uma das contas não existe mais. Atualize a lista.");
        Validador.Garantir(contas.Select(c => c.FornecedorId).Distinct().Count() == 1,
            "Só é possível pagar juntas contas do mesmo fornecedor.");

        var fornecedor = Nome(contas[0].Fornecedor);
        var descricao = contas.Count == 1
            ? fornecedor is null ? contas[0].Descricao : $"{fornecedor} - {contas[0].Descricao}"
            : $"{fornecedor ?? "Diversos"} - {contas.Count} contas";
        return (contas.Cast<TituloFinanceiro>().ToList(), descricao);
    }

    private static string? Nome(Fornecedor? fornecedor) =>
        fornecedor is null ? null : string.IsNullOrWhiteSpace(fornecedor.NomeFantasia) ? fornecedor.RazaoSocial : fornecedor.NomeFantasia;

    /// <summary>Baixa que quitou o título (nula se ele não está pago ou foi pago antes das baixas existirem).</summary>
    public async Task<BaixaResumo?> ObterDoTituloAsync(TipoBaixa tipo, int tituloId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var consulta = db.Baixas.AsNoTracking()
            .Include(b => b.Titulos).ThenInclude(t => t.Parcela)
            .Include(b => b.Titulos).ThenInclude(t => t.ContaPagar)
            .Include(b => b.Formas).ThenInclude(f => f.FormaPagamento)
            .Include(b => b.Formas).ThenInclude(f => f.Conta)
            .Include(b => b.Formas).ThenInclude(f => f.Cartao)
            .Include(b => b.Formas).ThenInclude(f => f.Faturas);
        var baixa = await (tipo == TipoBaixa.Recebimento
                ? consulta.Where(b => b.Tipo == tipo && b.Titulos.Any(t => t.ParcelaId == tituloId))
                : consulta.Where(b => b.Tipo == tipo && b.Titulos.Any(t => t.ContaPagarId == tituloId)))
            .OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);
        if (baixa is null)
            return null;

        return new BaixaResumo(baixa.Id, baixa.Tipo, baixa.Data, baixa.Total,
            baixa.Titulos.Select(t => $"{t.Titulo.Descricao}: {t.ValorEfetivo:C}").ToList(),
            baixa.Formas.Select(f => $"{f.FormaPagamento?.Nome}: {f.Valor:C}" +
                                     (f.Conta is not null ? $" ({f.Conta.Nome})" : f.Cartao is not null ? $" ({f.Cartao}, {f.Parcelas}x)" : "")).ToList(),
            baixa.Formas.Sum(f => f.Faturas.Count));
    }

    /// <summary>Desfaz a baixa: títulos, movimentos nas contas e faturas de cartão geradas.</summary>
    public async Task EstornarAsync(int baixaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var baixa = await db.Baixas
                        .Include(b => b.Titulos).ThenInclude(t => t.Parcela)
                        .Include(b => b.Titulos).ThenInclude(t => t.ContaPagar)
                        .Include(b => b.Titulos).ThenInclude(t => t.SaldoParcela)
                        .Include(b => b.Titulos).ThenInclude(t => t.SaldoContaPagar)
                        .Include(b => b.Formas).ThenInclude(f => f.Movimento!).ThenInclude(m => m.Conta)
                        .Include(b => b.Formas).ThenInclude(f => f.Faturas)
                        .FirstOrDefaultAsync(b => b.Id == baixaId, ct)
                    ?? throw new RegraNegocioException("Pagamento não encontrado. Atualize a lista.");

        foreach (var item in baixa.Estornar())
        {
            switch (item)
            {
                case Parcela parcela: db.Parcelas.Remove(parcela); break;
                case ContaPagar conta: db.ContasPagar.Remove(conta); break;
                case MovimentoConta movimento: db.MovimentosConta.Remove(movimento); break;
                case Baixa b: db.Baixas.Remove(b); break;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Estorna o pagamento de um título: pela baixa que o quitou (desfazendo-a por inteiro) ou, nos títulos pagos antes
    /// das baixas existirem, só reabrindo o título.
    /// </summary>
    public async Task EstornarTituloAsync(TipoBaixa tipo, int tituloId, CancellationToken ct = default)
    {
        if (await ObterDoTituloAsync(tipo, tituloId, ct) is { } baixa)
        {
            await EstornarAsync(baixa.Id, ct);
            return;
        }

        await using var db = fabrica.Criar();
        TituloFinanceiro titulo = tipo == TipoBaixa.Recebimento
            ? await db.Parcelas.FirstOrDefaultAsync(p => p.Id == tituloId, ct) ?? throw new RegraNegocioException("Parcela não encontrada.")
            : await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == tituloId, ct) ?? throw new RegraNegocioException("Conta não encontrada.");
        titulo.EstornarPagamento();
        await db.SaveChangesAsync(ct);
    }
}
