using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Domain.Financeiro;

public enum StatusTitulo
{
    Aberto,
    Pago,
    Cancelado
}

public enum CategoriaDespesa
{
    Insumos,
    Pessoal,
    Aluguel,
    Servicos,
    Impostos,
    Manutencao,
    Marketing,
    Outros,

    /// <summary>Comissões de vendedores (geradas pelo fechamento de comissões).</summary>
    Comissoes,

    /// <summary>Locação de louças, enxoval e mobiliário de terceiros para um evento.</summary>
    LocacaoMateriais,

    /// <summary>
    /// Parcela da fatura de um cartão de crédito usado para pagar outras contas. A despesa já foi contada na conta
    /// original; a fatura só representa a saída de dinheiro (não entra nos relatórios de despesa).
    /// </summary>
    FaturaCartao
}

/// <summary>
/// Comportamento comum de contas a receber (parcelas) e contas a pagar.
/// </summary>
public abstract class TituloFinanceiro : Entidade
{
    /// <summary>Sufixo dos títulos criados com o saldo de um pagamento parcial.</summary>
    public const string SufixoSaldo = " (saldo)";

    public string Descricao { get; protected set; } = string.Empty;
    public decimal Valor { get; protected set; }
    public DateOnly Vencimento { get; protected set; }
    public StatusTitulo Status { get; protected set; } = StatusTitulo.Aberto;
    public DateOnly? DataPagamento { get; protected set; }
    public decimal? ValorPago { get; protected set; }
    public int? FormaPagamentoId { get; protected set; }
    public FormaPagamento? FormaPagamento { get; protected set; }
    public string? Observacao { get; set; }

    public bool EstaVencido(DateOnly hoje) => Status == StatusTitulo.Aberto && Vencimento < hoje;

    public int DiasEmAtraso(DateOnly hoje) => EstaVencido(hoje) ? hoje.DayNumber - Vencimento.DayNumber : 0;

    /// <param name="valorPago">Pode ser maior que o valor (juros/multa) ou menor (desconto).</param>
    public void RegistrarPagamento(DateOnly data, decimal valorPago, int? formaPagamentoId)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente títulos em aberto podem ser pagos.");
        Validador.Garantir(valorPago > 0, "O valor pago deve ser maior que zero.");
        Status = StatusTitulo.Pago;
        DataPagamento = data;
        ValorPago = valorPago;
        FormaPagamentoId = formaPagamentoId ?? FormaPagamentoId;
    }

    public void EstornarPagamento()
    {
        Validador.Garantir(Status == StatusTitulo.Pago, "Somente títulos pagos podem ser estornados.");
        Status = StatusTitulo.Aberto;
        DataPagamento = null;
        ValorPago = null;
    }

    public virtual void Cancelar()
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente títulos em aberto podem ser cancelados.");
        Status = StatusTitulo.Cancelado;
    }

    /// <summary>
    /// Pagamento parcial: este título fica com o valor abatido (e será quitado) e o restante passa para um novo
    /// título em aberto, com o mesmo vencimento. Assim cada título continua sendo pago uma única vez.
    /// </summary>
    public TituloFinanceiro Desmembrar(decimal valorAbatido)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente títulos em aberto podem ser pagos.");
        Validador.Garantir(valorAbatido > 0 && valorAbatido < Valor,
            $"O valor parcial deve ser maior que zero e menor que o valor do título ({Valor:C}).");
        var saldo = CriarSaldo(Valor - valorAbatido);
        Valor = valorAbatido;
        return saldo;
    }

    /// <summary>Desfaz um <see cref="Desmembrar"/>: o saldo ainda em aberto volta a fazer parte deste título.</summary>
    public void Reincorporar(TituloFinanceiro saldo)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto && saldo.Status == StatusTitulo.Aberto && saldo.GetType() == GetType(),
            "Somente o saldo em aberto pode voltar para o título.");
        Valor += saldo.Valor;
    }

    protected abstract TituloFinanceiro CriarSaldo(decimal valor);

    protected static string DescricaoSaldo(string descricao, int maximo) =>
        descricao.EndsWith(SufixoSaldo, StringComparison.Ordinal)
            ? descricao
            : descricao[..Math.Min(descricao.Length, maximo - SufixoSaldo.Length)] + SufixoSaldo;

    protected void DefinirValores(string descricao, decimal valor, DateOnly vencimento)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Informe a descrição.");
        Validador.Garantir(valor > 0, "O valor deve ser maior que zero.");
        Descricao = descricao.Trim();
        Valor = valor;
        Vencimento = vencimento;
    }
}

/// <summary>Conta a receber vinculada a um evento.</summary>
public class Parcela : TituloFinanceiro
{
    public const int TamanhoDescricao = 100;

    private Parcela()
    {
    }

    internal Parcela(Evento evento, int numero, decimal valor, DateOnly vencimento, int? formaPagamentoId, string descricao)
    {
        Evento = evento;
        Numero = numero;
        FormaPagamentoId = formaPagamentoId;
        DefinirValores(descricao, valor, vencimento);
    }

    public int EventoId { get; private set; }
    public Evento? Evento { get; private set; }
    public int Numero { get; private set; }

    protected override TituloFinanceiro CriarSaldo(decimal valor)
    {
        var saldo = new Parcela
        {
            EventoId = EventoId,
            Evento = Evento,
            Numero = Numero,
            FormaPagamentoId = FormaPagamentoId,
            Observacao = Observacao
        };
        saldo.DefinirValores(DescricaoSaldo(Descricao, TamanhoDescricao), valor, Vencimento);
        return saldo;
    }
}

/// <summary>Despesa: compra de insumos, folha de freelancers, aluguel, impostos...</summary>
public class ContaPagar : TituloFinanceiro
{
    public const int TamanhoDescricao = 150;
    public const int TamanhoDocumento = 60;

    private ContaPagar()
    {
    }

    /// <summary>Parcela de um parcelamento (compra parcelada ou contas agrupadas).</summary>
    internal ContaPagar(ParcelamentoPagar parcelamento, ParcelaPlanejada parcela, int? formaPagamentoId)
        : this(Numerar(parcelamento.Descricao, parcela), parcelamento.Categoria, parcela.Valor, parcela.Vencimento,
            parcelamento.FornecedorId, parcelamento.EventoId)
    {
        Parcelamento = parcelamento;
        NumeroParcela = parcela.Numero;
        Documento = parcelamento.Documento;
        FormaPagamentoId = formaPagamentoId;
        Observacao = parcelamento.Observacao;
    }

    public ContaPagar(string descricao, CategoriaDespesa categoria, decimal valor, DateOnly vencimento,
        int? fornecedorId = null, int? eventoId = null)
    {
        Alterar(descricao, categoria, valor, vencimento, fornecedorId, eventoId, null);
    }

    /// <summary>Parcela da fatura do cartão usado num pagamento.</summary>
    internal ContaPagar(BaixaForma origem, string descricao, decimal valor, DateOnly vencimento, int? fornecedorId)
        : this(descricao, CategoriaDespesa.FaturaCartao, valor, vencimento, fornecedorId)
    {
        OrigemBaixaForma = origem;
    }

    public CategoriaDespesa Categoria { get; private set; }
    public int? FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }
    public int? EventoId { get; private set; }
    public Evento? Evento { get; private set; }

    /// <summary>Pagamento com cartão de crédito que gerou esta parcela de fatura (nulo nas demais contas).</summary>
    public int? OrigemBaixaFormaId { get; private set; }
    public BaixaForma? OrigemBaixaForma { get; private set; }

    public bool EhFaturaCartao => OrigemBaixaFormaId is not null || OrigemBaixaForma is not null;

    /// <summary>Número da nota fiscal, boleto ou fatura do fornecedor.</summary>
    public string? Documento { get; private set; }

    /// <summary>Parcelamento do qual esta conta é parcela (nulo nas contas avulsas).</summary>
    public int? ParcelamentoId { get; private set; }
    public ParcelamentoPagar? Parcelamento { get; private set; }

    /// <summary>Número da parcela no parcelamento (0 = entrada).</summary>
    public int? NumeroParcela { get; private set; }

    /// <summary>Parcelamento que substituiu esta conta (ela fica cancelada até o parcelamento ser desfeito).</summary>
    public int? AgrupadaEmId { get; private set; }
    public ParcelamentoPagar? AgrupadaEm { get; private set; }

    public bool EhParcela => ParcelamentoId is not null || Parcelamento is not null;
    public bool FoiAgrupada => AgrupadaEmId is not null || AgrupadaEm is not null;

    /// <summary>Meio previsto para pagar a conta (o pagamento pode usar outro).</summary>
    public void DefinirFormaPagamento(int? formaPagamentoId)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente contas em aberto podem ser alteradas.");
        FormaPagamentoId = formaPagamentoId is > 0 ? formaPagamentoId : null;
    }

    public void DefinirDocumento(string? documento)
    {
        Validador.Garantir(documento is null || documento.Trim().Length <= TamanhoDocumento,
            $"O documento pode ter no máximo {TamanhoDocumento} caracteres.");
        Documento = string.IsNullOrWhiteSpace(documento) ? null : documento.Trim();
    }

    public void Alterar(string descricao, CategoriaDespesa categoria, decimal valor, DateOnly vencimento,
        int? fornecedorId, int? eventoId, string? observacao)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente contas em aberto podem ser alteradas.");
        if (EhFaturaCartao)
        {
            Validador.Garantir(valor == Valor && categoria == Categoria && fornecedorId == FornecedorId,
                "Na fatura do cartão só a descrição, o vencimento, o evento e a observação podem mudar. Para corrigir o valor, estorne o pagamento feito com o cartão.");
        }
        if (EhParcela)
            Validador.Garantir(fornecedorId == FornecedorId,
                "O fornecedor de uma parcela não pode mudar. Desfaça o parcelamento para refazê-lo com outro fornecedor.");
        DefinirValores(descricao, valor, vencimento);
        Categoria = categoria;
        FornecedorId = fornecedorId;
        EventoId = eventoId;
        Observacao = observacao;
    }

    public override void Cancelar()
    {
        Validador.Garantir(!EhFaturaCartao, "Esta conta é parcela da fatura de um cartão. Para desfazê-la, estorne o pagamento feito com o cartão.");
        base.Cancelar();
    }

    /// <summary>A conta passa a fazer parte de um parcelamento novo (fica cancelada, ligada a ele).</summary>
    internal void AgruparEm(ParcelamentoPagar parcelamento)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, $"'{Descricao}' não está em aberto.");
        Validador.Garantir(!EhFaturaCartao, $"'{Descricao}' é parcela da fatura de um cartão e não pode ser reparcelada.");
        Status = StatusTitulo.Cancelado;
        AgrupadaEm = parcelamento;
    }

    /// <summary>Desfaz o <see cref="AgruparEm"/>: a conta volta a ficar em aberto.</summary>
    internal void Desagrupar()
    {
        Validador.Garantir(FoiAgrupada && Status == StatusTitulo.Cancelado, $"'{Descricao}' não foi reparcelada.");
        Status = StatusTitulo.Aberto;
        AgrupadaEm = null;
        AgrupadaEmId = null;
    }

    private static string Numerar(string descricao, ParcelaPlanejada parcela)
    {
        var sufixo = parcela.Entrada ? " (entrada)" : $" ({parcela.Numero}/{parcela.Quantidade})";
        return descricao[..Math.Min(descricao.Length, TamanhoDescricao - sufixo.Length)] + sufixo;
    }

    protected override TituloFinanceiro CriarSaldo(decimal valor)
    {
        var saldo = new ContaPagar
        {
            Categoria = Categoria,
            FornecedorId = FornecedorId,
            Fornecedor = Fornecedor,
            EventoId = EventoId,
            Evento = Evento,
            OrigemBaixaFormaId = OrigemBaixaFormaId,
            OrigemBaixaForma = OrigemBaixaForma,
            Documento = Documento,
            ParcelamentoId = ParcelamentoId,
            Parcelamento = Parcelamento,
            NumeroParcela = NumeroParcela,
            FormaPagamentoId = FormaPagamentoId,
            Observacao = Observacao
        };
        saldo.DefinirValores(DescricaoSaldo(Descricao, TamanhoDescricao), valor, Vencimento);
        return saldo;
    }
}
