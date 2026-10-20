using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using BuffetSharp.Infrastructure.Persistencia.Provedores;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Concorrência otimista (duas telas editando o mesmo registro), bloqueio de login e cópia de segurança.</summary>
[Collection(ColecaoBanco.Nome)]
public class ConcorrenciaSegurancaIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadastro_salvo_por_outro_usuario_nao_e_sobrescrito()
    {
        var servico = banco.Obter<ICadastroService<TipoEvento>>();
        var criado = await servico.SalvarAsync(new TipoEvento { Nome = "Concorrência" }, Ct);

        // Duas telas abrem o mesmo registro.
        var telaA = (await servico.ObterAsync(criado.Id, Ct))!;
        var telaB = (await servico.ObterAsync(criado.Id, Ct))!;

        telaA.Descricao = "Alterado pela tela A";
        await servico.SalvarAsync(telaA, Ct);

        telaB.Descricao = "Alterado pela tela B";
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => servico.SalvarAsync(telaB, Ct));
        Assert.Equal(Concorrencia.Mensagem, erro.Message);
        Assert.Equal("Alterado pela tela A", (await servico.ObterAsync(criado.Id, Ct))!.Descricao);

        // Reaberta, a tela B salva normalmente.
        var reaberta = (await servico.ObterAsync(criado.Id, Ct))!;
        reaberta.Descricao = "Alterado pela tela B";
        await servico.SalvarAsync(reaberta, Ct);
        await servico.ExcluirAsync(criado.Id, Ct);
    }

    [Fact]
    public async Task Conta_a_pagar_alterada_por_outro_usuario_avisa_ao_salvar()
    {
        var financeiro = banco.Obter<FinanceiroService>();
        await financeiro.SalvarContaAsync(new ContaPagarInput
        {
            Descricao = "Conta concorrente", Categoria = CategoriaDespesa.Outros, Valor = 100, Vencimento = new DateOnly(2030, 1, 10)
        }, Ct);
        var id = (await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = "Conta concorrente" },
            DateOnly.FromDateTime(DateTime.Today), Ct)).Single().Id;

        var telaA = await financeiro.ObterContaAsync(id, Ct);
        var telaB = await financeiro.ObterContaAsync(id, Ct);
        telaA.Valor = 150;
        await financeiro.SalvarContaAsync(telaA, Ct);

        telaB.Descricao = "Outra descrição";
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => financeiro.SalvarContaAsync(telaB, Ct));
        Assert.Equal(Concorrencia.Mensagem, erro.Message);
        Assert.Equal(150, (await financeiro.ObterContaAsync(id, Ct)).Valor);
        await financeiro.ExcluirContaAsync(id, Ct);
    }

    [Fact]
    public async Task Orcamento_salvo_duas_vezes_pela_mesma_tela_nao_gera_conflito()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var id = (await banco.Obter<IConsultasComercial>().ListarOrcamentosAsync(new FiltroOrcamentos(), Ct))
            .First(o => o.Status == Domain.Comercial.StatusOrcamento.Rascunho).Id;

        var tela = await orcamentos.ObterAsync(id, Ct);
        tela.Observacoes = "Primeira gravação";
        await orcamentos.SalvarAsync(tela, Ct);
        tela.Observacoes = "Segunda gravação";
        await orcamentos.SalvarAsync(tela, Ct);

        Assert.Equal("Segunda gravação", (await orcamentos.ObterAsync(id, Ct)).Observacoes);
    }

    [Fact]
    public async Task Login_bloqueado_por_senhas_erradas_e_liberado_pelo_administrador()
    {
        var usuarios = banco.Obter<UsuarioService>();
        await usuarios.SalvarAsync(new UsuarioInput
        {
            Nome = "Bloqueio", Login = "bloqueio.teste", Perfil = PerfilUsuario.Comercial, NovaSenha = "Senha123"
        }, Ct);

        for (var i = 1; i < Usuario.MaximoTentativas; i++)
            Assert.Null(await usuarios.AutenticarAsync("bloqueio.teste", "errada", Ct));
        var bloqueio = await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.AutenticarAsync("bloqueio.teste", "errada", Ct));
        Assert.Contains("bloqueado", bloqueio.Message);

        // Nem a senha certa entra enquanto durar o bloqueio.
        await Assert.ThrowsAsync<RegraNegocioException>(() => usuarios.AutenticarAsync("bloqueio.teste", "Senha123", Ct));

        var id = (await usuarios.ListarAsync(Ct)).Single(u => u.Login == "bloqueio.teste").Id;
        await usuarios.DesbloquearAsync(id, Ct);
        try
        {
            Assert.NotNull(await usuarios.AutenticarAsync("bloqueio.teste", "Senha123", Ct));
        }
        finally
        {
            // A autenticação trocou a sessão; os demais testes precisam do administrador.
            banco.Obter<ISessaoUsuario>().Iniciar(new UsuarioLogado(1, "Administrador", "admin", PerfilUsuario.Administrador));
        }
    }

    [Fact]
    public async Task Backup_do_sql_server_grava_e_mantem_so_as_ultimas_copias()
    {
        Assert.SkipWhen(banco.Banco != TipoBancoDados.SqlServer, "A cópia pelo sistema é testada no SQL Server (as demais usam pg_dump/mysqldump).");

        var pasta = Path.Combine(Path.GetTempPath(), $"BuffetSharp_Backup_{Guid.NewGuid():N}");
        try
        {
            var backup = new Infrastructure.Manutencao.BackupBanco(banco.Obter<Infrastructure.Persistencia.FabricaConexao>(),
                new BackupOptions { Pasta = pasta, Manter = 1 }, TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.Manutencao.BackupBanco>.Instance);

            var copia = await backup.FazerAsync(Ct);
            Assert.True(File.Exists(copia.Caminho));
            Assert.True(new FileInfo(copia.Caminho).Length > 0);
            Assert.Null(await backup.FazerSeNecessarioAsync(Ct)); // a última é de agora

            await Task.Delay(1100, Ct); // nome do arquivo tem precisão de segundos
            var nova = await backup.FazerAsync(Ct);
            Assert.Equal([nova.Caminho], backup.Listar().Select(a => a.Caminho));
        }
        finally
        {
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }
}
