using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

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
        -- Pré-reserva não tem situação de evento (NULL fica como Confirmado, o padrão). Um literal de texto aqui
        -- teria tipo diferente da coluna no Oracle (VARCHAR2 x NVARCHAR2) e o UNION falharia.
        SELECT o.Id, 'Orcamento', CONCAT('Pré-reserva: ', c.Nome), c.Nome, o.EspacoId, es.Nome,
               t.Nome, o.Inicio, o.Fim, o.QuantidadeConvidados, NULL
          FROM Orcamentos o
          JOIN Clientes c ON c.Id = o.ClienteId
          JOIN Espacos es ON es.Id = o.EspacoId
          JOIN TiposEvento t ON t.Id = o.TipoEventoId
         WHERE @PreReservas = 1
           AND o.Status = 'Enviado' AND o.ValidoAte >= @Hoje
           AND o.Inicio < @Fim AND o.Fim > @Inicio
           AND (@EspacoId IS NULL OR o.EspacoId = @EspacoId)
         ORDER BY Inicio
        """;

    // Dois intervalos conflitam quando se sobrepõem, já considerando o tempo de montagem/desmontagem
    // (aplicado em C# aos limites, para não depender de funções de data de cada banco).
    private const string SqlConflito = """
        SELECT e.Id, 'Evento' AS Origem, e.Titulo, c.Nome AS Cliente, e.EspacoId, es.Nome AS Espaco,
               t.Nome AS TipoEvento, e.Inicio, e.Fim, e.QuantidadeConvidados AS Convidados, e.Status
          FROM Eventos e
          JOIN Clientes c ON c.Id = e.ClienteId
          JOIN Espacos es ON es.Id = e.EspacoId
          JOIN TiposEvento t ON t.Id = e.TipoEventoId
         WHERE e.EspacoId = @EspacoId
           AND e.Status <> 'Cancelado'
           AND (@IgnorarEventoId IS NULL OR e.Id <> @IgnorarEventoId)
           AND e.Inicio < @FimComIntervalo
           AND e.Fim > @InicioComIntervalo
         ORDER BY e.Inicio
         {limite:1}
        """;

    public async Task<IReadOnlyList<ItemAgenda>> ListarAsync(DateTime inicio, DateTime fim, int? espacoId = null,
        bool incluirPreReservas = true, CancellationToken ct = default) =>
        await conexoes.ListarAsync<ItemAgenda>(SqlListar, new
        {
            Inicio = inicio,
            Fim = fim,
            EspacoId = espacoId,
            PreReservas = incluirPreReservas ? 1 : 0,
            Hoje = DateTime.Today
        }, ct);

    public Task<ItemAgenda?> BuscarConflitoAsync(int espacoId, DateTime inicio, DateTime fim, int intervaloMinutos,
        int? ignorarEventoId = null, CancellationToken ct = default) =>
        conexoes.PrimeiroAsync<ItemAgenda>(SqlConflito, new
        {
            EspacoId = espacoId,
            FimComIntervalo = fim.AddMinutes(intervaloMinutos),
            InicioComIntervalo = inicio.AddMinutes(-intervaloMinutos),
            IgnorarEventoId = ignorarEventoId
        }, ct);
}
