using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class FinanceiroService(IBuffetDbContextFactory fabrica, TimeProvider relogio)
{
    // Recebimentos e pagamentos ficam no BaixaService; aqui, o cadastro das contas a pagar e os parcelamentos.

    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    public async Task<ContaPagarInput> ObterContaAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var c = await db.ContasPagar.AsNoTracking().Include(x => x.FormaPagamento).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Conta não encontrada.");
        var entrada = await db.MovimentacoesEstoque.AsNoTracking().Include(m => m.Produto!).ThenInclude(p => p.UnidadeMedida)
            .FirstOrDefaultAsync(m => m.ContaPagarId == id || (c.ParcelamentoId != null && m.ParcelamentoPagarId == c.ParcelamentoId), ct);
        var nota = await db.NotasFiscaisEntrada.AsNoTracking().Include(n => n.Recebimentos).ThenInclude(r => r.PedidoCompra)
            .FirstOrDefaultAsync(n => n.ContaPagarId == id || (c.ParcelamentoId != null && n.ParcelamentoPagarId == c.ParcelamentoId), ct);
        return new ContaPagarInput
        {
            Id = c.Id,
            Versao = c.Versao,
            Descricao = c.Descricao,
            Categoria = c.Categoria,
            Valor = c.Valor,
            Vencimento = c.Vencimento,
            FornecedorId = c.FornecedorId,
            EventoId = c.EventoId,
            Documento = c.Documento,
            Observacao = c.Observacao,
            Status = c.Status,
            DataPagamento = c.DataPagamento,
            ValorPago = c.ValorPago,
            FormaPagamento = c.FormaPagamento?.Nome,
            TipoFormaPagamento = c.FormaPagamento?.Tipo,
            Parcelamento = c.ParcelamentoId is { } p ? $"PAR-{p:D5}" : c.AgrupadaEmId is { } a ? $"juntada em PAR-{a:D5}" : null,
            OrigemEstoque = nota is not null
                ? $"Nota fiscal de entrada {nota.Descricao} ({nota.Recebimentos.Count} recebimento(s) do(s) pedido(s) " +
                  $"{string.Join(", ", nota.Recebimentos.Select(r => r.PedidoCompra?.Codigo).Distinct())})"
                : entrada is null
                ? null
                : $"Entrada de {entrada.Quantidade:N3} {entrada.Produto?.UnidadeMedida?.Sigla} de {entrada.Produto?.Nome} em {entrada.Data:dd/MM/yyyy HH:mm}"
        };
    }

    public async Task SalvarContaAsync(ContaPagarInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        if (input.Id == 0)
        {
            Validador.Garantir(input.Repeticoes is >= 1 and <= 24, "As repetições devem estar entre 1 e 24 meses.");
            for (var i = 0; i < input.Repeticoes; i++)
            {
                var descricao = input.Repeticoes > 1 ? $"{input.Descricao} ({i + 1}/{input.Repeticoes})" : input.Descricao;
                var conta = new ContaPagar(descricao, input.Categoria, input.Valor, input.Vencimento.AddMonths(i),
                    input.FornecedorId, input.EventoId) { Observacao = input.Observacao };
                conta.DefinirDocumento(input.Documento);
                db.ContasPagar.Add(conta);
            }
        }
        else
        {
            var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == input.Id, ct)
                        ?? throw new RegraNegocioException("Conta não encontrada.");
            Concorrencia.Conferir(conta, input.Versao);
            conta.Alterar(input.Descricao, input.Categoria, input.Valor, input.Vencimento, input.FornecedorId,
                input.EventoId, input.Observacao);
            conta.DefinirDocumento(input.Documento);
        }
        await db.SaveChangesAsync(ct);
    }

    public Task CancelarContaAsync(int contaId, CancellationToken ct = default) =>
        AlterarAsync<ContaPagar>(contaId, c => c.Cancelar(), ct);

    public async Task ExcluirContaAsync(int contaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == contaId, ct)
                    ?? throw new RegraNegocioException("Conta não encontrada.");
        Validador.Garantir(conta.Status != StatusTitulo.Pago, "Contas pagas não podem ser excluídas. Estorne antes.");
        Validador.Garantir(!conta.EhFaturaCartao,
            "Esta conta é parcela da fatura de um cartão. Para desfazê-la, estorne o pagamento feito com o cartão.");
        Validador.Garantir(!conta.EhParcela && !conta.FoiAgrupada,
            $"Esta conta faz parte do parcelamento PAR-{conta.ParcelamentoId ?? conta.AgrupadaEmId:D5}. Use \"Desfazer parcelamento\" ou cancele a parcela.");
        foreach (var entrada in await db.MovimentacoesEstoque.Where(m => m.ContaPagarId == contaId).ToListAsync(ct))
            entrada.DesvincularConta();
        foreach (var nota in await db.NotasFiscaisEntrada.Where(n => n.ContaPagarId == contaId).ToListAsync(ct))
            nota.DesvincularConta();
        db.ContasPagar.Remove(conta);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------- Parcelamentos -------------------------

    /// <summary>
    /// Plano sugerido pela condição de pagamento para o valor. A quitação "antes do evento" só limita quando a conta está
    /// vinculada a um evento.
    /// </summary>
    public async Task<PlanoPagamentoInput> SugerirPlanoAsync(int condicaoPagamentoId, decimal valor, int? eventoId,
        CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var condicao = await db.CondicoesPagamento.AsNoTracking().Include(c => c.Parcelas).FirstOrDefaultAsync(c => c.Id == condicaoPagamentoId, ct)
                       ?? throw new RegraNegocioException("Condição de pagamento não encontrada.");
        var dataEvento = eventoId is { } id
            ? DateOnly.FromDateTime(await db.Eventos.Where(e => e.Id == id).Select(e => e.Inicio).FirstAsync(ct))
            : DateOnly.MaxValue;
        if (condicao.ParcelasDefinidas)
            return PlanoPagamentoInput.DaCondicao(condicao, condicao.Vencimentos(valor, Hoje, eventoId is null ? null : dataEvento),
                condicao.FormaPagamentoId);
        var plano = condicao.Simular(valor, Hoje, dataEvento);
        return new PlanoPagamentoInput
        {
            Quantidade = plano.Quantidade,
            PrimeiroVencimento = plano.PrimeiroVencimento,
            ValorEntrada = plano.ValorEntrada,
            VencimentoEntrada = plano.VencimentoEntrada ?? plano.PrimeiroVencimento,
            FormaPagamentoId = condicao.FormaPagamentoId,
            IntervaloDias = plano.IntervaloDias ?? 0,
            CondicaoPagamentoId = condicao.Id
        };
    }

    /// <summary>Gera uma compra parcelada ou junta as contas informadas num parcelamento novo. Retorna o Id.</summary>
    public async Task<int> ParcelarAsync(ParcelamentoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        ParcelamentoPagar parcelamento;
        if (input.ContasAgrupadasIds.Count == 0)
        {
            parcelamento = new ParcelamentoPagar(input.Descricao, input.Categoria, input.ValorTotal, input.FornecedorId,
                input.EventoId, input.Documento, input.Observacao);
        }
        else
        {
            var ids = input.ContasAgrupadasIds.Distinct().ToList();
            var contas = await db.ContasPagar.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
            Validador.Garantir(contas.Count == ids.Count, "Uma das contas não existe mais. Atualize a lista.");
            parcelamento = ParcelamentoPagar.Agrupar(contas, input.Descricao, input.Documento, input.Observacao);
        }

        var plano = input.Plano;
        if (plano.Vencimentos is { Count: > 0 } vencimentos)
            parcelamento.GerarParcelas(vencimentos.Select(v => v.ParaPlanejado()).ToList(), plano.FormaPagamentoId);
        else
            parcelamento.GerarParcelas(plano.Quantidade, plano.PrimeiroVencimento, plano.ValorEntrada, plano.VencimentoEntrada,
                plano.FormaPagamentoId, plano.IntervaloDias is > 0 ? plano.IntervaloDias : null);
        db.ParcelamentosPagar.Add(parcelamento);
        await db.SaveChangesAsync(ct);
        return parcelamento.Id;
    }

    public async Task<ParcelamentoInfo> ObterParcelamentoAsync(int parcelamentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var p = await CarregarParcelamentoAsync(db, parcelamentoId, ct);
        return new ParcelamentoInfo(p.Id, p.Codigo, p.Descricao, p.ValorTotal, p.Parcelas.Count, p.Agrupadas.Count, p.PodeDesfazer);
    }

    /// <summary>As contas juntadas voltam a ficar em aberto e as parcelas são excluídas.</summary>
    public async Task DesfazerParcelamentoAsync(int parcelamentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var parcelamento = await CarregarParcelamentoAsync(db, parcelamentoId, ct);
        foreach (var entrada in await db.MovimentacoesEstoque.Where(m => m.ParcelamentoPagarId == parcelamentoId).ToListAsync(ct))
            entrada.DesvincularConta();
        foreach (var nota in await db.NotasFiscaisEntrada.Where(n => n.ParcelamentoPagarId == parcelamentoId).ToListAsync(ct))
            nota.DesvincularConta();
        foreach (var item in parcelamento.Desfazer())
        {
            switch (item)
            {
                case ContaPagar conta: db.ContasPagar.Remove(conta); break;
                case ParcelamentoPagar p: db.ParcelamentosPagar.Remove(p); break;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<ParcelamentoPagar> CarregarParcelamentoAsync(IBuffetDbContext db, int id, CancellationToken ct) =>
        await db.ParcelamentosPagar.Include(p => p.Parcelas).Include(p => p.Agrupadas).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new RegraNegocioException("Parcelamento não encontrado.");

    private async Task AlterarAsync<T>(int id, Action<T> acao, CancellationToken ct) where T : TituloFinanceiro
    {
        await using var db = fabrica.Criar();
        var titulo = await db.Set<T>().FirstOrDefaultAsync(t => t.Id == id, ct)
                     ?? throw new RegraNegocioException("Título não encontrado.");
        acao(titulo);
        await db.SaveChangesAsync(ct);
    }
}
