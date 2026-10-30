using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Financeiro;

public enum TipoBaixa
{
    /// <summary>Contas a receber (parcelas dos eventos).</summary>
    Recebimento,

    /// <summary>Contas a pagar.</summary>
    Pagamento
}

/// <summary>
/// Um recebimento ou pagamento: quita um ou mais títulos da mesma pessoa (no todo ou em parte, com juros ou desconto)
/// usando uma ou mais formas de pagamento. Cada forma tem o seu efeito: entra ou sai de uma conta (caixa ou banco)
/// ou, no cartão de crédito de um pagamento, vira parcelas da fatura em contas a pagar.
/// </summary>
public class Baixa : Entidade
{
    private readonly List<BaixaTitulo> _titulos = [];
    private readonly List<BaixaForma> _formas = [];

    private Baixa()
    {
    }

    public Baixa(TipoBaixa tipo, DateOnly data, DateOnly hoje, string? observacao, string? usuario)
    {
        Validador.Garantir(data <= hoje, "A data do pagamento não pode ser futura.");
        Tipo = tipo;
        Data = data;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        Usuario = usuario;
    }

    public TipoBaixa Tipo { get; private set; }
    public DateOnly Data { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary>Login de quem registrou.</summary>
    public string? Usuario { get; private set; }

    public const int TamanhoMotivoDesconto = 300;

    /// <summary>Login de quem autorizou, com a senha, um desconto no recebimento acima da alçada de quem registrou.</summary>
    public string? DescontoAprovadoPor { get; private set; }

    public string? MotivoDesconto { get; private set; }

    public IReadOnlyCollection<BaixaTitulo> Titulos => _titulos;
    public IReadOnlyCollection<BaixaForma> Formas => _formas;

    /// <summary>Maior desconto (%) dado num título, sobre o valor abatido dele.</summary>
    public decimal MaiorDescontoPercentual =>
        _titulos.Where(t => t.Desconto > 0).Select(t => Math.Round(t.Desconto / t.Valor * 100, 2)).DefaultIfEmpty(0).Max();

    /// <summary>Desconto acima da alçada de quem registrou, autorizado por quem tem alçada (com o motivo).</summary>
    public void AprovarDesconto(string aprovador, string? motivo)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do desconto acima da sua alçada.");
        var texto = motivo!.Trim();
        DescontoAprovadoPor = aprovador;
        MotivoDesconto = texto.Length > TamanhoMotivoDesconto ? texto[..TamanhoMotivoDesconto] : texto;
    }

    /// <summary>Valor efetivamente pago: soma dos valores abatidos + juros − descontos.</summary>
    public decimal Total => _titulos.Sum(t => t.ValorEfetivo);

    /// <param name="valor">Quanto do título é quitado. Menor que o valor do título: o restante vira um novo título em aberto.</param>
    public void AdicionarTitulo(TituloFinanceiro titulo, decimal valor, decimal juros, decimal desconto)
    {
        Validador.Garantir(titulo is Parcela ? Tipo == TipoBaixa.Recebimento : Tipo == TipoBaixa.Pagamento,
            "Contas a receber e a pagar não podem ser baixadas juntas.");
        Validador.Garantir(_titulos.All(t => !ReferenceEquals(t.Titulo, titulo)), $"'{titulo.Descricao}' foi informado duas vezes.");
        Validador.Garantir(titulo.Status == StatusTitulo.Aberto, $"'{titulo.Descricao}' não está em aberto.");
        Validador.Garantir(valor > 0 && valor <= titulo.Valor,
            $"O valor a baixar de '{titulo.Descricao}' deve ser maior que zero e no máximo {titulo.Valor:C}.");
        Validador.Garantir(juros >= 0 && desconto >= 0, "Juros e desconto não podem ser negativos.");
        Validador.Garantir(valor + juros - desconto > 0, $"O desconto de '{titulo.Descricao}' não pode ser igual ou maior que o valor pago.");

        var saldo = valor < titulo.Valor ? titulo.Desmembrar(valor) : null;
        _titulos.Add(new BaixaTitulo(titulo, valor, juros, desconto, saldo));
    }

    /// <param name="conta">Caixa (dinheiro) ou conta bancária de onde o dinheiro sai ou para onde vai.</param>
    /// <param name="cartao">Cartão de crédito usado num pagamento (gera a fatura).</param>
    /// <param name="parcelas">Parcelas no cartão de crédito.</param>
    public BaixaForma AdicionarForma(FormaPagamento forma, decimal valor, ContaFinanceira? conta, CartaoCredito? cartao, int parcelas = 1)
    {
        Validador.Garantir(valor > 0, $"Informe o valor pago em '{forma.Nome}'.");

        if (forma.Tipo == TipoFormaPagamento.CartaoCredito && Tipo == TipoBaixa.Pagamento)
        {
            Validador.Garantir(cartao is not null, $"Escolha o cartão de crédito usado em '{forma.Nome}'.");
            Validador.Garantir(cartao!.Ativo, $"O cartão '{cartao.Nome}' está inativo.");
            Validador.Garantir(parcelas is >= 1 and <= 24, "O pagamento no cartão vai de 1 a 24 parcelas.");
            Validador.Garantir(conta is null, "O pagamento com cartão de crédito não sai de uma conta: ele gera a fatura do cartão.");
        }
        else
        {
            Validador.Garantir(cartao is null && parcelas == 1, $"'{forma.Nome}' não usa cartão de crédito parcelado.");
            if (forma.Tipo != TipoFormaPagamento.Outros)
            {
                Validador.Garantir(conta is not null, forma.Tipo == TipoFormaPagamento.Dinheiro
                    ? $"Escolha o caixa de '{forma.Nome}'."
                    : $"Escolha a conta bancária de '{forma.Nome}'.");
            }
            if (conta is not null)
            {
                Validador.Garantir(forma.Tipo == TipoFormaPagamento.Dinheiro ? conta.EhCaixa : !conta.EhCaixa,
                    forma.Tipo == TipoFormaPagamento.Dinheiro
                        ? "Dinheiro entra e sai de uma conta do tipo Caixa."
                        : $"'{forma.Nome}' movimenta conta bancária, não o caixa.");
            }
        }

        var baixaForma = new BaixaForma(forma, valor, conta, cartao, parcelas);
        _formas.Add(baixaForma);
        return baixaForma;
    }

    /// <summary>
    /// Confere os totais, quita os títulos e gera o efeito de cada forma (movimento na conta ou fatura do cartão).
    /// </summary>
    /// <param name="descricao">Quem pagou/recebeu e o quê (vai para o extrato e para a fatura do cartão).</param>
    public void Concluir(string descricao)
    {
        Validador.Garantir(_titulos.Count > 0, "Selecione ao menos uma conta.");
        Validador.Garantir(_formas.Count > 0, "Informe a forma de pagamento.");
        var formas = _formas.Sum(f => f.Valor);
        Validador.Garantir(formas == Total,
            $"A soma das formas de pagamento ({formas:C}) precisa ser igual ao total a {(Tipo == TipoBaixa.Recebimento ? "receber" : "pagar")} ({Total:C}).");

        // O título guarda a forma de maior valor (é a que aparece nas listagens).
        var principal = _formas.MaxBy(f => f.Valor)!.FormaPagamentoId;
        foreach (var item in _titulos)
            item.Titulo.RegistrarPagamento(Data, item.ValorEfetivo, principal);
        foreach (var forma in _formas)
            forma.GerarEfeitos(this, descricao);
    }

    /// <summary>
    /// Desfaz a baixa: os títulos voltam a ficar em aberto (o saldo de um pagamento parcial, se ainda em aberto, volta
    /// para o título), e os movimentos e as faturas geradas deixam de existir.
    /// </summary>
    /// <returns>O que deve ser excluído: a própria baixa, os saldos reincorporados, os movimentos e as faturas.</returns>
    public IReadOnlyList<object> Estornar()
    {
        foreach (var forma in _formas)
        {
            Validador.Garantir(forma.Faturas.All(f => f.Status == StatusTitulo.Aberto),
                $"A fatura do cartão gerada por este pagamento já foi paga. Estorne o pagamento da fatura antes.");
            if (forma.Movimento?.Conta is { } conta)
                conta.GarantirAberta(forma.Movimento.Data);
        }

        var remover = new List<object>();
        foreach (var item in _titulos)
        {
            item.Titulo.EstornarPagamento();
            if (item.Saldo is { Status: StatusTitulo.Aberto } saldo)
            {
                item.Titulo.Reincorporar(saldo);
                remover.Add(saldo);
            }
        }
        foreach (var forma in _formas)
        {
            remover.AddRange(forma.Faturas);
            if (forma.Movimento is not null)
                remover.Add(forma.Movimento);
        }
        remover.Add(this);
        return remover;
    }
}

/// <summary>Título quitado (no todo ou em parte) por uma baixa.</summary>
public class BaixaTitulo
{
    private BaixaTitulo()
    {
    }

    internal BaixaTitulo(TituloFinanceiro titulo, decimal valor, decimal juros, decimal desconto, TituloFinanceiro? saldo)
    {
        switch (titulo)
        {
            case Parcela parcela:
                Parcela = parcela;
                SaldoParcela = saldo as Parcela;
                break;
            case ContaPagar conta:
                ContaPagar = conta;
                SaldoContaPagar = saldo as ContaPagar;
                break;
        }
        Valor = valor;
        Juros = juros;
        Desconto = desconto;
    }

    public int Id { get; private set; }
    public int BaixaId { get; private set; }

    public int? ParcelaId { get; private set; }
    public Parcela? Parcela { get; private set; }
    public int? ContaPagarId { get; private set; }
    public ContaPagar? ContaPagar { get; private set; }

    /// <summary>Valor abatido do título.</summary>
    public decimal Valor { get; private set; }

    public decimal Juros { get; private set; }
    public decimal Desconto { get; private set; }
    public decimal ValorEfetivo => Valor + Juros - Desconto;

    /// <summary>Título criado com o restante, num pagamento parcial.</summary>
    public int? SaldoParcelaId { get; private set; }
    public Parcela? SaldoParcela { get; private set; }
    public int? SaldoContaPagarId { get; private set; }
    public ContaPagar? SaldoContaPagar { get; private set; }

    public TituloFinanceiro Titulo => (TituloFinanceiro?)Parcela ?? ContaPagar
        ?? throw new InvalidOperationException("Título da baixa não carregado.");

    public TituloFinanceiro? Saldo => (TituloFinanceiro?)SaldoParcela ?? SaldoContaPagar;
}

/// <summary>Uma das formas usadas na baixa (ex.: R$ 500 em dinheiro + R$ 1.500 no PIX).</summary>
public class BaixaForma
{
    private readonly List<ContaPagar> _faturas = [];

    private BaixaForma()
    {
    }

    internal BaixaForma(FormaPagamento forma, decimal valor, ContaFinanceira? conta, CartaoCredito? cartao, int parcelas)
    {
        FormaPagamento = forma;
        FormaPagamentoId = forma.Id;
        Valor = valor;
        Conta = conta;
        ContaFinanceiraId = conta?.Id;
        Cartao = cartao;
        CartaoCreditoId = cartao?.Id;
        Parcelas = parcelas;
    }

    public int Id { get; private set; }
    public int BaixaId { get; private set; }
    public int FormaPagamentoId { get; private set; }
    public FormaPagamento? FormaPagamento { get; private set; }
    public decimal Valor { get; private set; }
    public int? ContaFinanceiraId { get; private set; }
    public ContaFinanceira? Conta { get; private set; }
    public int? CartaoCreditoId { get; private set; }
    public CartaoCredito? Cartao { get; private set; }

    /// <summary>Parcelas no cartão de crédito (1 nas demais formas).</summary>
    public int Parcelas { get; private set; } = 1;

    /// <summary>Entrada ou saída na conta (nulo no cartão de crédito e nas formas sem conta).</summary>
    public MovimentoConta? Movimento { get; private set; }

    /// <summary>Parcelas da fatura do cartão geradas em contas a pagar.</summary>
    public IReadOnlyCollection<ContaPagar> Faturas => _faturas;

    internal void GerarEfeitos(Baixa baixa, string descricao)
    {
        if (Conta is not null)
        {
            var recebimento = baixa.Tipo == TipoBaixa.Recebimento;
            Movimento = new MovimentoConta(Conta, baixa.Data, recebimento ? Valor : -Valor,
                $"{(recebimento ? "Recebimento" : "Pagamento")} - {descricao}", OrigemMovimento.Baixa);
        }

        if (Cartao is not null)
        {
            var vencimentos = FaturaCartao.Vencimentos(baixa.Data, Cartao.DiaFechamento, Cartao.DiaVencimento, Parcelas);
            var valores = FaturaCartao.Dividir(Valor, Parcelas);
            for (var i = 0; i < Parcelas; i++)
            {
                var texto = $"Fatura {Cartao} - {descricao}";
                var numero = Parcelas > 1 ? $" ({i + 1}/{Parcelas})" : string.Empty;
                texto = texto[..Math.Min(texto.Length, ContaPagar.TamanhoDescricao - numero.Length - TituloFinanceiro.SufixoSaldo.Length)] + numero;
                _faturas.Add(new ContaPagar(this, texto, valores[i], vencimentos[i], Cartao.FornecedorId));
            }
        }
    }
}
