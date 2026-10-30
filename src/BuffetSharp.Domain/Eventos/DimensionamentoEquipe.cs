using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

/// <summary>
/// Quantas pessoas de um cargo o evento precisa: uma a cada N convidados, com um mínimo (ex.: garçom 1 a cada 15, mínimo 4).
/// Sem tipo de evento é a regra geral; a do tipo substitui a geral para o mesmo cargo.
/// </summary>
public class RegraEquipe : Entidade, IAtivavel, IValidatableObject
{
    /// <summary>Tipo de evento da regra; nulo = vale para todos os tipos sem regra própria do cargo.</summary>
    public int? TipoEventoId { get; set; }
    public TipoEvento? TipoEvento { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o cargo.")]
    public int CargoId { get; set; }
    public Cargo? Cargo { get; set; }

    /// <summary>Um profissional a cada tantos convidados (0 = só o mínimo, ex.: 1 maître por evento).</summary>
    [Range(0, 10_000, ErrorMessage = "Informe a cada quantos convidados.")]
    public int ConvidadosPorProfissional { get; set; }

    [Range(0, 1_000, ErrorMessage = "Mínimo inválido.")]
    public int Minimo { get; set; }

    public bool Ativo { get; set; } = true;

    public int Quantidade(int convidados) =>
        Math.Max(Minimo, ConvidadosPorProfissional <= 0 || convidados <= 0 ? 0 : (int)Math.Ceiling((decimal)convidados / ConvidadosPorProfissional));

    public string Descricao => ConvidadosPorProfissional > 0
        ? $"1 a cada {ConvidadosPorProfissional} convidados" + (Minimo > 0 ? $", mínimo {Minimo}" : "")
        : $"{Minimo} por evento";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConvidadosPorProfissional == 0 && Minimo == 0)
            yield return new ValidationResult("Informe a cada quantos convidados ou o mínimo por evento.", [nameof(ConvidadosPorProfissional)]);
    }
}

/// <summary>Quantas pessoas de um cargo o evento precisa e por qual regra.</summary>
public sealed record NecessidadeCargo(int CargoId, string Cargo, int Quantidade, decimal Diaria, string Regra)
{
    public decimal Custo => Quantidade * Diaria;
}

public static class DimensionamentoEquipe
{
    /// <summary>
    /// Equipe necessária: por cargo, a regra do tipo de evento ou, sem ela, a geral (as regras precisam do cargo carregado).
    /// </summary>
    public static IReadOnlyList<NecessidadeCargo> Calcular(IEnumerable<RegraEquipe> regras, int tipoEventoId, int convidados) =>
        regras.Where(r => r.Ativo && (r.TipoEventoId is null || r.TipoEventoId == tipoEventoId))
            .GroupBy(r => r.CargoId)
            .Select(g => g.OrderBy(r => r.TipoEventoId is null ? 1 : 0).First())
            .Select(r => new NecessidadeCargo(r.CargoId, r.Cargo?.Nome ?? "?", r.Quantidade(convidados), r.Cargo?.ValorDiariaPadrao ?? 0,
                (r.TipoEventoId is null ? "geral: " : "do tipo: ") + r.Descricao))
            .Where(n => n.Quantidade > 0)
            .OrderBy(n => n.Cargo)
            .ToList();

    /// <summary>Há regra (do tipo ou geral) para dimensionar este tipo de evento.</summary>
    public static bool TemRegras(IEnumerable<RegraEquipe> regras, int tipoEventoId) =>
        regras.Any(r => r.Ativo && (r.TipoEventoId is null || r.TipoEventoId == tipoEventoId));
}
