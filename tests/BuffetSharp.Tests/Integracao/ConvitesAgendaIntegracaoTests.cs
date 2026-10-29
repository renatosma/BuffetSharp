using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Convites de agenda no banco: envio aos pendentes, atualização quando o horário muda, saída da escala e cancelamento.</summary>
[Collection(ColecaoBanco.Nome)]
public class ConvitesAgendaIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Convites_do_evento_acompanham_as_mudancas()
    {
        var lookups = banco.Obter<IConsultasLookup>();
        var eventos = banco.Obter<EventoService>();
        var convites = banco.Obter<ConviteService>();
        var espaco = await banco.Obter<ICadastroService<Espaco>>().SalvarAsync(new Espaco
        {
            Nome = $"Salão convites {Guid.NewGuid():N}"[..30], CapacidadeMaxima = 100, ValorLocacao = 1000
        }, Ct);
        var data = DateOnly.FromDateTime(DateTime.Today).AddYears(5).AddDays(Random.Shared.Next(1, 300));
        var input = new EventoInput
        {
            Titulo = "Convites de teste", ClienteId = (await lookups.ListarAsync(TipoLookup.Clientes, ct: Ct)).First().Id,
            TipoEventoId = (await lookups.ListarAsync(TipoLookup.TiposEvento, ct: Ct)).First().Id, EspacoId = espaco.Id, Data = data,
            HoraInicio = new TimeOnly(19, 0), HoraFim = new TimeOnly(23, 0), Convidados = 50, ValorTotal = 6000m
        };
        var id = await eventos.SalvarAsync(input, Ct);
        var funcionario = await banco.Obter<ICadastroService<Funcionario>>().SalvarAsync(new Funcionario
        {
            Nome = $"Garçom convite {Guid.NewGuid():N}"[..24], CargoId = (await lookups.ListarAsync(TipoLookup.Cargos, ct: Ct)).First().Id,
            ValorDiaria = 150, Ativo = true, Email = "garcom@equipe.exemplo.com.br"
        }, Ct);
        await eventos.EscalarFuncionarioAsync(id, funcionario.Id, "Garçom", null, Ct);
        await eventos.SalvarFornecedorAsync(id, new ContratacaoFornecedorInput
        {
            FornecedorId = (await lookups.ListarAsync(TipoLookup.Fornecedores, ct: Ct)).First().Id, Servico = "DJ", Valor = 1500m,
            Chegada = data.ToDateTime(new TimeOnly(17, 0)), Plano = new PlanoPagamentoInput { Quantidade = 1, PrimeiroVencimento = data }
        }, Ct);

        var lista = await convites.DoEventoAsync(id, Ct);
        Assert.Equal([DestinoConvite.Cliente, DestinoConvite.Equipe, DestinoConvite.Fornecedor], lista.Select(c => c.Destino));
        Assert.All(lista, c => Assert.True(c.Pendente));
        var equipe = lista.Single(c => c.Destino == DestinoConvite.Equipe);
        Assert.True(equipe.Inicio < data.ToDateTime(new TimeOnly(19, 0)), "a equipe chega para a montagem");
        Assert.Equal(data.ToDateTime(new TimeOnly(17, 0)), lista.Single(c => c.Destino == DestinoConvite.Fornecedor).Inicio);

        var resultado = await convites.EnviarPendentesAsync(id, Ct);
        Assert.Equal(3, resultado.Emails + resultado.WhatsApps + resultado.SemCanal.Count);
        Assert.True(resultado.Emails >= 1);
        lista = await convites.DoEventoAsync(id, Ct);
        Assert.Equal(resultado.SemCanal.Count, lista.Count(c => c.Pendente));
        Assert.False(lista.Single(c => c.Destino == DestinoConvite.Equipe).Pendente);

        // O horário mudou: quem recebeu fica desatualizado e o convite novo sai com a sequência 1.
        var edicao = await eventos.ObterParaEdicaoAsync(id, Ct);
        edicao.HoraInicio = new TimeOnly(20, 0);
        await eventos.SalvarAsync(edicao, Ct);
        equipe = (await convites.DoEventoAsync(id, Ct)).Single(c => c.Destino == DestinoConvite.Equipe);
        Assert.Equal((true, 1), (equipe.Desatualizado, equipe.Sequencia));
        var ics = Encoding.UTF8.GetString(convites.Arquivo(equipe).Conteudo);
        Assert.Contains("SEQUENCE:1", ics);
        Assert.Contains("METHOD:REQUEST", ics);
        await convites.EnviarEmailAsync(equipe, equipe.Email!, ct: Ct);

        // Saiu da escala: o convite dele vira cancelamento.
        await eventos.RemoverDaEquipeAsync(id, funcionario.Id, Ct);
        var saiu = (await convites.DoEventoAsync(id, Ct)).Single(c => c.Uid == ConviteService.UidEquipe(id, funcionario.Id));
        Assert.Equal((true, true, "garcom@equipe.exemplo.com.br"), (saiu.Cancelado, saiu.Pendente, saiu.Email));
        Assert.Contains("METHOD:CANCEL", Encoding.UTF8.GetString(convites.Arquivo(saiu).Conteudo));

        // Evento cancelado: todos os convites enviados viram cancelamento.
        await eventos.CancelarAsync(id, "Teste dos convites", Ct);
        lista = await convites.DoEventoAsync(id, Ct);
        Assert.All(lista, c => Assert.True(c.Cancelado));
        Assert.All(lista.Where(c => c.UltimoEnvio is not null), c => Assert.True(c.Pendente));

        await using var db = banco.Obter<IBuffetDbContextFactory>().Criar();
        Assert.True(await db.ConvitesEnviados.CountAsync(c => c.EventoId == id, Ct) >= resultado.Emails + resultado.WhatsApps + 1);
    }

    [Fact]
    public async Task Degustacao_tem_convite_e_a_agenda_do_buffet_e_exportada()
    {
        var convites = banco.Obter<ConviteService>();
        await using (var db = banco.Obter<IBuffetDbContextFactory>().Criar())
        {
            var degustacao = await db.Degustacoes.Where(d => d.Status == StatusDegustacao.Agendada).Select(d => d.Id).FirstOrDefaultAsync(Ct);
            if (degustacao > 0)
            {
                var convite = await convites.DaDegustacaoAsync(degustacao, Ct);
                Assert.Equal((DestinoConvite.Degustacao, false, 90.0), (convite.Destino, convite.Cancelado, (convite.Fim - convite.Inicio).TotalMinutes));
                await convites.RegistrarAsync(convite, CanalConvite.Arquivo, null, Ct);
                Assert.False((await convites.DaDegustacaoAsync(degustacao, Ct)).Pendente);
            }
        }

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var agenda = await convites.AgendaAsync(hoje.AddMonths(-1), hoje.AddMonths(3), Ct);
        Assert.Contains("METHOD:PUBLISH", agenda);
        Assert.Contains("BEGIN:VEVENT", agenda);
        Assert.Contains("UID:agenda-evento-", agenda);
    }
}
