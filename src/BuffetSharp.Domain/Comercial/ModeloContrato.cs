using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Comercial;

/// <summary>
/// Texto-base do contrato com campos entre chaves duplas (ex.: {{CLIENTE_NOME}}, {{DATA_EVENTO}}), trocados pelos dados
/// do evento na emissão. Pode valer para um tipo de evento (ex.: casamento) ou para todos; um deles é o padrão.
/// </summary>
public class ModeloContrato : Entidade, IAtivavel, IValidatableObject, IPadraoUnico
{
    [Required(ErrorMessage = "Informe o nome do modelo.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Tipo de evento a que o modelo se aplica (vazio = todos).</summary>
    public int? TipoEventoId { get; set; }
    public TipoEvento? TipoEvento { get; set; }

    /// <summary>Modelo usado quando não há um específico para o tipo do evento. Só um modelo é o padrão.</summary>
    public bool Padrao { get; set; }

    [Required(ErrorMessage = "Informe o texto do modelo.")]
    public string Conteudo { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Conteudo))
            yield break;
        if (Conteudo.Trim().Length < 100)
            yield return new ValidationResult("O texto do modelo está muito curto (mínimo de 100 caracteres).", [nameof(Conteudo)]);

        var desconhecidos = CamposContrato.Desconhecidos(Conteudo);
        if (desconhecidos.Count > 0)
            yield return new ValidationResult(
                $"Campo(s) desconhecido(s) no texto: {string.Join(", ", desconhecidos.Select(CamposContrato.Marcador))}. " +
                "Use somente os campos da lista.", [nameof(Conteudo)]);
        if (Padrao && !Ativo)
            yield return new ValidationResult("O modelo padrão precisa estar ativo.", [nameof(Padrao)]);
    }

    public override string ToString() => Nome;
}

/// <summary>Campos aceitos nos modelos de contrato e a troca deles pelos valores.</summary>
public static partial class CamposContrato
{
    /// <summary>Numeração automática das cláusulas: cada {{N}} recebe o número seguinte (1, 2, 3...).</summary>
    public const string NumeroClausula = "N";

    public static readonly IReadOnlyList<(string Nome, string Descricao)> Todos =
    [
        (NumeroClausula, "Número da cláusula: 1, 2, 3... na ordem do texto (use \"CLÁUSULA {{N}}ª - TÍTULO\")"),
        ("CONTRATADA", "Qualificação da empresa: nome, CNPJ, endereço, telefone e e-mail"),
        ("EMPRESA_NOME", "Nome da empresa"),
        ("CONTRATANTE", "Qualificação do cliente: nome, CPF/CNPJ, endereço, telefone e e-mail"),
        ("CLIENTE_NOME", "Nome do cliente"),
        ("CLIENTE_DOCUMENTO", "CPF ou CNPJ do cliente"),
        ("CLIENTE_ENDERECO", "Endereço principal do cliente"),
        ("CLIENTE_TELEFONE", "Celular (ou telefone) do cliente"),
        ("CLIENTE_EMAIL", "E-mail do cliente"),
        ("EVENTO_TITULO", "Título do evento"),
        ("EVENTO_CODIGO", "Código do evento (EVT-00000)"),
        ("TIPO_EVENTO", "Tipo de evento (casamento, aniversário...)"),
        ("DATA_EVENTO", "Data do evento (dd/mm/aaaa)"),
        ("DATA_EVENTO_EXTENSO", "Data do evento por extenso, com o dia da semana"),
        ("HORA_INICIO", "Horário de início"),
        ("HORA_FIM", "Horário de término"),
        ("ESPACO", "Nome do espaço (salão)"),
        ("CAPACIDADE", "Capacidade máxima do espaço"),
        ("LOCAL_ENDERECO", "Endereço da festa (do espaço; se vazio, o da empresa)"),
        ("CONVIDADOS", "Quantidade de convidados contratada"),
        ("DIAS_CONFIRMACAO", "Dias de antecedência para confirmar os convidados"),
        ("EXCEDENTES", "Regra de cobrança dos convidados excedentes"),
        ("PACOTE", "Nome do pacote"),
        ("VALOR_POR_PESSOA", "Valor por pessoa"),
        ("CARDAPIO", "Cardápio do pacote, por seção"),
        ("SERVICOS", "Serviços adicionais com valores"),
        ("VALOR_TOTAL", "Valor total do evento"),
        ("VALOR_TOTAL_EXTENSO", "Valor total por extenso"),
        ("CONDICAO_PAGAMENTO", "Condição de pagamento (ex.: 30% de entrada + 5x mensais)"),
        ("PARCELAS", "Lista das parcelas com valores e vencimentos"),
        ("FORO", "Comarca do foro"),
        ("CIDADE", "Cidade da empresa (local da assinatura)"),
        ("DATA_HOJE", "Data da emissão por extenso")
    ];

    private static readonly HashSet<string> Nomes = Todos.Select(c => c.Nome).ToHashSet(StringComparer.Ordinal);

    public static string Marcador(string nome) => "{{" + nome + "}}";

    /// <summary>Campos escritos no texto que não existem na lista.</summary>
    public static IReadOnlyList<string> Desconhecidos(string texto) =>
        Campo().Matches(texto).Select(m => m.Groups[1].Value.ToUpperInvariant()).Where(n => !Nomes.Contains(n)).Distinct().ToList();

    /// <summary>
    /// Troca cada campo pelo valor informado. {{N}} recebe a numeração das cláusulas; campos desconhecidos ficam
    /// como estão (visíveis na revisão). Linhas em branco repetidas são reduzidas a uma.
    /// </summary>
    public static string Substituir(string modelo, IReadOnlyDictionary<string, string> valores)
    {
        var clausula = 0;
        var texto = Campo().Replace(modelo.ReplaceLineEndings("\n"), m =>
        {
            var nome = m.Groups[1].Value.ToUpperInvariant();
            if (nome == NumeroClausula)
                return (++clausula).ToString();
            return valores.TryGetValue(nome, out var valor) ? valor : m.Value;
        });
        texto = string.Join('\n', texto.Split('\n').Select(l => l.TrimEnd()));
        return LinhasEmBranco().Replace(texto, "\n\n").Trim();
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}")]
    private static partial Regex Campo();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex LinhasEmBranco();
}
