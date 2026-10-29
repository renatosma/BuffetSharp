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

    /// <summary>Fechamento de diárias em que esta escala foi paga (nulo enquanto não foi lançada em contas a pagar).</summary>
    public int? FechamentoEquipeId { get; internal set; }

    public const int TamanhoMotivoExcecao = 200;

    /// <summary>Motivo dado por um administrador para escalar apesar do conflito com outro evento (nulo: escala sem conflito).</summary>
    public string? ExcecaoConflito { get; private set; }

    public string? ExcecaoAutorizadaPor { get; private set; }

    /// <summary>Registra a exceção que permitiu escalar o funcionário mesmo em conflito com outro evento.</summary>
    public void AutorizarConflito(string motivo, string usuario)
    {
        Comum.Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo para escalar apesar do conflito.");
        Comum.Validador.Garantir(motivo.Trim().Length <= TamanhoMotivoExcecao,
            $"O motivo pode ter no máximo {TamanhoMotivoExcecao} caracteres.");
        ExcecaoConflito = motivo.Trim();
        ExcecaoAutorizadaPor = usuario;
    }
}

public class Convidado
{
    private Convidado()
    {
    }

    internal Convidado(string nome, string? telefone, string? email, int acompanhantes)
    {
        Nome = nome;
        Telefone = telefone;
        Email = email;
        Acompanhantes = acompanhantes;
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }
    public int Acompanhantes { get; private set; }
    /// <summary>Mesa do mapa onde o convidado senta com os acompanhantes (nula: ainda sem mesa).</summary>
    public EventoMesa? Mesa { get; internal set; }

    /// <summary>O convidado e os acompanhantes: os lugares que ocupa na mesa.</summary>
    public int Pessoas => 1 + Acompanhantes;

    public StatusRsvp Rsvp { get; internal set; } = StatusRsvp.Pendente;
    public DateTime? CheckInEm { get; internal set; }
}

/// <summary>
/// Material reservado para o evento. Parte pode vir do patrimônio próprio e parte ser alugada de terceiros;
/// na devolução, a diferença entre o enviado e o devolvido é registrada como perda (quebra/extravio).
/// </summary>
public class EventoMaterial
{
    private EventoMaterial()
    {
    }

    internal EventoMaterial(Estoque.Material material, int quantidade, int alugada)
    {
        MaterialId = material.Id;
        Material = material;
        Alterar(material, quantidade, alugada);
    }

    public int Id { get; private set; }
    public int EventoId { get; private set; }
    public int MaterialId { get; private set; }
    public Estoque.Material? Material { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Unidades alugadas de terceiros (o restante sai do patrimônio próprio).</summary>
    public int QuantidadeAlugada { get; private set; }

    /// <summary>Valores "congelados" na reserva.</summary>
    public decimal CustoLocacaoUnitario { get; private set; }
    public decimal ValorReposicaoUnitario { get; private set; }

    public int? QuantidadeDevolvida { get; private set; }
    public int QuantidadePerdida { get; private set; }
    public DateTime? DevolvidoEm { get; private set; }

    public int QuantidadePropria => Quantidade - QuantidadeAlugada;
    public bool Devolvido => DevolvidoEm is not null;
    public decimal CustoLocacao => QuantidadeAlugada * CustoLocacaoUnitario;
    public decimal CustoPerdas => QuantidadePerdida * ValorReposicaoUnitario;

    internal void Alterar(Estoque.Material material, int quantidade, int alugada)
    {
        Quantidade = quantidade;
        QuantidadeAlugada = alugada;
        CustoLocacaoUnitario = material.CustoLocacao;
        ValorReposicaoUnitario = material.ValorReposicao;
    }

    internal void RegistrarDevolucao(int devolvidas, int perdidas, DateTime agora)
    {
        QuantidadeDevolvida = devolvidas;
        QuantidadePerdida = perdidas;
        DevolvidoEm = agora;
    }
}
