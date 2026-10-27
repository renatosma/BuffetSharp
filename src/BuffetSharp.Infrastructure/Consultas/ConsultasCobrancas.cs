using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasCobrancas(FabricaConexao conexoes) : IConsultasCobrancas
{
    public async Task<IReadOnlyList<CobrancaResumo>> ListarAsync(FiltroCobrancas filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT cr.Id, cr.ParcelaId, cr.EventoId, e.Titulo AS Evento, e.ClienteId, c.Nome AS Cliente, cr.Descricao, cr.Ambiente, cr.Tipo,
                   cr.Status, cr.Valor, cr.Vencimento, cr.DataPagamento, cr.ValorPago, cr.ValorLiquido, cr.PagaCom, cr.Baixada, cr.Mensagem,
                   cr.IdExterno, cr.UrlBoleto, cr.UrlFatura, cr.SolicitadaEm, cr.Usuario
              FROM CobrancasRegistradas cr
              JOIN Eventos e ON e.Id = cr.EventoId
              JOIN Clientes c ON c.Id = e.ClienteId
             WHERE (cr.Status = 'Pendente' OR ((@De IS NULL OR cr.SolicitadaEm >= @De) AND (@AteExclusivo IS NULL OR cr.SolicitadaEm < @AteExclusivo)))
               AND (@Status IS NULL OR cr.Status = @Status)
             ORDER BY cr.SolicitadaEm DESC, cr.Id DESC
            """;
        return await conexoes.ListarAsync<CobrancaResumo>(sql, new
        {
            De = filtro.De?.Date,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString()
        }, ct);
    }
}
