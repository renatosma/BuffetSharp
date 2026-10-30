using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Alçada de desconto: pedido de aprovação, senha na hora, fila, lembrete, recebimento e relatório no banco real.</summary>
[Collection(ColecaoBanco.Nome)]
public class AlcadaDescontoIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

    /// <summary>Executa como o usuário de login informado e volta ao administrador no fim.</summary>
    private async Task ComoAsync(string login, Func<Task> acao)
    {
        var usuario = (await banco.Obter<UsuarioService>().ListarAsync(Ct)).Single(u => u.Login == login);
        var sessao = banco.Obter<ISessaoUsuario>();
        sessao.Iniciar(new UsuarioLogado(usuario.Id, usuario.Nome, usuario.Login, usuario.Perfil));
        try
        {
            await acao();
        }
        finally
        {
            sessao.Iniciar(new UsuarioLogado(1, "Administrador", "admin", PerfilUsuario.Administrador));
        }
    }

    private async Task<OrcamentoInput> NovoOrcamentoAsync(int diasAFrente, decimal desconto = 0, decimal? valorPorPessoa = null)
    {
        var input = banco.Obter<OrcamentoService>().Novo();
        input.ClienteId = (await Lookup(TipoLookup.Clientes)).Id;
        input.TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id;
        input.PacoteId = (await Lookup(TipoLookup.Pacotes, p => p.Nome == "Prata")).Id;
        input.EspacoId = (await Lookup(TipoLookup.Espacos, e => e.Numero >= 100)).Id;
        input.Data = Hoje.AddYears(3).AddDays(diasAFrente);
        input.HoraInicio = new TimeOnly(8, 0);
        input.HoraFim = new TimeOnly(9, 30);
        input.Convidados = 100;
        input.PercentualDesconto = desconto;
        input.ValorPorPessoa = valorPorPessoa;
        input.VendedorId = (await Lookup(TipoLookup.Vendedores, v => v.Nome == "Carla Comercial")).Id;
        return input;
    }

    [Fact]
    public async Task Desconto_acima_da_alcada_fica_na_fila_ate_quem_pode_aprovar()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var descontos = banco.Obter<DescontoService>();
        var input = await NovoOrcamentoAsync(10, desconto: 12);
        var id = 0;

        await ComoAsync("comercial", async () =>
        {
            Assert.Equal(5m, await descontos.MeuLimiteAsync(Ct));
            var alcada = await Assert.ThrowsAsync<AlcadaDescontoException>(() => orcamentos.SalvarAsync(input, Ct));
            Assert.Equal((12m, 5m), (alcada.Percentual, alcada.Limite));
            Assert.Equal(0, input.Id); // nada gravado

            input.PedidoDesconto = new PedidoDesconto { Motivo = "Cliente fechando com a concorrência" };
            id = await orcamentos.SalvarAsync(input, Ct);
            Assert.Null(input.PedidoDesconto);
            Assert.Equal(SituacaoDesconto.Aguardando, input.SituacaoDesconto);
            Assert.Contains("aguardando", (await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.EnviarAsync(id, Ct))).Message);
            await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<PropostaService>().PrepararEmailAsync(id, Ct));
            Assert.DoesNotContain(await descontos.LembretesAsync(Ct), l => l.ReferenciaId == id); // o comercial não pode aprovar
            Assert.False((await descontos.PendentesAsync(Ct)).Single(p => p.OrcamentoId == id).PodeAprovar);
            await Assert.ThrowsAsync<RegraNegocioException>(() => descontos.AprovarAsync(id, null, Ct));

            // Salvar de novo sem mudar o desconto não pede outra aprovação.
            var aberto = await orcamentos.ObterAsync(id, Ct);
            aberto.Observacoes = "Ligar na sexta";
            await orcamentos.SalvarAsync(aberto, Ct);
        });

        var resumo = (await banco.Obter<IConsultasComercial>().ListarOrcamentosAsync(new FiltroOrcamentos { Texto = $"ORC-{id:D5}" }, Ct)).Single();
        Assert.Equal((SituacaoDesconto.Aguardando, 12m), (resumo.SituacaoDesconto, resumo.PercentualDescontoAutorizado));
        var lembrete = Assert.Single(await descontos.LembretesAsync(Ct), l => l.ReferenciaId == id);
        Assert.Equal(TipoLembrete.AprovacaoDesconto, lembrete.Tipo);

        await descontos.AprovarAsync(id, "Cliente antigo", Ct);

        var aprovado = await orcamentos.ObterAsync(id, Ct);
        Assert.Equal((SituacaoDesconto.Aprovado, "admin", "comercial", "Cliente fechando com a concorrência"),
            (aprovado.SituacaoDesconto, aprovado.DescontoDecididoPor, aprovado.DescontoConcedidoPor, aprovado.MotivoDesconto));
        Assert.DoesNotContain(await descontos.LembretesAsync(Ct), l => l.ReferenciaId == id);
        await orcamentos.EnviarAsync(id, Ct);

        var linha = (await descontos.OrcamentosAsync(Hoje, Hoje, Ct)).Single(o => o.OrcamentoId == id);
        Assert.Equal((12m, true, "admin"), (linha.Percentual, linha.PassouDaAlcada, linha.DecididoPor));
        var vendedor = DescontoService.PorVendedor(await descontos.OrcamentosAsync(Hoje, Hoje, Ct)).Single(v => v.Vendedor == "Carla Comercial");
        Assert.True(vendedor.Aprovados >= 1);
        Assert.Contains("admin", vendedor.Aprovadores);
    }

    [Fact]
    public async Task Preco_negociado_abaixo_da_tabela_conta_e_a_senha_de_quem_pode_aprova_na_hora()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var pacote = (await banco.Obter<IConsultasEstoqueCardapio>().ListarPacotesAsync(Ct)).Single(p => p.Nome == "Prata");
        var input = await NovoOrcamentoAsync(20, valorPorPessoa: Math.Round(pacote.PrecoPorPessoa * 0.8m, 2)); // sem desconto no campo

        await ComoAsync("comercial", async () =>
        {
            var alcada = await Assert.ThrowsAsync<AlcadaDescontoException>(() => orcamentos.SalvarAsync(input, Ct));
            Assert.True(alcada.Percentual > 5);

            input.PedidoDesconto = new PedidoDesconto { Motivo = "Indicação de cliente", Login = "admin", Senha = "senha-errada" };
            Assert.Contains("não conferem", (await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.SalvarAsync(input, Ct))).Message);
            input.PedidoDesconto = new PedidoDesconto { Motivo = "Indicação de cliente", Login = "financeiro", Senha = "Financeiro@123" };
            Assert.Contains("também passa", (await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.SalvarAsync(input, Ct))).Message);

            input.PedidoDesconto = new PedidoDesconto { Motivo = "Indicação de cliente", Login = "admin", Senha = "Admin@123" };
            var id = await orcamentos.SalvarAsync(input, Ct);

            var salvo = await orcamentos.ObterAsync(id, Ct);
            Assert.Equal((SituacaoDesconto.Aprovado, "admin"), (salvo.SituacaoDesconto, salvo.DescontoDecididoPor));
            Assert.True(salvo.ValorTabela > 0);
            await orcamentos.EnviarAsync(id, Ct);
        });
        await banco.Obter<UsuarioService>().DesbloquearAsync(1, Ct); // zera a senha errada contada no admin
    }

    [Fact]
    public async Task Desconto_no_recebimento_acima_da_alcada_so_com_a_senha()
    {
        var parcela = (await banco.Obter<IConsultasFinanceiro>().ListarParcelasAsync(new FiltroTitulos { Status = StatusTitulo.Aberto }, Hoje, Ct))
            .Last(p => p.Valor > 1_000m && p.Descricao.StartsWith("Parcela", StringComparison.Ordinal));
        var pix = await Lookup(TipoLookup.FormasPagamento, f => f.Info == "Pix");
        var conta = await Lookup(TipoLookup.ContasFinanceiras, c => !c.Marcador);
        var desconto = Math.Round(parcela.Valor * 0.1m, 2);
        var input = new BaixaInput
        {
            Tipo = TipoBaixa.Recebimento,
            Data = Hoje,
            Titulos = [new BaixaTituloInput { TituloId = parcela.Id, Valor = parcela.Valor, Desconto = desconto }],
            Formas = [new BaixaFormaInput { FormaPagamentoId = pix.Id, Valor = parcela.Valor - desconto, ContaFinanceiraId = conta.Id }]
        };
        var baixas = banco.Obter<BaixaService>();

        await ComoAsync("financeiro", async () =>
        {
            var alcada = await Assert.ThrowsAsync<AlcadaDescontoException>(() => baixas.RegistrarAsync(input, Ct));
            Assert.Equal((10m, 5m, desconto), (alcada.Percentual, alcada.Limite, alcada.Valor));
            input.PedidoDesconto = new PedidoDesconto { Motivo = "Pagou à vista antes do vencimento" }; // sem senha não há fila no recebimento
            await Assert.ThrowsAsync<AlcadaDescontoException>(() => baixas.RegistrarAsync(input, Ct));

            input.PedidoDesconto = new PedidoDesconto { Motivo = "Pagou à vista antes do vencimento", Login = "admin", Senha = "Admin@123" };
            await baixas.RegistrarAsync(input, Ct);
        });

        var linha = Assert.Single(await banco.Obter<DescontoService>().RecebimentosAsync(Hoje, Hoje, Ct), r => r.Descricao.EndsWith(parcela.Descricao));
        Assert.Equal(("financeiro", "admin", "Pagou à vista antes do vencimento", 10m), (linha.Usuario, linha.AprovadoPor, linha.Motivo, linha.Percentual));
        await baixas.EstornarTituloAsync(TipoBaixa.Recebimento, parcela.Id, Ct);
    }

    [Fact]
    public async Task Demonstracao_tem_pedido_na_fila_e_descontos_aprovados_pelo_administrador()
    {
        var descontos = banco.Obter<DescontoService>();
        Assert.Contains(await descontos.PendentesAsync(Ct), p => p.Percentual == 12 && p.PodeAprovar);
        var orcamentos = await descontos.OrcamentosAsync(Hoje.AddYears(-3), Hoje, Ct);
        Assert.Contains(orcamentos, o => o.Situacao == SituacaoDesconto.Aprovado && o.DecididoPor == "admin" && o.Percentual > 5);
        // O que os vendedores deram sozinhos fica dentro dos 5% do comercial (o administrador tem alçada de 100%).
        Assert.All(orcamentos.Where(o => o.Situacao == SituacaoDesconto.DentroDaAlcada && o.ConcedidoPor is "comercial" or "vendas"),
            o => Assert.True(o.Percentual <= 5));
    }
}
