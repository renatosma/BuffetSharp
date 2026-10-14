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
    Outros
}

/// <summary>
/// Comportamento comum de contas a receber (parcelas) e contas a pagar.
/// </summary>
public abstract class TituloFinanceiro : Entidade
{
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

    public void Cancelar()
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente títulos em aberto podem ser cancelados.");
        Status = StatusTitulo.Cancelado;
    }

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
}

/// <summary>Despesa: compra de insumos, folha de freelancers, aluguel, impostos...</summary>
public class ContaPagar : TituloFinanceiro
{
    private ContaPagar()
    {
    }

    public ContaPagar(string descricao, CategoriaDespesa categoria, decimal valor, DateOnly vencimento,
        int? fornecedorId = null, int? eventoId = null)
    {
        Alterar(descricao, categoria, valor, vencimento, fornecedorId, eventoId, null);
    }

    public CategoriaDespesa Categoria { get; private set; }
    public int? FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }
    public int? EventoId { get; private set; }
    public Evento? Evento { get; private set; }

    public void Alterar(string descricao, CategoriaDespesa categoria, decimal valor, DateOnly vencimento,
        int? fornecedorId, int? eventoId, string? observacao)
    {
        Validador.Garantir(Status == StatusTitulo.Aberto, "Somente contas em aberto podem ser alteradas.");
        DefinirValores(descricao, valor, vencimento);
        Categoria = categoria;
        FornecedorId = fornecedorId;
        EventoId = eventoId;
        Observacao = observacao;
    }
}
