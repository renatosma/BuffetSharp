using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Application.Modelos;

// Modelos de entrada (comandos) trocados entre as telas e os serviços.
// Mantêm as entidades de nível 3 encapsuladas: a tela nunca altera o domínio diretamente.

public sealed class IngredienteInput
{
    public int ProdutoId { get; set; }
    public string Produto { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public decimal CustoUnitario { get; set; }
    public decimal QuantidadePorPessoa { get; set; }
    public decimal CustoPorPessoa => Math.Round(QuantidadePorPessoa * CustoUnitario, 4);
}

public sealed class ItemCardapioInput
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int CategoriaCardapioId { get; set; }
    public bool Ativo { get; set; } = true;
    public List<IngredienteInput> Ingredientes { get; set; } = [];
}

public sealed class PacoteInput
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal PrecoPorPessoa { get; set; }
    public int MinimoConvidados { get; set; } = 1;
    public bool Ativo { get; set; } = true;
    public List<int> ItensCardapioIds { get; set; } = [];
}

public sealed class ServicoItemInput
{
    public int ServicoAdicionalId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
    public decimal Total => Math.Round(Quantidade * ValorUnitario, 2);
}

public sealed class OrcamentoInput
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int TipoEventoId { get; set; }
    public int? EspacoId { get; set; }
    public int? PacoteId { get; set; }
    public int? VendedorId { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly HoraInicio { get; set; } = new(19, 0);
    public TimeOnly HoraFim { get; set; } = new(23, 59);
    public int Convidados { get; set; } = 100;
    public DateOnly ValidoAte { get; set; }

    /// <summary>Quando nulo, usa o preço de tabela do pacote.</summary>
    public decimal? ValorPorPessoa { get; set; }

    /// <summary>Quando nulo, usa o valor de locação do espaço.</summary>
    public decimal? ValorEspaco { get; set; }

    public decimal PercentualDesconto { get; set; }
    public string? Observacoes { get; set; }
    public List<ServicoItemInput> Servicos { get; set; } = [];

    // Somente leitura (preenchidos ao carregar)
    public string Codigo { get; set; } = "(novo)";
    public StatusOrcamento Status { get; set; } = StatusOrcamento.Rascunho;
    public string? MotivoRecusa { get; set; }
    public int? EventoId { get; set; }
    public List<EnvioPropostaInfo> Envios { get; set; } = [];
    public bool PodeEditar => Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado;
}

public sealed record EnvioPropostaInfo(CanalEnvio Canal, string Destinatario, string? Usuario, DateTime EnviadoEm);

public sealed class EventoInput
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public int TipoEventoId { get; set; }
    public int EspacoId { get; set; }
    public int? VendedorId { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly HoraInicio { get; set; } = new(19, 0);
    public TimeOnly HoraFim { get; set; } = new(23, 59);
    public int Convidados { get; set; } = 100;
    public decimal ValorTotal { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class PlanoPagamentoInput
{
    public int Quantidade { get; set; } = 1;
    public DateOnly PrimeiroVencimento { get; set; }
    public decimal ValorEntrada { get; set; }
    public DateOnly? VencimentoEntrada { get; set; }
    public int? FormaPagamentoId { get; set; }
}

public sealed class PagamentoInput
{
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }
    public int? FormaPagamentoId { get; set; }
}

public sealed class ContaPagarInput
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public decimal Valor { get; set; }
    public DateOnly Vencimento { get; set; }
    public int? FornecedorId { get; set; }
    public int? EventoId { get; set; }
    public string? Observacao { get; set; }
    public StatusTitulo Status { get; set; } = StatusTitulo.Aberto;

    /// <summary>Para lançamentos recorrentes (ex.: aluguel): repete por N meses.</summary>
    public int Repeticoes { get; set; } = 1;
}

public sealed class UsuarioInput
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Comercial;
    public bool Ativo { get; set; } = true;
    public string? NovaSenha { get; set; }
    public decimal PercentualComissao { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
}

/// <summary>Evento completo para a tela de detalhes, com indicadores de resultado.</summary>
public sealed class EventoDetalhe
{
    public required Evento Evento { get; init; }
    public decimal CustoInsumosEstimado { get; init; }
    public decimal DespesasVinculadas { get; init; }
    public decimal ResultadoEstimado =>
        Evento.ValorTotal - CustoInsumosEstimado - Evento.TotalEquipe - Evento.CustoMateriais - DespesasVinculadas;
    public decimal MargemEstimada => Evento.ValorTotal == 0 ? 0 : Math.Round(ResultadoEstimado / Evento.ValorTotal * 100, 1);
}

public sealed record ResultadoRealizacao(IReadOnlyList<string> Avisos);

public sealed class DegustacaoInput
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int? PacoteId { get; set; }
    public int? OrcamentoId { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly Hora { get; set; } = new(15, 0);
    public int Pessoas { get; set; } = 2;
    public string? Observacoes { get; set; }
    public DateTime DataHora => Data.ToDateTime(Hora);
}

public sealed class AssinaturaContratoInput
{
    public string NomeSignatario { get; set; } = string.Empty;
    public string? DocumentoSignatario { get; set; }
    public byte[]? AssinaturaCliente { get; set; }
    public string? NomeRepresentante { get; set; }
    public byte[]? AssinaturaEmpresa { get; set; }
}

/// <summary>E-mail pronto para revisão na tela antes do envio.</summary>
public sealed class EmailInput
{
    public string Para { get; set; } = string.Empty;
    public string Assunto { get; set; } = string.Empty;

    /// <summary>Mensagem livre exibida antes dos detalhes (texto simples; quebras de linha viram parágrafos).</summary>
    public string? Mensagem { get; set; }

    public AnexoEmail? Anexo { get; set; }
}

/// <summary>Mensagem para WhatsApp: o link abre o aplicativo/WhatsApp Web com o texto preenchido.</summary>
public sealed record MensagemWhatsApp(string Telefone, string Texto)
{
    /// <summary>Telefone no formato internacional (somente dígitos, com DDI 55 quando ausente).</summary>
    public string TelefoneInternacional
    {
        get
        {
            var digitos = new string(Telefone.Where(char.IsDigit).ToArray());
            return digitos.Length is 10 or 11 ? "55" + digitos : digitos;
        }
    }

    public string Link => $"https://wa.me/{TelefoneInternacional}?text={Uri.EscapeDataString(Texto)}";
}
