using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Domain.Seguranca;

public enum PerfilUsuario
{
    /// <summary>Acesso total, inclusive usuários.</summary>
    Administrador,

    /// <summary>Clientes, orçamentos, eventos e agenda.</summary>
    Comercial,

    /// <summary>Contas a receber/pagar e relatórios financeiros.</summary>
    Financeiro,

    /// <summary>Execução dos eventos: equipe, convidados, cardápio e estoque.</summary>
    Operacional
}

public class Usuario : Entidade
{
    private Usuario()
    {
    }

    public Usuario(string nome, string login, PerfilUsuario perfil, string senhaHash)
    {
        Alterar(nome, login, perfil, true);
        DefinirSenha(senhaHash);
    }

    public string Nome { get; private set; } = string.Empty;
    public string Login { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; } = true;
    public DateTime? UltimoAcessoEm { get; private set; }

    public void Alterar(string nome, string login, PerfilUsuario perfil, bool ativo)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(nome), "Informe o nome do usuário.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(login) && login.Trim().Length >= 3,
            "O login deve ter ao menos 3 caracteres.");
        Nome = nome.Trim();
        Login = login.Trim().ToLowerInvariant();
        Perfil = perfil;
        Ativo = ativo;
    }

    public void DefinirSenha(string senhaHash)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(senhaHash), "Senha inválida.");
        SenhaHash = senhaHash;
    }

    public void RegistrarAcesso(DateTime agora) => UltimoAcessoEm = agora;

    /// <summary>Regra mínima de senha forte, aplicada antes de gerar o hash.</summary>
    public static void ValidarForcaSenha(string senha)
    {
        Validador.Garantir(senha.Length >= 6, "A senha deve ter ao menos 6 caracteres.");
        Validador.Garantir(senha.Any(char.IsLetter) && senha.Any(char.IsDigit),
            "A senha deve conter letras e números.");
    }
}
