using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Fiscal;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>NFS-e do evento gravada no banco: ambiente simulado e o fluxo do provedor (Focus NFe trocado por um falso).</summary>
[Collection(ColecaoBanco.Nome)]
public class NfseIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ProvedorFalso : IProvedorNfse
    {
        public Queue<RespostaNfse> Respostas { get; } = new();
        public List<string> Chamadas { get; } = [];

        public Task<RespostaNfse> EmitirAsync(PedidoNfse pedido, CancellationToken ct = default)
        {
            Chamadas.Add($"emitir {pedido.Referencia} {pedido.Tomador.Documento} {pedido.Servico.Valor}");
            return Task.FromResult(Respostas.Dequeue());
        }

        public Task<RespostaNfse> ConsultarAsync(AmbienteNfse ambiente, string referencia, CancellationToken ct = default)
        {
            Chamadas.Add($"consultar {referencia}");
            return Task.FromResult(Respostas.Dequeue());
        }

        public Task<RespostaNfse> CancelarAsync(AmbienteNfse ambiente, string referencia, string justificativa, CancellationToken ct = default)
        {
            Chamadas.Add($"cancelar {referencia}");
            return Task.FromResult(Respostas.Dequeue());
        }

        public Task<byte[]> BaixarXmlAsync(AmbienteNfse ambiente, string caminhoXml, CancellationToken ct = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes($"<xml>{caminhoXml}</xml>"));
    }

    private NfseService Servico(IProvedorNfse provedor, NfseOptions? opcoes = null) =>
        new(banco.Obter<IBuffetDbContextFactory>(), provedor, opcoes ?? new NfseOptions { TokenHomologacao = "tok" },
            banco.Obter<ISessaoUsuario>(), TimeProvider.System);

    private async Task ConfigurarAsync(AmbienteNfse ambiente)
    {
        var servico = banco.Obter<NfseService>();
        var configuracao = await servico.ObterConfiguracaoAsync(Ct);
        configuracao.Ambiente = ambiente;
        configuracao.Cnpj = "11.222.333/0001-81";
        configuracao.InscricaoMunicipal = "1234567";
        configuracao.CodigoMunicipio = "3550308";
        configuracao.AliquotaIss = 3;
        await servico.SalvarConfiguracaoAsync(configuracao, Ct);
    }

    /// <summary>Evento confirmado, com valor e sem nota ativa (cada teste usa um diferente).</summary>
    private async Task<Evento> EventoSemNotaAsync(int pular = 0)
    {
        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        var comNota = await db.NotasFiscaisServico.Select(n => n.EventoId).Distinct().ToListAsync(Ct);
        return await db.Eventos.AsNoTracking().Include(e => e.Cliente)
            .Where(e => e.Status == StatusEvento.Confirmado && e.ValorTotal > 0 && !comNota.Contains(e.Id))
            .OrderBy(e => e.Id).Skip(pular).FirstAsync(Ct);
    }

    [Fact]
    public async Task Simulado_emite_uma_nota_por_evento_baixa_o_xml_e_cancela()
    {
        await ConfigurarAsync(AmbienteNfse.Simulado);
        var servico = banco.Obter<NfseService>();
        var evento = await EventoSemNotaAsync();

        var preparo = await servico.PrepararAsync(evento.Id, Ct);
        Assert.Empty(preparo.Pendencias);
        Assert.Null(preparo.NotaAtiva);
        Assert.Equal(evento.ValorTotal, preparo.Valor);
        Assert.Equal(3m, preparo.AliquotaIss);
        Assert.Contains(evento.Codigo, preparo.Discriminacao);
        Assert.Contains(evento.Titulo, preparo.Discriminacao);
        Assert.DoesNotContain("{{", preparo.Discriminacao);

        var notaId = await servico.EmitirAsync(new EmissaoNfseInput
        {
            EventoId = evento.Id, Valor = evento.ValorTotal - 100, Discriminacao = preparo.Discriminacao
        }, Ct);

        var nota = (await banco.Obter<IConsultasFiscais>().ListarNotasServicoAsync(new FiltroNotasServico(), Ct)).Single(n => n.Id == notaId);
        Assert.Equal((StatusNfse.Autorizada, AmbienteNfse.Simulado, evento.ValorTotal - 100), (nota.Status, nota.Ambiente, nota.ValorServicos));
        Assert.Equal($"SIM{notaId:D6}", nota.Numero);
        Assert.Equal(Documento.SomenteDigitos(evento.Cliente!.Documento), nota.TomadorDocumento);
        Assert.StartsWith(evento.Codigo, nota.Evento);
        Assert.Equal(Math.Round((evento.ValorTotal - 100) * 0.03m, 2), nota.ValorIss);
        Assert.Equal("admin", nota.Usuario);

        Assert.NotNull((await servico.PrepararAsync(evento.Id, Ct)).NotaAtiva);
        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.EmitirAsync(new EmissaoNfseInput
        {
            EventoId = evento.Id, Valor = 10, Discriminacao = "Outra"
        }, Ct));

        var (nome, xml) = await servico.BaixarXmlAsync(notaId, Ct);
        Assert.Equal($"NFS-e SIM{notaId:D6}.xml", nome);
        Assert.Contains("sem valor fiscal", Encoding.UTF8.GetString(xml));

        await Assert.ThrowsAsync<RegraNegocioException>(() => servico.CancelarAsync(notaId, "curto", Ct));
        await servico.CancelarAsync(notaId, "Valor da nota emitido errado", Ct);
        nota = (await banco.Obter<IConsultasFiscais>().ListarNotasServicoAsync(new FiltroNotasServico { Status = StatusNfse.Cancelada }, Ct))
            .Single(n => n.Id == notaId);
        Assert.Equal("Valor da nota emitido errado", nota.MotivoCancelamento);

        // Cancelada, o evento pode receber outra nota.
        Assert.Null((await servico.PrepararAsync(evento.Id, Ct)).NotaAtiva);
        await servico.EmitirAsync(new EmissaoNfseInput { EventoId = evento.Id, Valor = evento.ValorTotal, Discriminacao = "Buffet" }, Ct);
    }

    [Fact]
    public async Task Provedor_autoriza_depois_da_consulta_e_recusa_libera_o_evento()
    {
        await ConfigurarAsync(AmbienteNfse.Homologacao);
        try
        {
            var provedor = new ProvedorFalso();
            var servico = Servico(provedor);
            var evento = await EventoSemNotaAsync();

            provedor.Respostas.Enqueue(new RespostaNfse(StatusNfse.Processando, Mensagem: "Aguardando a prefeitura."));
            var notaId = await servico.EmitirAsync(new EmissaoNfseInput { EventoId = evento.Id, Valor = 500, Discriminacao = "Buffet" }, Ct);
            Assert.StartsWith($"emitir evt{evento.Id}-", provedor.Chamadas[0]);
            Assert.EndsWith($"{Documento.SomenteDigitos(evento.Cliente!.Documento)} 500", provedor.Chamadas[0]);
            Assert.Equal("NFS-e enviada, aguardando a prefeitura", (await servico.PrepararAsync(evento.Id, Ct)).NotaAtiva);

            provedor.Respostas.Enqueue(new RespostaNfse(StatusNfse.Autorizada, "4521", "X9K2", DateTime.Today, "https://prefeitura/4521", "/arquivos/4521.xml"));
            Assert.Equal(StatusNfse.Autorizada, await servico.AtualizarSituacaoAsync(notaId, Ct));
            Assert.Equal(StatusNfse.Autorizada, await servico.AtualizarSituacaoAsync(notaId, Ct)); // autorizada não consulta de novo
            Assert.Equal(2, provedor.Chamadas.Count);
            Assert.Equal("<xml>/arquivos/4521.xml</xml>", Encoding.UTF8.GetString((await servico.BaixarXmlAsync(notaId, Ct)).Conteudo));

            // A prefeitura não cancelou: a nota continua autorizada.
            provedor.Respostas.Enqueue(new RespostaNfse(StatusNfse.Erro, Mensagem: "Prazo de cancelamento expirado"));
            var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => servico.CancelarAsync(notaId, "Valor emitido errado no evento", Ct));
            Assert.Contains("Prazo de cancelamento expirado", erro.Message);
            Assert.NotNull((await servico.PrepararAsync(evento.Id, Ct)).NotaAtiva);

            // Outro evento: recusada na emissão, fica gravada com o motivo e o evento pode emitir de novo.
            var outro = await EventoSemNotaAsync();
            provedor.Respostas.Enqueue(new RespostaNfse(StatusNfse.Erro, Mensagem: "Inscrição municipal inválida"));
            var recusadaId = await servico.EmitirAsync(new EmissaoNfseInput { EventoId = outro.Id, Valor = 300, Discriminacao = "Buffet" }, Ct);
            var recusada = (await banco.Obter<IConsultasFiscais>().ListarNotasServicoAsync(new FiltroNotasServico { Status = StatusNfse.Erro }, Ct))
                .Single(n => n.Id == recusadaId);
            Assert.Equal("Inscrição municipal inválida", recusada.Mensagem);
            Assert.Null((await servico.PrepararAsync(outro.Id, Ct)).NotaAtiva);
        }
        finally
        {
            await ConfigurarAsync(AmbienteNfse.Simulado);
        }
    }

    [Fact]
    public async Task Sem_token_nao_emite_fora_do_simulado()
    {
        await ConfigurarAsync(AmbienteNfse.Homologacao);
        try
        {
            var provedor = new ProvedorFalso();
            var servico = Servico(provedor, new NfseOptions());
            var evento = await EventoSemNotaAsync(pular: 1);

            Assert.Contains((await servico.PrepararAsync(evento.Id, Ct)).Pendencias, p => p.Contains("Token do Focus NFe"));
            await Assert.ThrowsAsync<RegraNegocioException>(() =>
                servico.EmitirAsync(new EmissaoNfseInput { EventoId = evento.Id, Valor = 100, Discriminacao = "Buffet" }, Ct));
            Assert.Empty(provedor.Chamadas);

            await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
            Assert.False(await db.NotasFiscaisServico.AnyAsync(n => n.EventoId == evento.Id, Ct));
        }
        finally
        {
            await ConfigurarAsync(AmbienteNfse.Simulado);
        }
    }
}
