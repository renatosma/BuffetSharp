using System.ComponentModel.DataAnnotations;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Financeiro;

public enum TipoContaFinanceira
{
    /// <summary>Dinheiro em espécie (gaveta, cofre). Recebe as baixas em dinheiro.</summary>
    Caixa,
    ContaCorrente,
    Poupanca,
    Aplicacao
}

/// <summary>
/// Onde o dinheiro do buffet fica: o caixa (espécie) e as contas bancárias. O saldo é o inicial mais os movimentos
/// (recebimentos, pagamentos, transferências e lançamentos avulsos). Depois de conferida (fechamento do caixa ou
/// conciliação com o extrato do banco), a conta não aceita movimentos até a data do fechamento.
/// </summary>
public class ContaFinanceira : Entidade, IAtivavel, ICamposControlados
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public TipoContaFinanceira Tipo { get; set; } = TipoContaFinanceira.ContaCorrente;

    [MaxLength(60)]
    public string? Banco { get; set; }

    [MaxLength(20)]
    public string? Agencia { get; set; }

    [MaxLength(30)]
    public string? Numero { get; set; }

    [Range(-99_999_999, 99_999_999, ErrorMessage = "Saldo inicial inválido.")]
    public decimal SaldoInicial { get; set; }

    /// <summary>Data a partir da qual a conta é controlada pelo sistema (o saldo inicial é o desse dia).</summary>
    public DateOnly DataSaldoInicial { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Último dia conferido (fechamento do caixa ou conciliação). Mantido pelo sistema.</summary>
    public DateOnly? FechadaAte { get; set; }

    public bool Ativo { get; set; } = true;

    public bool EhCaixa => Tipo == TipoContaFinanceira.Caixa;

    public IReadOnlyCollection<string> CamposControladosPeloSistema => [nameof(FechadaAte)];

    public override string ToString() => Nome;

    /// <summary>Movimentos só podem entrar (ou sair) em datas ainda não conferidas e a partir do saldo inicial.</summary>
    public void GarantirAberta(DateOnly data)
    {
        Validador.Garantir(data >= DataSaldoInicial,
            $"A conta '{Nome}' só tem movimentos a partir de {DataSaldoInicial:dd/MM/yyyy} (data do saldo inicial).");
        Validador.Garantir(FechadaAte is null || data > FechadaAte,
            $"A conta '{Nome}' está fechada (conferida) até {FechadaAte:dd/MM/yyyy}. Reabra o fechamento para movimentar essa data.");
    }

    /// <summary>
    /// Fecha (confere) a conta até a data: compara o saldo do sistema com o contado/extrato. Uma diferença vira um
    /// movimento de ajuste (sobra ou falta de caixa), para o saldo do sistema bater com o real.
    /// </summary>
    public FechamentoConta Fechar(DateOnly ate, DateOnly hoje, decimal saldoSistema, decimal saldoInformado, string? observacao, string? usuario)
    {
        Validador.Garantir(Ativo, $"A conta '{Nome}' está inativa.");
        Validador.Garantir(ate <= hoje, "Não é possível fechar uma data futura.");
        Validador.Garantir(ate >= DataSaldoInicial, "A data do fechamento é anterior ao saldo inicial da conta.");
        Validador.Garantir(FechadaAte is null || ate > FechadaAte, $"A conta já está fechada até {FechadaAte:dd/MM/yyyy}.");

        var diferenca = saldoInformado - saldoSistema;
        MovimentoConta? ajuste = null;
        if (diferenca != 0)
        {
            Validador.Garantir(!string.IsNullOrWhiteSpace(observacao), "Há diferença entre o saldo do sistema e o informado: explique-a na observação.");
            ajuste = new MovimentoConta(this, ate, diferenca,
                diferenca > 0 ? "Ajuste de fechamento (sobra)" : "Ajuste de fechamento (falta)", OrigemMovimento.AjusteFechamento);
        }

        FechadaAte = ate;
        return new FechamentoConta(this, ate, saldoSistema, saldoInformado, observacao, usuario, ajuste);
    }

    /// <summary>Desfaz o último fechamento: a conta volta a aceitar movimentos depois do fechamento anterior.</summary>
    public void Reabrir(FechamentoConta ultimo, DateOnly? fechamentoAnterior)
    {
        Validador.Garantir(ultimo.ContaFinanceiraId == Id && ultimo.Data == FechadaAte, "Somente o último fechamento pode ser desfeito.");
        FechadaAte = fechamentoAnterior;
    }
}

public enum OrigemMovimento
{
    /// <summary>Recebimento ou pagamento de títulos.</summary>
    Baixa,

    /// <summary>Lançamento avulso: aporte, retirada, tarifa, rendimento...</summary>
    Lancamento,

    Transferencia,
    AjusteFechamento
}

/// <summary>Entrada (valor positivo) ou saída (negativo) de dinheiro numa conta.</summary>
public class MovimentoConta : Entidade
{
    private MovimentoConta()
    {
    }

    public MovimentoConta(ContaFinanceira conta, DateOnly data, decimal valor, string descricao, OrigemMovimento origem)
    {
        Validador.Garantir(conta.Ativo, $"A conta '{conta.Nome}' está inativa.");
        Validador.Garantir(valor != 0, "O valor do movimento não pode ser zero.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Informe a descrição do movimento.");
        if (origem != OrigemMovimento.AjusteFechamento)
            conta.GarantirAberta(data);

        Conta = conta;
        ContaFinanceiraId = conta.Id;
        Data = data;
        Valor = valor;
        Descricao = descricao.Trim()[..Math.Min(descricao.Trim().Length, 200)];
        Origem = origem;
    }

    public int ContaFinanceiraId { get; private set; }
    public ContaFinanceira? Conta { get; private set; }
    public DateOnly Data { get; private set; }

    /// <summary>Positivo = entrada; negativo = saída.</summary>
    public decimal Valor { get; private set; }

    public string Descricao { get; private set; } = string.Empty;
    public OrigemMovimento Origem { get; private set; }

    /// <summary>Forma de pagamento da baixa que gerou o movimento.</summary>
    public int? BaixaFormaId { get; private set; }

    /// <summary>Na transferência, o movimento da outra conta (gravado na saída).</summary>
    public int? ContrapartidaId { get; private set; }
    public MovimentoConta? Contrapartida { get; private set; }

    /// <summary>Saída de uma conta e entrada na outra, na mesma data.</summary>
    public static (MovimentoConta Saida, MovimentoConta Entrada) Transferir(ContaFinanceira origem, ContaFinanceira destino,
        DateOnly data, decimal valor, string? descricao)
    {
        Validador.Garantir(origem.Id != destino.Id, "Escolha contas diferentes para a transferência.");
        Validador.Garantir(valor > 0, "O valor da transferência deve ser maior que zero.");
        var texto = string.IsNullOrWhiteSpace(descricao) ? null : $" - {descricao.Trim()}";
        var entrada = new MovimentoConta(destino, data, valor, $"Transferência de {origem.Nome}{texto}", OrigemMovimento.Transferencia);
        var saida = new MovimentoConta(origem, data, -valor, $"Transferência para {destino.Nome}{texto}", OrigemMovimento.Transferencia)
        {
            Contrapartida = entrada
        };
        return (saida, entrada);
    }

    /// <summary>Movimentos gerados por baixa ou fechamento só saem pelo estorno/reabertura correspondente.</summary>
    public void GarantirExclusaoManual()
    {
        Validador.Garantir(Origem is OrigemMovimento.Lancamento or OrigemMovimento.Transferencia,
            Origem == OrigemMovimento.Baixa
                ? "Este movimento veio de um recebimento/pagamento: estorne-o em Contas a receber ou Contas a pagar."
                : "Este movimento é o ajuste de um fechamento: desfaça o fechamento para removê-lo.");
        Conta?.GarantirAberta(Data);
    }
}

/// <summary>Conferência de uma conta até uma data (fechamento do caixa ou conciliação bancária).</summary>
public class FechamentoConta : Entidade
{
    private FechamentoConta()
    {
    }

    internal FechamentoConta(ContaFinanceira conta, DateOnly data, decimal saldoSistema, decimal saldoInformado,
        string? observacao, string? usuario, MovimentoConta? ajuste)
    {
        Conta = conta;
        ContaFinanceiraId = conta.Id;
        Data = data;
        SaldoSistema = saldoSistema;
        SaldoInformado = saldoInformado;
        Observacao = observacao?.Trim();
        Usuario = usuario;
        Ajuste = ajuste;
    }

    public int ContaFinanceiraId { get; private set; }
    public ContaFinanceira? Conta { get; private set; }

    /// <summary>Último dia conferido.</summary>
    public DateOnly Data { get; private set; }

    public decimal SaldoSistema { get; private set; }

    /// <summary>Valor contado no caixa ou saldo do extrato bancário.</summary>
    public decimal SaldoInformado { get; private set; }

    public decimal Diferenca => SaldoInformado - SaldoSistema;
    public string? Observacao { get; private set; }
    public string? Usuario { get; private set; }

    public int? AjusteId { get; private set; }
    public MovimentoConta? Ajuste { get; private set; }
}
