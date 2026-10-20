namespace BuffetSharp.Domain.Comum;

/// <summary>
/// Classe base de todas as entidades persistidas.
/// </summary>
public abstract class Entidade
{
    public int Id { get; protected set; }

    /// <summary>Preenchido automaticamente pelo DbContext ao inserir.</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Preenchido automaticamente pelo DbContext ao alterar.</summary>
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>
    /// Controle de concorrência otimista: incrementada pelo DbContext a cada alteração feita pelo usuário. Quem salva
    /// um registro lido antes da última alteração recebe um aviso, em vez de sobrescrever o trabalho do outro.
    /// </summary>
    public int Versao { get; set; }

    public bool EhNovo => Id == 0;
}

/// <summary>
/// Cadastros que podem ser inativados em vez de excluídos.
/// </summary>
public interface IAtivavel
{
    bool Ativo { get; set; }
}

/// <summary>Cadastros em que só um registro pode ser o padrão: ao marcar um, os demais são desmarcados ao salvar.</summary>
public interface IPadraoUnico
{
    bool Padrao { get; set; }
}

/// <summary>
/// Entidades com propriedades mantidas apenas pelo sistema (ex.: saldo de estoque),
/// que não devem ser sobrescritas quando o cadastro é salvo pela tela.
/// </summary>
public interface ICamposControlados
{
    IReadOnlyCollection<string> CamposControladosPeloSistema { get; }
}
