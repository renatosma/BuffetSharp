using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Verifica se um espaço está livre no período (regra que depende do banco, por isso fica fora do domínio).
/// </summary>
public sealed class DisponibilidadeService(IConsultasAgenda agenda, BuffetOptions opcoes)
{
    public Task<ItemAgenda?> BuscarConflitoAsync(int espacoId, DateTime inicio, DateTime fim, int? ignorarEventoId,
        CancellationToken ct = default) =>
        agenda.BuscarConflitoAsync(espacoId, inicio, fim, opcoes.IntervaloMinimoEntreEventosMinutos, ignorarEventoId, ct);

    public async Task GarantirDisponivelAsync(int espacoId, DateTime inicio, DateTime fim, int? ignorarEventoId,
        CancellationToken ct = default)
    {
        var conflito = await BuscarConflitoAsync(espacoId, inicio, fim, ignorarEventoId, ct);
        if (conflito is not null)
            throw new RegraNegocioException(
                $"O espaço '{conflito.Espaco}' já está reservado para {conflito.Codigo} - {conflito.Titulo} " +
                $"({conflito.Inicio:dd/MM/yyyy HH:mm} às {conflito.Fim:HH:mm}). " +
                $"É necessário um intervalo de {opcoes.IntervaloMinimoEntreEventosMinutos} min entre eventos.");
    }
}
