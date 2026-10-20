using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

/// <summary>Tabelas de preço que o reajuste pode alcançar.</summary>
[Flags]
public enum AlcanceReajuste
{
    Nenhum = 0,

    /// <summary>Preço por pessoa dos pacotes.</summary>
    Pacotes = 1,

    /// <summary>Preço dos serviços adicionais.</summary>
    Servicos = 2,

    /// <summary>Valor de locação dos espaços.</summary>
    Espacos = 4
}

public enum ArredondamentoPreco
{
    /// <summary>Mantém os centavos (R$ 152,37).</summary>
    Centavos,

    /// <summary>Real inteiro mais próximo (R$ 152,00).</summary>
    Reais,

    /// <summary>Múltiplo de R$ 5 mais próximo (R$ 150,00), para tabelas "redondas".</summary>
    CincoReais
}

public enum StatusReajuste
{
    /// <summary>Aguardando a data de vigência.</summary>
    Agendado,
    Aplicado,
    Desfeito
}

public enum TipoItemReajuste
{
    Pacote,
    ServicoAdicional,
    Espaco
}

/// <summary>
/// Reajuste percentual das tabelas de preço (pacotes, serviços e espaços), aplicado na data de vigência. Guarda o valor
/// anterior e o novo de cada item, para consulta e para desfazer. Orçamentos e eventos já existentes não mudam: eles
/// guardam os valores negociados.
/// </summary>
public class ReajustePreco : Entidade
{
    public const decimal PercentualMaximo = 100m;
    private readonly List<ReajusteItem> _itens = [];

    private ReajustePreco()
    {
    }

    public ReajustePreco(string descricao, decimal percentual, DateOnly vigencia, AlcanceReajuste alcance, ArredondamentoPreco arredondamento,
        string? usuario)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Informe a descrição do reajuste (ex.: \"Tabela 2027\").");
        Validador.Garantir(percentual != 0 && percentual is >= -50 and <= PercentualMaximo,
            $"O percentual deve estar entre -50% e {PercentualMaximo}% (e ser diferente de zero).");
        Validador.Garantir(alcance != AlcanceReajuste.Nenhum, "Escolha ao menos uma tabela: pacotes, serviços ou espaços.");
        Descricao = descricao.Trim();
        Percentual = percentual;
        VigenciaEm = vigencia;
        Alcance = alcance;
        Arredondamento = arredondamento;
        Usuario = usuario;
    }

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Percentual de reajuste (negativo = redução).</summary>
    public decimal Percentual { get; private set; }

    public DateOnly VigenciaEm { get; private set; }
    public AlcanceReajuste Alcance { get; private set; }
    public ArredondamentoPreco Arredondamento { get; private set; }
    public StatusReajuste Status { get; private set; } = StatusReajuste.Agendado;
    public string? Usuario { get; private set; }
    public DateTime? AplicadoEm { get; private set; }
    public DateTime? DesfeitoEm { get; private set; }

    public IReadOnlyCollection<ReajusteItem> Itens => _itens;

    public bool Alcanca(TipoItemReajuste tipo) => tipo switch
    {
        TipoItemReajuste.Pacote => Alcance.HasFlag(AlcanceReajuste.Pacotes),
        TipoItemReajuste.ServicoAdicional => Alcance.HasFlag(AlcanceReajuste.Servicos),
        _ => Alcance.HasFlag(AlcanceReajuste.Espacos)
    };

    /// <summary>Novo valor de um preço: reajustado e arredondado (nunca negativo).</summary>
    public decimal Calcular(decimal valor) => Calcular(valor, Percentual, Arredondamento);

    public static decimal Calcular(decimal valor, decimal percentual, ArredondamentoPreco arredondamento)
    {
        var reajustado = valor * (1 + percentual / 100m);
        var arredondado = arredondamento switch
        {
            ArredondamentoPreco.Reais => Math.Round(reajustado, 0, MidpointRounding.AwayFromZero),
            ArredondamentoPreco.CincoReais => Math.Round(reajustado / 5m, 0, MidpointRounding.AwayFromZero) * 5m,
            _ => Math.Round(reajustado, 2, MidpointRounding.AwayFromZero)
        };
        return Math.Max(0, arredondado);
    }

    public bool PodeAplicar(DateOnly hoje) => Status == StatusReajuste.Agendado && VigenciaEm <= hoje;

    /// <summary>Registra a aplicação: o serviço já trocou os preços e informa o antes e o depois de cada item.</summary>
    public void RegistrarAplicacao(IEnumerable<(TipoItemReajuste Tipo, int ItemId, string Nome, decimal Anterior, decimal Novo)> itens,
        DateTime agora)
    {
        Validador.Garantir(PodeAplicar(DateOnly.FromDateTime(agora)), "Este reajuste não está pronto para ser aplicado.");
        foreach (var item in itens)
            _itens.Add(new ReajusteItem(item.Tipo, item.ItemId, item.Nome, item.Anterior, item.Novo));
        Status = StatusReajuste.Aplicado;
        AplicadoEm = agora;
    }

    /// <summary>Cancela um reajuste ainda agendado (nada foi alterado).</summary>
    public void Cancelar(DateTime agora)
    {
        Validador.Garantir(Status == StatusReajuste.Agendado, "Somente reajustes agendados podem ser cancelados. Para um reajuste aplicado, use Desfazer.");
        Status = StatusReajuste.Desfeito;
        DesfeitoEm = agora;
    }

    /// <summary>Marca o reajuste como desfeito. O serviço devolve o valor anterior aos itens que não mudaram depois.</summary>
    public void RegistrarDesfazer(DateTime agora)
    {
        Validador.Garantir(Status == StatusReajuste.Aplicado, "Somente reajustes aplicados podem ser desfeitos.");
        Status = StatusReajuste.Desfeito;
        DesfeitoEm = agora;
    }
}

/// <summary>Preço de um item antes e depois do reajuste.</summary>
public class ReajusteItem
{
    private ReajusteItem()
    {
    }

    internal ReajusteItem(TipoItemReajuste tipo, int itemId, string nome, decimal anterior, decimal novo)
    {
        Tipo = tipo;
        ItemId = itemId;
        Nome = nome.Length > 150 ? nome[..150] : nome;
        ValorAnterior = anterior;
        ValorNovo = novo;
    }

    public int Id { get; private set; }
    public int ReajustePrecoId { get; private set; }
    public TipoItemReajuste Tipo { get; private set; }
    public int ItemId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public decimal ValorAnterior { get; private set; }
    public decimal ValorNovo { get; private set; }
}
