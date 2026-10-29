using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

public enum TipoRegraPreco
{
    DiaSemana,
    Temporada,
    DataEspecial
}

/// <summary>
/// Ajuste do preço de tabela conforme a data do evento: dias da semana (sábado +15%), temporadas que se repetem todo ano
/// (alta de 15/11 a 31/01) ou datas especiais (réveillon). Pode valer para o valor por pessoa, a locação do espaço ou ambos,
/// para um pacote ou espaço específico, e exigir um mínimo de convidados pagantes.
/// </summary>
public class RegraPreco : Entidade, IAtivavel, IValidatableObject
{
    public const decimal PercentualMinimo = -50m;
    public const decimal PercentualMaximo = 200m;

    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(60)]
    public string Nome { get; set; } = string.Empty;

    public TipoRegraPreco Tipo { get; set; }

    /// <summary>Dias da semana em que vale (bit 0 = domingo ... bit 6 = sábado).</summary>
    public int DiasSemana { get; set; }

    /// <summary>Temporada: do dia/mês de início ao dia/mês de fim, todo ano (pode virar o ano, como 15/11 a 31/01).</summary>
    public int? DiaInicio { get; set; }

    public int? MesInicio { get; set; }
    public int? DiaFim { get; set; }
    public int? MesFim { get; set; }

    /// <summary>Data especial (com <see cref="Anual"/>, vale no mesmo dia e mês de todos os anos).</summary>
    public DateOnly? Data { get; set; }

    public bool Anual { get; set; }

    /// <summary>Data especial que substitui as demais regras no dia (ex.: réveillon com preço próprio).</summary>
    public bool Exclusiva { get; set; }

    /// <summary>Ajuste em % sobre o preço de tabela (negativo = desconto de baixa temporada).</summary>
    public decimal Percentual { get; set; }

    public bool AjustaPessoa { get; set; } = true;
    public bool AjustaLocacao { get; set; }

    /// <summary>Somente para este pacote (nulo = todos).</summary>
    public int? PacoteId { get; set; }

    public Pacote? Pacote { get; set; }

    /// <summary>Somente para este espaço (nulo = todos).</summary>
    public int? EspacoId { get; set; }

    public Espaco? Espaco { get; set; }

    /// <summary>Mínimo de convidados pagantes para fechar na data (nulo = sem exigência).</summary>
    public int? MinimoConvidados { get; set; }

    public bool Ativo { get; set; } = true;

    public bool ValeNoDia(DayOfWeek dia) => (DiasSemana & (1 << (int)dia)) != 0;

    public void DefinirDia(DayOfWeek dia, bool vale) =>
        DiasSemana = vale ? DiasSemana | (1 << (int)dia) : DiasSemana & ~(1 << (int)dia);

    /// <summary>A regra vale para o evento nesta data, com este pacote e espaço.</summary>
    public bool Vale(DateOnly data, int? pacoteId, int? espacoId)
    {
        if (!Ativo || (PacoteId is not null && PacoteId != pacoteId) || (EspacoId is not null && EspacoId != espacoId))
            return false;
        return Tipo switch
        {
            TipoRegraPreco.DiaSemana => ValeNoDia(data.DayOfWeek),
            TipoRegraPreco.Temporada => NaTemporada(data),
            _ => Data is { } especial && (Anual ? especial.Month == data.Month && especial.Day == data.Day : especial == data)
        };
    }

    private bool NaTemporada(DateOnly data)
    {
        if (DiaInicio is not { } di || MesInicio is not { } mi || DiaFim is not { } df || MesFim is not { } mf)
            return false;
        var dia = data.Month * 100 + data.Day;
        var inicio = mi * 100 + di;
        var fim = mf * 100 + df;
        return inicio <= fim ? dia >= inicio && dia <= fim : dia >= inicio || dia <= fim;
    }

    /// <summary>Quando vale, em texto: "sábados e domingos", "15/11 a 31/01", "31/12 (todo ano)".</summary>
    public string Quando => Tipo switch
    {
        TipoRegraPreco.DiaSemana => Textos.DiasDaSemana(Enum.GetValues<DayOfWeek>().Where(ValeNoDia)),
        TipoRegraPreco.Temporada => $"{DiaInicio:00}/{MesInicio:00} a {DiaFim:00}/{MesFim:00}",
        _ => Data is { } d ? (Anual ? $"{d:dd/MM} (todo ano)" : $"{d:dd/MM/yyyy}") + (Exclusiva ? ", exclusiva" : "") : ""
    };

    public string Ajuste => $"{(Percentual >= 0 ? "+" : "")}{Percentual:0.##}%";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Percentual is < PercentualMinimo or > PercentualMaximo)
            yield return new ValidationResult($"O ajuste vai de {PercentualMinimo}% a +{PercentualMaximo}%.", [nameof(Percentual)]);
        if (!AjustaPessoa && !AjustaLocacao)
            yield return new ValidationResult("Marque se o ajuste vale para o valor por pessoa, para a locação ou para ambos.", [nameof(AjustaPessoa)]);
        if (MinimoConvidados is < 1 or > 10_000)
            yield return new ValidationResult("O mínimo de convidados vai de 1 a 10.000.", [nameof(MinimoConvidados)]);
        if (Exclusiva && Tipo != TipoRegraPreco.DataEspecial)
            yield return new ValidationResult("Somente a data especial pode ser exclusiva.", [nameof(Exclusiva)]);

        switch (Tipo)
        {
            case TipoRegraPreco.DiaSemana when (DiasSemana & 0x7F) == 0:
                yield return new ValidationResult("Marque ao menos um dia da semana.", [nameof(DiasSemana)]);
                break;
            case TipoRegraPreco.Temporada when !DataValida(DiaInicio, MesInicio) || !DataValida(DiaFim, MesFim):
                yield return new ValidationResult("Informe o dia e o mês de início e de fim da temporada.", [nameof(DiaInicio)]);
                break;
            case TipoRegraPreco.DataEspecial when Data is null:
                yield return new ValidationResult("Informe a data especial.", [nameof(Data)]);
                break;
        }
    }

    private static bool DataValida(int? dia, int? mes) =>
        mes is >= 1 and <= 12 && dia is >= 1 && dia <= DateTime.DaysInMonth(2024, mes.Value);

    public override string ToString() => $"{Nome} ({Ajuste})";

    private static class Textos
    {
        private static readonly string[] Nomes = ["domingos", "segundas", "terças", "quartas", "quintas", "sextas", "sábados"];

        public static string DiasDaSemana(IEnumerable<DayOfWeek> dias)
        {
            var lista = dias.Select(d => Nomes[(int)d]).ToList();
            return lista.Count switch
            {
                0 => "",
                7 => "todos os dias",
                1 => lista[0],
                _ => string.Join(", ", lista[..^1]) + " e " + lista[^1]
            };
        }
    }
}

/// <summary>Ajuste da data: os percentuais somados das regras que valem e o maior mínimo de convidados exigido.</summary>
public sealed record AjustePreco(decimal PercentualPessoa, decimal PercentualLocacao, int? MinimoConvidados, IReadOnlyList<RegraPreco> Regras)
{
    public static readonly AjustePreco Nenhum = new(0, 0, null, []);

    /// <summary>O ajuste somado não deixa o preço abaixo de 10% da tabela.</summary>
    private const decimal Piso = -90m;

    public bool TemAjuste => PercentualPessoa != 0 || PercentualLocacao != 0 || MinimoConvidados is not null;

    /// <summary>Ex.: "Sábado +15%; Alta temporada +10%".</summary>
    public string Descricao => string.Join("; ", Regras.Select(r => $"{r.Nome} {r.Ajuste}" + (r.AjustaPessoa && r.AjustaLocacao ? "" : r.AjustaPessoa ? " (por pessoa)" : " (locação)")));

    public decimal Pessoa(decimal precoTabela) => Aplicar(precoTabela, PercentualPessoa);

    public decimal Locacao(decimal precoTabela) => Aplicar(precoTabela, PercentualLocacao);

    private static decimal Aplicar(decimal preco, decimal percentual) => Math.Round(preco * (1 + Math.Max(Piso, percentual) / 100m), 2);

    /// <summary>
    /// Soma os ajustes das regras ativas que valem na data, para o pacote e o espaço. Uma data especial exclusiva substitui as
    /// demais regras do dia.
    /// </summary>
    public static AjustePreco Calcular(IEnumerable<RegraPreco> regras, DateOnly data, int? pacoteId, int? espacoId)
    {
        var validas = regras.Where(r => r.Vale(data, pacoteId, espacoId)).OrderBy(r => r.Tipo).ThenBy(r => r.Nome).ToList();
        if (validas.Any(r => r.Exclusiva))
            validas = validas.Where(r => r.Exclusiva).ToList();
        if (validas.Count == 0)
            return Nenhum;
        return new AjustePreco(validas.Where(r => r.AjustaPessoa).Sum(r => r.Percentual), validas.Where(r => r.AjustaLocacao).Sum(r => r.Percentual),
            validas.Max(r => r.MinimoConvidados), validas);
    }
}
