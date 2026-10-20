using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasEquipe(FabricaConexao conexoes) : IConsultasEquipe
{
    private const string Escalas = """
        SELECT q.FuncionarioId, f.Nome AS Funcionario, c.Nome AS Cargo, q.Funcao, q.ValorDiaria, q.Presente,
               q.FechamentoEquipeId, e.Id AS EventoId, e.Titulo AS Evento, e.Inicio
          FROM EventoEquipe q
          JOIN Eventos e ON e.Id = q.EventoId
          JOIN Funcionarios f ON f.Id = q.FuncionarioId
          LEFT JOIN Cargos c ON c.Id = f.CargoId
         WHERE e.Status = 'Realizado' AND e.Inicio >= @De AND e.Inicio < @AteExclusivo
        """;

    private sealed class Linha
    {
        public int FuncionarioId { get; init; }
        public string Funcionario { get; init; } = string.Empty;
        public string? Cargo { get; init; }
        public decimal ValorDiaria { get; init; }
        public bool Presente { get; init; }
        public int? FechamentoEquipeId { get; init; }
    }

    public async Task<IReadOnlyList<DiariaFuncionario>> ApurarAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var linhas = await conexoes.ListarAsync<Linha>(Escalas, Periodo(de, ate), ct);
        return linhas.GroupBy(l => l.FuncionarioId)
            .Select(g =>
            {
                var presentes = g.Where(l => l.Presente).ToList();
                return new DiariaFuncionario
                {
                    FuncionarioId = g.Key,
                    Funcionario = g.First().Funcionario,
                    Cargo = g.First().Cargo,
                    Diarias = presentes.Count,
                    Valor = presentes.Sum(l => l.ValorDiaria),
                    Ausencias = g.Count(l => !l.Presente),
                    DiariasFechadas = presentes.Count(l => l.FechamentoEquipeId is not null),
                    ValorFechado = presentes.Where(l => l.FechamentoEquipeId is not null).Sum(l => l.ValorDiaria)
                };
            })
            .OrderBy(d => d.Funcionario)
            .ToList();
    }

    public async Task<IReadOnlyList<EscalaApurada>> DetalharAsync(int funcionarioId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var parametros = new { De = de.ToDateTime(TimeOnly.MinValue), AteExclusivo = ate.AddDays(1).ToDateTime(TimeOnly.MinValue), FuncionarioId = funcionarioId };
        return await conexoes.ListarAsync<EscalaApurada>(Escalas + " AND q.FuncionarioId = @FuncionarioId ORDER BY e.Inicio", parametros, ct);
    }

    public async Task<IReadOnlyList<FechamentoEquipeResumo>> ListarFechamentosAsync(CancellationToken ct = default) =>
        await conexoes.ListarAsync<FechamentoEquipeResumo>("""
            SELECT fe.Id, fe.FuncionarioId, f.Nome AS Funcionario, fe.De, fe.Ate, fe.Diarias, fe.Valor, fe.CriadoEm,
                   cp.Vencimento, cp.Status AS SituacaoConta
              FROM FechamentosEquipe fe
              JOIN Funcionarios f ON f.Id = fe.FuncionarioId
              JOIN ContasPagar cp ON cp.Id = fe.ContaPagarId
             ORDER BY fe.Ate DESC, f.Nome
            """, null, ct);

    private static object Periodo(DateOnly de, DateOnly ate) =>
        new { De = de.ToDateTime(TimeOnly.MinValue), AteExclusivo = ate.AddDays(1).ToDateTime(TimeOnly.MinValue) };
}
