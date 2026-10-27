using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Fiscal;

/// <summary>Onde a NFS-e é emitida.</summary>
public enum AmbienteNfse
{
    /// <summary>Sem internet nem prefeitura: a nota é "autorizada" na hora, só para treinar (sem valor fiscal).</summary>
    Simulado,

    /// <summary>Teste oficial do provedor/prefeitura (sem valor fiscal).</summary>
    Homologacao,

    Producao
}

public enum RegimeTributario
{
    SimplesNacional,

    /// <summary>Microempreendedor individual.</summary>
    Mei,

    LucroPresumido,
    LucroReal
}

/// <summary>
/// Dados fiscais do buffet para a NFS-e (um registro só): prestador, serviço, ISS e o ambiente de emissão. O token do
/// provedor não fica aqui: fica na configuração da máquina (appsettings.Local.json), como a senha do e-mail.
/// </summary>
public class ConfiguracaoFiscal : Entidade, IValidatableObject
{
    /// <summary>Texto padrão da descrição do serviço. Campos: {{EVENTO}}, {{CODIGO}}, {{DATA}}, {{CONVIDADOS}}, {{LOCAL}}, {{CONTRATANTE}}.</summary>
    public const string DiscriminacaoPadrao =
        "Prestação de serviços de buffet para o evento {{EVENTO}} ({{CODIGO}}), realizado em {{DATA}} para {{CONVIDADOS}} convidados, em {{LOCAL}}.";

    public AmbienteNfse Ambiente { get; set; } = AmbienteNfse.Simulado;

    /// <summary>CNPJ do buffet (só dígitos).</summary>
    [MaxLength(14)]
    public string? Cnpj { get; set; }

    [MaxLength(20)]
    public string? InscricaoMunicipal { get; set; }

    /// <summary>Código IBGE do município do buffet (7 dígitos, ex.: 3550308 = São Paulo).</summary>
    [MaxLength(7)]
    public string? CodigoMunicipio { get; set; }

    public RegimeTributario Regime { get; set; } = RegimeTributario.SimplesNacional;

    /// <summary>Item da lista de serviços da LC 116/2003 (17.11 = organização de festas e recepções; bufê).</summary>
    [Required(ErrorMessage = "Informe o item da lista de serviços.")]
    [MaxLength(10)]
    public string ItemListaServico { get; set; } = "17.11";

    /// <summary>Código do serviço na tabela da prefeitura (quando ela exige).</summary>
    [MaxLength(20)]
    public string? CodigoTributarioMunicipio { get; set; }

    [MaxLength(10)]
    public string? CodigoCnae { get; set; }

    /// <summary>Alíquota do ISS em % (2% a 5%).</summary>
    [Range(0, 5, ErrorMessage = "A alíquota do ISS deve estar entre 0% e 5%.")]
    public decimal AliquotaIss { get; set; } = 5;

    /// <summary>O tomador (cliente) retém o ISS.</summary>
    public bool IssRetido { get; set; }

    [MaxLength(2000)]
    public string Discriminacao { get; set; } = DiscriminacaoPadrao;

    public bool OptanteSimplesNacional => Regime is RegimeTributario.SimplesNacional or RegimeTributario.Mei;

    /// <summary>O que falta para emitir (vazio = pronto).</summary>
    public IReadOnlyList<string> Pendencias()
    {
        var faltam = new List<string>();
        if (string.IsNullOrWhiteSpace(Cnpj))
            faltam.Add("CNPJ do buffet");
        if (string.IsNullOrWhiteSpace(InscricaoMunicipal))
            faltam.Add("inscrição municipal");
        if (CodigoMunicipio is not { Length: 7 })
            faltam.Add("código IBGE do município");
        if (string.IsNullOrWhiteSpace(ItemListaServico))
            faltam.Add("item da lista de serviços");
        return faltam;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Cnpj) && !Documento.CnpjValido(Cnpj))
            yield return new ValidationResult("CNPJ inválido.", [nameof(Cnpj)]);
        if (!string.IsNullOrWhiteSpace(CodigoMunicipio) && (CodigoMunicipio.Length != 7 || !CodigoMunicipio.All(char.IsDigit)))
            yield return new ValidationResult("O código IBGE do município tem 7 dígitos (ex.: 3550308).", [nameof(CodigoMunicipio)]);
        if (string.IsNullOrWhiteSpace(Discriminacao))
            yield return new ValidationResult("Informe o texto da descrição do serviço.", [nameof(Discriminacao)]);
    }
}
