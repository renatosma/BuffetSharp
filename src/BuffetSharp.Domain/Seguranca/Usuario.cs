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

public class Usuario : Entidade, ICamposControlados
{
    /// <summary>Senhas erradas seguidas que bloqueiam o login.</summary>
    public const int MaximoTentativas = 5;

    /// <summary>Tempo de bloqueio depois de <see cref="MaximoTentativas"/> senhas erradas.</summary>
    public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);

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

    /// <summary>Senhas erradas desde o último acesso (zera ao entrar ou ao bloquear).</summary>
    public int TentativasFalhas { get; private set; }

    /// <summary>Login bloqueado até este horário por excesso de senhas erradas.</summary>
    public DateTime? BloqueadoAte { get; private set; }

    IReadOnlyCollection<string> ICamposControlados.CamposControladosPeloSistema =>
        [nameof(UltimoAcessoEm), nameof(TentativasFalhas), nameof(BloqueadoAte)];

    /// <summary>Comissão (%) sobre os valores recebidos dos eventos vendidos por este usuário.</summary>
    public decimal PercentualComissao { get; private set; }

    public const decimal ComissaoMaximaPercentual = 20m;

    /// <summary>
    /// Desconto máximo (%) que o usuário pode dar sozinho no orçamento e no recebimento; nulo usa o padrão do perfil
    /// (configuração "Buffet:AlcadaDesconto").
    /// </summary>
    public decimal? DescontoMaximo { get; private set; }

    public const int TamanhoEmail = 150;
    public const int TamanhoCelular = 20;

    /// <summary>Para receber avisos do sistema, como o pedido de aprovação de um desconto.</summary>
    public string? Email { get; private set; }

    public string? Celular { get; private set; }

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

    public void RegistrarAcesso(DateTime agora)
    {
        UltimoAcessoEm = agora;
        TentativasFalhas = 0;
        BloqueadoAte = null;
    }

    public bool EstaBloqueado(DateTime agora) => BloqueadoAte > agora;

    /// <summary>Conta uma senha errada; na <see cref="MaximoTentativas"/>ª seguida, bloqueia o login por <see cref="TempoBloqueio"/>.</summary>
    /// <returns>Verdadeiro se esta tentativa bloqueou o usuário.</returns>
    public bool RegistrarFalha(DateTime agora)
    {
        if (EstaBloqueado(agora))
            return false;
        TentativasFalhas++;
        if (TentativasFalhas < MaximoTentativas)
            return false;
        TentativasFalhas = 0;
        BloqueadoAte = agora.Add(TempoBloqueio);
        return true;
    }

    /// <summary>Liberação do login pelo administrador, antes de o bloqueio expirar.</summary>
    public void Desbloquear()
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
    }

    public void DefinirComissao(decimal percentual)
    {
        Validador.Garantir(percentual is >= 0 and <= ComissaoMaximaPercentual,
            $"A comissão deve estar entre 0% e {ComissaoMaximaPercentual}%.");
        PercentualComissao = percentual;
    }

    public void DefinirDescontoMaximo(decimal? percentual)
    {
        Validador.Garantir(percentual is null or (>= 0 and <= 100), "O desconto máximo deve estar entre 0% e 100%.");
        DescontoMaximo = percentual;
    }

    public void DefinirContato(string? email, string? celular)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        celular = string.IsNullOrWhiteSpace(celular) ? null : celular.Trim();
        Validador.Garantir(email is null || (email.Length <= TamanhoEmail && email.Contains('@') && !email.Contains(' ')), "E-mail inválido.");
        Validador.Garantir(celular is null || (celular.Length <= TamanhoCelular && celular.Count(char.IsDigit) >= 10),
            "Informe o celular com DDD.");
        Email = email;
        Celular = celular;
    }

    /// <summary>Somente usuários comerciais (e administradores) podem ser vendedores de orçamentos.</summary>
    public bool PodeVender => Ativo && Perfil is PerfilUsuario.Comercial or PerfilUsuario.Administrador;

    /// <summary>Regra mínima de senha forte, aplicada antes de gerar o hash.</summary>
    public static void ValidarForcaSenha(string senha)
    {
        Validador.Garantir(senha.Length >= 6, "A senha deve ter ao menos 6 caracteres.");
        Validador.Garantir(senha.Any(char.IsLetter) && senha.Any(char.IsDigit),
            "A senha deve conter letras e números.");
    }
}
