using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Preço por data: dia da semana, temporada que vira o ano, data especial exclusiva, soma dos ajustes e mínimo de convidados.</summary>
public class RegrasPrecoTests
{
    private static RegraPreco Sabado()
    {
        var regra = new RegraPreco { Nome = "Sábado", Tipo = TipoRegraPreco.DiaSemana, Percentual = 15, AjustaLocacao = true };
        regra.DefinirDia(DayOfWeek.Saturday, true);
        return regra;
    }

    private static RegraPreco AltaTemporada() => new()
    {
        Nome = "Alta temporada", Tipo = TipoRegraPreco.Temporada, Percentual = 10, DiaInicio = 15, MesInicio = 11, DiaFim = 31, MesFim = 1
    };

    private static RegraPreco Reveillon() => new()
    {
        Nome = "Réveillon", Tipo = TipoRegraPreco.DataEspecial, Percentual = 40, Data = new DateOnly(2020, 12, 31), Anual = true, Exclusiva = true,
        MinimoConvidados = 120
    };

    [Fact]
    public void Regras_da_data_se_somam()
    {
        RegraPreco[] regras = [Sabado(), AltaTemporada(), Reveillon()];

        // Sábado, 05/12/2026, na alta temporada: +25% por pessoa e +15% na locação (a temporada só ajusta a pessoa).
        var ajuste = AjustePreco.Calcular(regras, new DateOnly(2026, 12, 5), pacoteId: 1, espacoId: 1);
        Assert.Equal((25m, 15m, (int?)null), (ajuste.PercentualPessoa, ajuste.PercentualLocacao, ajuste.MinimoConvidados));
        Assert.Equal("Sábado +15%; Alta temporada +10% (por pessoa)", ajuste.Descricao);
        Assert.Equal((125m, 1150m), (ajuste.Pessoa(100m), ajuste.Locacao(1000m)));

        // A temporada vira o ano: 20/01 está dentro, 01/02 não.
        Assert.Equal(10m, AjustePreco.Calcular(regras, new DateOnly(2027, 1, 20), 1, 1).PercentualPessoa);
        Assert.Same(AjustePreco.Nenhum, AjustePreco.Calcular(regras, new DateOnly(2027, 2, 1), 1, 1));
        Assert.False(AjustePreco.Nenhum.TemAjuste);
    }

    [Fact]
    public void Data_especial_exclusiva_substitui_as_demais_e_exige_o_minimo()
    {
        // 31/12/2022 foi sábado e está na alta temporada, mas o réveillon tem preço próprio.
        var ajuste = AjustePreco.Calcular([Sabado(), AltaTemporada(), Reveillon()], new DateOnly(2022, 12, 31), null, null);
        Assert.Equal((40m, 0m, (int?)120), (ajuste.PercentualPessoa, ajuste.PercentualLocacao, ajuste.MinimoConvidados));
        Assert.Equal("Réveillon", Assert.Single(ajuste.Regras).Nome);
    }

    [Fact]
    public void Regra_de_um_pacote_ou_espaco_e_regra_inativa()
    {
        var sabado = Sabado();
        sabado.EspacoId = 7;
        var data = new DateOnly(2026, 6, 20);
        Assert.Equal(15m, AjustePreco.Calcular([sabado], data, 1, 7).PercentualPessoa);
        Assert.Empty(AjustePreco.Calcular([sabado], data, 1, 8).Regras);

        sabado.EspacoId = null;
        sabado.PacoteId = 3;
        Assert.Empty(AjustePreco.Calcular([sabado], data, 2, 7).Regras);
        sabado.Ativo = false;
        Assert.Empty(AjustePreco.Calcular([sabado], data, 3, 7).Regras);
    }

    [Fact]
    public void Desconto_somado_nao_passa_do_piso()
    {
        var baixa = new RegraPreco { Nome = "Baixa", Tipo = TipoRegraPreco.Temporada, Percentual = -50, DiaInicio = 1, MesInicio = 3, DiaFim = 30, MesFim = 6 };
        var segunda = new RegraPreco { Nome = "Segunda", Tipo = TipoRegraPreco.DiaSemana, Percentual = -50 };
        segunda.DefinirDia(DayOfWeek.Monday, true);
        var ajuste = AjustePreco.Calcular([baixa, segunda], new DateOnly(2026, 6, 1), null, null);
        Assert.Equal((-100m, 10m), (ajuste.PercentualPessoa, ajuste.Pessoa(100m)));
    }

    [Fact]
    public void Regra_precisa_de_dados_coerentes_com_o_tipo()
    {
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new RegraPreco { Nome = "Sem dia", Tipo = TipoRegraPreco.DiaSemana, Percentual = 10 }));
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new RegraPreco { Nome = "Temporada", Tipo = TipoRegraPreco.Temporada, Percentual = 10, DiaInicio = 31, MesInicio = 2, DiaFim = 1, MesFim = 3 }));
        Assert.Throws<ValidacaoException>(() => Validador.Validar(new RegraPreco { Nome = "Sem data", Tipo = TipoRegraPreco.DataEspecial, Percentual = 10 }));
        var exclusiva = Sabado();
        exclusiva.Exclusiva = true;
        Assert.Throws<ValidacaoException>(() => Validador.Validar(exclusiva));
        var nada = Sabado();
        (nada.AjustaPessoa, nada.AjustaLocacao) = (false, false);
        Assert.Throws<ValidacaoException>(() => Validador.Validar(nada));
        var exagero = Sabado();
        exagero.Percentual = 250;
        Assert.Throws<ValidacaoException>(() => Validador.Validar(exagero));

        Validador.Validar(Sabado());
        Validador.Validar(AltaTemporada());
        Validador.Validar(Reveillon());
        Assert.Equal(("sábados", "15/11 a 31/01", "31/12 (todo ano), exclusiva"), (Sabado().Quando, AltaTemporada().Quando, Reveillon().Quando));
    }

    [Fact]
    public void Orcamento_registra_o_ajuste_e_respeita_o_minimo_da_data()
    {
        var data = new DateOnly(2027, 12, 31);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(21, 0), new TimeOnly(3, 0), new QuantidadesConvidados(100), data.AddDays(-60));
        var ajuste = AjustePreco.Calcular([Reveillon()], data, null, null);

        Assert.Contains("mínimo é de 120", Assert.Throws<RegraNegocioException>(() => orcamento.AplicarRegrasPreco(ajuste)).Message);
        orcamento.AlterarDados(1, 1, data, new TimeOnly(21, 0), new TimeOnly(3, 0), new QuantidadesConvidados(130), data.AddDays(-60), null);
        orcamento.AplicarRegrasPreco(ajuste);
        Assert.Equal((40m, 0m, "Réveillon +40% (por pessoa)", (int?)120),
            (orcamento.AjustePessoaPercentual, orcamento.AjusteLocacaoPercentual, orcamento.AjusteDescricao, orcamento.MinimoConvidadosData));

        orcamento.AplicarRegrasPreco(AjustePreco.Nenhum);
        Assert.Null(orcamento.AjusteDescricao);
    }
}
