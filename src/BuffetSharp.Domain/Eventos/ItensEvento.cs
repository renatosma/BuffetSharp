using BuffetSharp.Domain.Cadastros;

namespace BuffetSharp.Domain.Eventos;

public enum StatusRsvp
{
    Pendente,
    Confirmado,
    Recusado
}

public class EventoServico
{
    private EventoServico()
    {
    }

    internal EventoServico(int? servicoAdicionalId, string descricao, decimal quantidade, decimal valorUnitario)
    {
        ServicoAdicionalId = servicoAdicionalId;
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public int? ServicoAdicionalId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal Total => Math.Round(Quantidade * ValorUnitario, 2);
}

public class EventoFuncionario
{
    private EventoFuncionario()
    {
    }

    internal EventoFuncionario(int funcionarioId, string funcao, decimal valorDiaria)
    {
        FuncionarioId = funcionarioId;
        Funcao = funcao;
        ValorDiaria = valorDiaria;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public int FuncionarioId { get; private set; }
    public Funcionario? Funcionario { get; private set; }
    public string Funcao { get; private set; } = string.Empty;
    public decimal ValorDiaria { get; private set; }
    public bool Presente { get; internal set; }
}

public class Convidado
{
    private Convidado()
    {
    }

    internal Convidado(string nome, string? telefone, string? email, int acompanhantes, string? mesa)
    {
        Nome = nome;
        Telefone = telefone;
        Email = email;
        Acompanhantes = acompanhantes;
        Mesa = mesa;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }
    public int Acompanhantes { get; private set; }
    public string? Mesa { get; private set; }
    public StatusRsvp Rsvp { get; internal set; } = StatusRsvp.Pendente;
    public DateTime? CheckInEm { get; internal set; }
}
