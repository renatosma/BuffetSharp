using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Dominio;

public class OperacaoTests
{
    private static readonly DateOnly DataEvento = new(2027, 3, 20);

    private static Evento NovoEvento() =>
        new("Casamento de teste", 1, 1, 1, DataEvento, new TimeOnly(19, 0), new TimeOnly(23, 0), new QuantidadesConvidados(100, 0, 0), 30_000m);

    private static ModeloChecklist Modelo() => new()
    {
        Nome = "Padrão",
        Itens =
        [
            new ModeloChecklistItem { Descricao = "Assinar contrato", DiasAntes = 60, Ordem = 1 },
            new ModeloChecklistItem { Descricao = "Lista de convidados", DiasAntes = 10, Responsavel = "Comercial", Ordem = 2 },
            new ModeloChecklistItem { Descricao = "Pesquisa de satisfação", DiasAntes = -3, Ordem = 3 }
        ]
    };

    [Fact]
    public void Checklist_aplicado_conta_os_prazos_a_partir_da_data_do_evento_e_nao_repete_tarefas()
    {
        var evento = NovoEvento();

        Assert.Equal(3, evento.AplicarChecklist(Modelo()));
        Assert.Equal(0, evento.AplicarChecklist(Modelo()));

        Assert.Equal(DataEvento.AddDays(-60), evento.Tarefas.Single(t => t.Descricao == "Assinar contrato").Prazo);
        Assert.Equal(DataEvento.AddDays(3), evento.Tarefas.Single(t => t.Descricao == "Pesquisa de satisfação").Prazo);
        Assert.Equal("Comercial", evento.Tarefas.Single(t => t.Descricao == "Lista de convidados").Responsavel);
        Assert.True(evento.Tarefas.Single(t => t.Descricao == "Assinar contrato").Atrasada(DataEvento));
    }

    [Fact]
    public void Modelo_de_checklist_valida_as_tarefas()
    {
        var vazio = new ModeloChecklist { Nome = "Vazio" };
        Assert.Throws<ValidacaoException>(() => Validador.Validar(vazio));

        var prazoInvalido = Modelo();
        prazoInvalido.Itens[0].DiasAntes = 1000;
        Assert.Throws<ValidacaoException>(() => Validador.Validar(prazoInvalido));

        Validador.Validar(Modelo());
    }

    [Fact]
    public void Tarefa_vazia_ou_em_evento_encerrado_e_recusada()
    {
        var evento = NovoEvento();
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarTarefa(" ", DataEvento, null));

        evento.Cancelar("Desistência");
        Assert.Throws<RegraNegocioException>(() => evento.AdicionarTarefa("Montagem", DataEvento, null));
    }

    [Fact]
    public void Fechamento_da_equipe_soma_as_diarias_com_presenca_e_gera_conta_de_pessoal()
    {
        var evento = NovoEvento();
        var garcom = new Funcionario { Nome = "Garçom de teste", ValorDiaria = 180 };
        var escala = evento.EscalarFuncionario(garcom, "Garçom");
        Assert.Throws<RegraNegocioException>(() =>
            new FechamentoEquipe(garcom, DataEvento, DataEvento, [escala], DataEvento.AddDays(5))); // sem presença

        evento.RegistrarPresenca(garcom.Id, true);
        var fechamento = new FechamentoEquipe(garcom, DataEvento, DataEvento, [escala], DataEvento.AddDays(5));

        Assert.Equal(180, fechamento.Valor);
        Assert.Equal(1, fechamento.Diarias);
        Assert.Equal(CategoriaDespesa.Pessoal, fechamento.ContaPagar!.Categoria);
        Assert.Equal(180, fechamento.ContaPagar.Valor);

        fechamento.ContaPagar.RegistrarPagamento(DataEvento.AddDays(5), 180, null);
        Assert.Throws<RegraNegocioException>(fechamento.Desfazer);
    }

    [Fact]
    public void Lista_de_compras_sugere_a_embalagem_do_fornecedor()
    {
        var caixa = new UnidadeMedida { Sigla = "CX", Descricao = "Caixa" };
        var produto = new Produto
        {
            Nome = "Refrigerante lata", FornecedorId = 7, UnidadeMedida = new UnidadeMedida { Sigla = "UN", Descricao = "Unidade" },
            Embalagens =
            [
                new ProdutoEmbalagem { FornecedorId = 7, Fator = 12, UnidadeMedida = caixa },
                new ProdutoEmbalagem { FornecedorId = 7, Fator = 6, UnidadeMedida = new UnidadeMedida { Sigla = "FD" } },
                new ProdutoEmbalagem { FornecedorId = 9, Fator = 24, UnidadeMedida = caixa }
            ]
        };

        Assert.Equal("4 CX de 12 UN", PlanejamentoService.SugerirEmbalagem(produto, 40));
        Assert.Equal("1 FD de 6 UN", PlanejamentoService.SugerirEmbalagem(produto, 5));
        Assert.Null(PlanejamentoService.SugerirEmbalagem(new Produto { Nome = "Sal" }, 3));
    }
}
