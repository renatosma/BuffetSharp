using System.Globalization;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Faixas etárias (inteira, meia e isentos) do pacote ao contrato, gravadas no banco.</summary>
[Collection(ColecaoBanco.Nome)]
public class FaixasEtariasIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

    [Fact]
    public async Task Orcamento_com_criancas_gera_evento_e_contrato_com_as_faixas()
    {
        var pacotes = await banco.Obter<IConsultasEstoqueCardapio>().ListarPacotesAsync(Ct);
        var pacote = pacotes.Single(p => p.Nome == "Prata");
        Assert.Equal((5, 10, 50m), (pacote.IdadeMaximaIsento, pacote.IdadeMaximaMeia, pacote.PercentualMeia));
        var infantil = pacotes.Single(p => p.Nome == "Infantil Alegria");
        Assert.Equal((3, 7), (infantil.IdadeMaximaIsento, infantil.IdadeMaximaMeia));

        var orcamentos = banco.Obter<OrcamentoService>();
        var input = orcamentos.Novo();
        input.ClienteId = (await Lookup(TipoLookup.Clientes)).Id;
        input.TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id;
        input.PacoteId = pacote.Id;
        input.EspacoId = (await Lookup(TipoLookup.Espacos, e => e.Numero >= 100)).Id;
        input.Data = Hoje.AddYears(2).AddDays(150);
        input.HoraInicio = new TimeOnly(8, 0);
        input.HoraFim = new TimeOnly(9, 30);
        input.Convidados = 100;
        input.ConvidadosMeia = 20;
        input.ConvidadosIsentos = 10;
        input.PercentualMeia = 40m; // negociado
        var id = await orcamentos.SalvarAsync(input, Ct);

        var salvo = await orcamentos.ObterAsync(id, Ct);
        Assert.Equal((100, 20, 10, 40m), (salvo.Convidados, salvo.ConvidadosMeia, salvo.ConvidadosIsentos, salvo.PercentualMeia));
        var valorMeia = Math.Round(pacote.PrecoPorPessoa * 0.4m, 2);
        var esperado = pacote.PrecoPorPessoa * 70 + valorMeia * 20 + salvo.ValorEspaco!.Value;
        var resumo = (await banco.Obter<IConsultasComercial>().ListarOrcamentosAsync(new FiltroOrcamentos { Texto = $"ORC-{id:D5}" }, Ct)).Single();
        Assert.Equal(esperado, resumo.ValorTotal);

        var whatsapp = await banco.Obter<PropostaService>().PrepararWhatsAppAsync(id, Ct);
        Assert.Contains("100 convidados (70 inteira, 20 meia e 10 isentos)", Uri.UnescapeDataString(whatsapp.Link));

        var eventoId = await orcamentos.AprovarAsync(id, "Batizado do Theo", Ct);
        var evento = await banco.Obter<EventoService>().ObterParaEdicaoAsync(eventoId, Ct);
        Assert.Equal((100, 20, 10), (evento.Convidados, evento.ConvidadosMeia, evento.ConvidadosIsentos));

        var contrato = await banco.Obter<ContratoService>().PreVisualizarAsync(eventoId,
            "{{CONVIDADOS}}{{CONVIDADOS_DETALHE}}|{{VALOR_MEIA}}", Ct);
        Assert.Equal("100, sendo 70 com valor integral, 20 crianças de 6 a 10 anos com meia (40%) e 10 crianças até 5 anos isentas|" +
                     valorMeia.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")), contrato);
    }
}
