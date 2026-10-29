using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Lembretes automáticos pelo WhatsApp do buffet (modo pasta) e o boleto registrado na mensagem de cobrança.</summary>
[Collection(ColecaoBanco.Nome)]
public class WhatsAppIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LembreteService Servico(IEnvioWhatsApp envio, WhatsAppOptions opcoesWhatsApp, bool automatico = true) =>
        new(banco.Obter<IBuffetDbContextFactory>(), banco.Obter<IConsultasLembretes>(), banco.Obter<IEnvioEmail>(), envio, opcoesWhatsApp,
            banco.Obter<BuffetOptions>(), new LembretesOptions { WhatsAppAutomatico = automatico }, banco.Obter<PixService>(),
            banco.Obter<EsperaService>(), banco.Obter<ISessaoUsuario>(), TimeProvider.System, banco.Obter<ILogger<LembreteService>>());

    [Fact]
    public async Task Lembretes_saem_uma_vez_pelo_whatsapp_so_no_horario()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"BuffetSharp_WhatsApp_{Guid.NewGuid():N}");
        var opcoes = new WhatsAppOptions { Modo = ModoEnvioWhatsApp.Pasta, Pasta = pasta, HorarioInicio = 0, HorarioFim = 24 };
        using var envio = new EnvioWhatsApp(opcoes);
        try
        {
            // Fora do horário nada sai (e nada é marcado como tratado).
            var foraDoHorario = new WhatsAppOptions { Modo = ModoEnvioWhatsApp.Pasta, Pasta = pasta, HorarioInicio = 24, HorarioFim = 24 };
            Assert.Equal(new ResultadoLembretesAutomaticos(0, 0), await Servico(envio, foraDoHorario).EnviarAutomaticosAsync(Ct));
            Assert.False(Directory.Exists(pasta) && Directory.GetFiles(pasta).Length > 0);

            // Desligado nas configurações: nada sai.
            Assert.Equal(new ResultadoLembretesAutomaticos(0, 0), await Servico(envio, opcoes, automatico: false).EnviarAutomaticosAsync(Ct));

            var servico = Servico(envio, opcoes);
            var candidatos = (await servico.ListarAsync(Ct)).Where(l =>
                l.Tipo is TipoLembrete.ParcelaAVencer or TipoLembrete.ParcelaVencida or TipoLembrete.Degustacao or TipoLembrete.Aniversario &&
                new Application.Modelos.MensagemWhatsApp(l.Telefone ?? "", "").TelefoneInternacional.Length is 12 or 13).ToList();
            Assert.Contains(candidatos, l => l.Tipo == TipoLembrete.ParcelaVencida);

            var resultado = await servico.EnviarAutomaticosAsync(Ct);
            Assert.Equal(candidatos.Count, resultado.WhatsApps);
            var arquivos = Directory.GetFiles(pasta, "*.txt");
            Assert.Equal(candidatos.Count, arquivos.Length);
            var vencida = candidatos.First(l => l.Tipo == TipoLembrete.ParcelaVencida);
            Assert.Contains(arquivos.Select(File.ReadAllText), t => t.Contains("Consta em aberto") && t.Contains(vencida.Pessoa!.Split(' ')[0]));

            // Já enviados: saíram da lista e não vão de novo.
            var restantes = await servico.ListarAsync(Ct);
            Assert.DoesNotContain(restantes, l => candidatos.Any(c => c.Chave == l.Chave));
            Assert.Equal(0, (await servico.EnviarAutomaticosAsync(Ct)).WhatsApps);
        }
        finally
        {
            await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
            await db.LembretesTratados.Where(t => t.Canal == CanalLembrete.WhatsAppAutomatico).ExecuteDeleteAsync(Ct);
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Cobranca_da_parcela_leva_o_boleto_registrado_pendente()
    {
        var servico = banco.Obter<LembreteService>();
        var vencida = (await servico.ListarAsync(Ct)).First(l => l.Tipo == TipoLembrete.ParcelaVencida);
        Assert.DoesNotContain("SIMULADO", (await servico.PrepararWhatsAppAsync(vencida, Ct))!.Texto);

        var cobrancas = banco.Obter<CobrancaService>();
        var configuracao = await cobrancas.ObterConfiguracaoAsync(Ct);
        configuracao.Ambiente = AmbienteCobranca.Simulado;
        configuracao.ContaFinanceiraId ??= (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.ContasFinanceiras, ct: Ct)).First(c => !c.Marcador).Id;
        await cobrancas.SalvarConfiguracaoAsync(configuracao, Ct);
        var preparo = await cobrancas.PrepararAsync(vencida.ReferenciaId, Ct);
        var id = preparo.CobrancaPendenteId ?? await cobrancas.EmitirAsync(
            new EmissaoCobrancaInput { ParcelaId = vencida.ReferenciaId, Tipo = TipoCobranca.Pix, Vencimento = preparo.VencimentoSugerido }, Ct);
        try
        {
            var pix = (await cobrancas.ObterAsync(id, Ct)).PixCopiaECola!;
            Assert.Contains(pix, (await servico.PrepararWhatsAppAsync(vencida, Ct))!.Texto);
            Assert.Contains(pix, (await servico.PrepararEmailAsync(vencida, Ct))!.Mensagem);
        }
        finally
        {
            await cobrancas.CancelarAsync(id, Ct);
        }
    }
}
