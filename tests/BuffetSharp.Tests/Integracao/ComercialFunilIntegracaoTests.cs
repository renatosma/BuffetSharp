using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Funil de leads, lembretes do dia e reajuste de tabela de preços.</summary>
[Collection(ColecaoBanco.Nome)]
public class ComercialFunilIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lead_recebe_contatos_e_vira_cliente()
    {
        var leads = banco.Obter<LeadService>();
        var input = leads.Novo();
        input.Nome = "Lead de Teste Conversão";
        input.Telefone = "(11) 97777-1234";
        input.Email = "lead.conversao@exemplo.com.br";
        input.Origem = OrigemLead.Feira;
        input.Convidados = 120;
        var id = await leads.SalvarAsync(input, Ct);

        await leads.RegistrarContatoAsync(id, "Quer visitar o salão no sábado.", DateOnly.FromDateTime(DateTime.Today).AddDays(2), Ct);
        var lista = await banco.Obter<IConsultasLeads>().ListarAsync(new FiltroLeads { SomenteEmAberto = true }, Ct);
        var resumo = lista.Single(l => l.Id == id);
        Assert.Equal(StatusLead.EmContato, resumo.Status);
        Assert.Equal(1, resumo.Contatos);

        var clienteId = await leads.ConverterAsync(new ConversaoLeadInput { LeadId = id }, Ct);
        var cliente = await banco.Obter<ICadastroService<Cliente>>().ObterAsync(clienteId, Ct);
        Assert.Equal("Lead de Teste Conversão", cliente!.Nome);
        Assert.Equal("(11) 97777-1234", cliente.Celular);

        var interacoes = await banco.Obter<IConsultasLeads>().InteracoesAsync(id, Ct);
        Assert.Equal(2, interacoes.Count);
        await Assert.ThrowsAsync<RegraNegocioException>(() => leads.ExcluirAsync(id, Ct));
    }

    [Fact]
    public async Task Funil_por_origem_soma_os_leads_da_base()
    {
        var funil = await banco.Obter<IConsultasLeads>().FunilPorOrigemAsync(DateTime.Today.AddYears(-2), DateTime.Today, Ct);

        Assert.NotEmpty(funil);
        Assert.All(funil, f => Assert.Equal(f.Leads, f.EmAberto + f.Convertidos + f.Perdidos));
        Assert.Contains(funil, f => f.Convertidos > 0 && f.EventosFechados > 0 && f.ValorFechado > 0);
    }

    [Fact]
    public async Task Lembretes_do_dia_somem_depois_de_tratados()
    {
        var servico = banco.Obter<LembreteService>();
        var lembretes = await servico.ListarAsync(Ct);

        // A base de demonstração tem parcelas vencidas, leads aguardando retorno e propostas em negociação.
        Assert.Contains(lembretes, l => l.Tipo == TipoLembrete.ParcelaVencida);
        Assert.Contains(lembretes, l => l.Tipo == TipoLembrete.RetornoLead);
        Assert.Equal(lembretes.Count, lembretes.Select(l => l.Chave).Distinct().Count());

        var cobranca = lembretes.First(l => l.Tipo == TipoLembrete.ParcelaVencida);
        var whatsApp = (await servico.PrepararWhatsAppAsync(cobranca, Ct))!;
        Assert.Contains("venc", whatsApp.Texto);
        Assert.Null(await servico.PrepararWhatsAppAsync(lembretes.FirstOrDefault(l => !LembreteService.TemMensagem(l.Tipo))
                                                         ?? cobranca with { Tipo = TipoLembrete.ContaPagarAVencer }, Ct));

        await servico.MarcarTratadoAsync(cobranca, CanalLembrete.Manual, Ct);
        await servico.MarcarTratadoAsync(cobranca, CanalLembrete.Manual, Ct); // tratar de novo não duplica
        Assert.DoesNotContain(await servico.ListarAsync(Ct), l => l.Chave == cobranca.Chave);
    }

    [Fact]
    public async Task Reajuste_imediato_muda_os_precos_e_pode_ser_desfeito()
    {
        var servico = banco.Obter<ReajusteService>();
        var espacos = banco.Obter<ICadastroService<Espaco>>();
        var antes = (await espacos.ListarAsync(q => q.Where(e => e.Ativo), Ct)).ToDictionary(e => e.Id, e => e.ValorLocacao);

        var input = new ReajusteInput
        {
            Descricao = "Teste de reajuste", Percentual = 10, VigenciaEm = DateOnly.FromDateTime(DateTime.Today),
            Pacotes = false, Servicos = false, Espacos = true, Arredondamento = ArredondamentoPreco.Reais
        };
        var simulacao = await servico.SimularAsync(input, Ct);
        Assert.Equal(antes.Count(a => ReajustePreco.Calcular(a.Value, 10, ArredondamentoPreco.Reais) != a.Value), simulacao.Count);

        var id = await servico.CriarAsync(input, Ct);
        try
        {
            var depois = await espacos.ListarAsync(q => q.Where(e => e.Ativo), Ct);
            Assert.All(depois, e => Assert.Equal(ReajustePreco.Calcular(antes[e.Id], 10, ArredondamentoPreco.Reais), e.ValorLocacao));
            Assert.Equal(simulacao.Count, (await servico.ItensAplicadosAsync(id, Ct)).Count);
        }
        finally
        {
            var resultado = await servico.DesfazerAsync(id, Ct);
            Assert.Empty(resultado.Mantidos);
        }
        Assert.All(await espacos.ListarAsync(q => q.Where(e => e.Ativo), Ct), e => Assert.Equal(antes[e.Id], e.ValorLocacao));
    }

    [Fact]
    public async Task Reajuste_agendado_so_vale_na_data_de_vigencia()
    {
        var relogio = new FakeTimeProvider(DateTimeOffset.Now);
        var servico = new ReajusteService(banco.Obter<IBuffetDbContextFactory>(), banco.Obter<ISessaoUsuario>(), relogio,
            NullLogger<ReajusteService>.Instance);
        var id = await servico.CriarAsync(new ReajusteInput
        {
            Descricao = "Agendado de teste", Percentual = 7, VigenciaEm = DateOnly.FromDateTime(DateTime.Today).AddDays(5),
            Pacotes = false, Servicos = true, Espacos = false
        }, Ct);

        Assert.Equal(0, await servico.AplicarPendentesAsync(Ct));
        Assert.Equal(StatusReajuste.Agendado, (await servico.ListarAsync(Ct)).Single(r => r.Id == id).Status);

        relogio.Advance(TimeSpan.FromDays(5));
        Assert.Equal(1, await servico.AplicarPendentesAsync(Ct));
        try
        {
            Assert.Equal(StatusReajuste.Aplicado, (await servico.ListarAsync(Ct)).Single(r => r.Id == id).Status);
        }
        finally
        {
            await servico.DesfazerAsync(id, Ct);
        }
    }
}
