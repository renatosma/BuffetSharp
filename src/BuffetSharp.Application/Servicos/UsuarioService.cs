using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class UsuarioService(IBuffetDbContextFactory fabrica, IHasherSenha hasher, ISessaoUsuario sessao,
    TimeProvider relogio)
{
    public async Task<UsuarioLogado?> AutenticarAsync(string login, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(senha))
            return null;

        await using var db = fabrica.Criar();
        var normalizado = login.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Login == normalizado && u.Ativo, ct);
        if (usuario is null || !hasher.Verificar(senha, usuario.SenhaHash))
            return null;

        usuario.RegistrarAcesso(relogio.GetLocalNow().DateTime);
        await db.SaveChangesAsync(ct);

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
                Nome = u.Nome,
                Login = u.Login,
                Perfil = u.Perfil,
                Ativo = u.Ativo,
                PercentualComissao = u.PercentualComissao,
                UltimoAcessoEm = u.UltimoAcessoEm
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
            db.Usuarios.Add(usuario);
        }
        else
        {
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == input.Id, ct)
                          ?? throw new RegraNegocioException("Usuário não encontrado.");

            var removendoUltimoAdmin = usuario.Perfil == PerfilUsuario.Administrador &&
                                       (input.Perfil != PerfilUsuario.Administrador || !input.Ativo) &&
                                       !await db.Usuarios.AnyAsync(u => u.Id != usuario.Id && u.Ativo &&
                                                                        u.Perfil == PerfilUsuario.Administrador, ct);
            Validador.Garantir(!removendoUltimoAdmin, "Deve existir ao menos um administrador ativo.");

            usuario.Alterar(input.Nome, input.Login, input.Perfil, input.Ativo);
            usuario.DefinirComissao(input.PercentualComissao);
            if (!string.IsNullOrEmpty(input.NovaSenha))
            {
                Usuario.ValidarForcaSenha(input.NovaSenha);
                usuario.DefinirSenha(hasher.Gerar(input.NovaSenha));
            }
        }

        await db.SaveChangesAsync(ct);
    }

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
