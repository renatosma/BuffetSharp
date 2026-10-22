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

/// <summary>Funil de leads, lembretes do dia, checklist do evento, cotação e pedido de compra operados pelas telas.</summary>
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
    public void Pedido_de_compra_recebido_em_parte_e_faturado_pela_nota() =>
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

            // Chegou só um dos dois fardos; a nota fica para depois.
            robo.RespostaConfirmacao = "Não";
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Receber mercadoria"), dialogo =>
            {
                var itens = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                robo.Esperar(() => itens.Rows.Count == 1, "item a receber");
                var linha = itens.Rows[0].DataBoundItem!;
                Assert.Equal(2m, linha.GetType().GetProperty("Chegou")!.GetValue(linha));
                linha.GetType().GetProperty("Chegou")!.SetValue(linha, 1m);
                robo.Clicar(dialogo, "Salvar");
            }, "receber parte");
            robo.Esperar(() => Robo.Linhas<PedidoCompraResumo>(Robo.Grade(tela)).Single(p => p.Id == id) is
                { Status: StatusPedidoCompra.ParcialmenteRecebido, Recebido: 51m, AFaturar: 1 }, "pedido recebido em parte");

            var recebimentos = robo.AbrirMenu<FormRecebimentos>(principal, "Recebimentos");
            robo.Esperar(() => Robo.Linhas<RecebimentoResumo>(Robo.Grade(recebimentos)).Any(r => r.PedidoCompraId == id), "recebimento na lista");
            robo.SelecionarLinha<RecebimentoResumo>(Robo.Grade(recebimentos), r => r.PedidoCompraId == id);
            robo.Dialogo<FormEdicao>(() => robo.Clicar(recebimentos, "Lançar nota fiscal"), dialogo =>
            {
                robo.Preencher(dialogo, "Número da nota", $"TELA{id}");
                robo.Esperar(() => Robo.Campo<NumericUpDown>(dialogo, "Valor total da nota").Value == 51m, "valor da nota pela mercadoria");
                robo.Clicar(dialogo, "Salvar");
            }, "lançar nota");
            robo.Esperar(() => Robo.Linhas<RecebimentoResumo>(Robo.Grade(recebimentos)).Single(r => r.PedidoCompraId == id) is
                { Status: StatusRecebimento.Faturado } faturado && faturado.NotaFiscal == $"TELA{id}", "recebimento faturado");
        });

    [Fact]
    public void Cotacao_com_mapa_de_precos_vencedor_escolhido_e_pedido_gerado() =>
        Robo.Executar(ambiente, robo =>
        {
            var cotacoes = robo.Servico<CotacaoService>();
            var lookups = robo.Servico<IConsultasLookup>();
            var produtos = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Produtos)).Take(2).ToList();
            var fornecedor = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Fornecedores)).First();
            var input = cotacoes.Nova();
            input.Descricao = "Cotação da tela de teste";
            input.Itens = produtos.Select(p => new CotacaoItemInput { ProdutoId = p.Id, Quantidade = 10 }).ToList();
            input.Participantes =
            [
                new CotacaoParticipanteInput { Nome = fornecedor.Nome, FornecedorId = fornecedor.Id },
                new CotacaoParticipanteInput { Nome = "Loja sem cadastro da tela" }
            ];
            var id = robo.Rodar(() => cotacoes.SalvarAsync(input));

            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormCotacoes>(principal, "Cotações");
            robo.Esperar(() => Robo.Linhas<CotacaoResumo>(Robo.Grade(tela)).Any(c => c.Id == id), "cotação na lista");
            robo.SelecionarLinha<CotacaoResumo>(Robo.Grade(tela), c => c.Id == id);

            // Colunas do mapa: produto, qtd., especificação, (preço, obs.) por participante, escolha, motivo, vencedor, total.
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Mapa de preços"), dialogo =>
            {
                var mapa = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                robo.Esperar(() => mapa.Rows.Count == 2, "produtos no mapa");
                mapa.Rows[0].Cells[3].Value = 10m;
                mapa.Rows[0].Cells[5].Value = 9m;
                mapa.Rows[1].Cells[3].Value = 5m;
                mapa.Rows[1].Cells[5].Value = 6m;
                Assert.Equal("Loja sem cadastro da tela", mapa.Rows[0].Cells[9].Value);
                Assert.Equal(fornecedor.Nome, mapa.Rows[1].Cells[9].Value);

                // O cliente quer a marca do fornecedor cadastrado, mesmo mais cara.
                var escolha = (DataGridViewComboBoxCell)mapa.Rows[0].Cells[7];
                escolha.Value = robo.Rodar(() => cotacoes.ObterMapaAsync(id)).Participantes.Single(p => p.FornecedorId == fornecedor.Id).Id;
                mapa.Rows[0].Cells[8].Value = "Marca pedida pelo cliente";
                Assert.Equal(fornecedor.Nome, mapa.Rows[0].Cells[9].Value);
                robo.Clicar(dialogo, "Salvar");
            }, "mapa de preços");
            robo.Esperar(() => Robo.Linhas<CotacaoResumo>(Robo.Grade(tela)).Single(c => c.Id == id) is
                { TotalVencedores: 150m, EscolhasManuais: 1, Respostas: "4 de 4" }, "vencedores no mapa");

            // Todos os produtos ficaram com o fornecedor cadastrado: um pedido só, sem cadastro novo.
            robo.RespostaConfirmacao = "Sim";
            robo.Clicar(tela, "Gerar pedidos");
            robo.Esperar(() => Robo.AbaAtual(principal) is FormPedidosCompra pedidos && Robo.Carregado(pedidos), "pedidos de compra abertos");
            var gerado = robo.Rodar(() => robo.Servico<IConsultasCompras>().ListarPedidosAsync(new FiltroPedidosCompra()))
                .Single(p => p.CotacaoCompraId == id);
            Assert.Equal((fornecedor.Id, 150m), (gerado.FornecedorId, gerado.Total));
        });
}
