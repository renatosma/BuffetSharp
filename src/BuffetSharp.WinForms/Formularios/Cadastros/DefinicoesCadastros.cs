using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Formularios.Estoque;
using BuffetSharp.WinForms.Infra;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>
/// Declaração dos cadastros atendidos pela tela genérica, do mais simples (nível 1: tabelas de apoio)
/// ao intermediário (nível 2: pessoas, produtos com estoque e relacionamentos).
/// </summary>
internal static class DefinicoesCadastros
{
    // =====================================================================
    // Nível 1 - tabelas de apoio
    // =====================================================================

    public static readonly DefinicaoCadastro<TipoEvento> TiposEvento = new()
    {
        Titulo = "Tipos de evento",
        Singular = "tipo de evento",
        Descricao = "Classificação usada em orçamentos, eventos e relatórios de faturamento.",
        Consulta = q => q.OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 40).Texto("Descrição", x => x.Descricao, 80).SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.Descricao}",
        Nome = x => x.Nome,
        ColunasEditor = 1,
        LarguraEditor = 520,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 80);
            e.Texto("Descrição", () => x.Descricao, v => x.Descricao = v, nameof(x.Descricao), 300, linhas: 3);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<CategoriaProduto> CategoriasProduto = new()
    {
        Titulo = "Categorias de produto",
        Singular = "categoria",
        Descricao = "Agrupamento dos insumos do estoque.",
        Consulta = q => q.OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome).SimNao("Ativo", x => x.Ativo, 20),
        TextoBusca = x => x.Nome,
        Nome = x => x.Nome,
        ColunasEditor = 1,
        LarguraEditor = 460,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 80);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<CategoriaCardapio> CategoriasCardapio = new()
    {
        Titulo = "Seções do cardápio",
        Singular = "seção",
        Descricao = "Seções exibidas na proposta (Entradas, Pratos principais...). A ordem define a sequência de impressão.",
        Consulta = q => q.OrderBy(x => x.Ordem).ThenBy(x => x.Nome),
        Colunas = g => g.Numero("Ordem", x => x.Ordem, peso: 15).Texto("Nome", x => x.Nome).SimNao("Ativo", x => x.Ativo, 20),
        TextoBusca = x => x.Nome,
        Nome = x => x.Nome,
        LarguraEditor = 520,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 80);
            e.Inteiro("Ordem", () => x.Ordem, v => x.Ordem = v, nameof(x.Ordem), 0, 999);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<UnidadeMedida> UnidadesMedida = new()
    {
        Titulo = "Unidades de medida",
        Singular = "unidade de medida",
        Consulta = q => q.OrderBy(x => x.Sigla),
        Colunas = g => g.Texto("Sigla", x => x.Sigla, 20).Texto("Descrição", x => x.Descricao).SimNao("Ativo", x => x.Ativo, 20),
        TextoBusca = x => $"{x.Sigla} {x.Descricao}",
        Nome = x => x.Sigla,
        LarguraEditor = 520,
        Campos = (e, x, _) =>
        {
            e.Texto("Sigla", () => x.Sigla, v => x.Sigla = (v ?? "").ToUpperInvariant(), nameof(x.Sigla), 10);
            e.Texto("Descrição", () => x.Descricao, v => x.Descricao = v ?? "", nameof(x.Descricao), 60);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<FormaPagamento> FormasPagamento = new()
    {
        Titulo = "Formas de pagamento",
        Singular = "forma de pagamento",
        Descricao = "Meios aceitos no recebimento das parcelas e no pagamento de despesas.",
        Consulta = q => q.OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome)
            .Texto("Tipo", x => Textos.Exibir(x.Tipo), 30)
            .Numero("Máx. parcelas", x => x.MaximoParcelas, peso: 25)
            .Numero("Taxa (%)", x => x.TaxaPercentual, "N2", 25)
            .SimNao("Ativo", x => x.Ativo, 20),
        TextoBusca = x => x.Nome,
        Nome = x => x.Nome,
        LarguraEditor = 560,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 60);
            e.Opcoes("Tipo (para onde vai o dinheiro)", () => x.Tipo, v => x.Tipo = v, nameof(x.Tipo));
            e.Inteiro("Máximo de parcelas", () => x.MaximoParcelas, v => x.MaximoParcelas = v, nameof(x.MaximoParcelas), 1, 48);
            e.Decimal("Taxa da operadora (%)", () => x.TaxaPercentual, v => x.TaxaPercentual = v, nameof(x.TaxaPercentual), 2, 0, 100);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<CartaoCredito> CartoesCredito = new()
    {
        Titulo = "Cartões de crédito",
        Singular = "cartão de crédito",
        Descricao = "Cartões da empresa usados para pagar fornecedores. Cada pagamento gera as parcelas da fatura em contas a pagar.",
        Consulta = q => q.Include(x => x.Fornecedor).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Cartão", x => x.ToString(), 40)
            .Numero("Fecha dia", x => x.DiaFechamento, peso: 18)
            .Numero("Vence dia", x => x.DiaVencimento, peso: 18)
            .Texto("Banco emissor", x => x.Fornecedor?.NomeFantasia ?? x.Fornecedor?.RazaoSocial, 35)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.Final}",
        Nome = x => x.Nome,
        LarguraEditor = 600,
        Campos = async (e, x, contexto) =>
        {
            var fornecedores = await contexto.Lookups.ListarAsync(TipoLookup.Fornecedores, somenteAtivos: false);
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 60, dica: "Ex.: Itaú Empresarial");
            e.Texto("Final (4 dígitos)", () => x.Final, v => x.Final = v, nameof(x.Final), 4);
            e.Inteiro("Dia de fechamento da fatura", () => x.DiaFechamento, v => x.DiaFechamento = v, nameof(x.DiaFechamento), 1, 31);
            e.Inteiro("Dia de vencimento da fatura", () => x.DiaVencimento, v => x.DiaVencimento = v, nameof(x.DiaVencimento), 1, 31);
            var banco = e.Lista("Banco emissor (fornecedor das faturas)", fornecedores, () => x.FornecedorId, v => x.FornecedorId = v,
                nameof(x.FornecedorId), permitirVazio: true, span: 2);
            e.BotaoAoLado(banco, "Novo banco", Icones.Adicionar).Click += async (botao, _) =>
            {
                var dono = ((Control)botao!).FindForm()!;
                try
                {
                    if (await NovoBancoAsync(dono, contexto.Navegador) is not { } novo)
                        return;
                    var opcoes = ((IEnumerable<ItemLookup>)banco.DataSource!).Append(novo).ToList();
                    banco.DataSource = opcoes;
                    banco.SelectedIndex = opcoes.Count - 1;
                }
                catch (Exception ex)
                {
                    Mensagens.Erro(dono, ex);
                }
            };
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
        }
    };

    /// <summary>
    /// Cadastra o banco emissor do cartão sem sair da tela. Ele fica como fornecedor, porque as parcelas da fatura são
    /// contas a pagar para ele.
    /// </summary>
    private static async Task<ItemLookup?> NovoBancoAsync(IWin32Window dono, Navegador navegador)
    {
        var banco = new Fornecedor();
        var e = new EditorCampos(2);
        e.Texto("Razão social", () => banco.RazaoSocial, v => banco.RazaoSocial = v ?? "", nameof(banco.RazaoSocial), 150, span: 2,
            dica: "Ex.: Itaú Unibanco S.A.");
        e.Texto("Nome nas listas", () => banco.NomeFantasia, v => banco.NomeFantasia = v, nameof(banco.NomeFantasia), 150,
            dica: "Ex.: Itaú");
        e.Documento("CNPJ", () => banco.Documento, v => banco.Documento = v, nameof(banco.Documento));
        e.Texto("Observações", () => banco.Observacoes, v => banco.Observacoes = v, nameof(banco.Observacoes), 1000, span: 2,
            dica: "Ex.: gerente, agência, central de atendimento");

        var servico = navegador.Servico<ICadastroService<Fornecedor>>();
        using var dialogo = new FormEdicao("Novo banco",
            "O banco fica cadastrado em Fornecedores: as parcelas da fatura do cartão viram contas a pagar para ele.",
            e, async () => banco = await servico.SalvarAsync(banco), 560);
        return dialogo.ShowDialog(dono) == DialogResult.OK ? new ItemLookup { Id = banco.Id, Nome = banco.NomeExibicao } : null;
    }

    public static readonly DefinicaoCadastro<CondicaoPagamento> CondicoesPagamento = new()
    {
        Titulo = "Condições de pagamento",
        Singular = "condição de pagamento",
        Descricao = "Prazos de pagamento: cada parcela com o seu prazo e percentual, ou a regra de entrada + parcelas. Usadas no orçamento, na proposta, no plano de pagamento, no contrato e nas compras.",
        Consulta = q => q.Include(x => x.FormaPagamento).Include(x => x.Parcelas).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 40)
            .Texto("Regra", x => x.Resumo, 60)
            .Texto("Forma sugerida", x => x.FormaPagamento?.Nome, 25)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.Descricao} {x.Resumo}",
        Nome = x => x.Nome,
        LarguraEditor = 680,
        Campos = async (e, x, contexto) =>
            EditorCondicaoPagamento.Montar(e, x, await contexto.Lookups.ListarAsync(TipoLookup.FormasPagamento, somenteAtivos: false))
    };

    public static readonly DefinicaoCadastro<ModeloContrato> ModelosContrato = new()
    {
        Titulo = "Modelos de contrato",
        Singular = "modelo de contrato",
        Descricao = "Textos-base dos contratos. Os campos entre {{ }} são trocados pelos dados do evento na emissão. " +
                    "O modelo do tipo de evento tem prioridade; na falta dele, vale o padrão.",
        Consulta = q => q.Include(x => x.TipoEvento).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 50)
            .Texto("Tipo de evento", x => x.TipoEvento?.Nome ?? "(todos)", 30)
            .SimNao("Padrão", x => x.Padrao, 15)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.TipoEvento?.Nome}",
        Nome = x => x.Nome,
        ColunasEditor = 3,
        LarguraEditor = 980,
        Acoes =
        [
            new("Pré-visualizar", Icones.Pagina, PreVisualizarModeloAsync),
            new("Duplicar", Icones.Copiar, async (modelo, ctx) =>
            {
                var servico = ctx.Navegador.Servico<ICadastroService<ModeloContrato>>();
                var original = await servico.ObterAsync(modelo.Id) ?? throw new RegraNegocioException("O modelo não existe mais.");
                var nomes = (await servico.ListarAsync()).Select(m => m.Nome).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
                var nome = $"{original.Nome} (cópia)";
                for (var i = 2; nomes.Contains(nome); i++)
                    nome = $"{original.Nome} (cópia {i})";
                await servico.SalvarAsync(new ModeloContrato
                {
                    Nome = nome.Length > 100 ? nome[..100] : nome,
                    TipoEventoId = original.TipoEventoId,
                    Conteudo = original.Conteudo,
                    Ativo = true
                });
                await ctx.Recarregar();
            })
        ],
        Campos = async (e, x, contexto) =>
        {
            // Um modelo novo começa com o texto do contrato padrão, para ser adaptado.
            if (x.EhNovo && string.IsNullOrWhiteSpace(x.Conteudo))
                x.Conteudo = GeradorContrato.ModeloPadrao;

            var tipos = await contexto.Lookups.ListarAsync(TipoLookup.TiposEvento, somenteAtivos: false);
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 100, dica: "Ex.: Contrato de casamento");
            e.Lista("Tipo de evento (vazio = todos)", tipos, () => x.TipoEventoId, v => x.TipoEventoId = v, nameof(x.TipoEventoId),
                permitirVazio: true);
            e.Marcador("Modelo padrão", () => x.Padrao, v => x.Padrao = v, nameof(x.Padrao));
            var conteudo = e.Texto("Texto do modelo", () => x.Conteudo, v => x.Conteudo = v ?? "", nameof(x.Conteudo), 100_000,
                span: 3, linhas: 20);
            conteudo.Font = new Font("Consolas", 9.5f);

            e.Secao("Campos disponíveis - dois cliques inserem o campo na posição do cursor");
            var campos = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                MinimumSize = new Size(0, 150),
                MaximumSize = new Size(0, 150),
                Height = 150,
                DataSource = CamposContrato.Todos.Select(c => $"{CamposContrato.Marcador(c.Nome),-24} {c.Descricao}").ToList()
            };
            campos.Font = new Font("Consolas", 9f);
            campos.DoubleClick += (_, _) =>
            {
                if (campos.SelectedIndex < 0 || conteudo.ReadOnly)
                    return;
                conteudo.SelectedText = CamposContrato.Marcador(CamposContrato.Todos[campos.SelectedIndex].Nome);
                conteudo.Focus();
            };
            var painel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            painel.Controls.Add(campos);
            e.Livre(painel);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v, nameof(x.Ativo));
        }
    };

    /// <summary>Mostra o texto que o modelo gera para um evento escolhido (sem emitir nada).</summary>
    private static async Task PreVisualizarModeloAsync(ModeloContrato modelo, ContextoCadastro contexto)
    {
        var lookups = contexto.Navegador.Servico<IConsultasLookup>();
        var eventos = await lookups.ListarAsync(TipoLookup.EventosConfirmados);
        if (eventos.Count == 0)
        {
            Mensagens.Aviso(contexto.Dono, "Não há eventos para a pré-visualização.");
            return;
        }

        int? eventoId = eventos[0].Id;
        var escolha = new EditorCampos(1);
        escolha.Lista("Evento", eventos, () => eventoId, v => eventoId = v);
        using (var dialogo = new FormEdicao("Pré-visualizar modelo", $"Escolha um evento para ver como fica o texto de \"{modelo.Nome}\".",
                   escolha, () => Task.CompletedTask, 620))
        {
            if (dialogo.ShowDialog(contexto.Dono) != DialogResult.OK)
                return;
        }
        if (eventoId is not { } id)
            return;

        var cadastro = await contexto.Navegador.Servico<ICadastroService<ModeloContrato>>().ObterAsync(modelo.Id)
                       ?? throw new RegraNegocioException("O modelo não existe mais.");
        var texto = await contexto.Navegador.Servico<ContratoService>().PreVisualizarAsync(id, cadastro.Conteudo);
        var visualizacao = new EditorCampos(1);
        visualizacao.Texto("Texto gerado", () => texto, _ => { }, "Texto", 100_000, linhas: 26);
        using var resultado = new FormEdicao($"Pré-visualização - {modelo.Nome}", "Somente leitura: nada é gravado.", visualizacao,
            () => Task.CompletedTask, 860, somenteLeitura: true);
        resultado.ShowDialog(contexto.Dono);
    }

    public static readonly DefinicaoCadastro<Cargo> Cargos = new()
    {
        Titulo = "Cargos",
        Singular = "cargo",
        Descricao = "Funções da equipe e valor de diária sugerido na escala dos eventos.",
        Consulta = q => q.OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome).Moeda("Diária padrão", x => x.ValorDiariaPadrao, 30).SimNao("Ativo", x => x.Ativo, 20),
        TextoBusca = x => x.Nome,
        Nome = x => x.Nome,
        LarguraEditor = 560,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 80);
            e.Decimal("Diária padrão (R$)", () => x.ValorDiariaPadrao, v => x.ValorDiariaPadrao = v, nameof(x.ValorDiariaPadrao));
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<Espaco> Espacos = new()
    {
        Titulo = "Espaços",
        Singular = "espaço",
        Descricao = "Salões e ambientes. A capacidade é validada nos orçamentos e a agenda impede reservas sobrepostas.",
        Consulta = q => q.OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 40)
            .Numero("Capacidade", x => x.CapacidadeMaxima, peso: 20)
            .Moeda("Locação", x => x.ValorLocacao, 22)
            .Texto("Descrição", x => x.Descricao, 70)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.Descricao}",
        Nome = x => x.Nome,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 100, span: 2);
            e.Inteiro("Capacidade máxima (pessoas)", () => x.CapacidadeMaxima, v => x.CapacidadeMaxima = v, nameof(x.CapacidadeMaxima), 0, 10_000);
            e.Decimal("Valor de locação (R$)", () => x.ValorLocacao, v => x.ValorLocacao = v, nameof(x.ValorLocacao));
            e.Texto("Descrição", () => x.Descricao, v => x.Descricao = v, nameof(x.Descricao), 500, span: 2, linhas: 3);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            CamposEndereco(e, x.Endereco);
            return Task.CompletedTask;
        },
        Acoes =
        [
            new("Mapa de mesas", Icones.Loja, (espaco, ctx) =>
            {
                using var mapa = new FormMapaEspaco(ctx.Navegador.Servico<MapaMesasService>(), espaco);
                mapa.ShowDialog(ctx.Dono);
                return Task.CompletedTask;
            })
        ]
    };

    public static readonly DefinicaoCadastro<ServicoAdicional> ServicosAdicionais = new()
    {
        Titulo = "Serviços adicionais",
        Singular = "serviço",
        Descricao = "Itens opcionais do orçamento. Serviços \"por pessoa\" são multiplicados pelos convidados.",
        Consulta = q => q.OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 40)
            .Moeda("Preço", x => x.Preco, 20)
            .Texto("Cobrança", x => x.CobradoPorPessoa ? "Por pessoa" : "Valor fixo", 20)
            .Texto("Descrição", x => x.Descricao, 70)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.Descricao}",
        Nome = x => x.Nome,
        Campos = (e, x, _) =>
        {
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 100, span: 2);
            e.Decimal("Preço (R$)", () => x.Preco, v => x.Preco = v, nameof(x.Preco));
            e.Marcador("Cobrado por pessoa", () => x.CobradoPorPessoa, v => x.CobradoPorPessoa = v);
            e.Texto("Descrição", () => x.Descricao, v => x.Descricao = v, nameof(x.Descricao), 500, span: 2, linhas: 3);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    // =====================================================================
    // Nível 2 - cadastros intermediários
    // =====================================================================

    internal static void CamposEndereco(EditorCampos e, Endereco endereco)
    {
        e.Secao("Endereço");
        e.Mascara("CEP", "00000-000", () => endereco.Cep, v => endereco.Cep = v);
        e.Texto("Logradouro", () => endereco.Logradouro, v => endereco.Logradouro = v, maximo: 150, span: 2);
        e.Texto("Número", () => endereco.Numero, v => endereco.Numero = v, maximo: 20);
        e.Texto("Complemento", () => endereco.Complemento, v => endereco.Complemento = v, maximo: 80);
        e.Texto("Bairro", () => endereco.Bairro, v => endereco.Bairro = v, maximo: 80);
        e.Texto("Cidade", () => endereco.Cidade, v => endereco.Cidade = v, maximo: 80);
        e.Texto("UF", () => endereco.Uf, v => endereco.Uf = v?.ToUpperInvariant(), maximo: 2);
    }

    public static readonly DefinicaoCadastro<Cliente> Clientes = new()
    {
        Titulo = "Clientes",
        Singular = "cliente",
        Descricao = "Pessoas físicas e jurídicas. Selecione um cliente e use \"Histórico\" para ver eventos, orçamentos e saldo.",
        Consulta = q => q.Include(x => x.Contatos).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 70)
            .Texto("Tipo", x => x.TipoPessoa == TipoPessoa.Fisica ? "PF" : "PJ", 12)
            .Texto("CPF/CNPJ", x => Documento.Formatar(x.Documento), 32)
            .Texto("Celular", x => x.Celular ?? x.Telefone, 28)
            .Texto("E-mail", x => x.Email, 50)
            .Texto("Cidade", x => x.Endereco.Cidade, 30),
        TextoBusca = x => $"{x.Nome} {x.NomeFantasia} {x.Documento} {x.Email} {x.Celular} {x.Telefone} {x.Endereco.Cidade} " +
                          string.Join(' ', x.Contatos.Select(c => c.Valor)),
        Nome = x => x.Nome,
        ColunasEditor = 3,
        LarguraEditor = 820,
        Acoes =
        [
            new("Histórico", Icones.Historico, (cliente, ctx) =>
            {
                using var dialogo = ctx.Navegador.Criar<FormHistoricoCliente>(cliente.Id, cliente.Nome);
                dialogo.ShowDialog(ctx.Dono);
                return Task.CompletedTask;
            }),
            new("Novo orçamento", Icones.Documento, (cliente, ctx) =>
            {
                using var dialogo = ctx.Navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(0, cliente.Id));
                dialogo.ShowDialog(ctx.Dono);
                return Task.CompletedTask;
            })
        ],
        Campos = (e, x, _) =>
        {
            e.Secao("Dados principais");
            e.Opcoes("Tipo de pessoa", () => x.TipoPessoa, v => x.TipoPessoa = v, nameof(x.TipoPessoa));
            e.Texto("Nome / Razão social", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 150, span: 2);
            e.Documento("CPF / CNPJ", () => x.Documento, v => x.Documento = v, nameof(x.Documento));
            e.Texto("Nome fantasia", () => x.NomeFantasia, v => x.NomeFantasia = v, nameof(x.NomeFantasia), 150);
            e.Data("Nascimento", () => x.DataNascimento, v => x.DataNascimento = v, nameof(x.DataNascimento), opcional: true);
            e.Secao("Contato");
            e.Mascara("Celular", "(00) 00000-0000", () => x.Celular, v => x.Celular = v, nameof(x.Celular));
            e.Mascara("Telefone", "(00) 0000-0000", () => x.Telefone, v => x.Telefone = v, nameof(x.Telefone));
            e.Texto("E-mail", () => x.Email, v => x.Email = v, nameof(x.Email), 150);
            CamposEndereco(e, x.Endereco);
            CamposContatos.Adicionar(e, x);
            e.Secao("Outros");
            e.Texto("Observações", () => x.Observacoes, v => x.Observacoes = v, nameof(x.Observacoes), 1000, span: 3, linhas: 3);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        }
    };

    public static readonly DefinicaoCadastro<IndisponibilidadeFuncionario> Indisponibilidades = new()
    {
        Titulo = "Indisponibilidades da equipe",
        Singular = "indisponibilidade",
        Descricao = "Folgas, férias e atestados: no período, o funcionário não pode ser escalado. Escalas já feitas aparecem como conflito.",
        Consulta = q => q.Include(x => x.Funcionario).OrderByDescending(x => x.Inicio),
        Colunas = g => g.Texto("Funcionário", x => x.Funcionario?.Nome, 40)
            .Texto("Motivo", x => IndisponibilidadeFuncionario.Nome(x.Motivo), 18)
            .Texto("De", x => x.Inicio.ToString("dd/MM/yyyy"), 16)
            .Texto("Até", x => x.Fim.ToString("dd/MM/yyyy"), 16)
            .Numero("Dias", x => x.Fim.DayNumber - x.Inicio.DayNumber + 1, peso: 10)
            .Texto("Observação", x => x.Observacao, 50),
        TextoBusca = x => $"{x.Funcionario?.Nome} {IndisponibilidadeFuncionario.Nome(x.Motivo)} {x.Observacao}",
        Nome = x => $"{x.Funcionario?.Nome} - {x.Descricao}",
        LarguraEditor = 640,
        Campos = async (e, x, contexto) =>
        {
            var funcionarios = await contexto.Lookups.ListarAsync(TipoLookup.Funcionarios);
            e.Lista("Funcionário", funcionarios, () => x.FuncionarioId, v => x.FuncionarioId = v ?? 0, nameof(x.FuncionarioId), span: 2);
            e.Opcoes("Motivo", () => x.Motivo, v => x.Motivo = v, nameof(x.Motivo));
            e.Data("De", () => x.Inicio, v => x.Inicio = v ?? x.Inicio, nameof(x.Inicio));
            e.Data("Até", () => x.Fim, v => x.Fim = v ?? x.Fim, nameof(x.Fim));
            e.Texto("Observação", () => x.Observacao, v => x.Observacao = v, nameof(x.Observacao), 300, span: 2, linhas: 2);
        }
    };

    public static readonly DefinicaoCadastro<Fornecedor> Fornecedores = new()
    {
        Titulo = "Fornecedores",
        Singular = "fornecedor",
        Descricao = "Fornecedores de insumos e serviços, usados no estoque e nas contas a pagar.",
        Consulta = q => q.Include(x => x.Contatos).OrderBy(x => x.RazaoSocial),
        Colunas = g => g.Texto("Nome fantasia", x => x.NomeExibicao, 40)
            .Texto("Razão social", x => x.RazaoSocial, 60)
            .Texto("CNPJ/CPF", x => Documento.Formatar(x.Documento), 32)
            .Texto("Contato", x => x.Contato, 25)
            .Texto("Telefone", x => x.Telefone, 28)
            .Texto("E-mail", x => x.Email, 45),
        TextoBusca = x => $"{x.RazaoSocial} {x.NomeFantasia} {x.Documento} {x.Contato} {x.Email} {x.Telefone} " +
                          string.Join(' ', x.Contatos.Select(c => $"{c.Valor} {c.Observacao}")),
        Nome = x => x.NomeExibicao,
        ColunasEditor = 3,
        LarguraEditor = 820,
        Campos = (e, x, _) =>
        {
            e.Secao("Dados principais");
            e.Texto("Razão social", () => x.RazaoSocial, v => x.RazaoSocial = v ?? "", nameof(x.RazaoSocial), 150, span: 2);
            e.Documento("CNPJ / CPF", () => x.Documento, v => x.Documento = v, nameof(x.Documento));
            e.Texto("Nome fantasia", () => x.NomeFantasia, v => x.NomeFantasia = v, nameof(x.NomeFantasia), 150);
            e.Texto("Contato", () => x.Contato, v => x.Contato = v, nameof(x.Contato), 100);
            e.Mascara("Telefone", "(00) 0000-0000", () => x.Telefone, v => x.Telefone = v, nameof(x.Telefone));
            e.Texto("E-mail", () => x.Email, v => x.Email = v, nameof(x.Email), 150, span: 3);
            CamposEndereco(e, x.Endereco);
            CamposContatos.Adicionar(e, x);
            e.Secao("Outros");
            e.Texto("Observações", () => x.Observacoes, v => x.Observacoes = v, nameof(x.Observacoes), 1000, span: 3, linhas: 3);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
            return Task.CompletedTask;
        },
        Acoes =
        [
            new("Avaliações", Icones.Estrela, (fornecedor, ctx) =>
            {
                using var avaliacoes = new FormAvaliacoesFornecedor(ctx.Navegador.Servico<EventoService>(), fornecedor);
                avaliacoes.ShowDialog(ctx.Dono);
                return Task.CompletedTask;
            })
        ]
    };

    public static readonly DefinicaoCadastro<Funcionario> Funcionarios = new()
    {
        Titulo = "Funcionários",
        Singular = "funcionário",
        Descricao = "Equipe fixa e freelancers escalados nos eventos.",
        Consulta = q => q.Include(x => x.Cargo).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 60)
            .Texto("Cargo", x => x.Cargo?.Nome, 35)
            .Moeda("Diária", x => x.ValorDiaria, 22)
            .Texto("Telefone", x => x.Telefone, 28)
            .Data("Admissão", x => x.DataAdmissao?.ToDateTime(TimeOnly.MinValue), peso: 22)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.Cargo?.Nome} {x.Cpf} {x.Telefone}",
        Nome = x => x.Nome,
        LarguraEditor = 760,
        Campos = async (e, x, contexto) =>
        {
            var cargos = await contexto.Lookups.ListarAsync(TipoLookup.Cargos, somenteAtivos: false);
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 150, span: 2);
            var cargo = e.Lista("Cargo", cargos, () => x.CargoId, v => x.CargoId = v ?? 0, nameof(x.CargoId));
            var diaria = e.Decimal("Valor da diária (R$)", () => x.ValorDiaria, v => x.ValorDiaria = v, nameof(x.ValorDiaria));
            // Sugere a diária padrão do cargo para novos funcionários.
            cargo.SelectionChangeCommitted += (_, _) =>
            {
                if (cargo.SelectedItem is ItemLookup item && diaria.Value == 0)
                    diaria.Value = item.Valor;
            };
            e.Documento("CPF", () => x.Cpf, v => x.Cpf = v, nameof(x.Cpf));
            e.Data("Admissão", () => x.DataAdmissao, v => x.DataAdmissao = v, nameof(x.DataAdmissao), opcional: true);
            e.Mascara("Telefone", "(00) 00000-0000", () => x.Telefone, v => x.Telefone = v, nameof(x.Telefone));
            e.Texto("E-mail", () => x.Email, v => x.Email = v, nameof(x.Email), 150);
            CamposEndereco(e, x.Endereco);
            CamposContatos.Adicionar(e, x);
            e.Secao("Outros");
            e.Texto("Observações", () => x.Observacoes, v => x.Observacoes = v, nameof(x.Observacoes), 1000, span: 2, linhas: 3);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
        }
    };

    public static readonly DefinicaoCadastro<Material> Materiais = new()
    {
        Titulo = "Materiais (louça, enxoval e mobiliário)",
        Singular = "material",
        Descricao = "Patrimônio enviado aos eventos. A reserva verifica o que está livre na data; o que faltar é alugado de terceiros. Perdas na devolução baixam o patrimônio.",
        Consulta = q => q.Include(x => x.Fornecedor).OrderBy(x => x.Categoria).ThenBy(x => x.Nome),
        Colunas = g => g.Texto("Material", x => x.Nome, 50)
            .Texto("Categoria", x => Textos.Exibir(x.Categoria), 22)
            .Numero("Patrimônio", x => x.QuantidadePropria, peso: 18)
            .Moeda("Reposição (un.)", x => x.ValorReposicao, 20)
            .Moeda("Locação (un.)", x => x.CustoLocacao, 20)
            .Moeda("Valor do patrimônio", x => x.ValorPatrimonio, 25)
            .Texto("Locador", x => x.Fornecedor?.NomeExibicao, 30)
            .SimNao("Ativo", x => x.Ativo, 14),
        TextoBusca = x => $"{x.Nome} {Textos.Exibir(x.Categoria)} {x.Fornecedor?.NomeExibicao}",
        Nome = x => x.Nome,
        Rodape = itens => $"{itens.Count} material(is)  •  {itens.Sum(m => m.QuantidadePropria):N0} peças  •  " +
                          $"valor do patrimônio: {Textos.Moeda(itens.Sum(m => m.ValorPatrimonio))}",
        Acoes =
        [
            new("Compra", Icones.Adicionar, (m, ctx) => AjustarPatrimonioAsync(ctx, m, entrada: true)),
            new("Baixa", Icones.Excluir, (m, ctx) => AjustarPatrimonioAsync(ctx, m, entrada: false))
        ],
        Campos = async (e, x, contexto) =>
        {
            var fornecedores = await contexto.Lookups.ListarAsync(TipoLookup.Fornecedores, somenteAtivos: false);
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 100, span: 2);
            e.Opcoes("Categoria", () => x.Categoria, v => x.Categoria = v, nameof(x.Categoria));
            if (x.EhNovo)
                e.Inteiro("Quantidade no patrimônio", () => x.QuantidadePropria, v => x.QuantidadePropria = v, nameof(x.QuantidadePropria), 0, 1_000_000);
            else
                e.Informacao("Patrimônio (via compras, baixas e perdas)", () => x.QuantidadePropria.ToString("N0"));
            e.Decimal("Valor de reposição por unidade (R$)", () => x.ValorReposicao, v => x.ValorReposicao = v, nameof(x.ValorReposicao));
            e.Decimal("Custo de locação por unidade (R$)", () => x.CustoLocacao, v => x.CustoLocacao = v, nameof(x.CustoLocacao));
            e.Lista("Locador padrão (fornecedor)", fornecedores, () => x.FornecedorId, v => x.FornecedorId = v, nameof(x.FornecedorId),
                permitirVazio: true, span: 2);
            e.Texto("Observações", () => x.Observacoes, v => x.Observacoes = v, nameof(x.Observacoes), 500, span: 2, linhas: 2);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);
        }
    };

    private static async Task AjustarPatrimonioAsync(ContextoCadastro contexto, Material material, bool entrada)
    {
        var quantidade = 1;
        var e = new EditorCampos(1);
        e.Informacao("Material", () => $"{material.Nome} - patrimônio atual: {material.QuantidadePropria:N0}");
        e.Inteiro(entrada ? "Quantidade comprada" : "Quantidade baixada (descarte, quebra fora de evento...)", () => quantidade,
            v => quantidade = v, minimo: 1, maximo: 1_000_000);
        using var dialogo = new FormEdicao(entrada ? "Compra de material" : "Baixa de material",
            "A alteração fica registrada na auditoria.", e,
            () => contexto.Navegador.Servico<EstoqueService>().AjustarPatrimonioAsync(material.Id, entrada ? quantidade : -quantidade), 520);
        if (dialogo.ShowDialog(contexto.Dono) == DialogResult.OK)
            await contexto.Recarregar();
    }

    public static readonly DefinicaoCadastro<Produto> Produtos = new()
    {
        Titulo = "Produtos e insumos",
        Singular = "produto",
        Descricao = "Saldo e custo médio são atualizados pelas movimentações (entrada, saída, ajuste e consumo dos eventos).",
        Consulta = q => q.Include(x => x.CategoriaProduto).Include(x => x.UnidadeMedida).Include(x => x.Fornecedor).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Código", x => x.Codigo, 18)
            .Texto("Produto", x => x.Nome, 55)
            .Texto("Categoria", x => x.CategoriaProduto?.Nome, 30)
            .Texto("Un.", x => x.UnidadeMedida?.Sigla, 12)
            .Numero("Custo médio", x => x.CustoMedio, "C4", 25)
            .Numero("Estoque", x => x.EstoqueAtual, "N3", 22, x => x.AbaixoDoMinimo ? Tema.Perigo : null)
            .Numero("Mínimo", x => x.EstoqueMinimo, "N3", 20)
            .Moeda("Valor em estoque", x => x.ValorEmEstoque, 28)
            .Texto("Situação", x => x.AbaixoDoMinimo ? "Repor" : "OK", 15, x => x.AbaixoDoMinimo ? Tema.Perigo : Tema.Sucesso),
        TextoBusca = x => $"{x.Codigo} {x.Nome} {x.CategoriaProduto?.Nome} {x.Fornecedor?.RazaoSocial} {(x.AbaixoDoMinimo ? "repor" : "")}",
        Nome = x => x.Nome,
        ColunasEditor = 3,
        LarguraEditor = 860,
        Rodape = itens => $"{itens.Count} produto(s)  •  {itens.Count(p => p.AbaixoDoMinimo)} abaixo do mínimo  •  " +
                          $"valor em estoque: {Textos.Moeda(itens.Sum(p => p.ValorEmEstoque))}",
        Acoes =
        [
            new("Entrada", Icones.Adicionar, (p, ctx) => FormMovimentarEstoque.AbrirAsync(ctx, p, TipoMovimentacao.Entrada)),
            new("Saída", Icones.Enviar, (p, ctx) => FormMovimentarEstoque.AbrirAsync(ctx, p, TipoMovimentacao.Saida)),
            new("Inventário", Icones.Calculadora, (p, ctx) => FormMovimentarEstoque.AbrirAsync(ctx, p, TipoMovimentacao.Ajuste)),
            new("Movimentações", Icones.Historico, (p, ctx) =>
            {
                ctx.Navegador.Abrir($"movimentacoes:{p.Id}", () => ctx.Navegador.Criar<FormMovimentacoes>(p.Id));
                return Task.CompletedTask;
            })
        ],
        Campos = async (e, x, contexto) =>
        {
            var categorias = await contexto.Lookups.ListarAsync(TipoLookup.CategoriasProduto, somenteAtivos: false);
            var unidades = await contexto.Lookups.ListarAsync(TipoLookup.UnidadesMedida, somenteAtivos: false);
            var fornecedores = await contexto.Lookups.ListarAsync(TipoLookup.Fornecedores, somenteAtivos: false);

            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 120, span: 2);
            e.Texto("Código (SKU)", () => x.Codigo, v => x.Codigo = v, nameof(x.Codigo), 30);
            e.Lista("Categoria", categorias, () => x.CategoriaProdutoId, v => x.CategoriaProdutoId = v ?? 0, nameof(x.CategoriaProdutoId));
            e.Lista("Unidade de medida", unidades, () => x.UnidadeMedidaId, v => x.UnidadeMedidaId = v ?? 0, nameof(x.UnidadeMedidaId));
            e.Lista("Fornecedor padrão", fornecedores, () => x.FornecedorId, v => x.FornecedorId = v, nameof(x.FornecedorId), permitirVazio: true);
            e.Secao("Custos e estoque");
            e.Decimal("Custo médio (R$)", () => x.CustoMedio, v => x.CustoMedio = v, nameof(x.CustoMedio), 4);
            e.Decimal("Estoque mínimo", () => x.EstoqueMinimo, v => x.EstoqueMinimo = v, nameof(x.EstoqueMinimo), 3);
            e.Informacao("Saldo atual (via movimentações)", () => x.EstoqueAtual.ToString("N3"));
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);

            // Conversão de unidade: cada fornecedor pode vender em uma embalagem diferente (caixa com 12, fardo com 6...).
            e.Secao("Embalagens de compra - conversão para a unidade do estoque");
            var opcoesFornecedor = new[] { new OpcaoGrade<int>(0, "(qualquer fornecedor)") }
                .Concat(fornecedores.Select(f => new OpcaoGrade<int>(f.Id, f.Nome)));
            var opcoesUnidade = new[] { new OpcaoGrade<int>(0, "(selecione)") }
                .Concat(unidades.Select(u => new OpcaoGrade<int>(u.Id, u.Info ?? u.Nome)));
            var embalagens = new GradeEditavel<LinhaEmbalagem>(() => new LinhaEmbalagem(), "Nova embalagem", "Remover embalagem")
                .Opcoes("Fornecedor", nameof(LinhaEmbalagem.FornecedorId), opcoesFornecedor, 34)
                .Opcoes("Comprado em", nameof(LinhaEmbalagem.UnidadeMedidaId), opcoesUnidade, 16)
                .Numero("Equivale a (unid. do estoque)", nameof(LinhaEmbalagem.Fator), "0.####", 20)
                .Texto("Descrição", nameof(LinhaEmbalagem.Descricao), 34, 60)
                .Texto("Código no fornecedor", nameof(LinhaEmbalagem.CodigoFornecedor), 18, 30);
            e.Livre(embalagens.Painel);
            e.Associar(nameof(x.Embalagens), embalagens.Controle);
            e.AoCarregar(() => embalagens.Carregar(x.Embalagens.Select(LinhaEmbalagem.De)));
            e.AoAplicar(() =>
            {
                embalagens.ConcluirEdicao();
                x.Embalagens = embalagens.Itens.Select(l => l.ParaEmbalagem()).ToList();
            });
        }
    };

    public static readonly DefinicaoCadastro<ModeloChecklist> ModelosChecklist = new()
    {
        Titulo = "Modelos de checklist",
        Singular = "modelo de checklist",
        Descricao = "Tarefas de preparação aplicadas aos eventos novos, com prazo em dias antes do evento. O modelo do tipo de evento " +
                    "tem prioridade; na falta dele, vale o modelo sem tipo.",
        Consulta = q => q.Include(x => x.TipoEvento).Include(x => x.Itens).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 50)
            .Texto("Tipo de evento", x => x.TipoEvento?.Nome ?? "(todos)", 30)
            .Numero("Tarefas", x => x.Itens.Count, peso: 15)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.TipoEvento?.Nome}",
        Nome = x => x.Nome,
        ColunasEditor = 2,
        LarguraEditor = 820,
        Campos = async (e, x, contexto) =>
        {
            var tipos = await contexto.Lookups.ListarAsync(TipoLookup.TiposEvento, somenteAtivos: false);
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 100);
            e.Lista("Tipo de evento", tipos, () => x.TipoEventoId, v => x.TipoEventoId = v, nameof(x.TipoEventoId), permitirVazio: true);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);

            e.Secao("Tarefas (prazo em dias antes do evento: 0 = no dia, negativo = depois)");
            var tarefas = new GradeEditavel<LinhaChecklist>(() => new LinhaChecklist(), "Nova tarefa", "Remover tarefa", 260)
                .Texto("Tarefa", nameof(LinhaChecklist.Descricao), 60, ModeloChecklistItem.TamanhoDescricao)
                .Numero("Dias antes", nameof(LinhaChecklist.DiasAntes), "0", 14)
                .Texto("Responsável", nameof(LinhaChecklist.Responsavel), 24, 60);
            e.Livre(tarefas.Painel);
            e.Associar(nameof(x.Itens), tarefas.Controle);
            e.AoCarregar(() => tarefas.Carregar(x.Itens.OrderBy(i => i.Ordem).Select(LinhaChecklist.De)));
            e.AoAplicar(() =>
            {
                tarefas.ConcluirEdicao();
                x.Itens = tarefas.Itens.Where(l => !string.IsNullOrWhiteSpace(l.Descricao)).Select((l, i) => l.ParaItem(i + 1)).ToList();
            });
        }
    };

    public static readonly DefinicaoCadastro<ModeloCronograma> ModelosCronograma = new()
    {
        Titulo = "Modelos de cronograma",
        Singular = "modelo de cronograma",
        Descricao = "O minuto a minuto da festa, aplicado aos eventos novos com o horário contado a partir do início do evento. O modelo " +
                    "do tipo de evento tem prioridade; na falta dele, vale o modelo sem tipo.",
        Consulta = q => q.Include(x => x.TipoEvento).Include(x => x.Itens).OrderBy(x => x.Nome),
        Colunas = g => g.Texto("Nome", x => x.Nome, 50)
            .Texto("Tipo de evento", x => x.TipoEvento?.Nome ?? "(todos)", 30)
            .Numero("Etapas", x => x.Itens.Count, peso: 15)
            .SimNao("Ativo", x => x.Ativo, 15),
        TextoBusca = x => $"{x.Nome} {x.TipoEvento?.Nome}",
        Nome = x => x.Nome,
        ColunasEditor = 2,
        LarguraEditor = 900,
        Campos = async (e, x, contexto) =>
        {
            var tipos = await contexto.Lookups.ListarAsync(TipoLookup.TiposEvento, somenteAtivos: false);
            e.Texto("Nome", () => x.Nome, v => x.Nome = v ?? "", nameof(x.Nome), 100);
            e.Lista("Tipo de evento", tipos, () => x.TipoEventoId, v => x.TipoEventoId = v, nameof(x.TipoEventoId), permitirVazio: true);
            e.Marcador("Ativo", () => x.Ativo, v => x.Ativo = v);

            e.Secao("Etapas (início em minutos a partir do começo do evento: 0 = no início, -60 = 1 hora antes, 120 = 2 horas depois)");
            var etapas = new GradeEditavel<LinhaCronograma>(() => new LinhaCronograma(), "Nova etapa", "Remover etapa", 300)
                .Numero("Início (min)", nameof(LinhaCronograma.MinutosDoInicio), "0", 12)
                .Numero("Duração (min)", nameof(LinhaCronograma.DuracaoMinutos), "0", 12)
                .Texto("Atividade", nameof(LinhaCronograma.Atividade), 45, EventoEtapa.TamanhoAtividade)
                .Texto("Local", nameof(LinhaCronograma.Local), 18, EventoEtapa.TamanhoLocal)
                .Texto("Responsável", nameof(LinhaCronograma.Responsavel), 18, EventoEtapa.TamanhoResponsavel);
            e.Livre(etapas.Painel);
            e.Associar(nameof(x.Itens), etapas.Controle);
            e.AoCarregar(() => etapas.Carregar(x.Itens.OrderBy(i => i.MinutosDoInicio).ThenBy(i => i.Ordem).Select(LinhaCronograma.De)));
            e.AoAplicar(() =>
            {
                etapas.ConcluirEdicao();
                x.Itens = etapas.Itens.Where(l => !string.IsNullOrWhiteSpace(l.Atividade))
                    .OrderBy(l => l.MinutosDoInicio).Select((l, i) => l.ParaItem(i + 1)).ToList();
            });
        }
    };
}
