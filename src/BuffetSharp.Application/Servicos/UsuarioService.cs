using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Application.Servicos;

public sealed class UsuarioService(IBuffetDbContextFactory fabrica, IHasherSenha hasher, ISessaoUsuario sessao,
    TimeProvider relogio, ILogger<UsuarioService> log)
{
    /// <summary>
    /// Confere login e senha. Depois de <see cref="Usuario.MaximoTentativas"/> senhas erradas seguidas o login fica
    /// bloqueado por <see cref="Usuario.TempoBloqueio"/> (ou até o administrador desbloquear).
    /// </summary>
    /// <returns>O usuário autenticado, ou nulo se o login ou a senha não conferem.</returns>
    /// <exception cref="RegraNegocioException">O usuário está bloqueado.</exception>
    public async Task<UsuarioLogado?> AutenticarAsync(string login, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(senha))
            return null;

        await using var db = fabrica.Criar();
        var normalizado = login.Trim().ToLowerInvariant();
        var agora = relogio.GetLocalNow().DateTime;
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Login == normalizado && u.Ativo, ct);
        if (usuario is null)
        {
            log.LogWarning("Tentativa de login com usuário inexistente ou inativo: {Login}", normalizado);
            return null;
        }

        if (usuario.EstaBloqueado(agora))
            throw new RegraNegocioException(MensagemBloqueio(usuario));

        if (!hasher.Verificar(senha, usuario.SenhaHash))
        {
            var bloqueou = usuario.RegistrarFalha(agora);
            await db.SaveChangesAsync(ct);
            if (!bloqueou)
            {
                log.LogWarning("Senha incorreta para {Login} ({Tentativas} de {Maximo} tentativas)", usuario.Login,
                    usuario.TentativasFalhas, Usuario.MaximoTentativas);
                return null;
            }
            log.LogWarning("Login de {Login} bloqueado até {BloqueadoAte:HH:mm} por excesso de senhas erradas", usuario.Login, usuario.BloqueadoAte);
            throw new RegraNegocioException(MensagemBloqueio(usuario));
        }

        usuario.RegistrarAcesso(agora);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Login de {Login} ({Perfil})", usuario.Login, usuario.Perfil);

        var logado = new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Login, usuario.Perfil);
        sessao.Iniciar(logado);
        return logado;
    }

    public async Task<List<UsuarioInput>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.Usuarios.AsNoTracking().OrderBy(u => u.Nome)
            .Select(u => new UsuarioInput
            {
                Id = u.Id,
                Versao = u.Versao,
                Nome = u.Nome,
                Login = u.Login,
                Perfil = u.Perfil,
                Ativo = u.Ativo,
                PercentualComissao = u.PercentualComissao,
                DescontoMaximo = u.DescontoMaximo,
                Email = u.Email,
                Celular = u.Celular,
                UltimoAcessoEm = u.UltimoAcessoEm,
                BloqueadoAte = u.BloqueadoAte
            }).ToListAsync(ct);
    }

    public async Task SalvarAsync(UsuarioInput input, CancellationToken ct = default)
    {
        GarantirAdministrador();
        await using var db = fabrica.Criar();

        if (input.Id == 0)
        {
            Validador.Garantir(!string.IsNullOrEmpty(input.NovaSenha), "Informe a senha do novo usuário.");
            Usuario.ValidarForcaSenha(input.NovaSenha!);
            var usuario = new Usuario(input.Nome, input.Login, input.Perfil, hasher.Gerar(input.NovaSenha!));
            if (!input.Ativo)
                usuario.Alterar(input.Nome, input.Login, input.Perfil, false);
            usuario.DefinirComissao(input.PercentualComissao);
            usuario.DefinirDescontoMaximo(input.DescontoMaximo);
            usuario.DefinirContato(input.Email, input.Celular);
            db.Usuarios.Add(usuario);
        }
        else
        {
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == input.Id, ct)
                          ?? throw new RegraNegocioException("Usuário não encontrado.");
            Concorrencia.Conferir(usuario, input.Versao);

            var removendoUltimoAdmin = usuario.Perfil == PerfilUsuario.Administrador &&
                                       (input.Perfil != PerfilUsuario.Administrador || !input.Ativo) &&
                                       !await db.Usuarios.AnyAsync(u => u.Id != usuario.Id && u.Ativo &&
                                                                        u.Perfil == PerfilUsuario.Administrador, ct);
            Validador.Garantir(!removendoUltimoAdmin, "Deve existir ao menos um administrador ativo.");

            usuario.Alterar(input.Nome, input.Login, input.Perfil, input.Ativo);
            usuario.DefinirComissao(input.PercentualComissao);
            usuario.DefinirDescontoMaximo(input.DescontoMaximo);
            usuario.DefinirContato(input.Email, input.Celular);
            if (!string.IsNullOrEmpty(input.NovaSenha))
            {
                Usuario.ValidarForcaSenha(input.NovaSenha);
                usuario.DefinirSenha(hasher.Gerar(input.NovaSenha));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Libera o login de um usuário bloqueado por senhas erradas.</summary>
    public async Task DesbloquearAsync(int usuarioId, CancellationToken ct = default)
    {
        GarantirAdministrador();
        await using var db = fabrica.Criar();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                      ?? throw new RegraNegocioException("Usuário não encontrado.");
        usuario.Desbloquear();
        await db.SaveChangesAsync(ct);
        log.LogInformation("Login de {Login} desbloqueado por {Administrador}", usuario.Login, sessao.Usuario?.Login);
    }

    private static string MensagemBloqueio(Usuario usuario) =>
        $"Este usuário foi bloqueado por {Usuario.MaximoTentativas} senhas erradas seguidas. " +
        $"Tente novamente depois das {usuario.BloqueadoAte:HH:mm} ou peça a um administrador para desbloqueá-lo.";

    public async Task AlterarMinhaSenhaAsync(string senhaAtual, string novaSenha, CancellationToken ct = default)
    {
        var logado = sessao.Usuario ?? throw new RegraNegocioException("Nenhum usuário autenticado.");
        await using var db = fabrica.Criar();
        var usuario = await db.Usuarios.FirstAsync(u => u.Id == logado.Id, ct);
        Validador.Garantir(hasher.Verificar(senhaAtual, usuario.SenhaHash), "A senha atual não confere.");
        Usuario.ValidarForcaSenha(novaSenha);
        usuario.DefinirSenha(hasher.Gerar(novaSenha));
        await db.SaveChangesAsync(ct);
    }

    private void GarantirAdministrador() =>
        Validador.Garantir(sessao.Usuario?.EhAdministrador == true,
            "Somente administradores podem gerenciar usuários.");
}
