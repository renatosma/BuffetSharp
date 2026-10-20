using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Domain.Financeiro;

/// <summary>
/// Pagamento das diárias de um funcionário pelos eventos realizados num período: junta as escalas com presença ainda não
/// pagas e gera a conta a pagar (categoria Pessoal). Desfazer só é possível enquanto a conta não foi paga.
/// </summary>
public class FechamentoEquipe : Entidade
{
    private readonly List<EventoFuncionario> _escalas = [];

    private FechamentoEquipe()
    {
    }

    public FechamentoEquipe(Funcionario funcionario, DateOnly de, DateOnly ate, IReadOnlyCollection<EventoFuncionario> escalas,
        DateOnly vencimento)
    {
        Validador.Garantir(de <= ate, "O início do período deve ser anterior ao fim.");
        Validador.Garantir(escalas.Count > 0, $"Não há diárias a pagar para {funcionario.Nome} no período.");
        Validador.Garantir(escalas.All(e => e.FuncionarioId == funcionario.Id && e.Presente),
            "Somente as diárias do funcionário com presença registrada entram no pagamento.");
        Validador.Garantir(escalas.All(e => e.FechamentoEquipeId is null), "Uma das diárias já foi paga em outro fechamento.");

        Funcionario = funcionario;
        FuncionarioId = funcionario.Id;
        De = de;
        Ate = ate;
        Diarias = escalas.Count;
        Valor = escalas.Sum(e => e.ValorDiaria);
        Validador.Garantir(Valor > 0, $"As diárias de {funcionario.Nome} no período estão sem valor.");
        _escalas.AddRange(escalas);
        ContaPagar = new ContaPagar($"Diárias - {funcionario.Nome} ({de:dd/MM/yyyy} a {ate:dd/MM/yyyy})",
            CategoriaDespesa.Pessoal, Valor, vencimento)
        {
            Observacao = $"{Diarias} diária(s) em eventos realizados no período."
        };
    }

    public int FuncionarioId { get; private set; }
    public Funcionario? Funcionario { get; private set; }
    public DateOnly De { get; private set; }
    public DateOnly Ate { get; private set; }
    public int Diarias { get; private set; }
    public decimal Valor { get; private set; }

    public int ContaPagarId { get; private set; }
    public ContaPagar? ContaPagar { get; private set; }

    /// <summary>Escalas pagas por este fechamento.</summary>
    public IReadOnlyCollection<EventoFuncionario> Escalas => _escalas;

    /// <summary>Libera as escalas para um novo fechamento (o serviço exclui o fechamento e a conta).</summary>
    public void Desfazer()
    {
        Validador.Garantir(ContaPagar is not null, "Carregue a conta a pagar do fechamento.");
        Validador.Garantir(ContaPagar!.Status != StatusTitulo.Pago,
            "As diárias já foram pagas. Estorne o pagamento em contas a pagar antes de desfazer o fechamento.");
        foreach (var escala in _escalas)
            escala.FechamentoEquipeId = null;
        _escalas.Clear();
    }
}
