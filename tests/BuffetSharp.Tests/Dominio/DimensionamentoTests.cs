using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;

namespace BuffetSharp.Tests.Dominio;

public class DimensionamentoTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static readonly Cargo Garcom = ComId(new Cargo { Nome = "Garçom", ValorDiariaPadrao = 180 }, 1);
    private static readonly Cargo Cozinheiro = ComId(new Cargo { Nome = "Cozinheiro", ValorDiariaPadrao = 260 }, 2);
    private static readonly Cargo Maitre = ComId(new Cargo { Nome = "Maître", ValorDiariaPadrao = 380 }, 3);
    private static readonly Cargo Barman = ComId(new Cargo { Nome = "Barman", ValorDiariaPadrao = 230 }, 4);
    private const int Casamento = 10;
    private const int Infantil = 11;

    private static RegraEquipe Regra(int? tipo, Cargo cargo, int porConvidados, int minimo, bool ativo = true) => new()
    {
        TipoEventoId = tipo, CargoId = cargo.Id, Cargo = cargo, ConvidadosPorProfissional = porConvidados, Minimo = minimo, Ativo = ativo
    };

    private static readonly RegraEquipe[] Regras =
    [
        Regra(null, Garcom, 15, 2), Regra(null, Cozinheiro, 60, 1), Regra(null, Maitre, 0, 1),
        Regra(Casamento, Garcom, 12, 4), Regra(Casamento, Barman, 80, 1),
        Regra(Infantil, Barman, 50, 1, ativo: false)
    ];

    [Fact]
    public void Um_a_cada_N_convidados_com_minimo()
    {
        Assert.Equal(7, Regra(null, Garcom, 15, 2).Quantidade(100)); // 100 / 15 = 6,7 → 7
        Assert.Equal(2, Regra(null, Garcom, 15, 2).Quantidade(10)); // o mínimo
        Assert.Equal(1, Regra(null, Maitre, 0, 1).Quantidade(300)); // só o mínimo, por evento
        Assert.Equal("1 a cada 15 convidados, mínimo 2", Regra(null, Garcom, 15, 2).Descricao);
        Assert.Single(Regra(null, Garcom, 0, 0).Validate(new System.ComponentModel.DataAnnotations.ValidationContext(new object())));
    }

    [Fact]
    public void Regra_do_tipo_substitui_a_geral_do_mesmo_cargo()
    {
        var casamento = DimensionamentoEquipe.Calcular(Regras, Casamento, 150);
        Assert.Equal([("Barman", 2), ("Cozinheiro", 3), ("Garçom", 13), ("Maître", 1)], casamento.Select(n => (n.Cargo, n.Quantidade)));
        Assert.Equal(2 * 230m + 3 * 260m + 13 * 180m + 380m, casamento.Sum(n => n.Custo));

        var infantil = DimensionamentoEquipe.Calcular(Regras, Infantil, 40); // a regra inativa do barman não vale
        Assert.Equal([("Cozinheiro", 1), ("Garçom", 3), ("Maître", 1)], infantil.Select(n => (n.Cargo, n.Quantidade)));
        Assert.True(DimensionamentoEquipe.TemRegras(Regras, 99));
        Assert.False(DimensionamentoEquipe.TemRegras([Regra(Casamento, Garcom, 12, 4)], 99));
    }

    private static Produto Produto(int id, decimal custo) => ComId(new Produto { Nome = $"P{id}", CustoMedio = custo }, id);

    /// <summary>Bebidas: refrigerante 0,07 por pessoa e hora e cerveja 0,6 por adulto e hora; salgado 0,1 por pessoa.</summary>
    private static Pacote Pacote()
    {
        var (refri, cerveja, salgado) = (Produto(1, 8m), Produto(2, 5m), Produto(3, 20m));
        var bebidas = ComId(new ItemCardapio { Nome = "Bebidas" }, 1);
        bebidas.DefinirFichaTecnica([(refri.Id, 0.07m, true, false), (cerveja.Id, 0.6m, true, true)]);
        var salgados = ComId(new ItemCardapio { Nome = "Salgados" }, 2);
        salgados.DefinirFichaTecnica([(salgado.Id, 0.1m, false, false)]);
        foreach (var (item, produtos) in new[] { (bebidas, new[] { refri, cerveja }), (salgados, new[] { salgado }) })
            foreach (var ficha in item.Ingredientes)
                typeof(FichaTecnicaItem).GetProperty(nameof(FichaTecnicaItem.Produto))!.SetValue(ficha, produtos.Single(p => p.Id == ficha.ProdutoId));

        var pacote = ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 1 }, 1);
        pacote.DefinirItens([1, 2]);
        foreach (var (item, prato) in pacote.Itens.Zip([bebidas, salgados]))
            typeof(PacoteItem).GetProperty(nameof(PacoteItem.ItemCardapio))!.SetValue(item, prato);
        return pacote;
    }

    [Fact]
    public void Bebidas_por_hora_e_alcool_so_para_adultos()
    {
        var pacote = Pacote();
        var consumo = pacote.CalcularConsumo(new ContextoConsumo(100, 80, 4));

        Assert.Equal(28m, consumo[1]); // 0,07 × 100 × 4
        Assert.Equal(192m, consumo[2]); // 0,6 × 80 adultos × 4
        Assert.Equal(10m, consumo[3]); // salgado não muda com a duração
        Assert.Equal(pacote.CalcularConsumo(100), pacote.CalcularConsumo(ContextoConsumo.Padrao(100))); // sem horário: 5 horas, todos adultos

        // Custo: 0,1 × 20 por pessoa + 0,07 × 8 por pessoa e hora + 0,6 × 5 por adulto e hora.
        Assert.Equal(new CustoInsumos(2m, 0.56m, 0, 3m), pacote.CustoInsumos);
        Assert.Equal(28m * 8 + 192m * 5 + 10m * 20, pacote.CustoInsumos.Calcular(new ContextoConsumo(100, 80, 4)));
        Assert.Equal(2m + 0.56m * 5 + 3m * 5, pacote.CustoPorPessoa); // referência: 5 horas, todos adultos
    }

    [Fact]
    public void Evento_usa_a_duracao_e_os_que_pagam_inteira()
    {
        var data = new DateOnly(2026, 6, 20);
        var orcamento = new Orcamento(1, 1, data, new TimeOnly(19, 0), new TimeOnly(23, 30), new QuantidadesConvidados(100, 12, 8), data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 1 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200 }, 1));
        orcamento.Enviar(Agora);
        var evento = orcamento.Aprovar("Casamento", Agora);

        Assert.Equal(new ContextoConsumo(100, 80, 4.5m), evento.ContextoConsumo(100));
        Assert.Equal(new ContextoConsumo(110, 88, 4.5m), evento.ContextoConsumo(110)); // excedentes na mesma proporção
        Assert.Equal(new ContextoConsumo(10, 10, 1), ContextoConsumo.De(10, 30, Agora, Agora.AddMinutes(30))); // no mínimo 1 hora
    }
}
