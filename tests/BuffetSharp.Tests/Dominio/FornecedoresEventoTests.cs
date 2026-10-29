using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

/// <summary>Fornecedores do evento: contas a pagar, chegada no cronograma, margem, cancelamento, confirmação e avaliação.</summary>
public class FornecedoresEventoTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 10, 0, 0);
    private static readonly DateOnly Data = new(2026, 6, 20);
    private static readonly DateTime Inicio = Data.ToDateTime(new TimeOnly(19, 0));

    private static T ComId<T>(T entidade, int id) where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    private static void DefinirId(object item, int id) => item.GetType().GetProperty("Id")!.SetValue(item, id);

    /// <summary>Festa das 19h às 23h com DJ vendido por R$ 2.800.</summary>
    private static Evento Evento()
    {
        var orcamento = new Orcamento(1, 1, Data, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100), validoAte: Data.AddDays(-30));
        orcamento.DefinirPacote(ComId(new Pacote { Nome = "Prata", PrecoPorPessoa = 100m, MinimoConvidados = 50 }, 1));
        orcamento.DefinirEspaco(ComId(new Espaco { Nome = "Salão", CapacidadeMaxima = 200, ValorLocacao = 0 }, 1));
        orcamento.DefinirServicos([(ComId(new ServicoAdicional { Nome = "DJ", Preco = 2800 }, 3), 1m, 2800m)]);
        orcamento.Enviar(Agora);
        var evento = orcamento.Aprovar("Casamento Ana e Bruno", Agora);
        DefinirId(evento.Servicos.Single(), 30);
        return evento;
    }

    private static Fornecedor Fornecedor(int id = 7) => ComId(new Fornecedor { RazaoSocial = "Som e Luz Ltda", NomeFantasia = "Som & Luz", Ativo = true }, id);

    [Fact]
    public void Contratar_gera_as_contas_a_pagar_e_a_chegada_no_cronograma()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), " DJ e iluminação ", 1900m, 30, Inicio.AddHours(-2), "Diego", "(11) 98201-4410");
        dj.Pagamento!.GerarParcelas(2, Data.AddDays(-10));

        Assert.Equal(("DJ e iluminação", SituacaoFornecedorEvento.Contratado), (dj.Servico, dj.Situacao));
        Assert.Equal((1900m, CategoriaDespesa.Servicos, 7), (dj.Pagamento.ValorTotal, dj.Pagamento.Categoria, dj.Pagamento.FornecedorId));
        Assert.Equal(2, dj.Pagamento.Parcelas.Count);
        Assert.Contains("Som & Luz", dj.Pagamento.Descricao);

        var etapa = Assert.Single(evento.Etapas);
        Assert.Same(etapa, dj.EtapaChegada);
        Assert.Equal((Inicio.AddHours(-2), 0, "Chegada: DJ e iluminação", "Som & Luz", "Contato: Diego - (11) 98201-4410"),
            (etapa.Horario, etapa.DuracaoMinutos, etapa.Atividade, etapa.Responsavel, etapa.Observacao));

        // Margem sobre o serviço vendido e nada mais a contratar.
        Assert.Equal(1900m, evento.CustoFornecedoresDoServico(evento.Servicos.Single()));
        Assert.Equal(1900m, evento.CustoFornecedores);
        Assert.Empty(evento.ServicosSemFornecedor());
    }

    [Fact]
    public void Contratacao_recusa_dados_invalidos()
    {
        var evento = Evento();
        Assert.Throws<RegraNegocioException>(() => evento.ContratarFornecedor(Fornecedor(), "DJ", 0));
        Assert.Throws<RegraNegocioException>(() => evento.ContratarFornecedor(Fornecedor(), " ", 100));
        Assert.Throws<RegraNegocioException>(() => evento.ContratarFornecedor(Fornecedor(), "DJ", 100, eventoServicoId: 99));
        Assert.Throws<RegraNegocioException>(() => evento.ContratarFornecedor(Fornecedor(), "DJ", 100, chegada: Inicio.AddHours(-13)));
        var inativo = Fornecedor();
        inativo.Ativo = false;
        Assert.Contains("inativo", Assert.Throws<RegraNegocioException>(() => evento.ContratarFornecedor(inativo, "DJ", 100)).Message);
        Assert.Single(evento.ServicosSemFornecedor());
    }

    [Fact]
    public void Mudar_o_horario_do_evento_leva_a_chegada_e_alterar_tira_do_cronograma()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), "DJ", 1900m, chegada: Inicio.AddHours(-2));
        DefinirId(dj, 5);
        evento.AlterarDados(evento.Titulo, evento.EspacoId, Data, new TimeOnly(20, 0), new TimeOnly(23, 59), evento.QuantidadesPorFaixa, null);
        Assert.Equal(Inicio.AddHours(-1), dj.Chegada);

        evento.AlterarFornecedor(5, "DJ e telão", null, Inicio.AddMinutes(-30), null, null, null);
        Assert.Equal(("Chegada: DJ e telão", Inicio.AddMinutes(-30)), (evento.Etapas.Single().Atividade, dj.Chegada));
        evento.AlterarFornecedor(5, "DJ e telão", null, null, null, null, null);
        Assert.Empty(evento.Etapas);
        Assert.Null(dj.Chegada);
    }

    [Fact]
    public void Remover_a_etapa_no_cronograma_deixa_o_fornecedor_sem_chegada()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), "DJ", 1900m, chegada: Inicio.AddHours(-2));
        var etapa = evento.Etapas.Single();
        DefinirId(etapa, 8);
        evento.RemoverEtapa(8);
        Assert.Null(dj.EtapaChegada);
    }

    [Fact]
    public void Cancelar_cancela_as_contas_em_aberto_e_devolve_o_ja_pago()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), "DJ", 1000m, 30, Inicio.AddHours(-2));
        DefinirId(dj, 5);
        var parcelas = dj.Pagamento!.GerarParcelas(2, Data.AddDays(-20), intervaloDias: 10);
        parcelas[0].RegistrarPagamento(Data.AddDays(-20), 500m, null);

        Assert.Contains("Estorne", Assert.Throws<RegraNegocioException>(() => evento.RefazerPagamentoFornecedor(5, 1200m)).Message);
        Assert.Equal(500m, evento.CancelarFornecedor(5));
        Assert.Equal((SituacaoFornecedorEvento.Cancelado, StatusTitulo.Pago, StatusTitulo.Cancelado),
            (dj.Situacao, parcelas[0].Status, parcelas[1].Status));
        Assert.Empty(evento.Etapas);
        Assert.Equal(0m, evento.CustoFornecedores);
        Assert.Single(evento.ServicosSemFornecedor());
        Assert.Throws<RegraNegocioException>(() => evento.ConfirmarFornecedor(5, Agora));
    }

    [Fact]
    public void Refazer_o_pagamento_troca_o_valor_e_o_parcelamento()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), "DJ", 1000m);
        DefinirId(dj, 5);
        var antigo = dj.Pagamento!;
        antigo.GerarParcelas(1, Data);

        var remover = evento.RefazerPagamentoFornecedor(5, 1500m);
        Assert.Contains(antigo, remover);
        Assert.Contains(antigo.Parcelas.Single(), remover);
        Assert.NotSame(antigo, dj.Pagamento);
        Assert.Equal((1500m, 1500m), (dj.Valor, dj.Pagamento!.ValorTotal));
    }

    [Fact]
    public void Confirmacao_e_avaliacao_depois_do_inicio()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), "DJ", 1000m);
        DefinirId(dj, 5);
        evento.ConfirmarFornecedor(5, Agora);
        Assert.Equal((SituacaoFornecedorEvento.Confirmado, Agora), (dj.Situacao, dj.ConfirmadoEm));

        Assert.Throws<RegraNegocioException>(() => evento.AvaliarFornecedor(5, 5, null, Inicio.AddHours(-1)));
        Assert.Throws<RegraNegocioException>(() => evento.AvaliarFornecedor(5, 6, null, Inicio.AddHours(1)));
        evento.AvaliarFornecedor(5, 4, "  Pontual, som bom  ", Inicio.AddHours(5));
        Assert.Equal((4, "Pontual, som bom"), (dj.Nota, dj.Comentario));
    }

    [Fact]
    public void Cancelar_o_evento_cancela_os_fornecedores()
    {
        var evento = Evento();
        var dj = evento.ContratarFornecedor(Fornecedor(), "DJ", 1000m, chegada: Inicio.AddHours(-1));
        dj.Pagamento!.GerarParcelas(1, Data);
        evento.Cancelar("Cliente desistiu");
        Assert.Equal((SituacaoFornecedorEvento.Cancelado, StatusTitulo.Cancelado), (dj.Situacao, dj.Pagamento.Parcelas.Single().Status));
    }
}
