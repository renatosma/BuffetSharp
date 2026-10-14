using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Fechamento de comissões: congela o valor apurado (Dapper, mesma consulta da tela) e gera a conta a pagar
/// ao vendedor. Desfazer só é possível enquanto a conta não foi paga.
/// </summary>
public sealed class ComissaoService(IBuffetDbContextFactory fabrica, IConsultasComissoes consultas, TimeProvider relogio)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);

    /// <summary>Fecha a comissão do vendedor no período e retorna o Id do fechamento.</summary>
    public async Task<int> FecharAsync(int vendedorId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        Validador.Garantir(de <= ate, "O início do período deve ser anterior ao fim.");
        Validador.Garantir(ate < Hoje, "Feche somente períodos já encerrados (até ontem).");

        var apurado = (await consultas.ApurarAsync(de, ate, ct)).FirstOrDefault(c => c.VendedorId == vendedorId);
        Validador.Garantir(apurado is { Comissao: > 0 }, "Não há comissão a pagar para este vendedor no período.");

        await using var db = fabrica.Criar();
        var vendedor = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == vendedorId, ct)
                       ?? throw new RegraNegocioException("Vendedor não encontrado.");

        var sobreposto = await db.FechamentosComissao.AsNoTracking()
            .FirstOrDefaultAsync(f => f.VendedorId == vendedorId && f.De <= ate && de <= f.Ate, ct);
        Validador.Garantir(sobreposto is null,
            $"Já existe fechamento de {vendedor.Nome} de {sobreposto?.De:dd/MM/yyyy} a {sobreposto?.Ate:dd/MM/yyyy}, que se sobrepõe a este período.");

        var fechamento = new FechamentoComissao(vendedor, de, ate, apurado!.BaseRecebida, apurado.Comissao, Hoje.AddDays(5));
        db.FechamentosComissao.Add(fechamento);
        await db.SaveChangesAsync(ct);
        return fechamento.Id;
    }

    /// <summary>Exclui o fechamento e a conta a pagar gerada (somente se ainda não paga).</summary>
    public async Task DesfazerAsync(int fechamentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var fechamento = await db.FechamentosComissao.Include(f => f.ContaPagar).FirstOrDefaultAsync(f => f.Id == fechamentoId, ct)
                         ?? throw new RegraNegocioException("Fechamento não encontrado.");
        Validador.Garantir(fechamento.ContaPagar!.Status != StatusTitulo.Pago,
            "A comissão já foi paga. Estorne o pagamento em contas a pagar antes de desfazer o fechamento.");

        db.FechamentosComissao.Remove(fechamento);
        db.ContasPagar.Remove(fechamento.ContaPagar);
        await db.SaveChangesAsync(ct);
    }
}
