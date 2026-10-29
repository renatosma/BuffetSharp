using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Infrastructure.Persistencia.Seed;

/// <summary>
/// Gera uma base fictícia coerente (~1 ano de operação) usando as próprias regras do domínio:
/// orçamentos enviados/aprovados/recusados, eventos realizados com baixa de estoque, check-in de
/// convidados, parcelas pagas e em atraso, despesas mensais, materiais enviados e devolvidos, degustações,
/// contratos assinados e comissões. Todos os nomes e documentos são fictícios.
/// </summary>
internal sealed class DadosDemonstracao(BuffetDbContext db, TimeProvider relogio, IHasherSenha hasher, BuffetOptions empresa)
{
    private readonly Random _rnd = new(2026);

    // Sorteios do financeiro (formas de recebimento): separados para não mudar a sequência dos demais dados.
    private readonly Random _rndFinanceiro = new(77);
    private ContaFinanceira _caixa = null!;
    private ContaFinanceira _banco = null!;
    private CartaoCredito _cartao = null!;
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    private Dictionary<string, TipoEvento> _tipos = [];
    private Dictionary<string, Produto> _produtos = [];
    private Dictionary<string, ItemCardapio> _itens = [];
    private Dictionary<string, Pacote> _pacotes = [];
    private Dictionary<string, ServicoAdicional> _servicos = [];
    private Dictionary<string, FormaPagamento> _formas = [];
    private Dictionary<string, Fornecedor> _fornecedores = [];
    private List<Espaco> _espacos = [];
    private readonly Dictionary<int, List<IMesaPlanta>> _plantas = [];
    private List<Cliente> _clientesPf = [];
    private List<Cliente> _clientesPj = [];
    private List<Funcionario> _funcionarios = [];
    private List<Usuario> _vendedores = [];
    private Dictionary<string, Material> _materiais = [];
    private readonly List<(DateOnly Data, int MaterialId, int Proprias, EventoMaterial Reserva)> _reservas = [];
    private readonly List<(Orcamento Orcamento, Destino Destino, DateOnly Criacao)> _negociacoes = [];
    private readonly List<Evento> _eventos = [];

    public async Task PopularAsync(CancellationToken ct)
    {
        await CriarCadastrosBasicosAsync(ct);
        await CriarCatalogoAsync(ct);
        await CriarPessoasAsync(ct);
        await CriarVendedoresAsync(ct);
        await CriarMateriaisAsync(ct);

        var plano = PlanejarEventos();
        await CriarEstoqueInicialAsync(plano, ct);
        foreach (var item in plano)
            await CriarOrcamentoEEventoAsync(item, ct);

        await CriarDespesasMensaisAsync(ct);
        await RegistrarComprasRecentesAsync(ct);
        await CriarDegustacoesAsync(ct);
        await FecharComissoesAsync(ct);
        await CriarCompraParceladaAsync(ct);
        await CriarLeadsAsync(ct);
        await CriarChecklistsAsync(ct);
        await PagarEquipeAsync(ct);
        await ContratarFornecedoresAsync(ct);
        await CriarIndisponibilidadesAsync(ct);
        await CriarRegrasPrecoAsync(ct);
        await CriarListaEsperaAsync(ct);
    }

    // =====================================================================
    // Nível 1 e 2 - cadastros
    // =====================================================================

    private async Task CriarCadastrosBasicosAsync(CancellationToken ct)
    {
        _tipos = new[]
        {
            ("Casamento", "Cerimônia e/ou recepção de casamento"),
            ("Aniversário Infantil", "Festas infantis com recreação"),
            ("Aniversário Adulto", "Comemorações de aniversário"),
            ("15 Anos", "Festa de debutante"),
            ("Corporativo", "Confraternizações, lançamentos e convenções"),
            ("Formatura", "Bailes e jantares de formatura"),
            ("Bodas", "Bodas de prata, ouro e afins"),
            ("Batizado", "Batizados e celebrações religiosas")
        }.Select(t => new TipoEvento { Nome = t.Item1, Descricao = t.Item2 }).ToDictionary(t => t.Nome);
        db.TiposEvento.AddRange(_tipos.Values);

        var categorias = new[] { "Carnes", "Aves", "Pescados", "Hortifrúti", "Laticínios", "Mercearia", "Bebidas", "Confeitaria", "Descartáveis" }
            .Select(n => new CategoriaProduto { Nome = n }).ToList();
        db.CategoriasProduto.AddRange(categorias);

        db.UnidadesMedida.AddRange(
            new UnidadeMedida { Sigla = "KG", Descricao = "Quilograma" },
            new UnidadeMedida { Sigla = "L", Descricao = "Litro" },
            new UnidadeMedida { Sigla = "UN", Descricao = "Unidade" },
            new UnidadeMedida { Sigla = "PCT", Descricao = "Pacote" },
            new UnidadeMedida { Sigla = "CX", Descricao = "Caixa" },
            new UnidadeMedida { Sigla = "DZ", Descricao = "Dúzia" });

        db.CategoriasCardapio.AddRange(
            new CategoriaCardapio { Nome = "Recepção e Salgados", Ordem = 1 },
            new CategoriaCardapio { Nome = "Entradas", Ordem = 2 },
            new CategoriaCardapio { Nome = "Pratos Principais", Ordem = 3 },
            new CategoriaCardapio { Nome = "Acompanhamentos", Ordem = 4 },
            new CategoriaCardapio { Nome = "Sobremesas", Ordem = 5 },
            new CategoriaCardapio { Nome = "Bebidas", Ordem = 6 });

        _formas = new[]
        {
            new FormaPagamento { Nome = "PIX", Tipo = TipoFormaPagamento.Pix, MaximoParcelas = 1 },
            new FormaPagamento { Nome = "Dinheiro", Tipo = TipoFormaPagamento.Dinheiro, MaximoParcelas = 1 },
            new FormaPagamento { Nome = "Cartão de Crédito", Tipo = TipoFormaPagamento.CartaoCredito, MaximoParcelas = 12, TaxaPercentual = 3.49m },
            new FormaPagamento { Nome = "Cartão de Débito", Tipo = TipoFormaPagamento.CartaoDebito, MaximoParcelas = 1, TaxaPercentual = 1.29m },
            new FormaPagamento { Nome = "Boleto Bancário", Tipo = TipoFormaPagamento.Boleto, MaximoParcelas = 12, TaxaPercentual = 1.50m },
            new FormaPagamento { Nome = "Transferência Bancária", Tipo = TipoFormaPagamento.Transferencia, MaximoParcelas = 1 }
        }.ToDictionary(f => f.Nome);
        db.FormasPagamento.AddRange(_formas.Values);

        db.Cargos.AddRange(
            new Cargo { Nome = "Garçom", ValorDiariaPadrao = 180 },
            new Cargo { Nome = "Cozinheiro(a)", ValorDiariaPadrao = 260 },
            new Cargo { Nome = "Auxiliar de Cozinha", ValorDiariaPadrao = 150 },
            new Cargo { Nome = "Maître", ValorDiariaPadrao = 380 },
            new Cargo { Nome = "Recepcionista", ValorDiariaPadrao = 170 },
            new Cargo { Nome = "Barman", ValorDiariaPadrao = 230 },
            new Cargo { Nome = "Segurança", ValorDiariaPadrao = 200 },
            new Cargo { Nome = "Copeiro(a)", ValorDiariaPadrao = 140 });

        _espacos =
        [
            new Espaco { Nome = "Salão Cristal", CapacidadeMaxima = 250, ValorLocacao = 9500, Descricao = "Salão principal com pé-direito duplo, lustres de cristal e pista de dança." },
            new Espaco { Nome = "Salão Jardim", CapacidadeMaxima = 160, ValorLocacao = 6500, Descricao = "Ambiente integrado ao jardim com pergolado e área de cerimônia ao ar livre." },
            new Espaco { Nome = "Espaço Kids", CapacidadeMaxima = 90, ValorLocacao = 3800, Descricao = "Brinquedoteca, cama elástica e área para recreação monitorada." },
            new Espaco { Nome = "Lounge Gourmet", CapacidadeMaxima = 60, ValorLocacao = 2800, Descricao = "Espaço intimista com cozinha show para eventos menores." }
        ];
        db.Espacos.AddRange(_espacos);

        _servicos = new[]
        {
            new ServicoAdicional { Nome = "DJ e Iluminação", Preco = 2800, Descricao = "DJ por até 6 horas, som e iluminação cênica." },
            new ServicoAdicional { Nome = "Decoração Completa", Preco = 5500, Descricao = "Projeto de decoração, flores naturais e mobiliário." },
            new ServicoAdicional { Nome = "Fotografia", Preco = 3200, Descricao = "Cobertura fotográfica com álbum digital." },
            new ServicoAdicional { Nome = "Filmagem", Preco = 3900, Descricao = "Filmagem com edição e making of." },
            new ServicoAdicional { Nome = "Bolo Decorado", Preco = 18, CobradoPorPessoa = true, Descricao = "Bolo cenográfico + bolo de corte." },
            new ServicoAdicional { Nome = "Open Bar de Drinks", Preco = 42, CobradoPorPessoa = true, Descricao = "Drinks clássicos e autorais com barman." },
            new ServicoAdicional { Nome = "Mesa de Doces Finos", Preco = 15, CobradoPorPessoa = true, Descricao = "Doces finos em forminhas decoradas." },
            new ServicoAdicional { Nome = "Recreação Infantil", Preco = 1600, Descricao = "Equipe de monitores e atividades por 4 horas." },
            new ServicoAdicional { Nome = "Cabine de Fotos", Preco = 1900, Descricao = "Cabine com impressão instantânea e acessórios." },
            new ServicoAdicional { Nome = "Mestre de Cerimônias", Preco = 1300, Descricao = "Condução de cerimonial e roteiro do evento." }
        }.ToDictionary(s => s.Nome);
        db.ServicosAdicionais.AddRange(_servicos.Values);

        _fornecedores = new[]
        {
            ("Frigorífico Boi Nobre Ltda", "Boi Nobre", "Sérgio"),
            ("Hortifruti Vale Verde Ltda", "Vale Verde", "Regina"),
            ("Distribuidora de Bebidas Gelada Ltda", "Gelada Bebidas", "Marcos"),
            ("Laticínios Serra Azul Ltda", "Serra Azul", "Cláudia"),
            ("Atacadão Central Alimentos Ltda", "Atacadão Central", "Paulo"),
            ("Festa & Cia Descartáveis Ltda", "Festa & Cia", "Denise"),
            ("Loca Festas Locação de Materiais Ltda", "Loca Festas", "Rubens")
        }.Select((f, i) => new Fornecedor
        {
            RazaoSocial = f.Item1,
            NomeFantasia = f.Item2,
            Contato = f.Item3,
            Documento = GerarCnpj(),
            Telefone = $"(11) 3{_rnd.Next(100, 999)}-{_rnd.Next(1000, 9999)}",
            Email = $"vendas{i + 1}@fornecedor.exemplo.com.br",
            Endereco = new Endereco { Cidade = "São Paulo", Uf = "SP", Bairro = "Vila Leopoldina", Logradouro = "Av. das Indústrias", Numero = $"{100 + i * 37}", Cep = "05319-000" }
        }).ToDictionary(f => f.NomeFantasia!);
        db.Fornecedores.AddRange(_fornecedores.Values);

        // Caixa (criado na inicialização do banco) e conta bancária, controlados desde antes da base de demonstração.
        var inicio = Hoje.AddYears(-2);
        _caixa = await db.ContasFinanceiras.FirstOrDefaultAsync(c => c.Tipo == TipoContaFinanceira.Caixa, ct)
                 ?? db.ContasFinanceiras.Add(new ContaFinanceira { Nome = "Caixa", Tipo = TipoContaFinanceira.Caixa }).Entity;
        _caixa.SaldoInicial = 500;
        _caixa.DataSaldoInicial = inicio;
        _banco = new ContaFinanceira
        {
            Nome = "Banco do Brasil - Conta PJ",
            Tipo = TipoContaFinanceira.ContaCorrente,
            Banco = "001 - Banco do Brasil",
            Agencia = "1234-5",
            Numero = "98765-4",
            SaldoInicial = 85_000,
            DataSaldoInicial = inicio
        };
        // O banco emissor do cartão é um fornecedor: as parcelas da fatura são contas a pagar para ele.
        var emissor = new Fornecedor
        {
            RazaoSocial = "Banco do Brasil S.A.",
            NomeFantasia = "Banco do Brasil",
            Contato = "Gerente PJ",
            Observacoes = "Banco emissor do Cartão Empresarial."
        };
        db.Fornecedores.Add(emissor);
        _cartao = new CartaoCredito { Nome = "Cartão Empresarial", Final = "4321", DiaFechamento = 25, DiaVencimento = 5, Fornecedor = emissor };
        db.ContasFinanceiras.Add(_banco);
        db.CartoesCredito.Add(_cartao);

        await db.SaveChangesAsync(ct);

        // Mapa padrão de cada salão: mesas redondas de 10 lugares para a capacidade.
        foreach (var espaco in _espacos)
        {
            var planta = new List<IMesaPlanta>();
            for (var numero = 1; numero <= Math.Min(espaco.CapacidadeMaxima / PlantaSalao.LugaresPadrao, 40); numero++)
            {
                var (x, y) = PlantaSalao.ProximaPosicao(planta);
                planta.Add(new MesaInput { Numero = numero, X = x, Y = y });
            }
            _plantas[espaco.Id] = planta;
            db.EspacoMesas.AddRange(planta.Select(m => new EspacoMesa(espaco.Id, m)));
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Quita o título por uma baixa, como a tela faz: dinheiro entra/sai do caixa, as demais formas da conta bancária e o
    /// cartão de crédito da empresa (nos pagamentos) gera a fatura.
    /// </summary>
    private Baixa Pagar(TituloFinanceiro titulo, DateOnly data, FormaPagamento forma)
    {
        var tipo = titulo is Parcela ? TipoBaixa.Recebimento : TipoBaixa.Pagamento;
        var baixa = new Baixa(tipo, data, Hoje, null, "sistema");
        baixa.AdicionarTitulo(titulo, titulo.Valor, 0, 0);
        var cartao = forma.Tipo == TipoFormaPagamento.CartaoCredito && tipo == TipoBaixa.Pagamento ? _cartao : null;
        var conta = cartao is not null ? null : forma.Tipo == TipoFormaPagamento.Dinheiro ? _caixa : _banco;
        baixa.AdicionarForma(forma, titulo.Valor, conta, cartao);
        baixa.Concluir(titulo.Descricao);
        db.Baixas.Add(baixa);
        return baixa;
    }

    private async Task CriarCatalogoAsync(CancellationToken ct)
    {
        var categorias = await db.CategoriasProduto.ToDictionaryAsync(c => c.Nome, ct);
        var unidades = await db.UnidadesMedida.ToDictionaryAsync(u => u.Sigla, ct);

        (string Nome, string Categoria, string Unidade, decimal Custo, decimal Minimo, string Fornecedor)[] produtos =
        [
            ("Filé mignon", "Carnes", "KG", 89.90m, 20, "Boi Nobre"),
            ("Picanha", "Carnes", "KG", 79.90m, 20, "Boi Nobre"),
            ("Peito de frango", "Aves", "KG", 21.90m, 25, "Boi Nobre"),
            ("Salmão (filé)", "Pescados", "KG", 99.90m, 10, "Atacadão Central"),
            ("Camarão limpo", "Pescados", "KG", 119.90m, 8, "Atacadão Central"),
            ("Arroz agulhinha", "Mercearia", "KG", 6.50m, 30, "Atacadão Central"),
            ("Massa penne", "Mercearia", "KG", 14.00m, 10, "Atacadão Central"),
            ("Farinha de trigo", "Mercearia", "KG", 5.50m, 20, "Atacadão Central"),
            ("Óleo de soja", "Mercearia", "L", 9.50m, 15, "Atacadão Central"),
            ("Molho de tomate", "Mercearia", "L", 10.00m, 15, "Atacadão Central"),
            ("Açúcar refinado", "Mercearia", "KG", 4.80m, 15, "Atacadão Central"),
            ("Massa folhada", "Confeitaria", "KG", 28.00m, 8, "Atacadão Central"),
            ("Chocolate meio amargo", "Confeitaria", "KG", 55.00m, 8, "Atacadão Central"),
            ("Leite condensado", "Confeitaria", "UN", 7.90m, 40, "Atacadão Central"),
            ("Morango", "Hortifrúti", "KG", 24.00m, 8, "Vale Verde"),
            ("Batata", "Hortifrúti", "KG", 5.90m, 25, "Vale Verde"),
            ("Tomate", "Hortifrúti", "KG", 8.90m, 15, "Vale Verde"),
            ("Alface americana", "Hortifrúti", "UN", 3.50m, 30, "Vale Verde"),
            ("Queijo muçarela", "Laticínios", "KG", 42.00m, 10, "Serra Azul"),
            ("Creme de leite", "Laticínios", "L", 18.00m, 15, "Serra Azul"),
            ("Manteiga", "Laticínios", "KG", 48.00m, 5, "Serra Azul"),
            ("Refrigerante 2L", "Bebidas", "UN", 8.50m, 60, "Gelada Bebidas"),
            ("Suco natural", "Bebidas", "L", 12.00m, 40, "Gelada Bebidas"),
            ("Água mineral 500ml", "Bebidas", "UN", 1.80m, 150, "Gelada Bebidas"),
            ("Cerveja long neck", "Bebidas", "UN", 6.90m, 200, "Gelada Bebidas"),
            ("Espumante", "Bebidas", "UN", 45.00m, 24, "Gelada Bebidas"),
            ("Guardanapo de papel", "Descartáveis", "PCT", 6.00m, 30, "Festa & Cia")
        ];

        _produtos = produtos.Select((p, i) => new Produto
        {
            Nome = p.Nome,
            Codigo = $"P{i + 1:D4}",
            CategoriaProdutoId = categorias[p.Categoria].Id,
            UnidadeMedidaId = unidades[p.Unidade].Id,
            FornecedorId = _fornecedores[p.Fornecedor].Id,
            CustoMedio = p.Custo,
            EstoqueMinimo = p.Minimo
        }).ToDictionary(p => p.Nome);

        // Embalagens de compra: cada fornecedor vende numa unidade diferente da usada no estoque.
        void Embalagem(string produto, string? fornecedor, string unidade, decimal fator, string descricao) =>
            _produtos[produto].Embalagens.Add(new ProdutoEmbalagem
            {
                FornecedorId = fornecedor is null ? null : _fornecedores[fornecedor].Id,
                UnidadeMedidaId = unidades[unidade].Id,
                Fator = fator,
                Descricao = descricao
            });
        Embalagem("Refrigerante 2L", "Gelada Bebidas", "PCT", 6, "Fardo com 6 garrafas");
        Embalagem("Água mineral 500ml", "Gelada Bebidas", "PCT", 12, "Pacote com 12 garrafas");
        Embalagem("Cerveja long neck", null, "CX", 24, "Caixa com 24 long necks");
        Embalagem("Leite condensado", "Atacadão Central", "CX", 27, "Caixa com 27 latas");
        Embalagem("Arroz agulhinha", "Atacadão Central", "PCT", 5, "Fardo de 5 kg");
        Embalagem("Guardanapo de papel", "Festa & Cia", "CX", 20, "Caixa com 20 pacotes");
        db.Produtos.AddRange(_produtos.Values);
        await db.SaveChangesAsync(ct);

        var secoes = await db.CategoriasCardapio.ToDictionaryAsync(c => c.Nome, ct);
        (string Nome, string Secao, string Descricao, (string Produto, decimal Qtd)[] Ficha)[] itens =
        [
            ("Coxinha de frango", "Recepção e Salgados", "Massa cremosa com recheio de frango temperado.",
                [("Peito de frango", 0.03m), ("Farinha de trigo", 0.02m), ("Óleo de soja", 0.01m)]),
            ("Mini quiche de queijo", "Recepção e Salgados", "Massa folhada com creme de queijos.",
                [("Massa folhada", 0.02m), ("Queijo muçarela", 0.015m), ("Creme de leite", 0.01m)]),
            ("Bruschetta de tomate", "Recepção e Salgados", "Pão italiano, tomate, manjericão e azeite.",
                [("Tomate", 0.02m), ("Queijo muçarela", 0.01m), ("Óleo de soja", 0.003m)]),
            ("Camarão empanado", "Recepção e Salgados", "Camarão crocante com molho tártaro.",
                [("Camarão limpo", 0.05m), ("Farinha de trigo", 0.01m), ("Óleo de soja", 0.01m)]),
            ("Salada verde", "Entradas", "Mix de folhas, tomate-cereja e molho de mostarda e mel.",
                [("Alface americana", 0.1m), ("Tomate", 0.03m)]),
            ("Salada caprese", "Entradas", "Tomate, muçarela de búfala e pesto.",
                [("Tomate", 0.05m), ("Queijo muçarela", 0.03m), ("Óleo de soja", 0.005m)]),
            ("Filé ao molho madeira", "Pratos Principais", "Medalhão de filé mignon com molho madeira e cogumelos.",
                [("Filé mignon", 0.18m), ("Creme de leite", 0.02m)]),
            ("Picanha na brasa", "Pratos Principais", "Picanha fatiada na brasa com farofa.",
                [("Picanha", 0.2m)]),
            ("Frango à parmegiana", "Pratos Principais", "Filé de frango empanado, molho de tomate e queijo gratinado.",
                [("Peito de frango", 0.18m), ("Queijo muçarela", 0.03m), ("Molho de tomate", 0.04m), ("Farinha de trigo", 0.01m)]),
            ("Salmão grelhado", "Pratos Principais", "Salmão grelhado na manteiga de ervas.",
                [("Salmão (filé)", 0.16m), ("Manteiga", 0.01m)]),
            ("Penne ao sugo", "Pratos Principais", "Massa penne com molho de tomates frescos e manjericão.",
                [("Massa penne", 0.1m), ("Molho de tomate", 0.05m)]),
            ("Arroz branco", "Acompanhamentos", "Arroz soltinho.", [("Arroz agulhinha", 0.08m)]),
            ("Batata gratinada", "Acompanhamentos", "Batatas ao creme com queijo gratinado.",
                [("Batata", 0.12m), ("Creme de leite", 0.03m), ("Queijo muçarela", 0.02m)]),
            ("Mousse de chocolate", "Sobremesas", "Mousse aerado de chocolate meio amargo.",
                [("Chocolate meio amargo", 0.03m), ("Creme de leite", 0.04m), ("Açúcar refinado", 0.01m)]),
            ("Pavê de morango", "Sobremesas", "Camadas de creme, morangos frescos e biscoito.",
                [("Morango", 0.05m), ("Leite condensado", 0.15m), ("Creme de leite", 0.03m)]),
            ("Brigadeiro gourmet", "Sobremesas", "Três brigadeiros gourmet por convidado.",
                [("Chocolate meio amargo", 0.015m), ("Leite condensado", 0.1m), ("Manteiga", 0.003m)]),
            ("Refrigerantes", "Bebidas", "Refrigerantes variados (normal e zero).", [("Refrigerante 2L", 0.35m), ("Guardanapo de papel", 0.02m)]),
            ("Sucos naturais", "Bebidas", "Laranja, uva e abacaxi com hortelã.", [("Suco natural", 0.3m)]),
            ("Água mineral", "Bebidas", "Com e sem gás.", [("Água mineral 500ml", 1m)]),
            ("Cerveja", "Bebidas", "Cerveja long neck gelada.", [("Cerveja long neck", 3m)]),
            ("Espumante para brinde", "Bebidas", "Taça de espumante para o brinde.", [("Espumante", 0.15m)])
        ];

        _itens = [];
        foreach (var (nome, secao, descricao, ficha) in itens)
        {
            var item = new ItemCardapio { Nome = nome, Descricao = descricao, CategoriaCardapioId = secoes[secao].Id };
            item.DefinirFichaTecnica(ficha.Select(f => (_produtos[f.Produto].Id, f.Qtd)));
            _itens[nome] = item;
        }
        db.ItensCardapio.AddRange(_itens.Values);
        await db.SaveChangesAsync(ct);

        (string Nome, decimal Preco, int Minimo, string Descricao, string[] Itens)[] pacotes =
        [
            ("Infantil Alegria", 69.90m, 30, "Cardápio pensado para festas infantis, com salgados, massa e muitos doces.",
                ["Coxinha de frango", "Mini quiche de queijo", "Penne ao sugo", "Frango à parmegiana", "Arroz branco", "Brigadeiro gourmet", "Refrigerantes", "Sucos naturais", "Água mineral"]),
            ("Bronze", 94.90m, 50, "Opção econômica e completa para comemorações.",
                ["Coxinha de frango", "Bruschetta de tomate", "Salada verde", "Frango à parmegiana", "Penne ao sugo", "Arroz branco", "Brigadeiro gourmet", "Refrigerantes", "Água mineral"]),
            ("Corporativo Executivo", 119.90m, 40, "Almoço ou jantar executivo para empresas.",
                ["Mini quiche de queijo", "Bruschetta de tomate", "Salada verde", "Frango à parmegiana", "Penne ao sugo", "Arroz branco", "Mousse de chocolate", "Refrigerantes", "Sucos naturais", "Água mineral"]),
            ("Prata", 139.90m, 60, "Nosso pacote mais vendido: filé ao madeira, entradas e sobremesas.",
                ["Coxinha de frango", "Mini quiche de queijo", "Bruschetta de tomate", "Salada caprese", "Filé ao molho madeira", "Penne ao sugo", "Arroz branco", "Batata gratinada", "Mousse de chocolate", "Brigadeiro gourmet", "Refrigerantes", "Sucos naturais", "Água mineral"]),
            ("Ouro", 199.90m, 80, "Experiência premium com frutos do mar, três pratos principais, cerveja e espumante.",
                ["Mini quiche de queijo", "Camarão empanado", "Bruschetta de tomate", "Salada caprese", "Filé ao molho madeira", "Salmão grelhado", "Picanha na brasa", "Arroz branco", "Batata gratinada", "Pavê de morango", "Mousse de chocolate", "Brigadeiro gourmet", "Refrigerantes", "Sucos naturais", "Água mineral", "Cerveja", "Espumante para brinde"])
        ];

        _pacotes = [];
        foreach (var (nome, preco, minimo, descricao, nomesItens) in pacotes)
        {
            var pacote = new Pacote { Nome = nome, PrecoPorPessoa = preco, MinimoConvidados = minimo, Descricao = descricao };
            if (nome == "Infantil Alegria")
            {
                // Festa infantil: os pequenos são o público, então a isenção e a meia valem para idades menores.
                pacote.IdadeMaximaIsento = 3;
                pacote.IdadeMaximaMeia = 7;
            }
            pacote.DefinirItens(nomesItens.Select(n => _itens[n].Id));
            _pacotes[nome] = pacote;
        }
        db.Pacotes.AddRange(_pacotes.Values);
        await db.SaveChangesAsync(ct);

        // Garante a navegação completa (pacote → itens → ficha técnica) para o cálculo de consumo.
        await db.Pacotes.Include(p => p.Itens).ThenInclude(i => i.ItemCardapio!).ThenInclude(i => i.Ingredientes).LoadAsync(ct);
    }

    private async Task CriarPessoasAsync(CancellationToken ct)
    {
        string[] cidades = ["São Paulo", "Santo André", "São Bernardo do Campo", "Osasco", "Guarulhos", "Barueri"];
        string[] bairros = ["Centro", "Jardim Paulista", "Vila Mariana", "Moema", "Santana", "Tatuapé", "Alphaville"];
        string[] ruas = ["Rua das Flores", "Av. Paulista", "Rua Augusta", "Rua dos Ipês", "Av. Brasil", "Rua Bela Vista", "Alameda Santos"];

        Endereco EnderecoAleatorio() => new()
        {
            Logradouro = Escolher(ruas),
            Numero = _rnd.Next(10, 2500).ToString(),
            Bairro = Escolher(bairros),
            Cidade = Escolher(cidades),
            Uf = "SP",
            Cep = $"0{_rnd.Next(1000, 9999)}-{_rnd.Next(100, 999)}"
        };

        string[] nomesPf =
        [
            "Ana Beatriz Moreira", "Bruno Henrique Castro", "Camila Ferreira Lopes", "Diego Albuquerque Santos",
            "Eduarda Nogueira Prado", "Felipe Augusto Ramos", "Gabriela Martins Teixeira", "Heloísa Cardoso Lima",
            "Igor Vasconcelos Rocha", "Juliana Pereira Campos", "Leonardo Sousa Brandão", "Mariana Ribeiro Duarte",
            "Natália Gonçalves Pinto", "Otávio Mendes Carvalho"
        ];

        _clientesPf = nomesPf.Select(nome => new Cliente
        {
            TipoPessoa = TipoPessoa.Fisica,
            Nome = nome,
            Documento = GerarCpf(),
            Email = $"{RemoverAcentos(nome.Split(' ')[0]).ToLowerInvariant()}.{RemoverAcentos(nome.Split(' ')[^1]).ToLowerInvariant()}@email.exemplo.com.br",
            Celular = $"(11) 9{_rnd.Next(1000, 9999)}-{_rnd.Next(1000, 9999)}",
            DataNascimento = Hoje.AddYears(-_rnd.Next(24, 60)).AddDays(-_rnd.Next(0, 365)),
            Endereco = EnderecoAleatorio()
        }).ToList();

        _clientesPj =
        [
            new Cliente { TipoPessoa = TipoPessoa.Juridica, Nome = "Tech Nova Soluções em Software Ltda", NomeFantasia = "TechNova", Documento = GerarCnpj(), Email = "eventos@technova.exemplo.com.br", Telefone = "(11) 3333-1020", Endereco = EnderecoAleatorio(), Observacoes = "Contato: Renata (RH). Emitir nota fiscal com antecedência." },
            new Cliente { TipoPessoa = TipoPessoa.Juridica, Nome = "Colégio Horizonte Educacional Ltda", NomeFantasia = "Colégio Horizonte", Documento = GerarCnpj(), Email = "formaturas@horizonte.exemplo.com.br", Telefone = "(11) 3444-2030", Endereco = EnderecoAleatorio() },
            new Cliente { TipoPessoa = TipoPessoa.Juridica, Nome = "Construtora Alicerce S.A.", NomeFantasia = "Alicerce", Documento = GerarCnpj(), Email = "marketing@alicerce.exemplo.com.br", Telefone = "(11) 3555-4050", Endereco = EnderecoAleatorio() }
        ];
        // Alguns clientes com mais de um telefone, e-mail e endereço.
        _clientesPf[0].Contatos.Add(new ContatoAdicional { Tipo = TipoContato.WhatsApp, Valor = "(11) 98888-1001", Observacao = "Mãe da noiva" });
        _clientesPf[0].Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Email, Valor = "familia.moreira@email.exemplo.com.br", Observacao = "Família" });
        _clientesPf[1].Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Telefone, Valor = "(11) 3222-1001", Observacao = "Trabalho" });
        _clientesPj[0].Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Email, Valor = "financeiro@technova.exemplo.com.br", Observacao = "Notas fiscais e boletos" });
        _clientesPj[0].Contatos.Add(new ContatoAdicional { Tipo = TipoContato.Celular, Valor = "(11) 97777-2002", Observacao = "Renata (RH)" });
        _clientesPj[0].Enderecos.Add(new EnderecoAdicional
        {
            Identificacao = "Cobrança",
            Endereco = new Endereco { Logradouro = "Av. das Nações Unidas", Numero = "12901", Complemento = "Torre Norte, 10º andar", Bairro = "Brooklin", Cidade = "São Paulo", Uf = "SP", Cep = "04578-000" }
        });
        db.Clientes.AddRange(_clientesPf);
        db.Clientes.AddRange(_clientesPj);

        var cargos = await db.Cargos.ToDictionaryAsync(c => c.Nome, ct);
        (string Nome, string Cargo)[] equipe =
        [
            ("Rafael Lima", "Garçom"), ("Thiago Souza", "Garçom"), ("Patrícia Gomes", "Garçom"), ("Lucas Oliveira", "Garçom"),
            ("Vanessa Alves", "Garçom"), ("Marcelo Tavares", "Garçom"), ("Marta Nascimento", "Cozinheiro(a)"),
            ("Joaquim Pires", "Cozinheiro(a)"), ("Sueli Barbosa", "Auxiliar de Cozinha"), ("André Melo", "Auxiliar de Cozinha"),
            ("Ricardo Fontes", "Maître"), ("Beatriz Antunes", "Recepcionista"), ("Caio Mendes", "Barman"),
            ("Rogério Dias", "Segurança"), ("Luzia Freitas", "Copeiro(a)")
        ];
        _funcionarios = equipe.Select(f => new Funcionario
        {
            Nome = f.Nome,
            Cargo = cargos[f.Cargo],
            CargoId = cargos[f.Cargo].Id,
            ValorDiaria = cargos[f.Cargo].ValorDiariaPadrao,
            Cpf = GerarCpf(),
            Telefone = $"(11) 9{_rnd.Next(1000, 9999)}-{_rnd.Next(1000, 9999)}",
            DataAdmissao = Hoje.AddMonths(-_rnd.Next(6, 60))
        }).ToList();
        db.Funcionarios.AddRange(_funcionarios);

        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Nível 3 - orçamentos, eventos, estoque e financeiro
    // =====================================================================

    private enum Destino { Realizado, Confirmado, Cancelado, Enviado, Recusado, Rascunho }

    private sealed record PlanoEvento(DateOnly Data, TipoEvento Tipo, Cliente Cliente, Espaco Espaco, Pacote Pacote,
        int Convidados, TimeOnly Inicio, TimeOnly Fim, Destino Destino, int Meia = 0, int Isentos = 0)
    {
        public QuantidadesConvidados Quantidades => new(Convidados, Meia, Isentos);
    }

    private List<PlanoEvento> PlanejarEventos()
    {
        var plano = new List<PlanoEvento>();
        var ocupados = new HashSet<(DateOnly, int)>();
        var sabadoAtual = Hoje.AddDays(((int)DayOfWeek.Saturday - (int)Hoje.DayOfWeek + 7) % 7);

        for (var semana = -34; semana <= 22; semana++)
        {
            var sorteio = _rnd.NextDouble();
            var quantidade = sorteio < 0.15 ? 0 : sorteio < 0.65 ? 1 : 2;

            for (var i = 0; i < quantidade; i++)
            {
                var deslocamento = _rnd.NextDouble() switch { < 0.7 => 0, < 0.85 => -1, _ => 1 };
                var data = sabadoAtual.AddDays(semana * 7 + deslocamento);
                var tipo = Escolher(_tipos.Values.ToArray());

                var (minimo, maximo) = tipo.Nome switch
                {
                    "Casamento" => (120, 240),
                    "Aniversário Infantil" => (40, 85),
                    "15 Anos" => (100, 200),
                    "Formatura" => (150, 240),
                    "Corporativo" => (40, 150),
                    "Batizado" => (40, 80),
                    _ => (60, 150)
                };
                var convidados = _rnd.Next(minimo, maximo + 1) / 5 * 5;

                var pacote = tipo.Nome switch
                {
                    "Aniversário Infantil" => _pacotes["Infantil Alegria"],
                    "Corporativo" => _pacotes["Corporativo Executivo"],
                    "Casamento" or "15 Anos" => _rnd.NextDouble() < 0.5 || convidados < 80 ? _pacotes["Prata"] : _pacotes["Ouro"],
                    _ => _rnd.NextDouble() < 0.5 ? _pacotes["Bronze"] : _pacotes["Prata"]
                };

                // Crianças (meia e isentas) em proporção fixa por tipo de festa; os pagantes cumprem o mínimo do pacote.
                var (percentualMeia, percentualIsentos) = tipo.Nome switch
                {
                    "Aniversário Infantil" => (30, 15),
                    "Batizado" => (15, 10),
                    "Casamento" or "15 Anos" => (5, 5),
                    _ => (0, 0)
                };
                convidados = Math.Max(convidados, pacote.MinimoConvidados);
                var meia = convidados * percentualMeia / 100;
                var isentos = convidados * percentualIsentos / 100;
                convidados = Math.Max(convidados, pacote.MinimoConvidados + isentos);

                var espaco = _espacos.Where(e => e.CapacidadeMaxima >= convidados && !ocupados.Contains((data, e.Id)))
                    .OrderBy(e => e.CapacidadeMaxima).FirstOrDefault();
                if (espaco is null)
                    continue;
                ocupados.Add((data, espaco.Id));

                var (inicio, fim) = tipo.Nome switch
                {
                    "Casamento" => (new TimeOnly(19, 0), new TimeOnly(2, 0)),
                    "15 Anos" => (new TimeOnly(20, 0), new TimeOnly(3, 0)),
                    "Formatura" => (new TimeOnly(21, 0), new TimeOnly(4, 0)),
                    "Aniversário Infantil" => (new TimeOnly(15, 0), new TimeOnly(19, 0)),
                    "Batizado" => (new TimeOnly(12, 0), new TimeOnly(17, 0)),
                    "Corporativo" => _rnd.NextDouble() < 0.5 ? (new TimeOnly(12, 0), new TimeOnly(16, 0)) : (new TimeOnly(19, 0), new TimeOnly(23, 30)),
                    _ => (new TimeOnly(19, 30), new TimeOnly(23, 59))
                };

                var cliente = tipo.Nome is "Corporativo" or "Formatura" ? Escolher(_clientesPj.ToArray()) : Escolher(_clientesPf.ToArray());

                var dias = data.DayNumber - Hoje.DayNumber;
                var r = _rnd.NextDouble();
                var destino = dias < 0
                    ? r switch { < 0.88 => Destino.Realizado, < 0.93 => Destino.Cancelado, _ => Destino.Recusado }
                    : dias <= 120
                        ? r switch { < 0.68 => Destino.Confirmado, < 0.88 => Destino.Enviado, < 0.95 => Destino.Recusado, _ => Destino.Cancelado }
                        : r switch { < 0.45 => Destino.Confirmado, < 0.80 => Destino.Enviado, _ => Destino.Rascunho };

                // Evita eventos "de hoje" ainda não realizados para manter a base coerente.
                if (dias == 0)
                    destino = Destino.Confirmado;

                plano.Add(new PlanoEvento(data, tipo, cliente, espaco, pacote, convidados, inicio, fim, destino, meia, isentos));
            }
        }
        return plano;
    }

    private async Task CriarEstoqueInicialAsync(List<PlanoEvento> plano, CancellationToken ct)
    {
        var consumoPrevisto = new Dictionary<int, decimal>();
        foreach (var item in plano.Where(p => p.Destino == Destino.Realizado))
            foreach (var (produtoId, quantidade) in item.Pacote.CalcularConsumo(item.Convidados))
                consumoPrevisto[produtoId] = consumoPrevisto.GetValueOrDefault(produtoId) + quantidade;

        var dataCompra = Agora.AddMonths(-9).Date.AddHours(9);
        foreach (var produto in _produtos.Values)
        {
            // Alguns produtos terminam abaixo do mínimo para alimentar os alertas do painel.
            var fator = (decimal)(_rnd.NextDouble() < 0.2 ? 0.3 + _rnd.NextDouble() * 0.5 : 1.2 + _rnd.NextDouble() * 1.8);
            var saldoFinal = Math.Round(produto.EstoqueMinimo * fator, 0);
            var quantidade = Math.Ceiling(consumoPrevisto.GetValueOrDefault(produto.Id) + saldoFinal);
            var custo = produto.CustoMedio;
            produto.CustoMedio = 0;
            db.MovimentacoesEstoque.Add(produto.RegistrarEntrada(quantidade, custo, dataCompra, "Estoque inicial / compra de abertura"));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task CriarOrcamentoEEventoAsync(PlanoEvento p, CancellationToken ct)
    {
        var dias = p.Data.DayNumber - Hoje.DayNumber;
        var criacao = dias < 0
            ? p.Data.AddDays(-_rnd.Next(45, 150))
            : DateOnly.FromDayNumber(Math.Min(Hoje.AddDays(-_rnd.Next(1, 25)).DayNumber, p.Data.AddDays(-30).DayNumber));
        var validade = DateOnly.FromDayNumber(Math.Min(criacao.AddDays(15).DayNumber, p.Data.DayNumber));
        if (p.Destino == Destino.Enviado && validade < Hoje)
            validade = DateOnly.FromDayNumber(Math.Min(Hoje.AddDays(_rnd.Next(5, 15)).DayNumber, p.Data.DayNumber));

        var orcamento = new Orcamento(p.Cliente.Id, p.Tipo.Id, p.Data, p.Inicio, p.Fim, p.Quantidades, validade)
        {
            CriadoEm = criacao.ToDateTime(new TimeOnly(10, 0))
        };
        orcamento.DefinirPacote(p.Pacote);
        orcamento.DefinirEspaco(p.Espaco);
        orcamento.DefinirServicos(SortearServicos(p));
        orcamento.AplicarDesconto(Escolher([0m, 0m, 3m, 5m, 5m, 8m, 10m]));
        var vendedor = _rnd.NextDouble() < 0.6 ? _vendedores[0] : _vendedores[1];
        orcamento.DefinirVendedor(vendedor.Id);
        db.Orcamentos.Add(orcamento);
        _negociacoes.Add((orcamento, p.Destino, criacao));

        if (p.Destino == Destino.Rascunho)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var envio = criacao.AddDays(1).ToDateTime(new TimeOnly(14, 0));
        var canal = _rnd.NextDouble() < 0.5 || p.Cliente.Email is null ? CanalEnvio.WhatsApp : CanalEnvio.Email;
        orcamento.RegistrarEnvio(canal, canal == CanalEnvio.Email ? p.Cliente.Email! : p.Cliente.Celular ?? p.Cliente.Telefone!,
            vendedor.Login, envio);

        if (p.Destino == Destino.Enviado)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var decisao = DateOnly.FromDayNumber(Math.Min(
            Math.Min(criacao.AddDays(_rnd.Next(2, 12)).DayNumber, validade.DayNumber),
            Math.Min(Hoje.DayNumber, p.Data.AddDays(-1).DayNumber)));
        var momentoDecisao = decisao.ToDateTime(new TimeOnly(16, 0));

        if (p.Destino == Destino.Recusado)
        {
            orcamento.Recusar(Escolher(["Fechou com outro buffet", "Valor acima do orçamento do cliente", "Evento adiado sem data", "Optou por espaço próprio"]), momentoDecisao);
            await db.SaveChangesAsync(ct);
            return;
        }

        var titulo = p.Tipo.Nome switch
        {
            "Corporativo" => $"Confraternização {p.Cliente.NomeFantasia ?? p.Cliente.Nome}",
            "Formatura" => $"Formatura {p.Cliente.NomeFantasia ?? p.Cliente.Nome}",
            "Casamento" => $"Casamento de {PrimeiroNome(p.Cliente)}",
            "Aniversário Infantil" => $"Aniversário infantil - família {UltimoNome(p.Cliente)}",
            "15 Anos" => $"15 anos - família {UltimoNome(p.Cliente)}",
            _ => $"{p.Tipo.Nome} de {PrimeiroNome(p.Cliente)}"
        };

        var evento = orcamento.Aprovar(titulo, momentoDecisao, vendedor.PercentualComissao);
        evento.CriadoEm = momentoDecisao;
        db.Eventos.Add(evento);
        _eventos.Add(evento);

        // Plano de pagamento: 30% de entrada e o restante em parcelas mensais até o evento.
        var forma = Escolher([_formas["PIX"], _formas["Boleto Bancário"], _formas["Cartão de Crédito"], _formas["Transferência Bancária"]]);
        var meses = Math.Max(1, (p.Data.DayNumber - decisao.DayNumber - 30) / 30 + 1);
        var parcelas = Math.Clamp(_rnd.Next(1, meses + 1), 1, 6);
        var entrada = Math.Round(evento.ValorTotal * 0.3m, 2);
        var geradas = evento.GerarParcelas(parcelas, decisao.AddDays(30), entrada, decisao.AddDays(2), forma.Id);

        foreach (var parcela in geradas.Where(x => x.Vencimento <= Hoje))
        {
            var atrasado = _rnd.NextDouble() < 0.08 && p.Destino != Destino.Cancelado;
            if (atrasado)
                continue;
            var pagamento = DateOnly.FromDayNumber(Math.Min(parcela.Vencimento.AddDays(_rnd.Next(-3, 4)).DayNumber, Hoje.DayNumber));
            Pagar(parcela, pagamento, _rndFinanceiro.NextDouble() < 0.1 ? _formas["Dinheiro"] : forma);
        }

        // Equipe escalada para eventos passados e próximos (até 45 dias).
        if (p.Destino == Destino.Realizado || (p.Destino == Destino.Confirmado && dias <= 45))
            EscalarEquipe(evento, p);

        // Materiais (louça, enxoval, mobiliário) para eventos passados e próximos.
        if (p.Destino == Destino.Realizado || (p.Destino == Destino.Confirmado && dias <= 45))
            ReservarMateriais(evento, p);

        // Lista de convidados (amostra), sentados no mapa do salão - precisa de Id para o check-in, por isso salva antes.
        evento.UsarPlanta(_plantas[evento.EspacoId]);
        AdicionarConvidados(evento);
        evento.DistribuirConvidados();
        await db.SaveChangesAsync(ct);

        // Contrato: assinado para eventos realizados recentes e para a maioria dos confirmados.
        if ((p.Destino == Destino.Realizado && dias > -120) || p.Destino == Destino.Confirmado)
            EmitirContrato(evento, p, decisao, vendedor, assinar: p.Destino == Destino.Realizado || _rnd.NextDouble() < 0.6);

        if (p.Destino == Destino.Confirmado)
        {
            DefinirRsvps(evento);
            await db.SaveChangesAsync(ct);
            return;
        }

        if (p.Destino == Destino.Cancelado)
        {
            evento.Cancelar(Escolher(["Cliente desistiu do evento", "Mudança de cidade", "Problemas de saúde na família"]));
            await db.SaveChangesAsync(ct);
            return;
        }

        if (p.Destino == Destino.Realizado)
        {
            foreach (var convidado in evento.Convidados.Where(_ => _rnd.NextDouble() < 0.85))
                evento.RegistrarCheckIn(convidado.Id, evento.Inicio.AddMinutes(_rnd.Next(0, 120)));
            foreach (var escala in evento.Equipe)
                evento.RegistrarPresenca(escala.FuncionarioId, _rnd.NextDouble() < 0.95);

            var encerramento = evento.Fim.AddHours(1);

            // Um em cada quatro recebeu gente além do contratado: os excedentes são cobrados no PIX depois da festa.
            if (evento.Id % 4 == 0)
            {
                var presentes = evento.QuantidadesPorFaixa with
                {
                    Total = Math.Max(evento.PessoasPresentes, evento.QuantidadeConvidados) + 3 + evento.Id % 9
                };
                var vencimento = DateOnly.FromDateTime(encerramento).AddDays(5);
                if (evento.ApurarExcedentes(presentes, evento.ValorPorPessoa, vencimento, _formas["PIX"].Id, encerramento) is { } cobranca &&
                    vencimento <= Hoje)
                    Pagar(cobranca, vencimento, _formas["PIX"]);
            }

            evento.Realizar(encerramento);
            foreach (var (produtoId, quantidade) in p.Pacote.CalcularConsumo(evento.PessoasAtendidas))
            {
                var produto = _produtos.Values.First(x => x.Id == produtoId);
                db.MovimentacoesEstoque.Add(produto.RegistrarSaida(quantidade, encerramento,
                    $"Consumo do evento EVT-{evento.Id:D5}", evento.Id, permitirSaldoNegativo: true));
            }
            evento.MarcarEstoqueBaixado();
            DevolverMateriais(evento, encerramento);

            // Despesas vinculadas ao evento.
            var dataPagamentoEquipe = DateOnly.FromDateTime(encerramento).AddDays(2);
            var equipe = new ContaPagar($"Pagamento da equipe - EVT-{evento.Id:D5}", CategoriaDespesa.Pessoal,
                evento.TotalEquipe, dataPagamentoEquipe, eventoId: evento.Id);
            if (dataPagamentoEquipe <= Hoje)
                Pagar(equipe, dataPagamentoEquipe, _formas["PIX"]);
            db.ContasPagar.Add(equipe);

            var locacao = evento.Materiais.Sum(m => m.CustoLocacao);
            if (locacao > 0)
            {
                var conta = new ContaPagar($"Locação de materiais - EVT-{evento.Id:D5}", CategoriaDespesa.LocacaoMateriais,
                    locacao, DateOnly.FromDateTime(encerramento).AddDays(7), _fornecedores["Loca Festas"].Id, evento.Id);
                if (conta.Vencimento <= Hoje)
                    Pagar(conta, conta.Vencimento, _formas["Boleto Bancário"]);
                db.ContasPagar.Add(conta);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private List<(ServicoAdicional, decimal, decimal)> SortearServicos(PlanoEvento p)
    {
        string[] opcoes = p.Tipo.Nome switch
        {
            "Casamento" => ["Decoração Completa", "DJ e Iluminação", "Fotografia", "Filmagem", "Bolo Decorado", "Open Bar de Drinks", "Mestre de Cerimônias"],
            "15 Anos" => ["Decoração Completa", "DJ e Iluminação", "Fotografia", "Bolo Decorado", "Cabine de Fotos", "Mesa de Doces Finos"],
            "Aniversário Infantil" => ["Recreação Infantil", "Bolo Decorado", "Decoração Completa", "Fotografia"],
            "Formatura" => ["DJ e Iluminação", "Fotografia", "Filmagem", "Open Bar de Drinks", "Mestre de Cerimônias"],
            "Corporativo" => ["DJ e Iluminação", "Fotografia", "Mestre de Cerimônias"],
            _ => ["Decoração Completa", "Bolo Decorado", "Fotografia", "DJ e Iluminação", "Mesa de Doces Finos"]
        };

        return opcoes.Where(_ => _rnd.NextDouble() < 0.55)
            .Select(nome => _servicos[nome])
            .Select(s => (s, s.CobradoPorPessoa ? (decimal)p.Quantidades.Pagantes : 1m, s.Preco))
            .ToList();
    }

    private void EscalarEquipe(Evento evento, PlanoEvento p)
    {
        IEnumerable<Funcionario> DoCargo(string cargo, int quantidade) =>
            _funcionarios.Where(f => f.Cargo!.Nome == cargo).OrderBy(_ => _rnd.Next()).Take(quantidade);

        var escalados = DoCargo("Maître", 1)
            .Concat(DoCargo("Garçom", Math.Clamp(p.Convidados / 30, 2, 6)))
            .Concat(DoCargo("Cozinheiro(a)", p.Convidados > 120 ? 2 : 1))
            .Concat(DoCargo("Auxiliar de Cozinha", p.Convidados > 100 ? 2 : 1))
            .Concat(DoCargo("Recepcionista", 1))
            .Concat(DoCargo("Copeiro(a)", 1));
        if (p.Pacote.Nome == "Ouro" || p.Convidados > 150)
            escalados = escalados.Concat(DoCargo("Barman", 1)).Concat(DoCargo("Segurança", 1));

        foreach (var funcionario in escalados)
            evento.EscalarFuncionario(funcionario, funcionario.Cargo!.Nome);
    }

    private static readonly string[] PrimeirosNomes =
    [
        "Alice", "Arthur", "Bernardo", "Cecília", "Davi", "Elisa", "Enzo", "Fernanda", "Gustavo", "Helena", "Isabela",
        "João", "Laura", "Lorenzo", "Manuela", "Miguel", "Nicolas", "Olívia", "Pedro", "Rafaela", "Samuel", "Sofia",
        "Theo", "Valentina", "Vitor", "Yasmin", "Lucas", "Clara", "Mateus", "Lívia"
    ];

    private static readonly string[] Sobrenomes =
    [
        "Silva", "Santos", "Oliveira", "Souza", "Rodrigues", "Ferreira", "Alves", "Pereira", "Lima", "Gomes", "Costa",
        "Ribeiro", "Martins", "Carvalho", "Almeida", "Lopes", "Soares", "Fernandes", "Vieira", "Barbosa"
    ];

    private void AdicionarConvidados(Evento evento)
    {
        var quantidade = _rnd.Next(12, 28);
        var nomes = new HashSet<string>();
        while (nomes.Count < quantidade)
            nomes.Add($"{Escolher(PrimeirosNomes)} {Escolher(Sobrenomes)}");

        foreach (var nome in nomes)
            evento.AdicionarConvidado(nome, $"(11) 9{_rnd.Next(1000, 9999)}-{_rnd.Next(1000, 9999)}", acompanhantes: _rnd.Next(0, 3));
    }

    /// <summary>Respostas de presença (RSVP) para eventos futuros. Requer os convidados já salvos (com Id).</summary>
    private void DefinirRsvps(Evento evento)
    {
        foreach (var convidado in evento.Convidados.Where(_ => _rnd.NextDouble() < 0.6))
            evento.DefinirRsvp(convidado.Id, _rnd.NextDouble() < 0.85 ? StatusRsvp.Confirmado : StatusRsvp.Recusado);
    }

    private async Task CriarDespesasMensaisAsync(CancellationToken ct)
    {
        var inicioMesAtual = new DateOnly(Hoje.Year, Hoje.Month, 1);
        for (var m = -9; m <= 2; m++)
        {
            var mes = inicioMesAtual.AddMonths(m);
            (string Descricao, CategoriaDespesa Categoria, decimal Valor, int Dia, string? Fornecedor)[] despesas =
            [
                ("Energia elétrica", CategoriaDespesa.Servicos, 3400 + _rnd.Next(0, 1200), 10, null),
                ("Água e esgoto", CategoriaDespesa.Servicos, 750 + _rnd.Next(0, 400), 12, null),
                ("Internet e telefonia", CategoriaDespesa.Servicos, 389.90m, 15, null),
                ("Aluguel do depósito de apoio", CategoriaDespesa.Aluguel, 4800, 5, null),
                ("Marketing digital (anúncios)", CategoriaDespesa.Marketing, 1200 + _rnd.Next(0, 800), 20, null),
                ("Simples Nacional", CategoriaDespesa.Impostos, 6500 + _rnd.Next(0, 4000), 20, null),
                ("Compra de mercearia", CategoriaDespesa.Insumos, 3500 + _rnd.Next(0, 3000), 8, "Atacadão Central"),
                ("Compra de carnes", CategoriaDespesa.Insumos, 9000 + _rnd.Next(0, 6000), 8, "Boi Nobre"),
                ("Compra de bebidas", CategoriaDespesa.Insumos, 4000 + _rnd.Next(0, 3000), 9, "Gelada Bebidas")
            ];

            foreach (var d in despesas)
            {
                var vencimento = mes.AddDays(d.Dia - 1);
                var conta = new ContaPagar($"{d.Descricao} - {mes:MM/yyyy}", d.Categoria, d.Valor, vencimento,
                    d.Fornecedor is null ? null : _fornecedores[d.Fornecedor].Id);
                if (vencimento < Hoje && _rnd.NextDouble() < 0.95)
                {
                    // Anúncios são pagos com o cartão da empresa: a fatura vira conta a pagar (paga quando já venceu).
                    if (d.Categoria != CategoriaDespesa.Marketing)
                        Pagar(conta, vencimento, _formas["Boleto Bancário"]);
                    else
                    {
                        foreach (var fatura in Pagar(conta, vencimento, _formas["Cartão de Crédito"]).Formas.SelectMany(f => f.Faturas))
                        {
                            if (fatura.Vencimento < Hoje)
                                Pagar(fatura, fatura.Vencimento, _formas["Transferência Bancária"]);
                        }
                    }
                }
                db.ContasPagar.Add(conta);
            }

            if (_rnd.NextDouble() < 0.35)
                db.ContasPagar.Add(new ContaPagar($"Manutenção de equipamentos de cozinha - {mes:MM/yyyy}",
                    CategoriaDespesa.Manutencao, 600 + _rnd.Next(0, 1800), mes.AddDays(24)));
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Equipamento comprado em entrada + 5x: as parcelas já vencidas estão pagas.</summary>
    private async Task CriarCompraParceladaAsync(CancellationToken ct)
    {
        var compra = new ParcelamentoPagar("Forno combinado 10 GN", CategoriaDespesa.Manutencao, 18_900m, documento: "NF 18.452");
        compra.GerarParcelas(5, Hoje.AddMonths(-1), 3_900m, Hoje.AddMonths(-2));
        foreach (var parcela in compra.Parcelas.Where(p => p.Vencimento < Hoje).ToList())
            Pagar(parcela, parcela.Vencimento, _formas["Boleto Bancário"]);
        db.ParcelamentosPagar.Add(compra);
        await db.SaveChangesAsync(ct);
    }

    private async Task RegistrarComprasRecentesAsync(CancellationToken ct)
    {
        // Compras recentes com reajuste de preço: demonstram o recálculo do custo médio ponderado.
        foreach (var produto in _produtos.Values.Where(p => p.EstoqueAtual < p.EstoqueMinimo * 2).Take(6))
        {
            var quantidade = Math.Ceiling(produto.EstoqueMinimo * 1.5m);
            var custo = Math.Round(produto.CustoMedio * 1.06m, 2);
            db.MovimentacoesEstoque.Add(produto.RegistrarEntrada(quantidade, custo, Agora.AddDays(-_rnd.Next(2, 20)),
                "Reposição de estoque (preço reajustado)"));
        }
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Comercial e operação: vendedores, materiais, contratos, degustações e comissões
    // =====================================================================

    private async Task CriarVendedoresAsync(CancellationToken ct)
    {
        var carla = await db.Usuarios.FirstAsync(u => u.Login == "comercial", ct);
        carla.DefinirComissao(3m);
        var vitor = await db.Usuarios.FirstOrDefaultAsync(u => u.Login == "vendas", ct);
        if (vitor is null)
        {
            vitor = new Usuario("Vítor Vendas", "vendas", PerfilUsuario.Comercial, hasher.Gerar("Vendas@123"));
            db.Usuarios.Add(vitor);
        }
        vitor.DefinirComissao(2.5m);
        await db.SaveChangesAsync(ct);
        _vendedores = [carla, vitor];
    }

    private async Task CriarMateriaisAsync(CancellationToken ct)
    {
        var locador = _fornecedores["Loca Festas"].Id;
        (string Nome, CategoriaMaterial Categoria, int Quantidade, decimal Reposicao, decimal Locacao)[] materiais =
        [
            ("Prato raso de porcelana", CategoriaMaterial.Louca, 260, 28, 2.50m),
            ("Prato de sobremesa", CategoriaMaterial.Louca, 260, 18, 1.80m),
            ("Jogo de talheres (garfo e faca)", CategoriaMaterial.Talheres, 260, 22, 2.00m),
            ("Taça de cristal", CategoriaMaterial.CoposETacas, 220, 35, 3.00m),
            ("Copo long drink", CategoriaMaterial.CoposETacas, 300, 12, 1.20m),
            ("Guardanapo de tecido", CategoriaMaterial.Enxoval, 300, 9, 1.00m),
            ("Toalha de mesa redonda", CategoriaMaterial.Enxoval, 32, 90, 15m),
            ("Mesa redonda (8 lugares)", CategoriaMaterial.Mobiliario, 28, 650, 45m),
            ("Cadeira Tiffany", CategoriaMaterial.Mobiliario, 180, 180, 12m),
            ("Sousplat dourado", CategoriaMaterial.Decoracao, 150, 25, 2.50m)
        ];
        _materiais = materiais.Select(m => new Material
        {
            Nome = m.Nome,
            Categoria = m.Categoria,
            QuantidadePropria = m.Quantidade,
            ValorReposicao = m.Reposicao,
            CustoLocacao = m.Locacao,
            FornecedorId = locador
        }).ToDictionary(m => m.Nome);
        db.Materiais.AddRange(_materiais.Values);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reserva louça, enxoval e mobiliário pelo número de convidados; o que faltar no patrimônio é alugado.</summary>
    private void ReservarMateriais(Evento evento, PlanoEvento p)
    {
        var mesas = (int)Math.Ceiling(p.Convidados / 8m);
        var itens = new List<(string Nome, int Quantidade)>
        {
            ("Prato raso de porcelana", p.Convidados),
            ("Prato de sobremesa", p.Convidados),
            ("Jogo de talheres (garfo e faca)", p.Convidados),
            ("Guardanapo de tecido", p.Convidados),
            (p.Tipo.Nome == "Aniversário Infantil" ? "Copo long drink" : "Taça de cristal", p.Convidados),
            ("Toalha de mesa redonda", mesas),
            ("Mesa redonda (8 lugares)", mesas),
            ("Cadeira Tiffany", p.Convidados)
        };
        if (p.Tipo.Nome is "Casamento" or "15 Anos")
            itens.Add(("Sousplat dourado", p.Convidados));

        foreach (var (nome, quantidade) in itens)
        {
            var material = _materiais[nome];
            var reservadas = _reservas.Where(r => r.MaterialId == material.Id && !r.Reserva.Devolvido &&
                                                  Math.Abs(r.Data.DayNumber - p.Data.DayNumber) <= 1).Sum(r => r.Proprias);
            var alugadas = Math.Max(0, quantidade - Math.Max(0, material.QuantidadePropria - reservadas));
            var reserva = evento.ReservarMaterial(material, quantidade, alugadas);
            _reservas.Add((p.Data, material.Id, quantidade - alugadas, reserva));
        }
    }

    /// <summary>Devolução no dia seguinte, com algumas quebras de louça e taças.</summary>
    private void DevolverMateriais(Evento evento, DateTime encerramento)
    {
        foreach (var item in evento.Materiais.ToList())
        {
            var fragil = item.Material!.Categoria is CategoriaMaterial.Louca or CategoriaMaterial.CoposETacas;
            var perdidas = fragil ? _rnd.Next(0, item.Quantidade / 60 + 2) : _rnd.NextDouble() < 0.08 ? 1 : 0;
            perdidas = Math.Min(perdidas, item.Quantidade);
            var proprias = evento.RegistrarDevolucao(item.MaterialId, item.Quantidade - perdidas, perdidas, encerramento.AddHours(10));
            if (proprias > 0)
                item.Material.AjustarPatrimonio(-proprias);
        }
    }

    /// <summary>Emite o contrato (texto do modelo oficial) e, opcionalmente, registra as assinaturas.</summary>
    private void EmitirContrato(Evento evento, PlanoEvento p, DateOnly decisao, Usuario vendedor, bool assinar)
    {
        var composicao = p.Pacote.Itens.Select(i => i.ItemCardapio!)
            .OrderBy(i => i.CategoriaCardapio!.Ordem).ThenBy(i => i.Nome)
            .Select(i => new ItemComposicao { Categoria = i.CategoriaCardapio!.Nome, Item = i.Nome, Descricao = i.Descricao })
            .ToList();

        var emissao = Min(decisao.AddDays(1).ToDateTime(new TimeOnly(11, 0)), Agora.AddHours(-2));
        var contrato = new Contrato(evento.Id, GeradorContrato.Gerar(empresa, evento, composicao, DateOnly.FromDateTime(emissao)), emissao)
        {
            CriadoEm = emissao
        };

        if (assinar)
        {
            var cliente = p.Cliente;
            var signatario = cliente.TipoPessoa == TipoPessoa.Fisica ? cliente.Nome : $"Representante de {cliente.NomeFantasia ?? cliente.Nome}";
            var assinatura = Min(emissao.AddDays(_rnd.Next(1, 6)).Date.AddHours(15), Agora.AddHours(-1));
            contrato.Assinar(signatario, cliente.Documento!, AssinaturaSintetica.Gerar(evento.Id), vendedor.Nome,
                AssinaturaSintetica.Gerar(10_000 + vendedor.Id), assinatura);
        }
        db.Contratos.Add(contrato);

        static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }

    private async Task CriarDegustacoesAsync(CancellationToken ct)
    {
        int[] horarios = [10, 11, 14, 15, 16, 17, 18, 19];
        string[] comentarios =
        [
            "Adoraram o risoto e as sobremesas.", "Pediram para trocar a entrada por uma opção vegetariana.",
            "Gostaram muito do prato principal.", "Acharam o valor alto, mas elogiaram o cardápio.",
            "Querem incluir a mesa de doces finos.", "Elogiaram o atendimento e a apresentação dos pratos."
        ];

        DateTime Horario(DateOnly dia)
        {
            while (dia.DayOfWeek == DayOfWeek.Sunday)
                dia = dia.AddDays(1);
            return dia.ToDateTime(new TimeOnly(Escolher(horarios), 0));
        }

        var atendidos = new HashSet<int>();
        foreach (var (orcamento, destino, criacao) in _negociacoes.Where(n => n.Destino != Destino.Rascunho && n.Criacao >= Hoje.AddMonths(-8)))
        {
            if (_rnd.NextDouble() > 0.4)
                continue;
            var quando = Horario(criacao.AddDays(_rnd.Next(3, 11)));
            if (quando >= orcamento.Inicio.AddDays(-7) || (quando > Agora && destino != Destino.Enviado))
                continue;

            var degustacao = new Degustacao(orcamento.ClienteId, quando, _rnd.Next(2, 6), orcamento.PacoteId, orcamento.Id, null,
                quando.AddDays(-5));
            if (quando < Agora)
            {
                if (_rnd.NextDouble() < 0.1)
                    degustacao.RegistrarAusencia(quando.AddHours(2));
                else
                    degustacao.Realizar(destino == Destino.Recusado ? _rnd.Next(2, 5) : _rnd.Next(4, 6), Escolher(comentarios), quando.AddHours(1));
            }
            db.Degustacoes.Add(degustacao);
            atendidos.Add(orcamento.Id);
        }

        // Agenda das próximas duas semanas para propostas ainda em negociação.
        foreach (var (orcamento, _, _) in _negociacoes.Where(n => n.Destino == Destino.Enviado && !atendidos.Contains(n.Orcamento.Id) &&
                                                                  n.Orcamento.Inicio > Agora.AddDays(30)).Take(6))
        {
            var quando = Horario(Hoje.AddDays(_rnd.Next(1, 15)));
            db.Degustacoes.Add(new Degustacao(orcamento.ClienteId, quando, _rnd.Next(2, 6), orcamento.PacoteId, orcamento.Id,
                "Confirmar presença por WhatsApp na véspera.", Agora));
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Comissões de dois meses atrás já fechadas e pagas; o mês passado fica em aberto para fechamento.</summary>
    private async Task FecharComissoesAsync(CancellationToken ct)
    {
        var de = new DateOnly(Hoje.Year, Hoje.Month, 1).AddMonths(-2);
        var ate = de.AddMonths(1).AddDays(-1);
        foreach (var vendedor in _vendedores)
        {
            var recebidos = _eventos.Where(e => e.VendedorId == vendedor.Id)
                .SelectMany(e => e.Parcelas
                    .Where(p => p.Status == StatusTitulo.Pago && p.DataPagamento >= de && p.DataPagamento <= ate)
                    .Select(p => (Valor: p.ValorPago!.Value, Percentual: e.PercentualComissao)))
                .ToList();
            var comissao = recebidos.Sum(r => Math.Round(r.Valor * r.Percentual / 100, 2, MidpointRounding.AwayFromZero));
            if (comissao <= 0)
                continue;

            var fechamento = new FechamentoComissao(vendedor, de, ate, recebidos.Sum(r => r.Valor), comissao, ate.AddDays(5))
            {
                CriadoEm = ate.AddDays(1).ToDateTime(new TimeOnly(9, 0))
            };
            Pagar(fechamento.ContaPagar!, ate.AddDays(5), _formas["PIX"]);
            db.FechamentosComissao.Add(fechamento);
        }
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Funil comercial (sorteios próprios: não mudam a sequência dos demais dados)
    // =====================================================================

    private async Task CriarLeadsAsync(CancellationToken ct)
    {
        var rnd = new Random(55);
        T Sortear<T>(T[] opcoes) => opcoes[rnd.Next(opcoes.Length)];
        OrigemLead[] origens =
        [
            OrigemLead.Instagram, OrigemLead.Instagram, OrigemLead.Instagram, OrigemLead.Indicacao, OrigemLead.Indicacao,
            OrigemLead.Google, OrigemLead.Site, OrigemLead.WhatsApp, OrigemLead.Feira, OrigemLead.Facebook, OrigemLead.Visita
        ];
        string[] conversas =
        [
            "Pediu fotos dos salões e o cardápio.", "Quer conhecer o espaço no sábado.", "Comparando com outros dois buffets.",
            "Enviei as opções de pacote por WhatsApp.", "Perguntou sobre opções vegetarianas.", "Vai confirmar a data com a família."
        ];
        string[] motivos =
        [
            "Preço acima do orçamento do cliente.", "Data indisponível.", "Fechou com outro buffet.", "Desistiu do evento.",
            "Não respondeu mais."
        ];
        var usuario = _vendedores[0];

        // Clientes que fecharam negócio: boa parte chegou como lead alguns dias antes do primeiro orçamento.
        foreach (var (orcamento, _, criacao) in _negociacoes.Where(n => n.Criacao >= Hoje.AddMonths(-10))
                     .GroupBy(n => n.Orcamento.ClienteId).Select(g => g.OrderBy(n => n.Criacao).First()))
        {
            if (rnd.NextDouble() > 0.55)
                continue;
            var cliente = _clientesPf.Concat(_clientesPj).First(c => c.Id == orcamento.ClienteId);
            var contato = criacao.ToDateTime(new TimeOnly(10, 0)).AddDays(-rnd.Next(3, 15));
            var lead = new Lead(cliente.Nome, cliente.Celular ?? cliente.Telefone, cliente.Email, Sortear(origens)) { CriadoEm = contato };
            lead.Alterar(lead.Nome, lead.Telefone, lead.Email, lead.Origem, lead.Origem == OrigemLead.Indicacao ? "Indicação de cliente antigo" : null,
                orcamento.TipoEventoId, orcamento.DataEvento, orcamento.QuantidadeConvidados, usuario.Id, null);
            lead.RegistrarContato(Sortear(conversas), usuario.Login, contato.AddHours(2), null);
            lead.Converter(cliente, usuario.Login, criacao.ToDateTime(new TimeOnly(9, 30)));
            db.Leads.Add(lead);
        }

        string NovoNome() => $"{Sortear(PrimeirosNomes)} {Sortear(Sobrenomes)}";
        string Celular() => $"(11) 9{rnd.Next(1000, 9999)}-{rnd.Next(1000, 9999)}";
        var tipos = _tipos.Values.ToArray();

        // Perdidos ao longo do ano.
        for (var i = 0; i < 14; i++)
        {
            var contato = Agora.AddDays(-rnd.Next(20, 330));
            var lead = new Lead(NovoNome(), Celular(), null, Sortear(origens)) { CriadoEm = contato };
            lead.Alterar(lead.Nome, lead.Telefone, null, lead.Origem, null, Sortear(tipos).Id,
                DateOnly.FromDateTime(contato.AddDays(rnd.Next(60, 240))), rnd.Next(5, 25) * 10, usuario.Id, null);
            lead.RegistrarContato(Sortear(conversas), usuario.Login, contato.AddHours(3), null);
            lead.MarcarPerdido(Sortear(motivos), usuario.Login, contato.AddDays(rnd.Next(3, 15)));
            db.Leads.Add(lead);
        }

        // Em negociação: novos (sem contato ainda) e em contato com retorno atrasado, hoje ou nos próximos dias.
        for (var i = 0; i < 10; i++)
        {
            var contato = Agora.AddDays(-rnd.Next(0, 20)).AddHours(-rnd.Next(1, 6));
            var lead = new Lead(NovoNome(), Celular(), null, Sortear(origens)) { CriadoEm = contato };
            lead.Alterar(lead.Nome, lead.Telefone, null, lead.Origem, null, Sortear(tipos).Id,
                DateOnly.FromDateTime(Agora.AddDays(rnd.Next(45, 300))), rnd.Next(5, 25) * 10, usuario.Id,
                "Chegou pelo formulário do site.");
            if (i >= 3)
            {
                var retorno = DateOnly.FromDateTime(Agora).AddDays(rnd.Next(-3, 6));
                lead.RegistrarContato(Sortear(conversas), usuario.Login, contato.AddHours(1),
                    retorno < DateOnly.FromDateTime(contato.AddHours(1)) ? null : retorno);
            }
            db.Leads.Add(lead);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Checklist e cronograma padrão nos eventos confirmados: as tarefas com prazo vencido ficam quase todas concluídas,
    /// algumas atrasadas para aparecerem nos lembretes.
    /// </summary>
    private async Task CriarChecklistsAsync(CancellationToken ct)
    {
        var rnd = new Random(91);
        var modelo = await db.ModelosChecklist.Include(m => m.Itens).FirstAsync(ct);
        var cronograma = await db.ModelosCronograma.Include(m => m.Itens).FirstAsync(ct);
        foreach (var evento in _eventos.Where(e => e.Status == StatusEvento.Confirmado))
        {
            evento.AplicarChecklist(modelo);
            evento.AplicarCronograma(cronograma);
            await db.SaveChangesAsync(ct);
            foreach (var tarefa in evento.Tarefas.Where(t => t.Prazo < Hoje && rnd.NextDouble() < 0.85))
                evento.ConcluirTarefa(tarefa.Id, _vendedores[rnd.Next(_vendedores.Count)].Login, tarefa.Prazo.ToDateTime(new TimeOnly(17, 0)));
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fornecedores de serviços (DJ, decoração, fotografia e bolo) contratados para os eventos confirmados dos próximos dois
    /// meses, com a conta a pagar no dia da festa e a chegada no cronograma; metade já confirmou.
    /// </summary>
    private async Task ContratarFornecedoresAsync(CancellationToken ct)
    {
        (string Razao, string Fantasia, string Contato, string Telefone, string Servico, int MinutosAntes)[] dados =
        [
            ("Som & Luz Eventos Ltda", "Som & Luz", "Diego", "(11) 98201-4410", "DJ e Iluminação", 120),
            ("Flor de Lis Decorações Ltda", "Flor de Lis", "Patrícia", "(11) 98322-7351", "Decoração Completa", 240),
            ("Clique Perfeito Fotografia Ltda", "Clique Perfeito", "Renan", "(11) 98433-1092", "Fotografia", 60),
            ("Doce Arte Confeitaria Ltda", "Doce Arte", "Juliana", "(11) 98544-6620", "Bolo Decorado", 180)
        ];
        var porServico = new Dictionary<string, (Fornecedor Fornecedor, int MinutosAntes)>();
        foreach (var d in dados)
        {
            var fornecedor = new Fornecedor
            {
                RazaoSocial = d.Razao, NomeFantasia = d.Fantasia, Contato = d.Contato, Telefone = d.Telefone,
                Email = $"contato@{d.Fantasia.Split(' ')[0].ToLowerInvariant()}.exemplo.com.br",
                Observacoes = $"Fornecedor de {d.Servico.ToLowerInvariant()} para os eventos."
            };
            db.Fornecedores.Add(fornecedor);
            porServico[d.Servico] = (fornecedor, d.MinutosAntes);
        }
        await db.SaveChangesAsync(ct);

        var eventos = _eventos.Where(e => e.Status == StatusEvento.Confirmado && e.DataEvento >= Hoje && e.DataEvento <= Hoje.AddDays(60)).ToList();
        foreach (var evento in eventos)
        {
            foreach (var servico in evento.Servicos.Where(s => porServico.ContainsKey(s.Descricao)).ToList())
            {
                var (fornecedor, minutosAntes) = porServico[servico.Descricao];
                var contratacao = evento.ContratarFornecedor(fornecedor, servico.Descricao, Math.Round(servico.Total * 0.6m, 2), servico.Id,
                    evento.Inicio.AddMinutes(-minutosAntes), fornecedor.Contato, fornecedor.Telefone);
                contratacao.Pagamento!.GerarParcelas(1, evento.DataEvento, formaPagamentoId: _formas["PIX"].Id);
            }
        }
        await db.SaveChangesAsync(ct);

        foreach (var evento in eventos.Where(e => e.Id % 2 == 0))
            foreach (var contratacao in evento.Fornecedores)
                evento.ConfirmarFornecedor(contratacao.Id, Agora.AddDays(-1));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Preços por data: sábado mais caro, segunda a quinta com desconto, alta temporada de fim de ano e o réveillon com preço
    /// próprio. Entram por último, para não mudar os orçamentos da base (que foram fechados pelo preço de tabela).
    /// </summary>
    private async Task CriarRegrasPrecoAsync(CancellationToken ct)
    {
        var sabado = new RegraPreco { Nome = "Sábado", Tipo = TipoRegraPreco.DiaSemana, Percentual = 15, AjustaPessoa = true, AjustaLocacao = true };
        sabado.DefinirDia(DayOfWeek.Saturday, true);
        var semana = new RegraPreco { Nome = "Segunda a quinta", Tipo = TipoRegraPreco.DiaSemana, Percentual = -10 };
        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday })
            semana.DefinirDia(dia, true);
        db.RegrasPreco.AddRange(sabado, semana,
            new RegraPreco
            {
                Nome = "Alta temporada", Tipo = TipoRegraPreco.Temporada, Percentual = 10, DiaInicio = 15, MesInicio = 11, DiaFim = 31, MesFim = 1
            },
            new RegraPreco
            {
                Nome = "Réveillon", Tipo = TipoRegraPreco.DataEspecial, Percentual = 40, Data = new DateOnly(Hoje.Year, 12, 31), Anual = true,
                Exclusiva = true, AjustaLocacao = true
            });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lista de espera: dois clientes na fila da data do próximo sábado ocupado e um lead esperando uma data que já está livre
    /// (vira lembrete de "data liberada").
    /// </summary>
    private async Task CriarListaEsperaAsync(CancellationToken ct)
    {
        var agora = Agora;
        var ocupado = _eventos.Where(e => e.Status == StatusEvento.Confirmado && e.Inicio > agora.AddDays(7)).OrderBy(e => e.Inicio).FirstOrDefault();
        if (ocupado is not null)
        {
            var interessados = _clientesPf.Where(c => c.Id != ocupado.ClienteId).OrderBy(c => c.Id).Take(2).ToList();
            for (var i = 0; i < interessados.Count; i++)
                db.EsperasData.Add(new EsperaData(interessados[i].Id, null, ocupado.DataEvento, ocupado.HoraInicio, ocupado.HoraFim, ocupado.EspacoId,
                    ocupado.TipoEventoId, Math.Min(ocupado.QuantidadeConvidados, 80), i == 0 ? "Quer muito esta data; aceita outro horário." : null,
                    agora.AddDays(-10 + i * 3)));
        }

        var lead = await db.Leads.Where(l => l.Status == StatusLead.Novo || l.Status == StatusLead.EmContato).OrderBy(l => l.Id).FirstOrDefaultAsync(ct);
        var livre = DateOnly.FromDateTime(agora).AddDays(200);
        var espaco = _espacos.OrderBy(e => e.Id).First();
        while (_eventos.Any(e => e.EspacoId == espaco.Id && e.Status != StatusEvento.Cancelado && Math.Abs(e.DataEvento.DayNumber - livre.DayNumber) <= 1))
            livre = livre.AddDays(1);
        if (lead is not null)
            db.EsperasData.Add(new EsperaData(null, lead.Id, livre, new TimeOnly(19, 0), new TimeOnly(23, 0), espaco.Id, null, 60,
                "Pediu para avisar se a data abrir.", agora.AddDays(-5)));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Férias de um funcionário e a folga de outro nas próximas semanas, para a agenda da equipe.</summary>
    private async Task CriarIndisponibilidadesAsync(CancellationToken ct)
    {
        var funcionarios = _funcionarios.OrderBy(f => f.Id).ToList();
        db.IndisponibilidadesFuncionario.AddRange(
            new IndisponibilidadeFuncionario
            {
                FuncionarioId = funcionarios[0].Id, Motivo = MotivoIndisponibilidade.Ferias, Inicio = Hoje.AddDays(10), Fim = Hoje.AddDays(24),
                Observacao = "Férias programadas"
            },
            new IndisponibilidadeFuncionario
            {
                FuncionarioId = funcionarios[1].Id, Motivo = MotivoIndisponibilidade.Folga, Inicio = Hoje.AddDays(3), Fim = Hoje.AddDays(3)
            });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Diárias da equipe pagas até dois meses atrás (uma conta por funcionário e mês); as recentes ficam em aberto.</summary>
    private async Task PagarEquipeAsync(CancellationToken ct)
    {
        var inicioMesAtual = new DateOnly(Hoje.Year, Hoje.Month, 1);
        for (var m = -10; m <= -2; m++)
        {
            var de = inicioMesAtual.AddMonths(m);
            var ate = de.AddMonths(1).AddDays(-1);
            var escalas = _eventos
                .Where(e => e.Status == StatusEvento.Realizado && DateOnly.FromDateTime(e.Inicio) >= de && DateOnly.FromDateTime(e.Inicio) <= ate)
                .SelectMany(e => e.Equipe)
                .Where(q => q.Presente && q.FechamentoEquipeId is null)
                .GroupBy(q => q.FuncionarioId);
            foreach (var grupo in escalas)
            {
                var funcionario = _funcionarios.First(f => f.Id == grupo.Key);
                var vencimento = ate.AddDays(5);
                var fechamento = new FechamentoEquipe(funcionario, de, ate, grupo.ToList(), vencimento)
                {
                    CriadoEm = ate.AddDays(1).ToDateTime(new TimeOnly(10, 0))
                };
                Pagar(fechamento.ContaPagar!, vencimento, _formas["Transferência Bancária"]);
                db.FechamentosEquipe.Add(fechamento);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Utilitários
    // =====================================================================

    private T Escolher<T>(T[] opcoes) => opcoes[_rnd.Next(opcoes.Length)];

    private static string PrimeiroNome(Cliente c) => c.Nome.Split(' ')[0];
    private static string UltimoNome(Cliente c) => c.Nome.Split(' ')[^1];

    private string GerarCpf()
    {
        var digitos = string.Concat(Enumerable.Range(0, 9).Select(_ => _rnd.Next(0, 10)));
        digitos += Documento.DigitoCpf(digitos, 9);
        digitos += Documento.DigitoCpf(digitos, 10);
        return Documento.CpfValido(digitos) ? digitos : GerarCpf();
    }

    private string GerarCnpj()
    {
        var digitos = string.Concat(Enumerable.Range(0, 8).Select(_ => _rnd.Next(0, 10))) + "0001";
        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        digitos += DigitoCnpj(digitos, pesos1);
        digitos += DigitoCnpj(digitos, pesos2);
        return Documento.CnpjValido(digitos) ? digitos : GerarCnpj();

        static int DigitoCnpj(string d, int[] pesos)
        {
            var resto = pesos.Select((p, i) => (d[i] - '0') * p).Sum() % 11;
            return resto < 2 ? 0 : 11 - resto;
        }
    }

    private static string RemoverAcentos(string texto)
    {
        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalizado.Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
