using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Cobrança dos convidados excedentes, gravada no banco.</summary>
[Collection(ColecaoBanco.Nome)]
public class ExcedentesIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyList<EventoDetalhe>> RealizadosAsync()
    {
        var eventos = banco.Obter<EventoService>();
        var lista = await banco.Obter<IConsultasEventos>().ListarAsync(new FiltroEventos { Status = StatusEvento.Realizado }, Ct);
        var detalhes = new List<EventoDetalhe>();
        foreach (var e in lista)
            detalhes.Add(await eventos.ObterDetalheAsync(e.Id, Ct));
        return detalhes;
    }

    [Fact]
    public async Task Base_de_demonstracao_tem_excedentes_cobrados_e_baixados_do_estoque()
    {
        var comExcedentes = (await RealizadosAsync()).Where(d => d.Evento.ValorExcedentes > 0).ToList();
        Assert.NotEmpty(comExcedentes);

        foreach (var evento in comExcedentes.Select(d => d.Evento))
        {
            var cobranca = Assert.Single(evento.Parcelas, p => p.Excedentes);
            Assert.Equal(evento.ValorExcedentes, cobranca.Valor);
            Assert.True(evento.PessoasAtendidas > evento.QuantidadeConvidados);
            Assert.Equal(0m, evento.ValorNaoParcelado);
        }
    }

    [Fact]
    public async Task Apura_cobra_e_desfaz_os_excedentes_de_um_evento_realizado()
    {
        var eventos = banco.Obter<EventoService>();
        var evento = (await RealizadosAsync()).Select(d => d.Evento)
            .First(e => !e.ExcedentesApurados && e.ValorPorPessoa > 0);
        var pix = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.FormasPagamento, ct: Ct)).First(f => f.Info == "Pix").Id;
        var valorOriginal = evento.ValorTotal;

        var (excedentes, parcelaId) = await eventos.ApurarExcedentesAsync(evento.Id, new ApuracaoExcedentesInput
        {
            Presentes = Math.Max(evento.PessoasPresentes, evento.QuantidadeConvidados) + 7,
            PresentesMeia = evento.ConvidadosMeia,
            PresentesIsentos = evento.ConvidadosIsentos,
            ValorPorPessoa = evento.ValorPorPessoa,
            Vencimento = evento.DataEvento.AddDays(5),
            FormaPagamentoId = pix
        }, Ct);

        var esperado = (Math.Max(evento.PessoasPresentes, evento.QuantidadeConvidados) + 7 - evento.QuantidadeConvidados) * evento.ValorPorPessoa;
        Assert.Equal(esperado, excedentes.Valor);
        Assert.NotNull(parcelaId);

        var detalhe = await eventos.ObterDetalheAsync(evento.Id, Ct);
        var cobranca = detalhe.Evento.Parcelas.Single(p => p.Id == parcelaId);
        Assert.True(cobranca.Excedentes);
        Assert.Equal((esperado, pix, StatusTitulo.Aberto), (cobranca.Valor, cobranca.FormaPagamentoId, cobranca.Status));
        Assert.Equal(valorOriginal + esperado, detalhe.Evento.ValorTotal);
        Assert.True(detalhe.Evento.ExcedentesApurados);

        var resumo = (await banco.Obter<IConsultasEventos>().ListarAsync(new FiltroEventos { Status = StatusEvento.Realizado }, Ct))
            .Single(e => e.Id == evento.Id);
        Assert.Equal(valorOriginal + esperado, resumo.ValorTotal);
        Assert.True(resumo.SaldoAReceber >= esperado);

        await Assert.ThrowsAsync<Domain.Comum.RegraNegocioException>(() => eventos.ApurarExcedentesAsync(evento.Id,
            new ApuracaoExcedentesInput { Presentes = evento.QuantidadeConvidados + 1, ValorPorPessoa = 1, Vencimento = evento.DataEvento }, Ct));

        await eventos.DesfazerApuracaoExcedentesAsync(evento.Id, Ct);
        detalhe = await eventos.ObterDetalheAsync(evento.Id, Ct);
        Assert.Equal(valorOriginal, detalhe.Evento.ValorTotal);
        Assert.False(detalhe.Evento.ExcedentesApurados);
        Assert.Equal(StatusTitulo.Cancelado, detalhe.Evento.Parcelas.Single(p => p.Id == parcelaId).Status);
    }
}
