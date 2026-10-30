using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Seguranca;

internal sealed class FormUsuarios : FormListagem<UsuarioInput>
{
    private readonly UsuarioService _servico;
    private readonly BuffetOptions _opcoes;

    public FormUsuarios(UsuarioService servico, BuffetOptions opcoes)
        : base("Usuários", "Acesso ao sistema. O perfil define os menus disponíveis (Administrador vê tudo).")
    {
        _servico = servico;
        _opcoes = opcoes;
        Botao("Novo", Icones.Adicionar, () => EditarAsync(new UsuarioInput()), destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } u ? EditarAsync(u) : Task.CompletedTask);
        Botao("Desbloquear", Icones.Cadeado, DesbloquearAsync, "Libera o login bloqueado por senhas erradas");

        Grade.Texto("Nome", u => u.Nome, 50)
            .Texto("Login", u => u.Login, 30)
            .Texto("Perfil", u => Textos.Exibir(u.Perfil), 25)
            .Numero("Comissão %", u => u.PercentualComissao, "N2", 15)
            .Texto("Desconto máx.", u => $"{u.DescontoMaximo ?? opcoes.AlcadaDesconto.Limite(u.Perfil):0.##}%" + (u.DescontoMaximo is null ? " (perfil)" : ""), 18,
                u => u.DescontoMaximo is null ? Tema.TextoSuave : null)
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
            PercentualComissao = usuario.PercentualComissao,
            DescontoMaximo = usuario.DescontoMaximo,
            Email = usuario.Email,
            Celular = usuario.Celular
        };

        var e = new EditorCampos();
        e.Texto("Nome", () => input.Nome, v => input.Nome = v ?? "", maximo: 100);
        e.Texto("Login", () => input.Login, v => input.Login = v ?? "", maximo: 60);
        e.Opcoes("Perfil", () => input.Perfil, v => input.Perfil = v);
        e.Marcador("Ativo", () => input.Ativo, v => input.Ativo = v);
        e.Decimal("Comissão sobre vendas (%)", () => input.PercentualComissao, v => input.PercentualComissao = v, casas: 2,
            maximo: Domain.Seguranca.Usuario.ComissaoMaximaPercentual, span: 2);
        var padroes = string.Join(", ", Enum.GetValues<Domain.Seguranca.PerfilUsuario>()
            .Select(perfil => $"{Textos.Exibir(perfil)} {_opcoes.AlcadaDesconto.Limite(perfil):0.##}%"));
        e.Texto("Desconto máximo sem aprovação (%)", () => input.DescontoMaximo?.ToString("0.##", Textos.PtBr), v => input.DescontoMaximo = v is null ? null
                : decimal.TryParse(v.Replace("%", "").Trim(), System.Globalization.NumberStyles.Number, Textos.PtBr, out var limite) ? limite
                : throw new Domain.Comum.RegraNegocioException("Informe o desconto máximo em %, ou deixe em branco para usar o do perfil."),
            maximo: 6, span: 2, dica: $"Em branco usa o do perfil ({padroes})");
        e.Texto("E-mail (avisos do sistema)", () => input.Email, v => input.Email = v, maximo: Domain.Seguranca.Usuario.TamanhoEmail);
        e.Texto("Celular (WhatsApp)", () => input.Celular, v => input.Celular = v, maximo: Domain.Seguranca.Usuario.TamanhoCelular);
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
