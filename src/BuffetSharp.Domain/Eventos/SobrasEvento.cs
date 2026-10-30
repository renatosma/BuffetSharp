using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Eventos;

/// <summary>O que foi feito com a comida que sobrou (só o descarte é desperdício).</summary>
public enum DestinoSobra
{
    Descarte,
    Doacao,
    RefeicaoEquipe,
    ClienteLevou
}

/// <summary>Quanto sobrou de um prato produzido para o evento e o destino da sobra.</summary>
public class SobraPrato
{
    public const int TamanhoPrato = 120;
    public const int TamanhoInstituicao = 120;

    private SobraPrato()
    {
    }

    internal SobraPrato(int? itemCardapioId, string prato, int porcoes, decimal percentual, DestinoSobra destino, string? instituicao,
        decimal custoPorcao)
    {
        ItemCardapioId = itemCardapioId;
        Prato = prato.Length > TamanhoPrato ? prato[..TamanhoPrato] : prato;
        Porcoes = porcoes;
        Percentual = percentual;
        Destino = destino;
        Instituicao = instituicao;
        Custo = Math.Round(porcoes * custoPorcao * percentual / 100, 2);
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }

    /// <summary>Prato do cardápio (nulo se foi excluído depois).</summary>
    public int? ItemCardapioId { get; private set; }

    public string Prato { get; private set; } = string.Empty;

    /// <summary>Porções produzidas (o plano do cardápio do evento, com os substitutos).</summary>
    public int Porcoes { get; private set; }

    /// <summary>Quanto do produzido sobrou (%).</summary>
    public decimal Percentual { get; private set; }

    public DestinoSobra Destino { get; private set; }

    /// <summary>Quem recebeu a doação.</summary>
    public string? Instituicao { get; private set; }

    /// <summary>Custo dos insumos da parte que sobrou (congelado no registro).</summary>
    public decimal Custo { get; private set; }

    public bool Desperdicio => Destino == DestinoSobra.Descarte;
    public decimal PorcoesSobra => Math.Round(Porcoes * Percentual / 100, 1);
}

/// <summary>Sobra informada de um prato (para <see cref="Evento.RegistrarSobras"/>).</summary>
public sealed record SobraInformada(int? ItemCardapioId, string Prato, int Porcoes, decimal CustoPorcao, decimal Percentual, DestinoSobra Destino,
    string? Instituicao);

/// <summary>Ajuste sugerido para a ficha técnica de um prato que sobra muito.</summary>
public static class AjusteFicha
{
    /// <summary>
    /// Fator para multiplicar as quantidades da ficha: tira a sobra média, deixando uma folga de segurança (ex.: sobra média de
    /// 20% com folga de 5% → 0,85). Nunca aumenta nem reduz mais da metade.
    /// </summary>
    public static decimal Fator(decimal sobraMedia, decimal folga) =>
        Math.Round(Math.Clamp(1 - (sobraMedia - folga) / 100, 0.5m, 1m), 2);
}
