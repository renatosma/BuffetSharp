using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Tests.Integracao;

/// <summary>Fluxos ponta a ponta das funcionalidades comerciais, de materiais, comissões e auditoria.</summary>
[Collection(ColecaoBanco.Nome)]
public class NovasFuncionalidadesIntegracaoTests(BancoFixture banco)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private async Task<ItemLookup> Lookup(TipoLookup tipo, Func<ItemLookup, bool>? filtro = null) =>
        (await banco.Obter<IConsultasLookup>().ListarAsync(tipo, ct: Ct)).First(filtro ?? (_ => true));

    /// <summary>Evento de venda direta em horário livre (manhã), longe dos eventos da base de demonstração.</summary>
    private async Task<int> CriarEventoAsync(DateOnly data, int convidados = 40, int? vendedorId = null)
    {
        var espaco = (await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Espacos, ct: Ct)).OrderBy(e => e.Numero)
            .First(e => e.Numero >= convidados);
        var cliente = await Lookup(TipoLookup.Clientes);
        return await banco.Obter<EventoService>().SalvarAsync(new EventoInput
        {
            Titulo = $"Brunch de teste {Guid.NewGuid():N}"[..30],
            ClienteId = cliente.Id,
            TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id,
            EspacoId = espaco.Id,
            VendedorId = vendedorId,
            Data = data,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 30),
            Convidados = convidados,
            ValorTotal = 8000m
        }, Ct);
    }

    // ------------------------- Auditoria -------------------------

    [Fact]
    public async Task Auditoria_registra_inclusao_alteracao_com_valor_anterior_e_exclusao()
    {
        var servico = banco.Obter<ICadastroService<TipoEvento>>();
        var tipo = await servico.SalvarAsync(new TipoEvento { Nome = "Chá de Bebê", Descricao = "Original" }, Ct);
        tipo.Descricao = "Revisada";
        await servico.SalvarAsync(tipo, Ct);
        await servico.ExcluirAsync(tipo.Id, Ct);

        var registros = await banco.Obter<IConsultasAuditoria>().ListarAsync(new FiltroAuditoria
        {
            De = DateTime.Today,
            Ate = DateTime.Today,
            Entidade = nameof(TipoEvento),
            RegistroId = tipo.Id.ToString()
        }, Ct);

        Assert.Equal([AcaoAuditoria.Exclusao, AcaoAuditoria.Alteracao, AcaoAuditoria.Inclusao], registros.Select(r => r.Acao));
        Assert.All(registros, r => Assert.Equal("admin", r.Usuario));
        var alteracao = Assert.Single(registros[1].Campos);
        Assert.Equal(new AlteracaoCampo(nameof(TipoEvento.Descricao), "Original", "Revisada"), alteracao);
        Assert.Contains(registros[2].Campos, c => c is { Campo: nameof(TipoEvento.Nome), Novo: "Chá de Bebê" });
        Assert.Equal("Chá de Bebê", registros[0].Descricao);
    }

    [Fact]
    public async Task Auditoria_registra_campos_de_tipo_complexo_e_oculta_senha()
    {
        var clientes = banco.Obter<ICadastroService<Cliente>>();
        var cliente = (await clientes.ListarAsync(q => q.Where(c => c.Endereco.Cidade != null), Ct)).First();
        var cidadeAnterior = cliente.Endereco.Cidade;
        cliente.Endereco.Cidade = "Campinas";
        await clientes.SalvarAsync(cliente, Ct);

        await banco.Obter<UsuarioService>().SalvarAsync(new UsuarioInput
        {
            Nome = "Auditada", Login = "auditada", NovaSenha = "Senha123", Perfil = Domain.Seguranca.PerfilUsuario.Comercial
        }, Ct);

        var consultas = banco.Obter<IConsultasAuditoria>();
        var alteracao = (await consultas.ListarAsync(new FiltroAuditoria
        {
            De = DateTime.Today, Ate = DateTime.Today, Entidade = nameof(Cliente), RegistroId = cliente.Id.ToString()
        }, Ct)).First();
        Assert.Equal(new AlteracaoCampo("Endereco.Cidade", cidadeAnterior, "Campinas"), Assert.Single(alteracao.Campos));

        var usuario = (await consultas.ListarAsync(new FiltroAuditoria
        {
            De = DateTime.Today, Ate = DateTime.Today, Entidade = "Usuario", Texto = "auditada"
        }, Ct)).First();
        Assert.Contains(usuario.Campos, c => c is { Campo: "SenhaHash", Novo: "(oculto)" });
    }

    // ------------------------- Materiais -------------------------

    [Fact]
    public async Task Reserva_de_material_respeita_o_patrimonio_livre_na_data()
    {
        var material = await banco.Obter<ICadastroService<Material>>().SalvarAsync(new Material
        {
            Nome = "Taça de teste", Categoria = CategoriaMaterial.CoposETacas, QuantidadePropria = 50, CustoLocacao = 3m, ValorReposicao = 20m
        }, Ct);
        var data = Hoje.AddYears(4).AddDays(11);
        var eventos = banco.Obter<EventoService>();
        var primeiro = await CriarEventoAsync(data);
        var segundo = await CriarEventoAsync(data.AddDays(1));

        await eventos.ReservarMaterialAsync(primeiro, material.Id, 40, 0, Ct);

        // No dia seguinte só restam 10 unidades próprias: o restante precisa ser alugado.
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.ReservarMaterialAsync(segundo, material.Id, 30, 0, Ct));
        Assert.Contains("Patrimônio insuficiente", erro.Message);
        await eventos.ReservarMaterialAsync(segundo, material.Id, 30, 20, Ct);

        var disponibilidade = (await banco.Obter<IConsultasEstoqueCardapio>().DisponibilidadeMateriaisAsync(data, null, Ct))
            .Single(d => d.Id == material.Id);
        Assert.Equal(50, disponibilidade.Reservada);
        Assert.Equal(0, disponibilidade.Disponivel);

        // A locação de terceiros vira conta a pagar e não é contada em dobro no resultado do evento.
        Assert.Equal(60m, await eventos.LancarLocacaoMateriaisAsync(segundo, Ct));
        await Assert.ThrowsAsync<RegraNegocioException>(() => eventos.LancarLocacaoMateriaisAsync(segundo, Ct));
        var detalhe = await eventos.ObterDetalheAsync(segundo, Ct);
        Assert.Equal(60m, detalhe.Evento.CustoMateriais);
        Assert.Equal(0m, detalhe.DespesasVinculadas);
    }

    [Fact]
    public async Task Devolucao_com_perdas_baixa_o_patrimonio_e_entra_na_lucratividade()
    {
        var material = await banco.Obter<ICadastroService<Material>>().SalvarAsync(new Material
        {
            Nome = "Prato de teste", Categoria = CategoriaMaterial.Louca, QuantidadePropria = 80, ValorReposicao = 25m
        }, Ct);
        var data = Hoje.AddDays(-2);
        var eventoId = await CriarEventoAsync(data);
        var eventos = banco.Obter<EventoService>();

        await eventos.ReservarMaterialAsync(eventoId, material.Id, 40, 0, Ct);
        await eventos.RegistrarDevolucaoMaterialAsync(eventoId, material.Id, 37, 3, Ct);

        Assert.Equal(77, (await banco.Obter<ICadastroService<Material>>().ObterAsync(material.Id, Ct))!.QuantidadePropria);
        var lucro = (await banco.Obter<IConsultasRelatorios>().LucratividadeAsync(data.ToDateTime(TimeOnly.MinValue),
            data.ToDateTime(TimeOnly.MinValue), Ct)).Single(l => l.Id == eventoId);
        Assert.Equal(75m, lucro.CustoMateriais);
        Assert.Equal(lucro.Receita - lucro.CustoInsumos - lucro.CustoEquipe - 75m - lucro.Despesas, lucro.Lucro);
    }

    // ------------------------- Contratos -------------------------

    [Fact]
    public async Task Contrato_gerado_emitido_assinado_e_reemitido_apos_cancelamento()
    {
        var eventoId = await CriarEventoAsync(Hoje.AddYears(4).AddDays(40));
        var eventos = banco.Obter<EventoService>();
        await eventos.GerarParcelasAsync(eventoId, new PlanoPagamentoInput { Quantidade = 2, PrimeiroVencimento = Hoje.AddMonths(1), ValorEntrada = 2000m }, Ct);
        var contratos = banco.Obter<ContratoService>();

        var minuta = await contratos.GerarMinutaAsync(eventoId, Ct);
        Assert.Contains("CLÁUSULA 1ª - DO OBJETO", minuta);
        Assert.Contains($"EVT-{eventoId:D5}", minuta);
        Assert.Contains("Entrada: R$ 2.000,00", minuta);

        var id = await contratos.EmitirAsync(eventoId, minuta, Ct);
        await Assert.ThrowsAsync<RegraNegocioException>(() => contratos.EmitirAsync(eventoId, minuta, Ct));

        var contrato = await contratos.ObterAsync(id, Ct);
        await contratos.AssinarAsync(id, new AssinaturaContratoInput
        {
            NomeSignatario = contrato.Evento!.Cliente!.Nome,
            DocumentoSignatario = contrato.Evento.Cliente.Documento,
            AssinaturaCliente = Png
        }, Ct);

        var assinado = await contratos.ObterAsync(id, Ct);
        Assert.Equal(StatusContrato.Assinado, assinado.Status);
        Assert.True(assinado.ConteudoIntegro);
        Assert.Equal(Png, assinado.AssinaturaCliente);

        var resumo = (await banco.Obter<IConsultasEventos>().ListarAsync(new FiltroEventos { Texto = $"EVT-{eventoId:D5}" }, Ct)).Single();
        Assert.Equal(StatusContrato.Assinado, resumo.Contrato);

        await contratos.CancelarAsync(id, "Aditivo de convidados", Ct);
        var novo = await contratos.EmitirAsync(eventoId, minuta, Ct);
        var lista = await banco.Obter<IConsultasComercial>().ListarContratosAsync(null, Ct);
        Assert.Contains(lista, c => c.Id == id && c.Status == StatusContrato.Cancelado);
        Assert.Contains(lista, c => c.Id == novo && c.Status == StatusContrato.AguardandoAssinatura);
    }

    // ------------------------- Degustações -------------------------

    [Fact]
    public async Task Degustacoes_respeitam_o_limite_por_horario()
    {
        var servico = banco.Obter<DegustacaoService>();
        var cliente = await Lookup(TipoLookup.Clientes);
        var data = Hoje.AddYears(3).AddDays(5);
        while (data.DayOfWeek == DayOfWeek.Sunday)
            data = data.AddDays(1);

        var limite = banco.Obter<BuffetOptions>().LimiteDegustacoesPorHorario;
        for (var i = 0; i < limite; i++)
            await servico.SalvarAsync(new DegustacaoInput { ClienteId = cliente.Id, Data = data, Hora = new TimeOnly(15, i * 10), Pessoas = 2 }, Ct);

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            servico.SalvarAsync(new DegustacaoInput { ClienteId = cliente.Id, Data = data, Hora = new TimeOnly(15, 30), Pessoas = 2 }, Ct));
        Assert.Contains("limite", erro.Message);
        await servico.SalvarAsync(new DegustacaoInput { ClienteId = cliente.Id, Data = data, Hora = new TimeOnly(17, 0), Pessoas = 2 }, Ct);

        var agenda = await banco.Obter<IConsultasComercial>().ListarDegustacoesAsync(new FiltroDegustacoes
        {
            De = data.ToDateTime(TimeOnly.MinValue), Ate = data.ToDateTime(TimeOnly.MinValue)
        }, Ct);
        Assert.Equal(limite + 1, agenda.Count);
        Assert.All(agenda, d => Assert.Equal(StatusDegustacao.Agendada, d.Status));
    }

    // ------------------------- Envio de proposta -------------------------

    [Fact]
    public async Task Proposta_enviada_por_email_e_whatsapp_fica_registrada()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var input = orcamentos.Novo();
        input.ClienteId = (await Lookup(TipoLookup.Clientes)).Id;
        input.TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id;
        input.PacoteId = (await Lookup(TipoLookup.Pacotes, p => p.Numero <= 50)).Id;
        input.Convidados = 80;
        input.Data = Hoje.AddYears(2).AddDays(60);
        var id = await orcamentos.SalvarAsync(input, Ct);

        var propostas = banco.Obter<PropostaService>();
        var email = await propostas.PrepararEmailAsync(id, Ct);
        Assert.Contains($"ORC-{id:D5}", email.Assunto);
        await Assert.ThrowsAsync<RegraNegocioException>(() => propostas.EnviarEmailAsync(id, new EmailInput { Para = "invalido", Assunto = "x" }, Ct));

        var pasta = Path.Combine(AppContext.BaseDirectory, banco.Obter<EmailOptions>().Pasta);
        var antes = Directory.Exists(pasta) ? Directory.GetFiles(pasta, "*.eml").ToHashSet() : [];
        email.Para = "cliente.teste@email.exemplo.com.br";
        email.Anexo = new AnexoEmail("Proposta.pdf", [1, 2, 3]);
        await propostas.EnviarEmailAsync(id, email, Ct);

        var novo = Assert.Single(Directory.GetFiles(pasta, "*.eml"), f => !antes.Contains(f));
        var conteudo = await File.ReadAllTextAsync(novo, Ct);
        File.Delete(novo);
        Assert.Contains("cliente.teste@email.exemplo.com.br", conteudo);
        Assert.Contains("Proposta.pdf", conteudo);

        var whatsapp = await propostas.PrepararWhatsAppAsync(id, Ct);
        Assert.StartsWith("https://wa.me/55", whatsapp.Link);
        await propostas.RegistrarWhatsAppAsync(id, "(11) 98888-7777", Ct);

        var orcamento = await orcamentos.ObterAsync(id, Ct);
        Assert.Equal(StatusOrcamento.Enviado, orcamento.Status);
        Assert.Equal([CanalEnvio.WhatsApp, CanalEnvio.Email], orcamento.Envios.Select(e => e.Canal));
        var resumo = (await banco.Obter<IConsultasComercial>().ListarOrcamentosAsync(new FiltroOrcamentos { Texto = $"ORC-{id:D5}" }, Ct)).Single();
        Assert.Equal(CanalEnvio.WhatsApp, resumo.CanalUltimoEnvio);
    }

    [Fact]
    public async Task Vendedor_do_orcamento_precisa_ser_comercial_ou_administrador()
    {
        var orcamentos = banco.Obter<OrcamentoService>();
        var input = orcamentos.Novo();
        Assert.Equal(1, input.VendedorId); // sessão do administrador
        input.ClienteId = (await Lookup(TipoLookup.Clientes)).Id;
        input.TipoEventoId = (await Lookup(TipoLookup.TiposEvento)).Id;
        input.Data = Hoje.AddYears(2).AddDays(90);

        var financeiro = (await banco.Obter<UsuarioService>().ListarAsync(Ct)).First(u => u.Login == "financeiro");
        input.VendedorId = financeiro.Id;
        await Assert.ThrowsAsync<RegraNegocioException>(() => orcamentos.SalvarAsync(input, Ct));
    }

    // ------------------------- Comissões -------------------------

    [Fact]
    public async Task Comissao_apurada_fecha_gera_conta_e_nao_permite_periodo_sobreposto()
    {
        var consultas = banco.Obter<IConsultasComissoes>();
        var de = new DateOnly(Hoje.Year, Hoje.Month, 1).AddMonths(-4);
        var ate = de.AddMonths(1).AddDays(-1);

        var apurado = (await consultas.ApurarAsync(de, ate, Ct)).First(c => c.Comissao > 0);
        var detalhe = await consultas.DetalharAsync(apurado.VendedorId, de, ate, Ct);
        Assert.Equal(apurado.Comissao, detalhe.Sum(p => p.Comissao));
        Assert.Equal(apurado.BaseRecebida, detalhe.Sum(p => p.ValorPago));

        var comissoes = banco.Obter<ComissaoService>();
        var fechamentoId = await comissoes.FecharAsync(apurado.VendedorId, de, ate, Ct);
        await Assert.ThrowsAsync<RegraNegocioException>(() => comissoes.FecharAsync(apurado.VendedorId, ate.AddDays(-5), ate.AddDays(10), Ct));

        var fechada = (await consultas.ApurarAsync(de, ate, Ct)).Single(c => c.VendedorId == apurado.VendedorId);
        Assert.Equal(fechamentoId, fechada.FechamentoId);
        var contas = await banco.Obter<IConsultasFinanceiro>().ListarContasPagarAsync(new FiltroTitulos { Texto = "Comissão de vendas" }, Hoje, Ct);
        Assert.Contains(contas, c => c.Categoria == CategoriaDespesa.Comissoes && c.Valor == apurado.Comissao && c.Status == StatusTitulo.Aberto);

        await comissoes.DesfazerAsync(fechamentoId, Ct);
        Assert.Null((await consultas.ApurarAsync(de, ate, Ct)).Single(c => c.VendedorId == apurado.VendedorId).FechamentoId);
    }

    [Fact]
    public async Task Base_de_demonstracao_tem_contratos_degustacoes_materiais_e_comissoes()
    {
        var comercial = banco.Obter<IConsultasComercial>();
        Assert.Contains(await comercial.ListarContratosAsync(StatusContrato.Assinado, Ct), c => c.AssinadoEm is not null);
        Assert.NotEmpty(await comercial.ListarDegustacoesAsync(new FiltroDegustacoes { Status = StatusDegustacao.Realizada }, Ct));
        Assert.NotEmpty(await banco.Obter<IConsultasComissoes>().ListarFechamentosAsync(Ct));
        Assert.Contains(await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Materiais, ct: Ct), m => m.Numero > 0);
        Assert.Contains(await banco.Obter<IConsultasLookup>().ListarAsync(TipoLookup.Vendedores, ct: Ct), v => v.Valor > 0);
    }
}
