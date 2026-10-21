using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Cadastros;
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
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int CategoriaCardapioId { get; set; }
    public bool Ativo { get; set; } = true;
    public List<IngredienteInput> Ingredientes { get; set; } = [];
}

public sealed class PacoteInput
{
    public int Id { get; set; }
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal PrecoPorPessoa { get; set; }
    public int MinimoConvidados { get; set; } = 1;
    public int IdadeMaximaIsento { get; set; } = RegraFaixasEtarias.Padrao.IdadeMaximaIsento;
    public int IdadeMaximaMeia { get; set; } = RegraFaixasEtarias.Padrao.IdadeMaximaMeia;
    public decimal PercentualMeia { get; set; } = RegraFaixasEtarias.Padrao.PercentualMeia;
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
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public int ClienteId { get; set; }
    public int TipoEventoId { get; set; }
    public int? EspacoId { get; set; }
    public int? PacoteId { get; set; }
    public int? VendedorId { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly HoraInicio { get; set; } = new(19, 0);
    public TimeOnly HoraFim { get; set; } = new(23, 59);
    /// <summary>Total de pessoas (inteira + meia + isentos).</summary>
    public int Convidados { get; set; } = 100;
    public int ConvidadosMeia { get; set; }
    public int ConvidadosIsentos { get; set; }
    public DateOnly ValidoAte { get; set; }

    /// <summary>Quando nulo, usa o preço de tabela do pacote.</summary>
    public decimal? ValorPorPessoa { get; set; }

    /// <summary>Percentual do valor por pessoa cobrado na meia; quando nulo, usa o do pacote.</summary>
    public decimal? PercentualMeia { get; set; }

    /// <summary>Idades da regra (somente leitura: vêm do pacote).</summary>
    public int IdadeMaximaIsento { get; set; } = RegraFaixasEtarias.Padrao.IdadeMaximaIsento;
    public int IdadeMaximaMeia { get; set; } = RegraFaixasEtarias.Padrao.IdadeMaximaMeia;

    public QuantidadesConvidados QuantidadesPorFaixa => new(Convidados, ConvidadosMeia, ConvidadosIsentos);
    public RegraFaixasEtarias RegraFaixas =>
        new(IdadeMaximaIsento, IdadeMaximaMeia, PercentualMeia ?? RegraFaixasEtarias.Padrao.PercentualMeia);

    /// <summary>Quando nulo, usa o valor de locação do espaço.</summary>
    public decimal? ValorEspaco { get; set; }

    public decimal PercentualDesconto { get; set; }
    public int? CondicaoPagamentoId { get; set; }
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
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public int TipoEventoId { get; set; }
    public int EspacoId { get; set; }
    public int? VendedorId { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly HoraInicio { get; set; } = new(19, 0);
    public TimeOnly HoraFim { get; set; } = new(23, 59);

    /// <summary>Total de pessoas (inteira + meia + isentos).</summary>
    public int Convidados { get; set; } = 100;
    public int ConvidadosMeia { get; set; }
    public int ConvidadosIsentos { get; set; }
    public QuantidadesConvidados QuantidadesPorFaixa => new(Convidados, ConvidadosMeia, ConvidadosIsentos);
    public decimal ValorTotal { get; set; }
    public int? CondicaoPagamentoId { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class PlanoPagamentoInput
{
    public int Quantidade { get; set; } = 1;
    public DateOnly PrimeiroVencimento { get; set; }
    public decimal ValorEntrada { get; set; }
    public DateOnly? VencimentoEntrada { get; set; }
    public int? FormaPagamentoId { get; set; }

    /// <summary>Dias entre as parcelas; nulo ou zero = mensais (mesmo dia de cada mês).</summary>
    public int? IntervaloDias { get; set; }

    /// <summary>Condição usada para montar o plano (fica registrada no evento).</summary>
    public int? CondicaoPagamentoId { get; set; }
}

/// <summary>Entrada de estoque de uma compra, na unidade do fornecedor (embalagem) ou na unidade do estoque.</summary>
public sealed class EntradaEstoqueInput
{
    public int ProdutoId { get; set; }
    public int? FornecedorId { get; set; }

    /// <summary>Embalagem de compra (nula = unidade do estoque).</summary>
    public int? ProdutoEmbalagemId { get; set; }

    /// <summary>Quantidade na unidade de compra.</summary>
    public decimal Quantidade { get; set; }

    /// <summary>Custo de uma unidade de compra.</summary>
    public decimal CustoUnitario { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Lança o valor da compra (quantidade × custo) em contas a pagar, na mesma gravação da entrada.</summary>
    public bool GerarContaPagar { get; set; }

    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;

    /// <summary>Nota fiscal ou boleto do fornecedor.</summary>
    public string? Documento { get; set; }

    /// <summary>Vencimento da conta (ou da primeira parcela).</summary>
    public DateOnly? Vencimento { get; set; }

    /// <summary>1 = conta única; mais de 1 = compra parcelada (parcelamento PAR-xxxxx).</summary>
    public int Parcelas { get; set; } = 1;

    /// <summary>Dias entre as parcelas; nulo ou zero = mensais (mesmo dia de cada mês).</summary>
    public int? IntervaloDias { get; set; }

    public decimal ValorCompra => Math.Round(Quantidade * CustoUnitario, 2);
}

/// <summary>Recebimento ou pagamento de uma ou mais contas da mesma pessoa, com uma ou mais formas.</summary>
public sealed class BaixaInput
{
    public TipoBaixa Tipo { get; set; }
    public DateOnly Data { get; set; }
    public string? Observacao { get; set; }
    public List<BaixaTituloInput> Titulos { get; set; } = [];
    public List<BaixaFormaInput> Formas { get; set; } = [];
}

public sealed class BaixaTituloInput
{
    /// <summary>Id da parcela (recebimento) ou da conta a pagar (pagamento).</summary>
    public int TituloId { get; set; }

    /// <summary>Quanto abater do título; menor que o valor dele = pagamento parcial.</summary>
    public decimal Valor { get; set; }

    public decimal Juros { get; set; }
    public decimal Desconto { get; set; }
}

public sealed class BaixaFormaInput
{
    public int FormaPagamentoId { get; set; }
    public decimal Valor { get; set; }
    public int? ContaFinanceiraId { get; set; }
    public int? CartaoCreditoId { get; set; }
    public int Parcelas { get; set; } = 1;
}

/// <summary>O que um estorno vai desfazer, para o usuário confirmar.</summary>
public sealed record BaixaResumo(int Id, TipoBaixa Tipo, DateOnly Data, decimal Total, IReadOnlyList<string> Titulos,
    IReadOnlyList<string> Formas, int FaturasGeradas);

/// <summary>Como uma conta foi paga (ou uma parcela recebida): o pagamento inteiro, com as outras contas quitadas junto.</summary>
public sealed record DetalhePagamento(int BaixaId, TipoBaixa Tipo, DateOnly Data, decimal Total, string? Usuario, DateTime RegistradoEm,
    string? Observacao, IReadOnlyList<PagamentoTitulo> Titulos, IReadOnlyList<PagamentoForma> Formas);

/// <summary>Conta quitada no pagamento: valor abatido + juros − desconto = pago.</summary>
/// <param name="Restante">No pagamento parcial, para onde foi o restante (ex.: "R$ 300,00 ficou para \"Boleto (saldo)\"").</param>
public sealed record PagamentoTitulo(int TituloId, string Descricao, decimal Abatido, decimal Juros, decimal Desconto, decimal Pago,
    string? Restante);

/// <summary>Forma usada no pagamento.</summary>
/// <param name="Onde">Conta ou caixa movimentado, ou o cartão com o número de parcelas.</param>
/// <param name="Faturas">Parcelas da fatura do cartão geradas em contas a pagar (vencimento, valor e situação).</param>
public sealed record PagamentoForma(string Forma, TipoFormaPagamento Tipo, decimal Valor, string? Onde, IReadOnlyList<string> Faturas);

/// <summary>Lançamento avulso numa conta: aporte, retirada, tarifa, rendimento...</summary>
public sealed class MovimentoInput
{
    public int ContaFinanceiraId { get; set; }
    public DateOnly Data { get; set; }
    public bool Entrada { get; set; }
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

public sealed class TransferenciaInput
{
    public int OrigemId { get; set; }
    public int DestinoId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
}

public sealed class FechamentoInput
{
    public int ContaFinanceiraId { get; set; }
    public DateOnly Ate { get; set; }
    public decimal SaldoInformado { get; set; }
    public string? Observacao { get; set; }
}

public sealed class ContaPagarInput
{
    public int Id { get; set; }
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public decimal Valor { get; set; }
    public DateOnly Vencimento { get; set; }
    public int? FornecedorId { get; set; }
    public int? EventoId { get; set; }

    /// <summary>Nota fiscal, boleto ou fatura do fornecedor.</summary>
    public string? Documento { get; set; }
    public string? Observacao { get; set; }
    public StatusTitulo Status { get; set; } = StatusTitulo.Aberto;

    /// <summary>Para lançamentos recorrentes (ex.: aluguel): repete por N meses.</summary>
    public int Repeticoes { get; set; } = 1;

    /// <summary>Somente leitura: parcelamento do qual a conta é parcela ("PAR-00001") ou no qual foi juntada.</summary>
    public string? Parcelamento { get; set; }

    /// <summary>Somente leitura: quando, quanto e com que forma foi pago.</summary>
    public DateOnly? DataPagamento { get; set; }
    public decimal? ValorPago { get; set; }
    public string? FormaPagamento { get; set; }
    public TipoFormaPagamento? TipoFormaPagamento { get; set; }
}

/// <summary>
/// Compra parcelada (valor total, sem contas) ou contas em aberto do mesmo fornecedor juntadas e reparceladas
/// (<see cref="ContasAgrupadasIds"/>; o total é a soma delas e categoria, fornecedor e evento vêm delas).
/// </summary>
public sealed class ParcelamentoInput
{
    public string Descricao { get; set; } = string.Empty;
    public CategoriaDespesa Categoria { get; set; } = CategoriaDespesa.Insumos;
    public int? FornecedorId { get; set; }
    public int? EventoId { get; set; }
    public string? Documento { get; set; }
    public string? Observacao { get; set; }
    public decimal ValorTotal { get; set; }
    public List<int> ContasAgrupadasIds { get; set; } = [];
    public PlanoPagamentoInput Plano { get; set; } = new();
}

/// <summary>Resumo de um parcelamento para confirmar o desfazer.</summary>
public sealed record ParcelamentoInfo(int Id, string Codigo, string Descricao, decimal ValorTotal, int Parcelas, int ContasAgrupadas,
    bool PodeDesfazer);

public sealed class UsuarioInput
{
    public int Id { get; set; }
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Comercial;
    public bool Ativo { get; set; } = true;
    public string? NovaSenha { get; set; }
    public decimal PercentualComissao { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }

    /// <summary>Somente leitura: login bloqueado por senhas erradas até este horário.</summary>
    public DateTime? BloqueadoAte { get; set; }
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
    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
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

public sealed class LeadInput
{
    public int Id { get; set; }

    /// <summary>Versão lida do banco (concorrência otimista); nula em registros novos.</summary>
    public int? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public OrigemLead Origem { get; set; } = OrigemLead.Instagram;
    public string? DetalheOrigem { get; set; }
    public int? TipoEventoId { get; set; }
    public DateOnly? DataPrevista { get; set; }
    public int? Convidados { get; set; }
    public int? ResponsavelId { get; set; }
    public DateOnly? ProximoContato { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Somente leitura.</summary>
    public StatusLead Status { get; set; } = StatusLead.Novo;
}

/// <summary>Como o lead vira cliente: cadastrando um cliente novo com os dados dele ou ligando a um cliente existente.</summary>
public sealed class ConversaoLeadInput
{
    public int LeadId { get; set; }

    /// <summary>Cliente já cadastrado; nulo para cadastrar um novo com os dados do lead.</summary>
    public int? ClienteExistenteId { get; set; }
}

/// <summary>Reajuste das tabelas de preço (novo ou simulação).</summary>
public sealed class ReajusteInput
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
    public DateOnly VigenciaEm { get; set; }
    public bool Pacotes { get; set; } = true;
    public bool Servicos { get; set; } = true;
    public bool Espacos { get; set; }
    public ArredondamentoPreco Arredondamento { get; set; } = ArredondamentoPreco.Centavos;

    public AlcanceReajuste Alcance =>
        (Pacotes ? AlcanceReajuste.Pacotes : 0) | (Servicos ? AlcanceReajuste.Servicos : 0) | (Espacos ? AlcanceReajuste.Espacos : 0);
}

/// <summary>Preço de um item antes e depois do reajuste (simulação ou histórico).</summary>
public sealed record ItemReajusteInfo(TipoItemReajuste Tipo, int ItemId, string Nome, decimal ValorAnterior, decimal ValorNovo)
{
    public decimal Diferenca => ValorNovo - ValorAnterior;
}

public sealed record ReajusteResumo(int Id, string Descricao, decimal Percentual, DateOnly VigenciaEm, AlcanceReajuste Alcance,
    ArredondamentoPreco Arredondamento, StatusReajuste Status, int Itens, DateTime? AplicadoEm, DateTime? DesfeitoEm, string? Usuario);

/// <summary>Resultado do desfazer: os itens alterados à mão depois do reajuste não voltam ao preço anterior.</summary>
public sealed record ResultadoDesfazerReajuste(int Restaurados, IReadOnlyList<string> Mantidos);
