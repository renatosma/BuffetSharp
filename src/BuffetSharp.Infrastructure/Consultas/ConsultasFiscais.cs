using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasFiscais(FabricaConexao conexoes) : IConsultasFiscais
{
    public async Task<IReadOnlyList<NotaServicoResumo>> ListarNotasServicoAsync(FiltroNotasServico filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT n.Id, n.Referencia, n.EventoId, e.Titulo AS Evento, e.Inicio AS DataEvento, n.Ambiente, n.Status, n.Numero, n.CodigoVerificacao,
                   n.DataEmissao, n.SolicitadaEm, n.TomadorDocumento, n.TomadorNome, n.ValorServicos, n.AliquotaIss, n.Url, n.Mensagem,
                   n.MotivoCancelamento, n.Usuario
              FROM NotasFiscaisServico n
              JOIN Eventos e ON e.Id = n.EventoId
             WHERE (@De IS NULL OR n.SolicitadaEm >= @De)
               AND (@AteExclusivo IS NULL OR n.SolicitadaEm < @AteExclusivo)
               AND (@Status IS NULL OR n.Status = @Status)
             ORDER BY n.SolicitadaEm DESC, n.Id DESC
            """;
        var notas = await conexoes.ListarAsync<NotaServicoResumo>(sql, new
        {
            De = filtro.De?.Date,
            AteExclusivo = Filtros.FimDoDia(filtro.Ate),
            Status = filtro.Status?.ToString()
        }, ct);
        return notas.Select(n => n with { Evento = $"EVT-{n.EventoId:D5} - {n.Evento}" }).ToList();
    }
}
