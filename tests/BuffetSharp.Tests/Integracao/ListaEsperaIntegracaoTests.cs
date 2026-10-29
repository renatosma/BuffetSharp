using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Lista de espera no banco: a fila da data ocupada, o lembrete quando ela libera, o aviso, a desistência e a conversão.</summary>
[Collection(ColecaoBanco.Nome)]
public class ListaEsperaIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Quando_a_data_libera_o_primeiro_da_fila_vira_lembrete_e_a_vez_anda()
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var esperas = banco.Obter<EsperaService>();
        var lembretes = banco.Obter<LembreteService>();
        var espaco = await banco.Obter<ICadastroService<Espaco>>().SalvarAsync(new Espaco
        {
            Nome = $"Salão espera {Guid.NewGuid():N}"[..30], CapacidadeMaxima = 200, ValorLocacao = 1000
        }, Ct);
        var data = DateOnly.FromDateTime(DateTime.Today).AddYears(5).AddDays(Random.Shared.Next(1, 300));
        var clientes = await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct);
        var eventos = banco.Obter<EventoService>();
        var eventoId = await eventos.SalvarAsync(new EventoInput
        {
            Titulo = "Data disputada", ClienteId = clientes[0].Id, TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id,
            EspacoId = espaco.Id, Data = data, HoraInicio = new TimeOnly(19, 0), HoraFim = new TimeOnly(23, 0), Convidados = 80, ValorTotal = 8000m
        }, Ct);

        int leadId;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var lead = new Lead($"Lead da espera {Guid.NewGuid():N}"[..24], "(11) 98888-7777", null, OrigemLead.Instagram);
            db.Leads.Add(lead);
            await db.SaveChangesAsync(Ct);
            leadId = lead.Id;
        }

        var primeira = await esperas.SalvarAsync(new EsperaInput { ClienteId = clientes[1].Id, Data = data, EspacoId = espaco.Id, Convidados = 90 }, Ct);
        var segunda = await esperas.SalvarAsync(new EsperaInput { LeadId = leadId, Data = data, EspacoId = espaco.Id, Convidados = 70 }, Ct);
        await Assert.ThrowsAsync<Domain.Comum.RegraNegocioException>(() =>
            esperas.SalvarAsync(new EsperaInput { ClienteId = clientes[2].Id, Data = data, EspacoId = espaco.Id, Convidados = 500 }, Ct));

        var lista = (await esperas.ListarAsync(ct: Ct)).Where(e => e.EspacoId == espaco.Id).ToList();
        Assert.Equal([(primeira, 1), (segunda, 2)], lista.Select(e => (e.Id, e.Posicao!.Value)));
        Assert.All(lista, e => Assert.False(e.DataLivre));
        Assert.DoesNotContain(await lembretes.ListarAsync(Ct), l => l.Tipo == TipoLembrete.EsperaData && l.ReferenciaId == primeira);
        Assert.Equal(2, (await esperas.PorDiaAsync(data, data, Ct))[data]);
        Assert.All(await esperas.AlternativasAsync(data, new TimeOnly(19, 0), new TimeOnly(23, 0), espaco.Id, 90, ct: Ct),
            a => Assert.Equal((data.DayOfWeek, espaco.Id), (a.Data.DayOfWeek, a.EspacoId)));

        // O evento foi cancelado: a data liberou e a vez é do primeiro da fila.
        await eventos.CancelarAsync(eventoId, "Cliente desistiu", Ct);
        var lembrete = (await lembretes.ListarAsync(Ct)).Single(l => l.Tipo == TipoLembrete.EsperaData && l.ReferenciaId == primeira);
        Assert.Contains("Data liberada", lembrete.Titulo);
        Assert.Contains("ficou disponível", (await lembretes.PrepararWhatsAppAsync(lembrete, Ct))!.Texto);

        // Avisado, ele tem o prazo para responder: ninguém mais é chamado.
        await lembretes.MarcarTratadoAsync(lembrete, CanalLembrete.WhatsApp, Ct);
        Assert.DoesNotContain(await esperas.LembretesAsync(Ct), l => l.ReferenciaId is var id && (id == primeira || id == segunda));
        Assert.Equal(SituacaoEspera.Avisado, (await esperas.ListarAsync(ct: Ct)).Single(e => e.Id == primeira).Situacao);

        // Ele desistiu: a vez passa para o lead, que vira cliente para o orçamento.
        await esperas.DesistirAsync(primeira, Ct);
        Assert.Single(await esperas.LembretesAsync(Ct), l => l.ReferenciaId == segunda);
        var clienteId = await esperas.ClienteParaOrcamentoAsync(segunda, Ct);
        Assert.Equal(clienteId, await esperas.ClienteParaOrcamentoAsync(segunda, Ct));

        int orcamentoId;
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
            orcamentoId = await db.Orcamentos.Select(o => o.Id).FirstAsync(Ct);
        await esperas.ConverterAsync(segunda, orcamentoId, Ct);
        Assert.DoesNotContain(await esperas.ListarAsync(ct: Ct), e => e.EspacoId == espaco.Id);
        var encerradas = (await esperas.ListarAsync(somenteAtivas: false, Ct)).Where(e => e.EspacoId == espaco.Id).ToList();
        Assert.Equal([SituacaoEspera.Desistiu, SituacaoEspera.Convertida], encerradas.OrderBy(e => e.Id).Select(e => e.Situacao));
    }
}
