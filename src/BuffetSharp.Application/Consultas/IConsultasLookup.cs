namespace BuffetSharp.Application.Consultas;

public enum TipoLookup
{
    Clientes,
    TiposEvento,
    Espacos,
    Pacotes,
    ServicosAdicionais,
    FormasPagamento,
    Fornecedores,
    Funcionarios,
    Cargos,
    CategoriasProduto,
    CategoriasCardapio,
    UnidadesMedida,
    Produtos,
    ItensCardapio,
    EventosConfirmados
}

/// <summary>
/// Item genérico para combos e listas de seleção. Os campos auxiliares variam por tipo:
/// Espaços (Valor = locação, Numero = capacidade), Pacotes (Valor = preço/pessoa, Numero = mínimo),
/// Serviços (Valor = preço, Marcador = cobrado por pessoa), Funcionários (Valor = diária, Info = cargo),
/// Produtos (Valor = custo médio, Info = unidade), Formas de pagamento (Numero = máx. parcelas).
/// </summary>
public sealed record ItemLookup
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public decimal Valor { get; init; }
    public int Numero { get; init; }
    public bool Marcador { get; init; }
    public string? Info { get; init; }

    public override string ToString() => Nome;
}

/// <summary>Consultas leves (Dapper) para preencher combos.</summary>
public interface IConsultasLookup
{
    Task<IReadOnlyList<ItemLookup>> ListarAsync(TipoLookup tipo, bool somenteAtivos = true, CancellationToken ct = default);
}
