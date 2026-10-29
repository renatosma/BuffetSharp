using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Domain.Cadastros;

/// <summary>Salão / ambiente onde os eventos acontecem.</summary>
public class Espaco : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(1, 10_000, ErrorMessage = "A capacidade deve estar entre 1 e 10.000 pessoas.")]
    public int CapacidadeMaxima { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Valor de locação inválido.")]
    public decimal ValorLocacao { get; set; }

    /// <summary>Onde fica o espaço (citado no contrato). Vazio: usa o endereço da empresa.</summary>
    public Endereco Endereco { get; set; } = new();

    public bool Ativo { get; set; } = true;

    public override string ToString() => $"{Nome} (até {CapacidadeMaxima} pessoas)";
}

/// <summary>Mesa do mapa padrão do espaço: o evento novo no salão já nasce com este arranjo.</summary>
public class EspacoMesa : IMesaPlanta
{
    private EspacoMesa()
    {
    }

    public EspacoMesa(int espacoId, IMesaPlanta mesa)
    {
        EspacoId = espacoId;
        Alterar(mesa);
    }

    public int Id { get; private set; }
    public int EspacoId { get; private set; }
    public int Numero { get; private set; }
    public string? Nome { get; private set; }
    public int Lugares { get; private set; }
    public FormatoMesa Formato { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }

    public void Alterar(IMesaPlanta mesa)
    {
        PlantaSalao.ValidarMesa(mesa.Numero, mesa.Nome, mesa.Lugares);
        Numero = mesa.Numero;
        Nome = PlantaSalao.Limpo(mesa.Nome);
        Lugares = mesa.Lugares;
        Formato = mesa.Formato;
        (X, Y) = PlantaSalao.Limitar(mesa.X, mesa.Y);
    }
}

/// <summary>DJ, decoração, fotografia, bolo, open bar...</summary>
public class ServicoAdicional : Entidade, IAtivavel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Preço inválido.")]
    public decimal Preco { get; set; }

    /// <summary>Quando verdadeiro, o preço é multiplicado pela quantidade de convidados.</summary>
    public bool CobradoPorPessoa { get; set; }

    public bool Ativo { get; set; } = true;

    public override string ToString() => Nome;
}
