using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;
using Dapper;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasAgenda(FabricaConexao conexoes) : IConsultasAgenda
{
    private const string SqlListar = """
        SELECT e.Id, 'Evento' AS Origem, e.Titulo, c.Nome AS Cliente, e.EspacoId, es.Nome AS Espaco,
               t.Nome AS TipoEvento, e.Inicio, e.Fim, e.QuantidadeConvidados AS Convidados, e.Status
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN Espacos es ON es.Id = e.EspacoId
          JOIN TiposEvento t ON t.Id = e.TipoEventoId
         WHERE e.Status <> 'Cancelado'
           AND e.Inicio < @Fim AND e.Fim > @Inicio
           AND (@EspacoId IS NULL OR e.EspacoId = @EspacoId)
        UNION ALL
        SELECT o.Id, 'Orcamento', CONCAT('Pré-reserva: ', c.Nome), c.Nome, o.EspacoId, es.Nome,
               t.Nome, o.Inicio, o.Fim, o.QuantidadeConvidados, 'Confirmado'
          FROM Orcamentos o
          JOIN Clientes c ON c.Id = o.ClienteId
          JOIN Espacos es ON es.Id = o.EspacoId
          JOIN TiposEvento t ON t.Id = o.TipoEventoId
         WHERE @PreReservas = 1
           AND o.Status = 'Enviado' AND o.ValidoAte >= CAST(GETDATE() AS date)
           AND o.Inicio < @Fim AND o.Fim > @Inicio
           AND (@EspacoId IS NULL OR o.EspacoId = @EspacoId)
         ORDER BY Inicio;
        """;

    // Dois intervalos conflitam quando se sobrepõem, já considerando o tempo de montagem/desmontagem.
    private const string SqlConflito = """
        SELECT TOP (1) e.Id, 'Evento' AS Origem, e.Titulo, c.Nome AS Cliente, e.EspacoId, es.Nome AS Espaco,
               t.Nome AS TipoEvento, e.Inicio, e.Fim, e.QuantidadeConvidados AS Convidados, e.Status
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN Espacos es ON es.Id = e.EspacoId
          JOIN TiposEvento t ON t.Id = e.TipoEventoId
         WHERE e.EspacoId = @EspacoId
           AND e.Status <> 'Cancelado'
           AND (@IgnorarEventoId IS NULL OR e.Id <> @IgnorarEventoId)
           AND e.Inicio < DATEADD(MINUTE, @Intervalo, @Fim)
           AND DATEADD(MINUTE, @Intervalo, e.Fim) > @Inicio
         ORDER BY e.Inicio;
        """;

    public async Task<IReadOnlyList<ItemAgenda>> ListarAsync(DateTime inicio, DateTime fim, int? espacoId = null,
        bool incluirPreReservas = true, CancellationToken ct = default)
    {
        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<ItemAgenda>(new CommandDefinition(SqlListar,
            new { Inicio = inicio, Fim = fim, EspacoId = espacoId, PreReservas = incluirPreReservas }, cancellationToken: ct));
        return itens.AsList();
    }

    public async Task<ItemAgenda?> BuscarConflitoAsync(int espacoId, DateTime inicio, DateTime fim, int intervaloMinutos,
        int? ignorarEventoId = null, CancellationToken ct = default)
    {
        await using var conexao = conexoes.Criar();
        return await conexao.QueryFirstOrDefaultAsync<ItemAgenda>(new CommandDefinition(SqlConflito,
            new
            {
                EspacoId = espacoId,
                Inicio = inicio,
                Fim = fim,
                Intervalo = intervaloMinutos,
                IgnorarEventoId = ignorarEventoId
            }, cancellationToken: ct));
    }
}
