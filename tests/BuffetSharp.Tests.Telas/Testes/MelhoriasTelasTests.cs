using System.Runtime.InteropServices;
using BuffetSharp.Application.Abstracoes;
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
using Microsoft.EntityFrameworkCore;

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
    public void Excedentes_cobrados_e_desfeitos_na_aba_financeiro() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var eventos = robo.Servico<EventoService>();
            var realizados = robo.Rodar(() => robo.Servico<IConsultasEventos>().ListarAsync(new FiltroEventos { Status = StatusEvento.Realizado }));
            var alvo = realizados.Select(e => robo.Rodar(() => eventos.ObterDetalheAsync(e.Id)).Evento)
                .First(e => !e.ExcedentesApurados && e.ValorPorPessoa > 0);

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{alvo.Id}", () => navegador.Criar<FormEventoDetalhe>(alvo.Id));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;
            robo.SelecionarAba(evento, "Financeiro");
            var parcelas = Robo.Interno<Grade<Domain.Financeiro.Parcela>>(evento, "_parcelas");

            var presentes = Math.Max(alvo.PessoasPresentes, alvo.QuantidadeConvidados) + 4;
            robo.RespostaConfirmacao = "Não"; // não abre o PIX agora
            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Convidados excedentes"), dialogo =>
            {
                robo.Preencher(dialogo, "Pessoas presentes", presentes);
                robo.Clicar(dialogo, "Salvar");
            }, "convidados excedentes");
            robo.Esperar(() => parcelas.Itens.Any(p => p.Excedentes && p.Status == Domain.Financeiro.StatusTitulo.Aberto), "cobrança dos excedentes");
            var cobranca = parcelas.Itens.Single(p => p.Excedentes);
            Assert.Equal((presentes - alvo.QuantidadeConvidados) * alvo.ValorPorPessoa, cobranca.Valor);

            robo.RespostaConfirmacao = "Sim";
            robo.Clicar(evento, "Desfazer excedentes");
            robo.Esperar(() => parcelas.Itens.Single(p => p.Id == cobranca.Id).Status == Domain.Financeiro.StatusTitulo.Cancelado,
                "cobrança cancelada");
            Assert.Equal(alvo.ValorTotal, robo.Rodar(() => eventos.ObterDetalheAsync(alvo.Id)).Evento.ValorTotal);
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

                // A caixa "Chegou": desmarcar zera; marcar de novo traz o que falta.
                var caixa = (DataGridViewCheckBoxCell)itens.Rows[0].Cells[0];
                Assert.Equal(true, caixa.FormattedValue);
                itens.CurrentCell = caixa;
                robo.Clicar(dialogo, "Desmarcar todos");
                Assert.Equal(0m, linha.GetType().GetProperty("Chegou")!.GetValue(linha));
                itens.Refresh();
                Assert.Equal(false, caixa.FormattedValue);
                linha.GetType().GetProperty("Marcado")!.SetValue(linha, true);
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

    [Fact]
    public void NFe_de_compra_sem_pedido_importada_pelo_XML() =>
        Robo.Executar(ambiente, robo =>
        {
            static string Cnpj()
            {
                var d = Enumerable.Range(0, 8).Select(_ => Random.Shared.Next(10)).Concat([0, 0, 0, 1]).ToList();
                int Digito(int[] pesos) => d.Select((x, i) => x * pesos[i]).Sum() % 11 is var resto && resto < 2 ? 0 : 11 - resto;
                d.Add(Digito([5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]));
                d.Add(Digito([6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]));
                return string.Concat(d);
            }
            var numero = Random.Shared.Next(100_000, 999_999).ToString();
            var cnpj = Cnpj();
            var chave = $"3526{DateTime.Today:MM}{cnpj}55001{numero.PadLeft(9, '0')}1{Random.Shared.Next(10_000_000, 99_999_999)}0";
            var arquivo = Path.Combine(Path.GetTempPath(), $"nfe-{numero}.xml");
            File.WriteAllText(arquivo, $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe><infNFe Id="NFe{chave}">
                  <ide><mod>55</mod><serie>1</serie><nNF>{numero}</nNF><dhEmi>{DateTime.Today:yyyy-MM-dd}T09:00:00-03:00</dhEmi></ide>
                  <emit><CNPJ>{cnpj}</CNPJ><xNome>Empório da Tela Ltda</xNome><xFant>Empório da Tela</xFant></emit>
                  <det nItem="1"><prod><cProd>A1</cProd><xProd>ARROZ AGULHINHA TIPO 1</xProd><uCom>KG</uCom><qCom>10</qCom><vUnCom>6.00</vUnCom><vProd>60.00</vProd></prod></det>
                  <det nItem="2"><prod><cProd>Z9</cProd><xProd>VELA DE ANIVERSARIO</xProd><uCom>UN</uCom><qCom>1</qCom><vUnCom>5.00</vUnCom><vProd>5.00</vProd></prod></det>
                  <total><ICMSTot><vProd>65.00</vProd><vFrete>0</vFrete><vDesc>0</vDesc><vNF>65.00</vNF></ICMSTot></total>
                  <cobr><dup><nDup>001</nDup><dVenc>{DateTime.Today.AddDays(21):yyyy-MM-dd}</dVenc><vDup>65.00</vDup></dup></cobr>
                </infNFe></NFe></nfeProc>
                """);
            var escolher = DialogosNotaEntrada.EscolherXml;
            DialogosNotaEntrada.EscolherXml = _ => arquivo;
            try
            {
                var principal = robo.Entrar();
                var tela = robo.AbrirMenu<FormNotasEntrada>(principal, "Notas fiscais de entrada");
                robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Importar XML da NF-e"), dialogo =>
                {
                    // O arroz vem reconhecido pelo nome; a vela não é do estoque.
                    var itens = Robo.Descendentes(dialogo).OfType<DataGridView>().First(g => g.Columns.Count == 12); // com lote e validade
                    robo.Esperar(() => itens.Rows.Count == 2, "itens da NF-e");
                    Assert.Equal("Arroz agulhinha", itens.Rows[0].Cells[6].FormattedValue);
                    Assert.Equal("Escolha o produto", itens.Rows[1].Cells[9].FormattedValue);
                    var vela = itens.Rows[1].DataBoundItem!;
                    vela.GetType().GetProperty("ProdutoId")!.SetValue(vela, -1);
                    itens.Refresh();
                    Assert.Equal("Fora do estoque", itens.Rows[1].Cells[9].FormattedValue);
                    robo.Clicar(dialogo, "Salvar");
                }, "importar NF-e");
                robo.Esperar(() => Robo.Linhas<NotaEntradaResumo>(Robo.Grade(tela)).Any(n => n.Numero == numero && n.ValorTotal == 65m),
                    "nota importada na lista");
                var nota = Robo.Linhas<NotaEntradaResumo>(Robo.Grade(tela)).Single(n => n.Numero == numero);
                Assert.Equal(("Empório da Tela", 60m, "Em aberto"), (nota.Fornecedor, nota.ValorProdutos, nota.Pagamento));
            }
            finally
            {
                DialogosNotaEntrada.EscolherXml = escolher;
                File.Delete(arquivo);
            }
        });

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr janela, int mensagem, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Com monitores de escalas diferentes, a lista de edição da grade recebe o aviso de mudança de escala ao ser encaixada
    /// e o ComboBox padrão tentava se recriar ("Erro ao criar identificador de janela"). A lista do sistema ignora o aviso.
    /// </summary>
    [Fact]
    public void Lista_da_grade_do_pedido_aguenta_a_mudanca_de_escala_do_monitor() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormPedidosCompra>(principal, "Pedidos de compra");
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Novo pedido"), dialogo =>
            {
                var itens = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                robo.Clicar(dialogo, "Incluir produto");
                robo.Esperar(() => itens.EditingControl is not null, "lista de produtos em edição");
                var lista = Assert.IsType<EdicaoLista>(itens.EditingControl);
                var janela = lista.Handle;
                SendMessage(janela, 0x02E2, IntPtr.Zero, IntPtr.Zero); // WM_DPICHANGED_BEFOREPARENT
                Assert.Equal(janela, lista.Handle);
                Assert.All(Robo.Descendentes(dialogo).OfType<DataGridView>().SelectMany(g => g.Columns.OfType<DataGridViewComboBoxColumn>()),
                    c => Assert.IsType<ColunaLista>(c));
                robo.Clicar(dialogo, "Cancelar");
            }, "novo pedido");
        });

    [Fact]
    public void Condicao_de_pagamento_com_parcelas_preenchidas_na_grade() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<Form>(principal, "Condições de pagamento");
            var nome = $"Grade {Guid.NewGuid():N}"[..20];
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Novo"), dialogo =>
            {
                robo.Preencher(dialogo, "Nome", nome);
                robo.Clicar(dialogo, "Preencher a grade"); // padrão: 3 parcelas, 30 dias, a cada 30
                var grade = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                robo.Esperar(() => grade.Rows.Count == 3, "parcelas preenchidas");
                Assert.Equal(["30", "60", "90"], grade.Rows.Cast<DataGridViewRow>().Select(r => r.Cells[1].FormattedValue?.ToString()));
                Assert.Equal("R$ 333,40", grade.Rows[2].Cells[5].FormattedValue); // 33,34% e os centavos
                Assert.Contains(Robo.Descendentes(dialogo).OfType<Label>(), l => l.Text.StartsWith("Total: 100% em 3 parcela(s)"));
                robo.Clicar(dialogo, "Salvar");
            }, "nova condição");

            var salva = robo.Rodar(() => robo.Servico<ICadastroService<Domain.Cadastros.CondicaoPagamento>>()
                    .ListarAsync(q => q.Include(c => c.Parcelas).Where(c => c.Nome == nome)))
                .Single();
            Assert.Equal([33.33m, 33.33m, 33.34m], salva.Parcelas.OrderBy(p => p.Dias).Select(p => p.Percentual));
            Assert.Equal("30/60/90 dias", salva.Resumo);
        });

    /// <summary>Uma lista aberta antes não pode ficar desatualizada: voltar para a aba traz o que foi feito em outra tela.</summary>
    [Fact]
    public void Lista_aberta_antes_mostra_o_recebimento_feito_depois_sem_F5() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var recebimentos = robo.AbrirMenu<FormRecebimentos>(principal, "Recebimentos");
            robo.AbrirMenu<FormPedidosCompra>(principal, "Pedidos de compra");

            // Pedido recebido enquanto a lista de recebimentos estava aberta em outra aba.
            var compras = robo.Servico<CompraService>();
            var lookups = robo.Servico<IConsultasLookup>();
            var pedido = compras.Novo(robo.Rodar(() => lookups.ListarAsync(TipoLookup.Fornecedores)).First().Id);
            pedido.Itens = [new PedidoCompraItemInput { ProdutoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Produtos)).First().Id, Quantidade = 3, CustoUnitario = 10m }];
            var id = robo.Rodar(() => compras.SalvarAsync(pedido));
            var item = robo.Rodar(() => robo.Servico<IConsultasCompras>().ItensAsync(id)).Single();
            var recebimento = robo.Rodar(() => compras.ReceberAsync(new RecebimentoInput { PedidoId = id, Data = Hoje, Itens = [new(item.Id, 3)] }));
            Assert.DoesNotContain(Robo.Linhas<RecebimentoResumo>(Robo.Grade(recebimentos)), r => r.Id == recebimento);

            Assert.Same(recebimentos, robo.AbrirMenu<FormRecebimentos>(principal, "Recebimentos")); // a mesma aba, sem F5
            robo.Esperar(() => Robo.Linhas<RecebimentoResumo>(Robo.Grade(recebimentos)).Any(r => r.Id == recebimento),
                "recebimento novo na lista ao voltar para a aba");
        });
}
