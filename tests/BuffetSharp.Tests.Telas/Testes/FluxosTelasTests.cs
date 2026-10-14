using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Cadastros;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Formularios.Financeiro;
using BuffetSharp.WinForms.Formularios.Seguranca;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Fluxos completos operados pelas telas, como o usuário faria.</summary>
public class FluxosTelasTests(AmbienteTelas ambiente)
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public void Cadastro_generico_valida_o_campo_obrigatorio_e_inclui_o_registro() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormCadastro<TipoEvento>>(principal, "Tipos de evento");

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Novo"), dialogo =>
            {
                robo.Clicar(dialogo, "Salvar");
                robo.Aguardar(200);
                var editor = Robo.Interno<EditorCampos>(dialogo, "_editor");
                var nome = Robo.Campo<TextBox>(dialogo, "Nome");
                Assert.Equal("Informe o nome.", Robo.Interno<ErrorProvider>(editor, "_erros").GetError(nome));
                Assert.True(dialogo.Visible, "Com erro de validação o diálogo deve continuar aberto.");

                robo.Preencher(dialogo, "Nome", "Chá de Panela");
                robo.Preencher(dialogo, "Descrição", "Incluído pelo teste de tela");
                robo.Clicar(dialogo, "Salvar");
            }, "novo tipo de evento");

            robo.Esperar(() => Robo.Linhas<TipoEvento>(Robo.Grade(tela)).Any(t => t.Nome == "Chá de Panela"), "novo tipo na lista");
        });

    [Fact]
    public void Compra_de_material_aumenta_o_patrimonio() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormCadastro<Material>>(principal, "Materiais e enxoval");
            var grade = Robo.Grade(tela);
            var material = Robo.Linhas<Material>(grade).First(m => m.Nome == "Cadeira Tiffany");
            robo.SelecionarLinha<Material>(grade, m => m.Id == material.Id);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Compra"), dialogo =>
            {
                robo.Preencher(dialogo, "Quantidade comprada", 12);
                robo.Clicar(dialogo, "Salvar");
            }, "compra de material");

            robo.Esperar(() => Robo.Linhas<Material>(grade).Single(m => m.Id == material.Id).QuantidadePropria == material.QuantidadePropria + 12,
                "patrimônio atualizado na lista");
        });

    [Fact]
    public void Orcamento_novo_calcula_o_total_em_tempo_real_verifica_o_espaco_e_salva() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormOrcamentos>(principal, "Orçamentos");
            decimal esperado = 0;

            robo.Dialogo<FormOrcamentoEdicao>(() => robo.Clicar(tela, "Novo"), editor =>
            {
                robo.Escolher(editor, "Cliente", "Mariana");
                robo.Escolher(editor, "Tipo de evento", "Casamento");
                Robo.Campo<DateTimePicker>(editor, "Data do evento").Value = Hoje.AddYears(3).AddDays(17).ToDateTime(TimeOnly.MinValue);
                robo.Preencher(editor, "Convidados", 120);
                robo.Escolher(editor, "Pacote", "Prata");
                robo.Escolher(editor, "Espaço", "Salão Cristal");

                var pacote = (ItemLookup)Robo.Campo<ComboBox>(editor, "Pacote").SelectedItem!;
                var espaco = (ItemLookup)Robo.Campo<ComboBox>(editor, "Espaço").SelectedItem!;
                esperado = pacote.Valor * 120 + espaco.Valor;
                Assert.Equal(Textos.Moeda(esperado), Robo.Interno<Label>(editor, "_total").Text);

                var disponivel = Robo.Interno<Label>(editor, "_disponivel");
                robo.Esperar(() => disponivel.Text.StartsWith('✔'), "verificação de disponibilidade do espaço");

                robo.Clicar(editor, "Salvar");
            }, "novo orçamento");

            robo.Esperar(() => Robo.Linhas<OrcamentoResumo>(Robo.Grade(tela))
                .Any(o => o.Cliente.StartsWith("Mariana") && o.ValorTotal == esperado && o.Status == StatusOrcamento.Rascunho), "orçamento na lista");
            var novo = Robo.Linhas<OrcamentoResumo>(Robo.Grade(tela)).First(o => o.ValorTotal == esperado && o.Cliente.StartsWith("Mariana"));
            Assert.Equal("Administrador", novo.Vendedor); // vendedor padrão: usuário logado
        });

    [Fact]
    public void Evento_reserva_material_e_emite_contrato_assinado_na_tela() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var eventoId = CriarEvento(robo, Hoje.AddYears(3).AddDays(45));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{eventoId}", () => navegador.Criar<FormEventoDetalhe>(eventoId));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;

            // Materiais: a quantidade sugerida é uma por convidado.
            robo.SelecionarAba(evento, "Materiais");
            robo.Escolher(Robo.Interno<ComboBox>(evento, "_material"), "Prato raso");
            Assert.Equal(40, Robo.Interno<NumericUpDown>(evento, "_quantidadeMaterial").Value);
            robo.Clicar(evento, "Reservar / atualizar");
            var materiais = Robo.Interno<Grade<Domain.Eventos.EventoMaterial>>(evento, "_materiais");
            robo.Esperar(() => materiais.Itens.Any(m => m.Material?.Nome == "Prato raso de porcelana" && m.Quantidade == 40), "material reservado");

            // Contrato: gerar, emitir e coletar a assinatura desenhada.
            robo.SelecionarAba(evento, "Contrato");
            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Gerar contrato"), dialogo =>
            {
                Assert.Contains("CLÁUSULA 1ª - DO OBJETO", Robo.Campo<TextBox>(dialogo, "Texto do contrato").Text);
                robo.Clicar(dialogo, "Salvar");
            }, "emissão do contrato");
            var situacao = Robo.Interno<Label>(evento, "_situacaoContrato");
            robo.Esperar(() => situacao.Text.Contains("Aguardando assinatura"), "contrato emitido");

            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Coletar assinaturas"), dialogo =>
            {
                var painel = Robo.Encontrar<PainelAssinatura>(dialogo);
                Assert.True(painel.Vazio);
                robo.Desenhar(painel);
                Assert.False(painel.Vazio);
                Robo.Campo<CheckBox>(dialogo, "O contratante leu e concorda com todas as cláusulas do contrato").Checked = true;
                robo.Clicar(dialogo, "Salvar");
            }, "coleta de assinaturas");
            robo.Esperar(() => situacao.Text.Contains("Assinado"), "contrato assinado");

            var contrato = robo.Rodar(() => robo.Servico<ContratoService>().ListarDoEventoAsync(eventoId)).Single();
            Assert.Equal(StatusContrato.Assinado, contrato.Status);
            Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], contrato.AssinaturaCliente![..4]);
            Assert.Null(contrato.AssinaturaEmpresa); // a do representante é opcional e não foi desenhada
        });

    [Fact]
    public void Degustacao_agendada_e_cancelada_pela_tela() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormDegustacoes>(principal, "Degustações");
            var dia = Hoje.AddDays(40);
            while (dia.DayOfWeek == DayOfWeek.Sunday)
                dia = dia.AddDays(1);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Agendar"), dialogo =>
            {
                robo.Escolher(dialogo, "Cliente", "Natália");
                Robo.Campo<DateTimePicker>(dialogo, "Data").Value = dia.ToDateTime(TimeOnly.MinValue);
                Robo.Campo<DateTimePicker>(dialogo, "Horário").Value = DateTime.Today.AddHours(10);
                robo.Preencher(dialogo, "Pessoas", 3);
                robo.Escolher(dialogo, "Pacote a degustar", "Ouro");
                robo.Clicar(dialogo, "Salvar");
            }, "agendar degustação");

            var quando = dia.ToDateTime(new TimeOnly(10, 0));
            bool Agendada(DegustacaoResumo d) => d.Cliente.StartsWith("Natália") && d.DataHora == quando;
            robo.Esperar(() => Robo.Linhas<DegustacaoResumo>(Robo.Grade(tela)).Any(d => Agendada(d) && d.Status == StatusDegustacao.Agendada),
                "degustação agendada na lista");

            robo.SelecionarLinha<DegustacaoResumo>(Robo.Grade(tela), Agendada);
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Cancelar"), dialogo =>
            {
                robo.Preencher(dialogo, "Motivo", "Cliente pediu para remarcar");
                robo.Clicar(dialogo, "Salvar");
            }, "cancelar degustação");
            robo.Esperar(() => Robo.Linhas<DegustacaoResumo>(Robo.Grade(tela)).Any(d => Agendada(d) && d.Status == StatusDegustacao.Cancelada),
                "degustação cancelada");
        });

    [Fact]
    public void Comissao_fechada_e_desfeita_pela_tela() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormComissoes>(principal, "Comissões");
            var inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-3);
            Robo.Interno<DateTimePicker>(tela, "_de").Value = inicio;
            Robo.Interno<DateTimePicker>(tela, "_ate").Value = inicio.AddMonths(1).AddDays(-1);
            robo.Esperar(() => Robo.Carregado(tela) && Robo.Linhas<ComissaoVendedor>(Robo.Grade(tela)).Any(c => c.Comissao > 0 && !c.Fechada),
                "comissões apuradas no período");

            var vendedor = Robo.Linhas<ComissaoVendedor>(Robo.Grade(tela)).First(c => c.Comissao > 0 && !c.Fechada);
            robo.SelecionarLinha<ComissaoVendedor>(Robo.Grade(tela), c => c.VendedorId == vendedor.VendedorId);
            robo.Clicar(tela, "Fechar comissão");
            robo.Esperar(() => Robo.Linhas<ComissaoVendedor>(Robo.Grade(tela)).Single(c => c.VendedorId == vendedor.VendedorId).Fechada,
                "comissão fechada");
            var fechamentos = Robo.Interno<Grade<FechamentoComissaoResumo>>(tela, "_fechamentos");
            robo.Esperar(() => fechamentos.Itens.Any(f => f.VendedorId == vendedor.VendedorId && f.Valor == vendedor.Comissao),
                "fechamento na lista de fechamentos");

            robo.SelecionarLinha<ComissaoVendedor>(Robo.Grade(tela), c => c.VendedorId == vendedor.VendedorId);
            robo.Clicar(tela, "Desfazer fechamento");
            robo.Esperar(() => !Robo.Linhas<ComissaoVendedor>(Robo.Grade(tela)).Single(c => c.VendedorId == vendedor.VendedorId).Fechada,
                "fechamento desfeito");
        });

    [Fact]
    public void Proposta_enviada_por_email_pela_tela_e_whatsapp_mostra_a_mensagem_pronta() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormOrcamentos>(principal, "Orçamentos");
            var orcamento = Robo.Linhas<OrcamentoResumo>(Robo.Grade(tela)).First(o => o.Status == StatusOrcamento.Enviado);
            robo.SelecionarLinha<OrcamentoResumo>(Robo.Grade(tela), o => o.Id == orcamento.Id);

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "E-mail"), dialogo =>
            {
                Assert.Contains(orcamento.Codigo, Robo.Campo<TextBox>(dialogo, "Assunto").Text);
                Robo.Campo<CheckBox>(dialogo, "Anexar proposta (PDF)").Checked = false; // não depende de impressora PDF
                robo.Clicar(dialogo, "Salvar");
            }, "e-mail da proposta");
            robo.Esperar(() => robo.Mensagens.Any(m => m.Titulo == "E-mail enviado"), "confirmação do envio");
            Assert.Contains(Directory.GetFiles(ambiente.PastaEmails, "*.eml"),
                arquivo => File.ReadAllText(arquivo).Contains(orcamento.Codigo));
            robo.Esperar(() => Robo.Linhas<OrcamentoResumo>(Robo.Grade(tela)).Single(o => o.Id == orcamento.Id).CanalUltimoEnvio == CanalEnvio.Email,
                "último envio na lista");

            // WhatsApp: a mensagem é montada para revisão; o envio em si abriria o aplicativo, então cancelamos.
            robo.SelecionarLinha<OrcamentoResumo>(Robo.Grade(tela), o => o.Id == orcamento.Id);
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "WhatsApp"), dialogo =>
            {
                var mensagem = Robo.Campo<TextBox>(dialogo, "Mensagem").Text;
                Assert.Contains(orcamento.Codigo, mensagem);
                Assert.Contains("*Total:", mensagem);
                robo.Clicar(dialogo, "Cancelar");
            }, "mensagem de WhatsApp");
        });

    [Fact]
    public void Alteracao_feita_na_tela_aparece_na_auditoria_do_registro() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormCadastro<Cliente>>(principal, "Clientes");
            var cliente = Robo.Linhas<Cliente>(Robo.Grade(tela)).First(c => c.TipoPessoa == TipoPessoa.Fisica);
            robo.SelecionarLinha<Cliente>(Robo.Grade(tela), c => c.Id == cliente.Id);
            var observacao = $"Prefere contato por WhatsApp ({Guid.NewGuid():N})"[..40];

            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Editar"), dialogo =>
            {
                robo.Preencher(dialogo, "Observações", observacao);
                robo.Clicar(dialogo, "Salvar");
            }, "editar cliente");
            robo.Esperar(() => Robo.Linhas<Cliente>(Robo.Grade(tela)).Single(c => c.Id == cliente.Id).Observacoes == observacao, "cliente salvo");

            robo.SelecionarLinha<Cliente>(Robo.Grade(tela), c => c.Id == cliente.Id);
            robo.Clicar(tela, "Alterações");
            robo.Esperar(() => Robo.AbaAtual(principal) is FormAuditoria auditoria && Robo.Carregado(auditoria) &&
                               Robo.Linhas<RegistroAuditoriaResumo>(Robo.Grade(auditoria)).Count > 0, "histórico do cliente");

            var registro = Robo.Linhas<RegistroAuditoriaResumo>(Robo.Grade(Robo.AbaAtual(principal)!)).First();
            Assert.Equal(AcaoAuditoria.Alteracao, registro.Acao);
            Assert.Equal("admin", registro.Usuario);
            Assert.Equal(new AlteracaoCampo(nameof(Cliente.Observacoes), cliente.Observacoes, observacao), Assert.Single(registro.Campos));
        });

    private static int CriarEvento(Robo robo, DateOnly data)
    {
        var lookups = robo.Servico<IConsultasLookup>();
        var cliente = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First();
        var tipo = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First();
        var espaco = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).OrderBy(e => e.Numero).First(e => e.Numero >= 40);
        var eventos = robo.Servico<EventoService>();
        var id = robo.Rodar(() => eventos.SalvarAsync(new EventoInput
        {
            Titulo = "Coquetel do teste de tela",
            ClienteId = cliente.Id,
            TipoEventoId = tipo.Id,
            EspacoId = espaco.Id,
            Data = data,
            HoraInicio = new TimeOnly(18, 0),
            HoraFim = new TimeOnly(22, 0),
            Convidados = 40,
            ValorTotal = 9000m
        }));
        robo.Rodar(() => eventos.GerarParcelasAsync(id,
            new PlanoPagamentoInput { Quantidade = 2, PrimeiroVencimento = Hoje.AddMonths(1), ValorEntrada = 3000m }));
        return id;
    }
}
