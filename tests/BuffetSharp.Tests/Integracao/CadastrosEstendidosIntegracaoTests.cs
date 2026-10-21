using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Contatos e endereços adicionais, condições de pagamento, modelos de contrato e compras em embalagens.</summary>
[Collection(ColecaoBanco.Nome)]
public class CadastrosEstendidosIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

    /// <summary>Evento de venda direta de manhã, longe dos eventos da base de demonstração.</summary>
    private async Task<int> CriarEventoAsync(DateOnly data, decimal valor = 10_000m)
    {
        var espaco = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Espacos, ct: Ct)).OrderBy(e => e.Numero)
            .First(e => e.Numero >= 40);
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = $"Café de teste {Guid.NewGuid():N}"[..30],
            ClienteId = (await Lookup(TipoLookup.Clientes)).Id,
            TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id,
            EspacoId = espaco.Id,
            Data = data,
            HoraInicio = new TimeOnly(7, 0),
            HoraFim = new TimeOnly(8, 0),
            Convidados = 40,
            ValorTotal = valor
        }, Ct);
    }

    [Fact]
    public async Task Cliente_grava_altera_e_remove_contatos_e_enderecos_adicionais()
    {
        var servico = banco.Obter<ICadastroService<Cliente>>();
        var cliente = new Cliente { Nome = $"Cliente com contatos {Guid.NewGuid():N}"[..40], Celular = "(11) 91234-5678" };
        cliente.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Email, Valor = "contato@cliente.exemplo.com.br" });
        cliente.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.WhatsApp, Valor = "11955554444", Observacao = "Noiva" });
        cliente.Enderecos.Add(new EnderecoAdicional
        {
            Identificacao = "Cobrança",
            Endereco = new Endereco { Logradouro = "Rua das Palmeiras", Numero = "10", Cidade = "São Paulo", Uf = "SP" }
        });
        await servico.SalvarAsync(cliente, Ct);

        // Como a tela faz: recarrega, altera um item, remove outro e inclui um novo.
        var naTela = (await servico.ObterAsync(cliente.Id, Ct))!;
        Assert.Equal(2, naTela.Contatos.Count);
        Assert.Equal("(11) 95555-4444", naTela.Contatos.Single(c => c.Tipo == TipoContato.WhatsApp).Valor);
        Assert.Equal("Rua das Palmeiras", Assert.Single(naTela.Enderecos).Endereco.Logradouro);

        naTela.Contatos.Single(c => c.Tipo == TipoContato.Email).Valor = "novo@cliente.exemplo.com.br";
        naTela.Contatos.RemoveAll(c => c.Tipo == TipoContato.WhatsApp);
        naTela.Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Telefone, Valor = "1133334444", Observacao = "Escritório" });
        naTela.Enderecos[0].Endereco.Numero = "20";
        await servico.SalvarAsync(naTela, Ct);

        var salvo = (await servico.ObterAsync(cliente.Id, Ct))!;
        Assert.Equal(new[] { "(11) 3333-4444", "novo@cliente.exemplo.com.br" }, salvo.Contatos.Select(c => c.Valor).OrderBy(v => v, StringComparer.Ordinal));
        Assert.Equal("20", Assert.Single(salvo.Enderecos).Endereco.Numero);

        salvo.Enderecos.Clear();
        await servico.SalvarAsync(salvo, Ct);
        Assert.Empty((await servico.ObterAsync(cliente.Id, Ct))!.Enderecos);

        // Excluir o cliente leva junto os contatos.
        await servico.ExcluirAsync(cliente.Id, Ct);
        Assert.Null(await servico.ObterAsync(cliente.Id, Ct));
    }

    [Fact]
    public async Task Condicao_de_pagamento_sugere_o_plano_e_vai_para_o_contrato()
    {
        var condicao = await banco.Obter<ICadastroService<CondicaoPagamento>>().SalvarAsync(new CondicaoPagamento
        {
            Nome = $"Teste 20% + 2x {Guid.NewGuid():N}"[..30],
            PercentualEntrada = 20,
            DiasEntrada = 1,
            QuantidadeParcelas = 2,
            DiasPrimeiraParcela = 15,
            Mensal = false,
            IntervaloDias = 15
        }, Ct);
        var eventos = banco.Obter<EventoService>();
        var eventoId = await CriarEventoAsync(Hoje.AddYears(5).AddDays(20));

        var plano = await eventos.SugerirPlanoAsync(eventoId, condicao.Id, Ct);
        Assert.Equal(2_000m, plano.ValorEntrada);
        Assert.Equal(2, plano.Quantidade);
        Assert.Equal(15, plano.IntervaloDias);
        Assert.Equal(Hoje.AddDays(15), plano.PrimeiroVencimento);

        await eventos.GerarParcelasAsync(eventoId, plano, Ct);
        var evento = (await eventos.ObterDetalheAsync(eventoId, Ct)).Evento;
        Assert.Equal(condicao.Id, evento.CondicaoPagamentoId);
        var parcelas = evento.Parcelas.OrderBy(p => p.Numero).ToList();
        Assert.Equal(3, parcelas.Count);
        Assert.Equal(15, parcelas[2].Vencimento.DayNumber - parcelas[1].Vencimento.DayNumber);
        Assert.Equal(10_000m, parcelas.Sum(p => p.Valor));

        var minuta = await banco.Obter<ContratoService>().GerarMinutaAsync(eventoId, Ct);
        Assert.Contains("Condição de pagamento: 20% de entrada + 2x a cada 15 dias.", minuta);
        Assert.Contains("(dez mil reais)", minuta);
    }

    [Fact]
    public async Task Modelo_do_tipo_de_evento_tem_prioridade_e_padrao_e_unico()
    {
        var modelos = banco.Obter<ICadastroService<ModeloContrato>>();
        var contratos = banco.Obter<ContratoService>();
        var padrao = Assert.Single(await modelos.ListarAsync(q => q.Where(m => m.Padrao), Ct));
        Assert.Equal("Contrato padrão", padrao.Nome);

        // Marcar outro como padrão desmarca o anterior.
        var outro = await modelos.SalvarAsync(new ModeloContrato { Nome = "Padrão de teste", Conteudo = GeradorContrato.ModeloPadrao, Padrao = true }, Ct);
        Assert.Equal(outro.Id, Assert.Single(await modelos.ListarAsync(q => q.Where(m => m.Padrao), Ct)).Id);

        // Desmarcado pelo sistema, o modelo mudou de versão: a tela o lê de novo antes de editar.
        padrao = (await modelos.ObterAsync(padrao.Id, Ct))!;
        padrao.Padrao = true;
        await modelos.SalvarAsync(padrao, Ct);
        await modelos.ExcluirAsync(outro.Id, Ct);

        var eventoId = await CriarEventoAsync(Hoje.AddYears(5).AddDays(60));
        var tipo = await Lookup(TipoLookup.TiposEvento);
        var especifico = await modelos.SalvarAsync(new ModeloContrato
        {
            Nome = "Modelo do tipo (teste)",
            TipoEventoId = tipo.Id,
            Conteudo = GeradorContrato.ModeloPadrao.Replace("- DO OBJETO", "- DO OBJETO DO EVENTO")
        }, Ct);
        try
        {
            Assert.Equal(especifico.Id, await contratos.ModeloSugeridoAsync(eventoId, Ct));
            var minuta = await contratos.GerarMinutaAsync(eventoId, Ct);
            Assert.Contains("CLÁUSULA 1ª - DO OBJETO DO EVENTO", minuta);
            Assert.DoesNotContain("{{", minuta);

            var pelaEscolha = await contratos.GerarMinutaAsync(eventoId, padrao.Id, ct: Ct);
            Assert.DoesNotContain("DO OBJETO DO EVENTO", pelaEscolha);

            var emBranco = await contratos.GerarMinutaAsync(eventoId, null, emBranco: true, ct: Ct);
            Assert.Contains("(Escreva aqui as demais condições.)", emBranco);

            var id = await contratos.EmitirAsync(eventoId, minuta, especifico.Id, Ct);
            Assert.Equal(especifico.Id, (await contratos.ObterAsync(id, Ct)).ModeloContratoId);
            await contratos.CancelarAsync(id, "Teste de modelo", Ct);

            // Modelo usado em contrato não pode ser excluído: só inativado.
            await Assert.ThrowsAsync<RegraNegocioException>(() => modelos.ExcluirAsync(especifico.Id, Ct));
        }
        finally
        {
            especifico.Ativo = false;
            await modelos.SalvarAsync(especifico, Ct);
        }
    }

    [Fact]
    public async Task Compra_em_embalagem_do_fornecedor_entra_convertida_e_aparece_no_kardex()
    {
        var produtoId = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Refrigerante 2L")).Id;
        var estoque = banco.Obter<EstoqueService>();
        var fardo = Assert.Single(await estoque.ListarEmbalagensAsync(produtoId, Ct));
        Assert.Equal(6, fardo.Fator);
        Assert.Equal("PCT", fardo.UnidadeMedida!.Sigla);

        var movimentacao = await estoque.RegistrarCompraAsync(new EntradaEstoqueInput
        {
            ProdutoId = produtoId,
            FornecedorId = fardo.FornecedorId,
            ProdutoEmbalagemId = fardo.Id,
            Quantidade = 3,
            CustoUnitario = 51m,
            Observacao = "Teste de embalagem"
        }, Ct);
        Assert.Equal(18, movimentacao.Quantidade);
        Assert.Equal(8.5m, movimentacao.CustoUnitario);

        var kardex = await banco.Obter<IConsultasEstoqueCardapio>().ListarMovimentacoesAsync(DateTime.Today, DateTime.Today, produtoId, ct: Ct);
        var linha = kardex.Single(m => m.Id == movimentacao.Id);
        Assert.Equal("3 PCT", linha.Compra);
        Assert.Equal("Gelada Bebidas", linha.Fornecedor);

        // A embalagem de outro fornecedor não vale para esta compra.
        var outroFornecedor = (await Lookup(TipoLookup.Fornecedores, f => f.Id != fardo.FornecedorId)).Id;
        await Assert.ThrowsAsync<RegraNegocioException>(() => estoque.RegistrarCompraAsync(new EntradaEstoqueInput
        {
            ProdutoId = produtoId, FornecedorId = outroFornecedor, ProdutoEmbalagemId = fardo.Id, Quantidade = 1, CustoUnitario = 50m
        }, Ct));

        // A tela de produto mantém as embalagens e grava as novas.
        var cadastro = banco.Obter<ICadastroService<Produto>>();
        var produto = (await cadastro.ObterAsync(produtoId, Ct))!;
        produto.Embalagens.Add(new ProdutoEmbalagem
        {
            UnidadeMedidaId = (await Lookup(TipoLookup.UnidadesMedida, u => u.Info == "CX")).Id,
            Fator = 12,
            Descricao = "Caixa com 12 (teste)"
        });
        await cadastro.SalvarAsync(produto, Ct);
        Assert.Equal(2, (await estoque.ListarEmbalagensAsync(produtoId, Ct)).Count);
    }

    [Fact]
    public async Task Entrada_de_compra_lanca_a_conta_a_pagar_unica_ou_parcelada()
    {
        var produtoId = (await Lookup(TipoLookup.Produtos, p => p.Nome == "Refrigerante 2L")).Id;
        var fornecedorId = (await Lookup(TipoLookup.Fornecedores)).Id;
        var estoque = banco.Obter<EstoqueService>();
        var nota = $"NF {Guid.NewGuid():N}"[..20];

        var entradaUnica = await estoque.RegistrarCompraAsync(new EntradaEstoqueInput
        {
            ProdutoId = produtoId, FornecedorId = fornecedorId, Quantidade = 10, CustoUnitario = 8.75m,
            GerarContaPagar = true, Documento = nota, Vencimento = Hoje.AddDays(28)
        }, Ct);

        var parcelada = nota + "P";
        var entradaParcelada = await estoque.RegistrarCompraAsync(new EntradaEstoqueInput
        {
            ProdutoId = produtoId, FornecedorId = fornecedorId, Quantidade = 30, CustoUnitario = 10m,
            GerarContaPagar = true, Documento = parcelada, Vencimento = Hoje.AddDays(30), Parcelas = 3,
            Categoria = Domain.Financeiro.CategoriaDespesa.Outros
        }, Ct);

        await using var db = banco.Obter<Application.Abstracoes.IBuffetDbContextFactory>().Criar();
        var unica = await db.ContasPagar.SingleAsync(c => c.Documento == nota, Ct);
        Assert.Equal(87.50m, unica.Valor);
        Assert.Equal(Hoje.AddDays(28), unica.Vencimento);
        Assert.Equal(fornecedorId, unica.FornecedorId);
        Assert.Equal(Domain.Financeiro.CategoriaDespesa.Insumos, unica.Categoria);
        Assert.StartsWith("Compra de Refrigerante 2L", unica.Descricao);

        var parcelas = await db.ContasPagar.Where(c => c.Documento == parcelada).OrderBy(c => c.Vencimento).ToListAsync(Ct);
        Assert.Equal(3, parcelas.Count);
        Assert.Equal(300m, parcelas.Sum(c => c.Valor));
        Assert.All(parcelas, c => Assert.NotNull(c.ParcelamentoId));
        Assert.All(parcelas, c => Assert.Equal(Domain.Financeiro.CategoriaDespesa.Outros, c.Categoria));
        Assert.Equal(Hoje.AddDays(30), parcelas[0].Vencimento);

        // Vínculo nos dois sentidos: a entrada aponta para a conta e a conta mostra de qual entrada veio.
        Assert.Equal(unica.Id, entradaUnica.ContaPagarId);
        Assert.Equal(parcelas[0].ParcelamentoId, entradaParcelada.ParcelamentoPagarId);
        var financeiro = banco.Obter<FinanceiroService>();
        Assert.Contains("Refrigerante 2L", (await financeiro.ObterContaAsync(unica.Id, Ct)).OrigemEstoque);
        Assert.Contains("Refrigerante 2L", (await financeiro.ObterContaAsync(parcelas[1].Id, Ct)).OrigemEstoque);
        var kardex = await banco.Obter<IConsultasEstoqueCardapio>().ListarMovimentacoesAsync(DateTime.Today, DateTime.Today, produtoId, ct: Ct);
        Assert.Equal("Conta em aberto", kardex.Single(m => m.Id == entradaUnica.Id).ContaPagar);
        Assert.Equal($"PAR-{entradaParcelada.ParcelamentoPagarId:D5}: 0 de 3 paga(s)", kardex.Single(m => m.Id == entradaParcelada.Id).ContaPagar);

        // Excluir a conta ou desfazer o parcelamento solta o vínculo; a entrada continua no histórico.
        await financeiro.ExcluirContaAsync(unica.Id, Ct);
        await financeiro.DesfazerParcelamentoAsync(entradaParcelada.ParcelamentoPagarId!.Value, Ct);
        kardex = await banco.Obter<IConsultasEstoqueCardapio>().ListarMovimentacoesAsync(DateTime.Today, DateTime.Today, produtoId, ct: Ct);
        Assert.Equal("", kardex.Single(m => m.Id == entradaUnica.Id).ContaPagar);
        Assert.Equal("", kardex.Single(m => m.Id == entradaParcelada.Id).ContaPagar);

        // Sem vencimento não há conta: nada é gravado, nem a entrada no estoque.
        var saldo = (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produtoId, Ct))!.EstoqueAtual;
        await Assert.ThrowsAsync<RegraNegocioException>(() => estoque.RegistrarCompraAsync(new EntradaEstoqueInput
        {
            ProdutoId = produtoId, Quantidade = 5, CustoUnitario = 9m, GerarContaPagar = true
        }, Ct));
        Assert.Equal(saldo, (await banco.Obter<ICadastroService<Produto>>().ObterAsync(produtoId, Ct))!.EstoqueAtual);
    }
}
