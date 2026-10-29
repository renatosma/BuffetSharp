using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Lista de espera: interessado, fila pela ordem de entrada, aviso com prazo de resposta e encerramento.</summary>
public class EsperaDataTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly Data = new(2026, 6, 20);

    private static EsperaData Espera(int clienteId, DateTime entrou, int id)
    {
        var espera = new EsperaData(clienteId, null, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), 3, null, 80, null, entrou);
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(espera, id);
        return espera;
    }

    [Fact]
    public void Espera_precisa_de_um_interessado_e_de_data_futura()
    {
        Assert.Throws<RegraNegocioException>(() => new EsperaData(null, null, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), null, null, 80, null, Agora));
        Assert.Throws<RegraNegocioException>(() => new EsperaData(1, 2, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), null, null, 80, null, Agora));
        Assert.Throws<RegraNegocioException>(() => new EsperaData(1, null, new DateOnly(2026, 3, 1), new TimeOnly(19, 0), new TimeOnly(23, 0), null, null, 80, null, Agora));
        Assert.Throws<RegraNegocioException>(() => new EsperaData(null, 4, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), 0, 0, 0, null, Agora));

        var lead = new EsperaData(null, 4, Data, new TimeOnly(19, 0), new TimeOnly(1, 0), 0, 0, 120, "  Pode ser outro salão  ", Agora);
        Assert.Equal(((int?)null, (int?)null, (int?)null, "Pode ser outro salão", Data.AddDays(1).ToDateTime(new TimeOnly(1, 0))),
            (lead.ClienteId, lead.EspacoId, lead.TipoEventoId, lead.Observacao, lead.Fim));
        Assert.Equal((SituacaoEspera.Aguardando, Agora, true), (lead.Situacao, lead.EntrouEm, lead.Ativa));
    }

    [Fact]
    public void Fila_segue_a_ordem_de_entrada_e_pula_quem_nao_respondeu()
    {
        var primeira = Espera(1, Agora.AddDays(-5), 10);
        var segunda = Espera(2, Agora.AddDays(-3), 11);
        var terceira = Espera(3, Agora.AddDays(-1), 12);
        Assert.Equal([primeira, segunda, terceira], EsperaData.Fila([terceira, primeira, segunda], Agora, diasResposta: 2));

        primeira.RegistrarAviso(Agora);
        Assert.Equal((SituacaoEspera.Avisado, Agora), (primeira.Situacao, primeira.AvisadoEm));
        Assert.Same(primeira, EsperaData.Fila([primeira, segunda, terceira], Agora.AddDays(1), 2)[0]);

        // Passou o prazo sem resposta: a vez é do próximo.
        Assert.True(primeira.SemResposta(Agora.AddDays(3), 2));
        Assert.Equal([segunda, terceira], EsperaData.Fila([primeira, segunda, terceira], Agora.AddDays(3), 2));

        primeira.VoltarParaFila();
        Assert.Equal((SituacaoEspera.Aguardando, (DateTime?)null), (primeira.Situacao, primeira.AvisadoEm));
        segunda.Desistir(Agora);
        Assert.Equal([primeira, terceira], EsperaData.Fila([primeira, segunda, terceira], Agora, 2));
    }

    [Fact]
    public void Espera_encerrada_nao_muda_mais()
    {
        var espera = Espera(1, Agora, 10);
        espera.Converter(55, Agora);
        Assert.Equal((SituacaoEspera.Convertida, 55, Agora), (espera.Situacao, espera.OrcamentoId, espera.EncerradaEm));
        Assert.Throws<RegraNegocioException>(() => espera.RegistrarAviso(Agora));
        Assert.Throws<RegraNegocioException>(() => espera.Desistir(Agora));
        Assert.Throws<RegraNegocioException>(() => espera.Alterar(Data, new TimeOnly(19, 0), new TimeOnly(23, 0), null, null, 50, null, Agora));
        Assert.Throws<RegraNegocioException>(() => espera.VoltarParaFila());
    }
}
