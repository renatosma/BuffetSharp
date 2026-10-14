using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Fluxos ponta a ponta: EF Core (escrita) + Dapper (leitura) no mesmo banco.</summary>
[Collection(ColecaoBanco.Nome)]
public class ServicosTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true)).Id;

    [Fact]
    public async Task Cadastro_generico_salva_altera_e_traduz_duplicidade()
    {
        var servico = banco.Obter<ICadastroService<TipoEvento>>();

        var novo = await servico.SalvarAsync(new TipoEvento { Nome = "Chá Revelação" }, Ct);
        Assert.True(novo.Id > 0);

        novo.Descricao = "Revelação do sexo do bebê";
        await servico.SalvarAsync(novo, Ct);
        Assert.Equal("Revelação do sexo do bebê", (await servico.ObterAsync(novo.Id, Ct))!.Descricao);

        var duplicado = await Assert.ThrowsAsync<RegraNegocioException>(() => servico.SalvarAsync(new TipoEvento { Nome = "Chá Revelação" }, Ct));
        Assert.Contains("duplicado", duplicado.Message);

        await servico.ExcluirAsync(novo.Id, Ct);
        Assert.Null(await servico.ObterAsync(novo.Id, Ct));
    }

    [Fact]
    public async Task Excluir_cadastro_em_uso_retorna_mensagem_amigavel()
    {
        var espacoId = await Lookup(TipoLookup.Espacos);
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => banco.Obter<ICadastroService<Espaco>>().ExcluirAsync(espacoId, Ct));
        Assert.Contains("inative", erro.Message);
    }

    [Fact]
    public async Task Salvar_produto_pela_tela_nao_sobrescreve_o_saldo()
    {
        var cadastro = banco.Obter<ICadastroService<Domain.Estoque.Produto>>();
        var produtoId = await Lookup(TipoLookup.Produtos);
        var produtoNaTela = (await cadastro.ObterAsync(produtoId, Ct))!;

        // Enquanto a tela está aberta, outra operação movimenta o estoque.
        await banco.Obter<EstoqueService>().RegistrarEntradaAsync(produtoId, 10, produtoNaTela.CustoMedio, "Teste", Ct);

        produtoNaTela.EstoqueMinimo += 1;
        await cadastro.SalvarAsync(produtoNaTela, Ct);

        var atual = (await cadastro.ObterAsync(produtoId, Ct))!;
        Assert.Equal(produtoNaTela.EstoqueAtual + 10, atual.EstoqueAtual);
        Assert.Equal(produtoNaTela.EstoqueMinimo, atual.EstoqueMinimo);
    }

    [Fact]
    public async Task Orcamento_aprovado_vira_evento_e_bloqueia_o_espaco()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var espaco = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Espacos, ct: Ct)).MaxBy(e => e.Numero)!;
        var pacoteId = await Lookup(TipoLookup.Pacotes, p => p.Numero <= 50);
        var servico = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.ServicosAdicionais, ct: Ct)).First();

        var input = orcamentos.Novo();
        input.ClienteId = await Lookup(TipoLookup.Clientes);
        input.TipoEventoId = await Lookup(TipoLookup.TiposEvento);
        input.EspacoId = espaco.Id;
        input.PacoteId = pacoteId;
        input.Data = DateOnly.FromDateTime(DateTime.Today).AddYears(2).AddDays(3); // data sem eventos na base
        input.ValidoAte = DateOnly.FromDateTime(DateTime.Today).AddDays(10);
        input.Convidados = 100;
        input.PercentualDesconto = 5;
        input.Servicos.Add(new ServicoItemInput { ServicoAdicionalId = servico.Id, Quantidade = 1, ValorUnitario = servico.Valor });

        var id = await orcamentos.SalvarAsync(input, Ct);
        await orcamentos.EnviarAsync(id, Ct);
        var eventoId = await orcamentos.AprovarAsync(id, "Evento de teste", Ct);

        var detalhe = await banco.Obter<EventoService>().ObterDetalheAsync(eventoId, Ct);
        Assert.Equal(StatusEvento.Confirmado, detalhe.Evento.Status);
        Assert.Equal(id, detalhe.Evento.OrcamentoId);
        Assert.True(detalhe.CustoInsumosEstimado > 0);
        Assert.Single(detalhe.Evento.Servicos);

        // Segundo orçamento no mesmo espaço e horário: a aprovação deve ser bloqueada.
        input.Id = 0;
        input.Servicos.Clear();
        var concorrente = await orcamentos.SalvarAsync(input, Ct);
        await orcamentos.EnviarAsync(concorrente, Ct);
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.AprovarAsync(concorrente, "Conflito", Ct));
        Assert.Contains("reservado", erro.Message);
    }

    [Fact]
    public async Task Evento_parcelado_pago_e_realizado_baixa_estoque()
    {
        var eventos = banco.Obter<EventoService>();
        var lookup = banco.Obter<IConsultasLookup>();
        var espaco = (await lookup.ListarAsync(TipoLookup.Espacos, ct: Ct)).MinBy(e => e.Numero)!;
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        // Evento fechado direto, sem orçamento prévio.
        var input = new EventoInput
        {
            Titulo = "Coquetel de teste",
            ClienteId = await Lookup(TipoLookup.Clientes),
            TipoEventoId = await Lookup(TipoLookup.TiposEvento),
            EspacoId = espaco.Id,
            Data = hoje.AddYears(3),
            HoraInicio = new TimeOnly(10, 0),
            HoraFim = new TimeOnly(12, 0),
            Convidados = 20,
            ValorTotal = 3000m
        };
        var eventoId = await eventos.SalvarAsync(input, Ct);

        await eventos.GerarParcelasAsync(eventoId, new PlanoPagamentoInput { Quantidade = 2, PrimeiroVencimento = hoje, ValorEntrada = 1000m }, Ct);
        var detalhe = await eventos.ObterDetalheAsync(eventoId, Ct);
        Assert.Equal(3, detalhe.Evento.Parcelas.Count);
        Assert.Equal(3000m, detalhe.Evento.Parcelas.Sum(p => p.Valor));

        var financeiro = banco.Obter<FinanceiroService>();
        var entrada = detalhe.Evento.Parcelas.OrderBy(p => p.Numero).First();
        await financeiro.PagarParcelaAsync(entrada.Id, new PagamentoInput { Data = hoje, Valor = entrada.Valor }, Ct);

        await eventos.AdicionarConvidadoAsync(eventoId, "Convidado Teste", null, null, 1, "Mesa 1", Ct);
        var funcionarioId = await Lookup(TipoLookup.Funcionarios);
        await eventos.EscalarFuncionarioAsync(eventoId, funcionarioId, null, null, Ct);

        detalhe = await eventos.ObterDetalheAsync(eventoId, Ct);
        Assert.Equal(1000m, detalhe.Evento.TotalRecebido);
        Assert.Equal(2000m, detalhe.Evento.SaldoAReceber);
        Assert.Equal(2, detalhe.Evento.TotalPessoasNaLista);
        Assert.Single(detalhe.Evento.Equipe);

        // Ainda não começou: não pode ser realizado.
        await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.RealizarAsync(eventoId, Ct));

        await eventos.CancelarAsync(eventoId, "Teste de cancelamento", Ct);
        detalhe = await eventos.ObterDetalheAsync(eventoId, Ct);
        Assert.Equal(StatusEvento.Cancelado, detalhe.Evento.Status);
        Assert.All(detalhe.Evento.Parcelas.Where(p => p.Id != entrada.Id), p => Assert.Equal(StatusTitulo.Cancelado, p.Status));
    }

    [Fact]
    public async Task Base_de_demonstracao_tem_eventos_realizados_com_estoque_baixado()
    {
        var realizados = await banco.Obter<IConsultasEventos>().ListarAsync(new FiltroEventos { Status = StatusEvento.Realizado }, Ct);
        var detalhe = await banco.Obter<EventoService>().ObterDetalheAsync(realizados[0].Id, Ct);

        Assert.True(detalhe.Evento.EstoqueBaixado);
        Assert.True(detalhe.Evento.PessoasPresentes > 0);

        var movimentos = await banco.Obter<IConsultasEstoqueCardapio>()
            .ListarMovimentacoesAsync(detalhe.Evento.Inicio.AddDays(-1), detalhe.Evento.Fim.AddDays(1), ct: Ct);
        Assert.Contains(movimentos, m => m.EventoId == detalhe.Evento.Id);
    }

    [Fact]
    public async Task Autenticacao_e_gestao_de_usuarios()
    {
        var usuarios = banco.Obter<UsuarioService>();

        Assert.Null(await usuarios.AutenticarAsync("admin", "senha-errada", Ct));
        var admin = await usuarios.AutenticarAsync("ADMIN", "Admin@123", Ct);
        Assert.NotNull(admin);
        Assert.True(admin.EhAdministrador);

        await usuarios.SalvarAsync(new UsuarioInput { Nome = "Teste", Login = "teste.usuario", NovaSenha = "abc12345" }, Ct);
        Assert.NotNull(await usuarios.AutenticarAsync("teste.usuario", "abc12345", Ct));

        // A autenticação acima trocou a sessão; volta para o administrador.
        await usuarios.AutenticarAsync("admin", "Admin@123", Ct);
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            usuarios.SalvarAsync(new UsuarioInput { Nome = "Fraca", Login = "fraca", NovaSenha = "123" }, Ct));
    }
}
