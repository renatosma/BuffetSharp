using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Seguranca;

internal sealed class FormUsuarios : FormListagem<UsuarioInput>
{
    private readonly UsuarioService _servico;

    public FormUsuarios(UsuarioService servico)
        : base("Usuários", "Acesso ao sistema. O perfil define os menus disponíveis (Administrador vê tudo).")
    {
        _servico = servico;
        Botao("Novo", Icones.Adicionar, () => EditarAsync(new UsuarioInput()), destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } u ? EditarAsync(u) : Task.CompletedTask);
        Botao("Desbloquear", Icones.Cadeado, DesbloquearAsync, "Libera o login bloqueado por senhas erradas");

        Grade.Texto("Nome", u => u.Nome, 50)
            .Texto("Login", u => u.Login, 30)
            .Texto("Perfil", u => Textos.Exibir(u.Perfil), 25)
            .Numero("Comissão %", u => u.PercentualComissao, "N2", 15)
            .SimNao("Ativo", u => u.Ativo, 15)
            .Data("Último acesso", u => u.UltimoAcessoEm, "dd/MM/yyyy HH:mm", 25)
            .Texto("Bloqueio", u => Bloqueado(u) ? $"até {u.BloqueadoAte:HH:mm}" : "", 18, u => Bloqueado(u) ? Tema.Perigo : null)
            .CorDaLinha(u => u.Ativo ? null : Tema.Neutro)
            .Busca(u => $"{u.Nome} {u.Login} {u.Perfil}");
    }

    protected override async Task CarregarAsync() => Exibir(await _servico.ListarAsync());

    protected override Task AoAbrirAsync(UsuarioInput item) => EditarAsync(item);

    private static bool Bloqueado(UsuarioInput usuario) => usuario.BloqueadoAte > DateTime.Now;

    private async Task DesbloquearAsync()
    {
        if (Selecionado() is not { } usuario)
            return;
        if (!Bloqueado(usuario))
        {
            Mensagens.Aviso(this, $"O login de {usuario.Nome} não está bloqueado.");
            return;
        }
        await _servico.DesbloquearAsync(usuario.Id);
        await CarregarAsync();
        Mensagens.Info(this, $"{usuario.Nome} já pode entrar no sistema.");
    }

    private async Task EditarAsync(UsuarioInput usuario)
    {
        var input = new UsuarioInput
        {
            Id = usuario.Id,
            Versao = usuario.Versao,
            Nome = usuario.Nome,
            Login = usuario.Login,
            Perfil = usuario.Perfil,
            Ativo = usuario.Ativo,
            PercentualComissao = usuario.PercentualComissao
        };

        var e = new EditorCampos();
        e.Texto("Nome", () => input.Nome, v => input.Nome = v ?? "", maximo: 100);
        e.Texto("Login", () => input.Login, v => input.Login = v ?? "", maximo: 60);
        e.Opcoes("Perfil", () => input.Perfil, v => input.Perfil = v);
        e.Marcador("Ativo", () => input.Ativo, v => input.Ativo = v);
        e.Decimal("Comissão sobre vendas (%)", () => input.PercentualComissao, v => input.PercentualComissao = v, casas: 2,
            maximo: Domain.Seguranca.Usuario.ComissaoMaximaPercentual, span: 2);
        e.Senha(input.Id == 0 ? "Senha" : "Nova senha (deixe em branco para manter)", v => input.NovaSenha = v, span: 2,
            dica: "Mínimo 6 caracteres, com letras e números");

        using var dialogo = new FormEdicao(input.Id == 0 ? "Novo usuário" : "Editar usuário", null, e, () => _servico.SalvarAsync(input), 560);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }
}

internal static class AlteracaoSenha
{
    public static void Exibir(IWin32Window dono, UsuarioService servico)
    {
        string? atual = null, nova = null, confirmacao = null;
        var e = new EditorCampos(1);
        e.Senha("Senha atual", v => atual = v);
        e.Senha("Nova senha", v => nova = v, dica: "Mínimo 6 caracteres, com letras e números");
        e.Senha("Confirme a nova senha", v => confirmacao = v);
        using var dialogo = new FormEdicao("Alterar minha senha", null, e, async () =>
        {
            if (nova != confirmacao)
                throw new Domain.Comum.RegraNegocioException("A confirmação não confere com a nova senha.");
            await servico.AlterarMinhaSenhaAsync(atual ?? "", nova ?? "");
        }, 440);
        if (dialogo.ShowDialog(dono) == DialogResult.OK)
            Mensagens.Info(dono, "Senha alterada com sucesso.");
    }
}
