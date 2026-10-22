using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Formularios.Estoque;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Funil de leads, lembretes do dia, checklist do evento e pedido de compra operados pelas telas.</summary>
public class MelhoriasTelasTests(AmbienteTelas ambiente)
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public void Lead_cadastrado_pela_tela_e_convertido_em_cliente() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormLeads>(principal, "Leads (funil)");
            const string nome = "Lead da Tela de Teste";

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Novo lead"), dialogo =>
            {
                robo.Preencher(dialogo, "Nome", nome);
                robo.Preencher(dialogo, "Telefone / WhatsApp", "11977776666");
                robo.Escolher(dialogo, "Origem", "Indicação");
                robo.Clicar(dialogo, "Salvar");
            }, "novo lead");
            robo.Esperar(() => Robo.Linhas<LeadResumo>(Robo.Grade(tela)).Any(l => l.Nome == nome && l.Status == StatusLead.Novo), "lead na lista");

            robo.SelecionarLinha<LeadResumo>(Robo.Grade(tela), l => l.Nome == nome);
            robo.RespostaConfirmacao = "Não"; // não abre o orçamento agora
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Converter em cliente"), dialogo => robo.Clicar(dialogo, "Salvar"),
                "converter lead");
            robo.Esperar(() => Robo.Linhas<LeadResumo>(Robo.Grade(tela)).Count(l => l.Nome == nome) == 0, "lead convertido sai da negociação");

            var convertido = robo.Rodar(() => robo.Servico<IConsultasLeads>().ListarAsync(new FiltroLeads { Status = StatusLead.Convertido }))
                .Single(l => l.Nome == nome);
            Assert.Equal(nome, convertido.Cliente);
        });

    [Fact]
    public void Lembrete_resolvido_pela_tela_sai_da_lista() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormLembretes>(principal, "Lembretes");
            robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<Lembrete>(Robo.Grade(tela)).Any(l => l.Tipo == TipoLembrete.RetornoLead),
                "lembretes do dia");

            var lembrete = Robo.Linhas<Lembrete>(Robo.Grade(tela)).First(l => l.Tipo == TipoLembrete.RetornoLead);
            robo.SelecionarLinha<Lembrete>(Robo.Grade(tela), l => l.Chave == lembrete.Chave);
            robo.Clicar(tela, "Resolvido");
            robo.Esperar(() => Robo.Linhas<Lembrete>(Robo.Grade(tela)).All(l => l.Chave != lembrete.Chave), "lembrete resolvido");
        });

    [Fact]
    public void Tarefa_do_checklist_concluida_na_aba_do_evento() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
            var eventoId = robo.Rodar(() => robo.Servico<EventoService>().SalvarAsync(new EventoInput
            {
                Titulo = "Checklist do teste de tela",
                ClienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id,
                TipoEventoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First().Id,
                EspacoId = espaco.Id,
                Data = Hoje.AddYears(4).AddDays(20),
                HoraInicio = new TimeOnly(9, 0),
                HoraFim = new TimeOnly(11, 0),
                Convidados = 40,
                ValorTotal = 5000m
            }));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            robo.SelecionarAba(evento, "Checklist");
            var tarefas = Robo.Interno<Grade<EventoTarefa>>(evento, "_tarefas");
            Assert.True(tarefas.Itens.Count >= 10, "O evento novo deve vir com o checklist padrão.");
            var primeira = tarefas.Itens.OrderBy(t => t.Prazo).First();
            tarefas.Selecionar(t => t.Id == primeira.Id);
            robo.Clicar(evento, "Concluir / reabrir");
            robo.Esperar(() => tarefas.Itens.Single(t => t.Id == primeira.Id).Concluida, "tarefa concluída");
        });

    [Fact]
    public void Pedido_de_compra_aberto_na_tela_e_recebido_com_a_nota() =>
        Robo.Executar(ambiente, robo =>
        {
            var compras = robo.Servico<CompraService>();
            var lookups = robo.Servico<IConsultasLookup>();
            var refrigerante = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Produtos)).First(p => p.Nome == "Refrigerante 2L").Id;
            var fardo = robo.Rodar(() => robo.Servico<EstoqueService>().ListarEmbalagensAsync(refrigerante)).Single(e => e.Fator == 6);
            var pedido = compras.Novo(fardo.FornecedorId!.Value);
            pedido.Itens = [new PedidoCompraItemInput { ProdutoId = refrigerante, ProdutoEmbalagemId = fardo.Id, Quantidade = 2, CustoUnitario = 51m }];
            var id = robo.Rodar(() => compras.SalvarAsync(pedido));

            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormPedidosCompra>(principal, "Pedidos de compra");
            robo.Esperar(() => Robo.Linhas<PedidoCompraResumo>(Robo.Grade(tela)).Any(p => p.Id == id), "pedido na lista");
            robo.SelecionarLinha<PedidoCompraResumo>(Robo.Grade(tela), p => p.Id == id);

            // O editor mostra o item na embalagem do fornecedor (a lista de unidades é de cada linha).
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Editar"), dialogo =>
            {
                var itens = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                robo.Esperar(() => itens.Rows.Count == 1 && itens.Rows[0].Cells[1].FormattedValue?.ToString()?.StartsWith("PCT com 6") == true,
                    "unidade de compra do item");
                Assert.Equal("Refrigerante 2L", itens.Rows[0].Cells[0].FormattedValue);
                robo.Clicar(dialogo, "Cancelar");
            }, "editar pedido");

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Receber (nota fiscal)"), dialogo =>
            {
                robo.Preencher(dialogo, "Nota fiscal", "NF TELA 1");
                robo.Clicar(dialogo, "Salvar");
            }, "receber pedido");
            robo.Esperar(() => Robo.Linhas<PedidoCompraResumo>(Robo.Grade(tela)).Single(p => p.Id == id).Status == StatusPedidoCompra.Recebido,
                "pedido recebido");
        });
}
