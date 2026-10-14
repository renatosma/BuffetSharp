using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Agenda de degustações: agendamento com limite por horário, realização com avaliação, ausência e cancelamento.</summary>
public sealed class DegustacaoService(IBuffetDbContextFactory fabrica, BuffetOptions opcoes, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    /// <summary>Sugestão para nova degustação: próximo dia útil às 15h.</summary>
    public DegustacaoInput Novo(int clienteId = 0, int? orcamentoId = null)
    {
        var data = DateOnly.FromDateTime(Agora).AddDays(1);
        while (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            data = data.AddDays(1);
        return new DegustacaoInput { ClienteId = clienteId, OrcamentoId = orcamentoId, Data = data };
    }

    public async Task<DegustacaoInput> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var d = await db.Degustacoes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Degustação não encontrada.");
        return new DegustacaoInput
        {
            Id = d.Id,
            ClienteId = d.ClienteId,
            PacoteId = d.PacoteId,
            OrcamentoId = d.OrcamentoId,
            Data = DateOnly.FromDateTime(d.DataHora),
            Hora = TimeOnly.FromDateTime(d.DataHora),
            Pessoas = d.Pessoas,
            Observacoes = d.Observacoes
        };
    }

    public async Task<int> SalvarAsync(DegustacaoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();

        if (input.OrcamentoId is { } orcamentoId)
        {
            var clienteDoOrcamento = await db.Orcamentos.Where(o => o.Id == orcamentoId).Select(o => (int?)o.ClienteId).FirstOrDefaultAsync(ct);
            Validador.Garantir(clienteDoOrcamento == input.ClienteId, "O orçamento selecionado é de outro cliente.");
        }

        // Limite de degustações simultâneas (com tolerância de 1 hora para cada lado).
        var inicio = input.DataHora.AddMinutes(-59);
        var fim = input.DataHora.AddMinutes(59);
        var simultaneas = await db.Degustacoes.CountAsync(d => d.Id != input.Id && d.Status == StatusDegustacao.Agendada &&
                                                                 d.DataHora >= inicio && d.DataHora <= fim, ct);
        Validador.Garantir(simultaneas < opcoes.LimiteDegustacoesPorHorario,
            $"Já há {simultaneas} degustação(ões) agendada(s) próximas a {input.DataHora:dd/MM HH:mm} " +
            $"(limite de {opcoes.LimiteDegustacoesPorHorario} por horário). Escolha outro horário.");

        Degustacao degustacao;
        if (input.Id == 0)
        {
            degustacao = new Degustacao(input.ClienteId, input.DataHora, input.Pessoas, input.PacoteId, input.OrcamentoId,
                input.Observacoes, Agora);
            db.Degustacoes.Add(degustacao);
        }
        else
        {
            degustacao = await db.Degustacoes.FirstOrDefaultAsync(d => d.Id == input.Id, ct)
                         ?? throw new RegraNegocioException("Degustação não encontrada.");
            degustacao.Alterar(input.ClienteId, input.DataHora, input.Pessoas, input.PacoteId, input.OrcamentoId, input.Observacoes, Agora);
        }

        await db.SaveChangesAsync(ct);
        return degustacao.Id;
    }

    public Task RealizarAsync(int id, int avaliacao, string? comentario, CancellationToken ct = default) =>
        AlterarAsync(id, d => d.Realizar(avaliacao, comentario, Agora), ct);

    public Task RegistrarAusenciaAsync(int id, CancellationToken ct = default) =>
        AlterarAsync(id, d => d.RegistrarAusencia(Agora), ct);

    public Task CancelarAsync(int id, string motivo, CancellationToken ct = default) =>
        AlterarAsync(id, d => d.Cancelar(motivo), ct);

    private async Task AlterarAsync(int id, Action<Degustacao> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var degustacao = await db.Degustacoes.FirstOrDefaultAsync(d => d.Id == id, ct)
                         ?? throw new RegraNegocioException("Degustação não encontrada.");
        acao(degustacao);
        await db.SaveChangesAsync(ct);
    }
}
