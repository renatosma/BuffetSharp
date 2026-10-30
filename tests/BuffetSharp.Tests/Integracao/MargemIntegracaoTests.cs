using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Margem prevista do orçamento: custos congelados, margem mínima na alçada, relatório e previsto × realizado.</summary>
[Collection(ColecaoBanco.Nome)]
public class MargemIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

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

    private async Task<OrcamentoInput> NovoOrcamentoAsync(int diasAFrente)
    {
        var input = banco.Obter<OrcamentoService>().Novo();
        input.ClienteId = (await Lookup(TipoLookup.Clientes)).Id;
        input.TipoEventoId = (await Lookup(TipoLookup.TiposEvento, t => t.Nome == "Corporativo")).Id;
        input.PacoteId = (await Lookup(TipoLookup.Pacotes, p => p.Nome == "Prata")).Id;
        input.EspacoId = (await Lookup(TipoLookup.Espacos, e => e.Nome == "Salão Jardim")).Id;
        input.Data = Hoje.AddYears(3).AddDays(diasAFrente);
        input.HoraInicio = new TimeOnly(8, 0);
        input.HoraFim = new TimeOnly(9, 30);
        input.Convidados = 100;
        input.VendedorId = (await Lookup(TipoLookup.Vendedores, v => v.Nome == "Carla Comercial")).Id;
        input.FormaPagamentoId = (await Lookup(TipoLookup.FormasPagamento, f => f.Nome == "Cartão de Crédito")).Id;
        return input;
    }

    [Fact]
    public async Task Custos_previstos_saem_dos_cadastros_e_ficam_gravados()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var input = await NovoOrcamentoAsync(40);
        var id = await orcamentos.SalvarAsync(input, Ct);

        var salvo = await orcamentos.ObterAsync(id, Ct);
        var prata = (await banco.Obter<IConsultasEstoqueCardapio>().ListarPacotesAsync(Ct)).Single(p => p.Nome == "Prata");
        var custos = salvo.Custos;
        Assert.InRange(custos.Insumos, prata.CustoPorPessoa * 100 - 0.5m, prata.CustoPorPessoa * 100 + 0.5m); // a consulta arredonda o custo
        Assert.Equal(700m + 16m * 100, custos.Equipe); // corporativo: R$ 700 fixos + R$ 16 por convidado
        Assert.Equal(1_300m, custos.Espaco);
        var total = (await banco.Obter<IConsultasComercial>().ListarOrcamentosAsync(new FiltroOrcamentos { Texto = $"ORC-{id:D5}" }, Ct)).Single();
        Assert.Equal((Math.Round(total.ValorTotal * 0.08m, 2), Math.Round(total.ValorTotal * 0.03m, 2), Math.Round(total.ValorTotal * 0.0349m, 2)),
            (custos.Impostos, custos.Comissao, custos.Taxa));
        Assert.Equal(CalculadoraMargem.Percentual(total.ValorTotal, custos), total.MargemPercentual);

        var parametros = await banco.Obter<MargemService>().ParametrosAsync(Ct);
        Assert.Equal(custos, parametros.Calcular(total.ValorTotal, input.PacoteId, 100, input.TipoEventoId, input.EspacoId, [],
            input.VendedorId, input.FormaPagamentoId)); // a tela calcula igual ao serviço
        Assert.True(parametros.PodeVerCustos);
    }

    [Fact]
    public async Task Margem_abaixo_da_minima_so_o_administrador_aprova()
    {
        int servicoCaro;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var servico = new ServicoAdicional { Nome = $"Show exclusivo {Guid.NewGuid():N}"[..30], Preco = 1_000m, Custo = 30_000m };
            db.ServicosAdicionais.Add(servico);
            await db.SaveChangesAsync(Ct);
            servicoCaro = servico.Id;
        }
        var orcamentos = banco.Obter<OrcamentoService>();
        var descontos = banco.Obter<DescontoService>();
        var input = await NovoOrcamentoAsync(50);
        input.Servicos = [new ServicoItemInput { ServicoAdicionalId = servicoCaro, Quantidade = 1, ValorUnitario = 1_000m }];
        var id = 0;

        await ComoAsync("comercial", async () =>
        {
            Assert.False(banco.Obter<MargemService>().PodeVerCustos);
            await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<MargemService>().OrcamentosAsync(Hoje, Hoje, Ct));
            var alcada = await Assert.ThrowsAsync<AlcadaDescontoException>(() => orcamentos.SalvarAsync(input, Ct));
            Assert.Equal(0m, alcada.Percentual); // sem desconto: o problema é só a margem
            Assert.NotNull(alcada.Margem);
            Assert.DoesNotContain("%", alcada.Message); // o comercial não vê o número da margem

            input.PedidoDesconto = new PedidoDesconto { Motivo = "Cliente quer o show", Login = "financeiro", Senha = "Financeiro@123" };
            Assert.Contains("só o administrador", (await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.SalvarAsync(input, Ct))).Message);
            input.PedidoDesconto = new PedidoDesconto { Motivo = "Cliente quer o show" };
            id = await orcamentos.SalvarAsync(input, Ct);
            Assert.False((await descontos.PendentesAsync(Ct)).Single(p => p.OrcamentoId == id).PodeAprovar);
        });
        await ComoAsync("financeiro", async () =>
        {
            Assert.False((await descontos.PendentesAsync(Ct)).Single(p => p.OrcamentoId == id).PodeAprovar);
            await Assert.ThrowsAsync<RegraNegocioException>(() => descontos.AprovarAsync(id, null, Ct));
        });

        var pedido = (await descontos.PendentesAsync(Ct)).Single(p => p.OrcamentoId == id);
        Assert.True(pedido.PodeAprovar);
        Assert.True(pedido.Margem < 0);
        await descontos.AprovarAsync(id, "Show fecha o contrato de outros dois eventos", Ct);
        await orcamentos.EnviarAsync(id, Ct);

        var relatorio = await banco.Obter<MargemService>().OrcamentosAsync(Hoje, Hoje, Ct);
        Assert.Equal(FaixaMargem.AbaixoDaMinima, relatorio.Single(o => o.OrcamentoId == id).Faixa);
        var porVendedor = MargemService.Agrupar(relatorio, o => o.Vendedor).Single(g => g.Grupo == "Carla Comercial");
        Assert.True(porVendedor.AbaixoDaMinima >= 1);
    }

    [Fact]
    public async Task Previsto_do_orcamento_e_realizado_do_evento_encerrado()
    {
        int eventoId;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            eventoId = await db.Eventos.Where(e => e.Status == StatusEvento.Realizado && e.Orcamento != null && e.Pacote != null)
                .OrderByDescending(e => e.Inicio).Select(e => e.Id).FirstAsync(Ct);

        var comparativo = await banco.Obter<MargemService>().ComparativoAsync(eventoId, Ct);

        Assert.True(comparativo.Encerrado);
        Assert.NotNull(comparativo.Orcamento);
        Assert.Equal(9, comparativo.Linhas.Count);
        Assert.True(comparativo.ReceitaPrevista > 0 && comparativo.ReceitaRealizada > 0);
        Assert.True(comparativo.Linhas.Single(l => l.Item == "Insumos do cardápio") is { Previsto: > 0, Realizado: > 0 });
        Assert.NotNull(comparativo.MargemPrevista);
        Assert.NotNull(comparativo.MargemRealizada);
    }
}
