using System.Reflection;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class OrcamentoEventoTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly DataEvento = new(2026, 6, 20);

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static Pacote Pacote(decimal preco = 100m, int minimo = 50) =>
        ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = preco, MinimoConvidados = minimo }, 1);

    private static Espaco Espaco(int capacidade = 200, decimal locacao = 5000m) =>
        ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = capacidade, ValorLocacao = locacao }, 1);

    private static Orcamento NovoOrcamento(int convidados = 100) =>
        new(clienteId: 1, tipoEventoId: 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), convidados,
            validoAte: DataEvento.AddDays(-30));

    [Fact]
    public void Calcula_total_com_pacote_espaco_servicos_e_desconto()
    {
        var orcamento = NovoOrcamento(100);
        orcamento.DefinirPacote(Pacote(100m));
        orcamento.DefinirEspaco(Espaco(locacao: 5000m));
        orcamento.DefinirServicos([(ComId(new ServicoAdicional { Nome = "DJ", Preco = 2000 }, 1), 1m, 2000m)]);
        orcamento.AplicarDesconto(10);

        // (100 x 100) + 5000 + 2000 = 17000 - 10% = 15300
        Assert.Equal(17000m, orcamento.Resumo.Subtotal);
        Assert.Equal(1700m, orcamento.Resumo.Desconto);
        Assert.Equal(15300m, orcamento.ValorTotal);
    }

    [Fact]
    public void Evento_que_termina_apos_meia_noite_termina_no_dia_seguinte()
    {
        var orcamento = new Orcamento(1, 1, DataEvento, new TimeOnly(20, 0), new TimeOnly(3, 0), 100, DataEvento);

        Assert.Equal(DataEvento.AddDays(1).ToDateTime(new TimeOnly(3, 0)), orcamento.Fim);
    }

    [Fact]
    public void Respeita_minimo_do_pacote_e_capacidade_do_espaco()
    {
        var orcamento = NovoOrcamento(30);
        Assert.Throws<RegraNegocioException>(() => orcamento.DefinirPacote(Pacote(minimo: 50)));

        var grande = NovoOrcamento(300);
        Assert.Throws<RegraNegocioException>(() => grande.DefinirEspaco(Espaco(capacidade: 200)));
    }

    [Fact]
    public void Desconto_acima_do_limite_e_rejeitado()
    {
        var orcamento = NovoOrcamento();
        Assert.Throws<RegraNegocioException>(() => orcamento.AplicarDesconto(CalculadoraValores.DescontoMaximoPercentual + 1));
    }

    [Fact]
    public void Fluxo_completo_rascunho_enviado_aprovado_gera_evento()
    {
        var orcamento = NovoOrcamento();
        orcamento.DefinirPacote(Pacote());
        orcamento.DefinirEspaco(Espaco());

        orcamento.Enviar(Agora);
        var evento = orcamento.Aprovar("Casamento Ana & João", Agora.AddDays(1));

        Assert.Equal(StatusOrcamento.Aprovado, orcamento.Status);
        Assert.Equal(StatusEvento.Confirmado, evento.Status);
        Assert.Equal(orcamento.ValorTotal, evento.ValorTotal);
        Assert.Equal(orcamento.Inicio, evento.Inicio);
        Assert.Throws<RegraNegocioException>(() => orcamento.AplicarDesconto(5));
    }

    [Fact]
    public void Nao_aprova_sem_espaco_nem_orcamento_expirado()
    {
        var semEspaco = NovoOrcamento();
        semEspaco.DefinirPacote(Pacote());
        semEspaco.Enviar(Agora);
        Assert.Throws<RegraNegocioException>(() => semEspaco.Aprovar("X", Agora));

        var expirado = NovoOrcamento();
        expirado.DefinirPacote(Pacote());
        expirado.DefinirEspaco(Espaco());
        expirado.Enviar(Agora);
        var depoisDaValidade = expirado.ValidoAte.AddDays(1).ToDateTime(TimeOnly.MinValue);
        Assert.True(expirado.EstaExpirado(DateOnly.FromDateTime(depoisDaValidade)));
        Assert.Throws<RegraNegocioException>(() => expirado.Aprovar("X", depoisDaValidade));
    }

    private static Evento EventoAprovado(decimal valorPorPessoa = 100m, int convidados = 100)
    {
        var orcamento = NovoOrcamento(convidados);
        orcamento.DefinirPacote(Pacote(valorPorPessoa));
        orcamento.DefinirEspaco(Espaco(locacao: 0));
        orcamento.Enviar(Agora);
        return orcamento.Aprovar("Evento teste", Agora);
    }

    [Fact]
    public void Gera_parcelas_com_entrada_e_ajuste_de_centavos_na_ultima()
    {
        var evento = EventoAprovado(valorPorPessoa: 100m, convidados: 100); // 10.000,00

        var parcelas = evento.GerarParcelas(3, new DateOnly(2026, 4, 1), valorEntrada: 1000m, vencimentoEntrada: new DateOnly(2026, 3, 5));

        Assert.Equal(4, parcelas.Count);
        Assert.Equal(1000m, parcelas[0].Valor);
        Assert.Equal([3000m, 3000m, 3000m], parcelas.Skip(1).Select(p => p.Valor));
        Assert.Equal(evento.ValorTotal, evento.Parcelas.Sum(p => p.Valor));

        var quebrado = EventoAprovado(valorPorPessoa: 100m, convidados: 100);
        quebrado.GerarParcelas(3, new DateOnly(2026, 4, 1));
        Assert.Equal(10000m, quebrado.Parcelas.Sum(p => p.Valor));
        Assert.Equal(3333.34m, quebrado.Parcelas.Last().Valor);
    }

    [Fact]
    public void Regerar_parcelas_preserva_as_pagas_e_recalcula_o_saldo()
    {
        var evento = EventoAprovado(); // 10.000,00
        var primeiras = evento.GerarParcelas(2, new DateOnly(2026, 4, 1));
        primeiras[0].RegistrarPagamento(new DateOnly(2026, 4, 1), 5000m, null);

        evento.GerarParcelas(2, new DateOnly(2026, 5, 1));

        Assert.Equal(3, evento.Parcelas.Count);
        Assert.All(evento.Parcelas.Where(p => p.Status == StatusTitulo.Aberto), p => Assert.Equal(2500m, p.Valor));
        Assert.Equal(5000m, evento.SaldoAReceber);
        Assert.Equal(5000m, evento.TotalRecebido);
        Assert.Equal(0m, evento.ValorNaoParcelado);
    }

    [Fact]
    public void Cancelar_evento_cancela_parcelas_abertas()
    {
        var evento = EventoAprovado();
        var parcelas = evento.GerarParcelas(2, new DateOnly(2026, 4, 1));
        parcelas[0].RegistrarPagamento(new DateOnly(2026, 4, 1), parcelas[0].Valor, null);

        evento.Cancelar("Cliente desistiu");

        Assert.Equal(StatusEvento.Cancelado, evento.Status);
        Assert.Equal(StatusTitulo.Pago, parcelas[0].Status);
        Assert.Equal(StatusTitulo.Cancelado, parcelas[1].Status);
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarConvidado("Novo"));
    }

    [Fact]
    public void Nao_permite_convidado_duplicado_nem_funcionario_escalado_duas_vezes()
    {
        var evento = EventoAprovado();
        evento.AdicionarConvidado("Maria Souza");
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarConvidado("maria souza"));

        var garcom = ComId(new Funcionario { Nome = "Rafael", CargoId = 1, ValorDiaria = 180 }, 7);
        evento.EscalarFuncionario(garcom, "Garçom");
        Assert.Throws<RegraNegocioException>(() => evento.EscalarFuncionario(garcom));
        Assert.Equal(180m, evento.TotalEquipe);
    }

    [Fact]
    public void Realizar_exige_que_o_evento_ja_tenha_comecado()
    {
        var evento = EventoAprovado();
        Assert.Throws<RegraNegocioException>(() => evento.Realizar(evento.Inicio.AddMinutes(-1)));

        evento.Realizar(evento.Fim);
        Assert.Equal(StatusEvento.Realizado, evento.Status);
    }

    [Fact]
    public void Pagamento_e_estorno_de_titulo()
    {
        var conta = new ContaPagar("Energia", CategoriaDespesa.Servicos, 500m, new DateOnly(2026, 3, 10));
        Assert.True(conta.EstaVencido(new DateOnly(2026, 3, 11)));
        Assert.Equal(5, conta.DiasEmAtraso(new DateOnly(2026, 3, 15)));

        conta.RegistrarPagamento(new DateOnly(2026, 3, 15), 510m, null);
        Assert.Equal(StatusTitulo.Pago, conta.Status);
        Assert.False(conta.EstaVencido(new DateOnly(2026, 3, 20)));

        conta.EstornarPagamento();
        Assert.Equal(StatusTitulo.Aberto, conta.Status);
        Assert.Null(conta.ValorPago);
    }

    [Fact]
    public void Pacote_calcula_consumo_agregado_por_produto()
    {
        var pacote = Pacote();
        var item1 = new ItemCardapio { Nome = "Arroz" };
        item1.DefinirFichaTecnica([(ProdutoId: 10, QuantidadePorPessoa: 0.08m)]);
        var item2 = new ItemCardapio { Nome = "Risoto" };
        item2.DefinirFichaTecnica([(10, 0.05m), (11, 0.02m)]);

        pacote.DefinirItens([1, 2]);
        // Simula a navegação carregada pelo EF.
        var itens = pacote.Itens.ToList();
        typeof(PacoteItem).GetProperty(nameof(PacoteItem.ItemCardapio), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(itens[0], item1);
        typeof(PacoteItem).GetProperty(nameof(PacoteItem.ItemCardapio))!.SetValue(itens[1], item2);

        var consumo = pacote.CalcularConsumo(100);

        Assert.Equal(13m, consumo[10]);
        Assert.Equal(2m, consumo[11]);
    }
}
