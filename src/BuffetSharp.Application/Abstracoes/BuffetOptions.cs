using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Application.Abstracoes;

/// <summary>
/// Parâmetros do negócio (seção "Buffet" do appsettings.json).
/// </summary>
public sealed class BuffetOptions
{
    public const string Secao = "Buffet";

    public string NomeEmpresa { get; set; } = "BuffetSharp Eventos";
    public string? Documento { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Endereco { get; set; }

    /// <summary>Tempo de montagem/desmontagem exigido entre dois eventos no mesmo espaço.</summary>
    public int IntervaloMinimoEntreEventosMinutos { get; set; } = 120;

    /// <summary>
    /// Descanso mínimo da equipe entre o fim de um trabalho e o início do próximo (0 = sem exigência). O intervalo entre eventos
    /// acima também vale para a equipe, como tempo de deslocamento.
    /// </summary>
    public int DescansoMinimoEquipeHoras { get; set; } = 11;

    /// <summary>Dias que o primeiro da lista de espera tem para responder ao aviso de data liberada, antes de a vez passar.</summary>
    public int DiasRespostaEspera { get; set; } = 2;

    /// <summary>Link para o cliente avaliar o buffet no Google (enviado aos promotores da pesquisa de satisfação); vazio desliga.</summary>
    public string? LinkAvaliacaoGoogle { get; set; }

    public int DiasValidadeOrcamento { get; set; } = 15;

    /// <summary>Foro de eleição citado no contrato.</summary>
    public string CidadeForo { get; set; } = "São Paulo/SP";

    /// <summary>Antecedência para o cliente confirmar o número final de convidados (cláusula do contrato).</summary>
    public int DiasConfirmacaoConvidados { get; set; } = 10;

    /// <summary>Degustações simultâneas permitidas (mesmo horário, com tolerância de 1 hora).</summary>
    public int LimiteDegustacoesPorHorario { get; set; } = 3;

    /// <summary>
    /// Alta de preço (em %) sobre o último preço pago acima da qual o pedido, a entrada e a NF-e pedem confirmação antes de gravar
    /// (0 desliga o aviso).
    /// </summary>
    public decimal LimiteAltaPrecoCompra { get; set; } = 10;

    /// <summary>
    /// Desconto máximo (%) que cada perfil pode dar sozinho, no orçamento (desconto efetivo sobre a tabela) e no recebimento;
    /// o limite do cadastro do usuário, quando informado, vale no lugar deste.
    /// </summary>
    public AlcadaDescontoOptions AlcadaDesconto { get; set; } = new();
}

/// <summary>Desconto máximo (%) de cada perfil, para quem não tem um limite próprio no cadastro de usuários.</summary>
public sealed class AlcadaDescontoOptions
{
    public decimal Administrador { get; set; } = 100;
    public decimal Comercial { get; set; } = 5;
    public decimal Financeiro { get; set; } = 5;
    public decimal Operacional { get; set; }

    public decimal Limite(PerfilUsuario perfil) => perfil switch
    {
        PerfilUsuario.Administrador => Administrador,
        PerfilUsuario.Comercial => Comercial,
        PerfilUsuario.Financeiro => Financeiro,
        _ => Operacional
    };
}
