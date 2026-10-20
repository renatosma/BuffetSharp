using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Tests.Dominio;

public class SegurancaTests
{
    private static readonly DateTime Agora = new(2026, 10, 20, 14, 0, 0);

    private static Usuario NovoUsuario() => new("Teste", "teste", PerfilUsuario.Comercial, "hash");

    [Fact]
    public void Login_bloqueia_na_quinta_senha_errada_seguida()
    {
        var usuario = NovoUsuario();

        for (var i = 1; i < Usuario.MaximoTentativas; i++)
            Assert.False(usuario.RegistrarFalha(Agora));
        Assert.False(usuario.EstaBloqueado(Agora));

        Assert.True(usuario.RegistrarFalha(Agora));
        Assert.True(usuario.EstaBloqueado(Agora.AddMinutes(14)));
        Assert.False(usuario.EstaBloqueado(Agora.Add(Usuario.TempoBloqueio)));
    }

    [Fact]
    public void Acesso_correto_zera_as_tentativas()
    {
        var usuario = NovoUsuario();
        for (var i = 1; i < Usuario.MaximoTentativas; i++)
            usuario.RegistrarFalha(Agora);

        usuario.RegistrarAcesso(Agora);

        Assert.Equal(0, usuario.TentativasFalhas);
        Assert.False(usuario.RegistrarFalha(Agora));
    }

    [Fact]
    public void Administrador_desbloqueia_antes_do_tempo()
    {
        var usuario = NovoUsuario();
        for (var i = 0; i < Usuario.MaximoTentativas; i++)
            usuario.RegistrarFalha(Agora);

        usuario.Desbloquear();

        Assert.False(usuario.EstaBloqueado(Agora));
        Assert.Null(usuario.BloqueadoAte);
    }
}
