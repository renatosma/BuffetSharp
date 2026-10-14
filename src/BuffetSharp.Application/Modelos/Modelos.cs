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
    public bool PodeEditar => Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado;
}

public sealed class EventoInput
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public int TipoEventoId { get; set; }
    public int EspacoId { get; set; }
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
    public DateTime? UltimoAcessoEm { get; set; }
}

/// <summary>Evento completo para a tela de detalhes, com indicadores de resultado.</summary>
public sealed class EventoDetalhe
{
    public required Evento Evento { get; init; }
    public decimal CustoInsumosEstimado { get; init; }
    public decimal DespesasVinculadas { get; init; }
    public decimal ResultadoEstimado => Evento.ValorTotal - CustoInsumosEstimado - Evento.TotalEquipe - DespesasVinculadas;
    public decimal MargemEstimada => Evento.ValorTotal == 0 ? 0 : Math.Round(ResultadoEstimado / Evento.ValorTotal * 100, 1);
}

public sealed record ResultadoRealizacao(IReadOnlyList<string> Avisos);
