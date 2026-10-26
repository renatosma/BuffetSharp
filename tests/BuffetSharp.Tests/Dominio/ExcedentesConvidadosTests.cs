using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class ExcedentesConvidadosTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly DataEvento = new(2026, 6, 20);
    private static readonly QuantidadesConvidados Contratado = new(100, Meia: 20, Isentos: 10); // 70 inteira

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    /// <summary>100 pessoas (70 inteira, 20 meia, 10 isentos) a R$ 100: 7.000 + 1.000 = 8.000.</summary>
    private static Evento EventoComCriancas(QuantidadesConvidados? convidados = null)
    {
        var orcamento = new Orcamento(1, 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), convidados ?? Contratado,
            validoAte: DataEvento.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.Enviar(Agora);
        return orcamento.Aprovar("Evento teste", Agora);
    }

    private static DateTime NaFesta(Evento evento) => evento.Inicio.AddHours(1);

    private static ExcedentesConvidados Calcular(int inteira, int meia, int isentos) =>
        ExcedentesConvidados.Calcular(Contratado, new QuantidadesConvidados(inteira + meia + isentos, meia, isentos),
            RegraFaixasEtarias.Padrao, 100m);

    [Fact]
    public void Cobra_inteira_e_meia_de_quem_passou_do_contratado()
    {
        var excedentes = Calcular(inteira: 75, meia: 25, isentos: 10);

        Assert.Equal((5, 5, 0), (excedentes.Inteira, excedentes.Meia, excedentes.Isentos));
        Assert.Equal(5 * 100m + 5 * 50m, excedentes.Valor);
        Assert.Equal(10, excedentes.Pagantes);
    }

    [Fact]
    public void Vaga_de_adulto_cobre_crianca_mas_vaga_de_crianca_nao_cobre_adulto()
    {
        // Faltaram 5 adultos e vieram 5 crianças a mais: a mesma conta, nada a cobrar.
        Assert.Equal(ExcedentesConvidados.Nenhum, Calcular(inteira: 65, meia: 25, isentos: 10));

        // Faltaram 5 crianças e vieram 5 adultos a mais: os adultos pagam inteira.
        var adultos = Calcular(inteira: 75, meia: 15, isentos: 10);
        Assert.Equal((5, 0, 500m), (adultos.Inteira, adultos.Meia, adultos.Valor));
    }

    [Fact]
    public void Isentos_excedentes_entram_na_contagem_sem_custo()
    {
        var excedentes = Calcular(inteira: 70, meia: 20, isentos: 15);

        Assert.Equal(5, excedentes.Total);
        Assert.Equal(0m, excedentes.Valor);

        // As vagas que sobraram (2 adultos que faltaram) cobrem parte dos isentos.
        Assert.Equal(3, Calcular(inteira: 68, meia: 20, isentos: 15).Isentos);
    }

    [Fact]
    public void Apuracao_lanca_parcela_propria_que_o_plano_de_pagamento_preserva()
    {
        var evento = EventoComCriancas();
        evento.GerarParcelas(2, new DateOnly(2026, 4, 1));
        Assert.Throws<RegraNegocioException>(() =>
            evento.ApurarExcedentes(new QuantidadesConvidados(110, 20, 10), 100m, DataEvento.AddDays(5), null, evento.Inicio.AddMinutes(-1)));

        var parcela = evento.ApurarExcedentes(new QuantidadesConvidados(112, 20, 10), 100m, DataEvento.AddDays(5), 3, NaFesta(evento));

        Assert.NotNull(parcela);
        Assert.True(parcela.Excedentes);
        Assert.Equal((1200m, 3, 3), (parcela.Valor, parcela.Numero, parcela.FormaPagamentoId));
        Assert.Equal("Convidados excedentes (12)", parcela.Descricao);
        Assert.Equal(9200m, evento.ValorTotal);
        Assert.Equal((112, 12, 1200m), (evento.PresentesApurados, evento.Excedentes.Total, evento.ValorExcedentes));
        Assert.Equal(112, evento.PessoasAtendidas);
        Assert.Equal(0m, evento.ValorNaoParcelado);
        Assert.Equal(8000m, evento.SaldoAParcelar);

        // Refazer o plano não engole a cobrança dos excedentes.
        var plano = evento.GerarParcelas(4, new DateOnly(2026, 3, 10));
        Assert.Equal(8000m, plano.Sum(p => p.Valor));
        Assert.Contains(evento.Parcelas, p => p.Excedentes && p.Status == StatusTitulo.Aberto);
        Assert.Equal(9200m, evento.SaldoAReceber);

        Assert.Throws<RegraNegocioException>(() =>
            evento.ApurarExcedentes(new QuantidadesConvidados(115, 20, 10), 100m, DataEvento.AddDays(5), null, NaFesta(evento)));
        Assert.Throws<RegraNegocioException>(() =>
            evento.AlterarDados("Evento teste", 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), 120, null));
    }

    [Fact]
    public void Contagem_parte_do_check_in()
    {
        var evento = EventoComCriancas();
        for (var id = 1; id <= 5; id++)
        {
            var convidado = evento.AdicionarConvidado($"Família {id}", acompanhantes: 20);
            typeof(Convidado).GetProperty(nameof(Convidado.Id))!.SetValue(convidado, id);
            evento.RegistrarCheckIn(id, NaFesta(evento));
        }
        Assert.Equal(105, evento.PessoasPresentes);

        Assert.Throws<RegraNegocioException>(() =>
            evento.ApurarExcedentes(new QuantidadesConvidados(104, 20, 10), 100m, DataEvento, null, NaFesta(evento)));

        var parcela = evento.ApurarExcedentes(new QuantidadesConvidados(105, 20, 10), 100m, DataEvento, null, NaFesta(evento));
        Assert.Equal(500m, parcela!.Valor);
    }

    [Fact]
    public void Sem_excedentes_registra_a_contagem_sem_cobranca()
    {
        var evento = EventoComCriancas();

        var parcela = evento.ApurarExcedentes(new QuantidadesConvidados(95, 20, 10), 100m, DataEvento, null, NaFesta(evento));

        Assert.Null(parcela);
        Assert.True(evento.ExcedentesApurados);
        Assert.Equal(8000m, evento.ValorTotal);
        Assert.Equal(100, evento.PessoasAtendidas); // os insumos continuam pelo contratado
        Assert.Empty(evento.Parcelas);
    }

    [Fact]
    public void Desfazer_cancela_a_cobranca_e_devolve_o_total_enquanto_nao_foi_paga()
    {
        var evento = EventoComCriancas();
        var parcela = evento.ApurarExcedentes(new QuantidadesConvidados(110, 20, 10), 100m, DataEvento.AddDays(5), null, NaFesta(evento))!;
        var saldo = (Parcela)parcela.Desmembrar(400m);
        Assert.True(saldo.Excedentes);
        evento.Realizar(evento.Fim);

        parcela.RegistrarPagamento(DataEvento, 400m, null);
        Assert.Throws<RegraNegocioException>(evento.DesfazerApuracaoExcedentes);

        parcela.EstornarPagamento();
        evento.DesfazerApuracaoExcedentes();

        Assert.All(evento.Parcelas, p => Assert.Equal(StatusTitulo.Cancelado, p.Status));
        Assert.Equal(8000m, evento.ValorTotal);
        Assert.False(evento.ExcedentesApurados);
        Assert.Equal((0, 0m), (evento.Excedentes.Total, evento.ValorExcedentes));

        // Depois de realizado, a contagem pode ser refeita.
        Assert.NotNull(evento.ApurarExcedentes(new QuantidadesConvidados(101, 20, 10), 100m, DataEvento.AddDays(5), null, evento.Fim));
        Assert.Equal(8100m, evento.ValorTotal);
    }
}
