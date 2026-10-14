using System.ComponentModel.DataAnnotations;

namespace BuffetSharp.Domain.Comum;

/// <summary>
/// Endereço mapeado como tipo complexo (colunas na própria tabela do dono).
/// </summary>
public class Endereco
{
    [MaxLength(9)] public string? Cep { get; set; }
    [MaxLength(150)] public string? Logradouro { get; set; }
    [MaxLength(20)] public string? Numero { get; set; }
    [MaxLength(80)] public string? Complemento { get; set; }
    [MaxLength(80)] public string? Bairro { get; set; }
    [MaxLength(80)] public string? Cidade { get; set; }
    [MaxLength(2)] public string? Uf { get; set; }

    public override string ToString()
    {
        var partes = new[]
        {
            string.Join(", ", new[] { Logradouro, Numero }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Complemento,
            Bairro,
            string.Join("/", new[] { Cidade, Uf }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Cep
        };
        return string.Join(" - ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
