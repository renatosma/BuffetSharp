using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Application.Abstracoes;

public interface IHasherSenha
{
    string Gerar(string senha);
    bool Verificar(string senha, string hash);
}

public sealed record UsuarioLogado(int Id, string Nome, string Login, PerfilUsuario Perfil)
{
    public bool EhAdministrador => Perfil == PerfilUsuario.Administrador;

    public bool PossuiPerfil(params PerfilUsuario[] perfis) => EhAdministrador || perfis.Contains(Perfil);
}

/// <summary>Usuário autenticado na aplicação desktop (uma sessão por processo).</summary>
public interface ISessaoUsuario
{
    UsuarioLogado? Usuario { get; }
    bool Autenticado { get; }
    void Iniciar(UsuarioLogado usuario);
    void Encerrar();
}

internal sealed class SessaoUsuario : ISessaoUsuario
{
    public UsuarioLogado? Usuario { get; private set; }
    public bool Autenticado => Usuario is not null;
    public void Iniciar(UsuarioLogado usuario) => Usuario = usuario;
    public void Encerrar() => Usuario = null;
}
