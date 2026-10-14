using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Infrastructure.Persistencia.Seed;

/// <summary>
/// Gera uma base fictícia coerente (~1 ano de operação) usando as próprias regras do domínio:
/// orçamentos enviados/aprovados/recusados, eventos realizados com baixa de estoque, check-in de
/// convidados, parcelas pagas e em atraso e despesas mensais. Todos os nomes e documentos são fictícios.
/// </summary>
internal sealed class DadosDemonstracao(BuffetDbContext db, TimeProvider relogio)
{
    private readonly Random _rnd = new(2026);
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
    private List<Cliente> _clientesPf = [];
    private List<Cliente> _clientesPj = [];
    private List<Funcionario> _funcionarios = [];

    public async Task PopularAsync(CancellationToken ct)
    {
        await CriarCadastrosBasicosAsync(ct);
        await CriarCatalogoAsync(ct);
        await CriarPessoasAsync(ct);

        var plano = PlanejarEventos();
        await CriarEstoqueInicialAsync(plano, ct);
        foreach (var item in plano)
            await CriarOrcamentoEEventoAsync(item, ct);

        await CriarDespesasMensaisAsync(ct);
        await RegistrarComprasRecentesAsync(ct);
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
            new FormaPagamento { Nome = "PIX", MaximoParcelas = 1 },
            new FormaPagamento { Nome = "Dinheiro", MaximoParcelas = 1 },
            new FormaPagamento { Nome = "Cartão de Crédito", MaximoParcelas = 12, TaxaPercentual = 3.49m },
            new FormaPagamento { Nome = "Cartão de Débito", MaximoParcelas = 1, TaxaPercentual = 1.29m },
            new FormaPagamento { Nome = "Boleto Bancário", MaximoParcelas = 12, TaxaPercentual = 1.50m },
            new FormaPagamento { Nome = "Transferência Bancária", MaximoParcelas = 1 }
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
            ("Festa & Cia Descartáveis Ltda", "Festa & Cia", "Denise")
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

        await db.SaveChangesAsync(ct);
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
        int Convidados, TimeOnly Inicio, TimeOnly Fim, Destino Destino);

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

                convidados = Math.Max(convidados, pacote.MinimoConvidados);

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

                plano.Add(new PlanoEvento(data, tipo, cliente, espaco, pacote, convidados, inicio, fim, destino));
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

        var orcamento = new Orcamento(p.Cliente.Id, p.Tipo.Id, p.Data, p.Inicio, p.Fim, p.Convidados, validade)
        {
            CriadoEm = criacao.ToDateTime(new TimeOnly(10, 0))
        };
        orcamento.DefinirPacote(p.Pacote);
        orcamento.DefinirEspaco(p.Espaco);
        orcamento.DefinirServicos(SortearServicos(p));
        orcamento.AplicarDesconto(Escolher([0m, 0m, 3m, 5m, 5m, 8m, 10m]));
        db.Orcamentos.Add(orcamento);

        if (p.Destino == Destino.Rascunho)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var envio = criacao.AddDays(1).ToDateTime(new TimeOnly(14, 0));
        orcamento.Enviar(envio);

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

        var evento = orcamento.Aprovar(titulo, momentoDecisao);
        evento.CriadoEm = momentoDecisao;
        db.Eventos.Add(evento);

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
            parcela.RegistrarPagamento(pagamento, parcela.Valor, forma.Id);
        }

        // Equipe escalada para eventos passados e próximos (até 45 dias).
        if (p.Destino == Destino.Realizado || (p.Destino == Destino.Confirmado && dias <= 45))
            EscalarEquipe(evento, p);

        // Lista de convidados (amostra) - precisa de Id para o check-in, por isso salva antes.
        AdicionarConvidados(evento);
        await db.SaveChangesAsync(ct);

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
            evento.Realizar(encerramento);
            foreach (var (produtoId, quantidade) in p.Pacote.CalcularConsumo(evento.QuantidadeConvidados))
            {
                var produto = _produtos.Values.First(x => x.Id == produtoId);
                db.MovimentacoesEstoque.Add(produto.RegistrarSaida(quantidade, encerramento,
                    $"Consumo do evento EVT-{evento.Id:D5}", evento.Id, permitirSaldoNegativo: true));
            }
            evento.MarcarEstoqueBaixado();

            // Despesas vinculadas ao evento.
            var dataPagamentoEquipe = DateOnly.FromDateTime(encerramento).AddDays(2);
            var equipe = new ContaPagar($"Pagamento da equipe - EVT-{evento.Id:D5}", CategoriaDespesa.Pessoal,
                evento.TotalEquipe, dataPagamentoEquipe, eventoId: evento.Id);
            if (dataPagamentoEquipe <= Hoje)
                equipe.RegistrarPagamento(dataPagamentoEquipe, equipe.Valor, _formas["PIX"].Id);
            db.ContasPagar.Add(equipe);

            if (_rnd.NextDouble() < 0.4)
            {
                var extra = new ContaPagar($"Locação de louças e mobiliário extra - EVT-{evento.Id:D5}", CategoriaDespesa.Servicos,
                    Math.Round(p.Convidados * (decimal)(4 + _rnd.NextDouble() * 6), 2), DateOnly.FromDateTime(encerramento).AddDays(7),
                    eventoId: evento.Id);
                if (extra.Vencimento <= Hoje)
                    extra.RegistrarPagamento(extra.Vencimento, extra.Valor, _formas["Boleto Bancário"].Id);
                db.ContasPagar.Add(extra);
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
            .Select(s => (s, s.CobradoPorPessoa ? (decimal)p.Convidados : 1m, s.Preco))
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

        var mesa = 1;
        foreach (var nome in nomes)
        {
            evento.AdicionarConvidado(nome, $"(11) 9{_rnd.Next(1000, 9999)}-{_rnd.Next(1000, 9999)}",
                acompanhantes: _rnd.Next(0, 3), mesa: $"Mesa {mesa++ / 4 + 1}");
        }
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
                    conta.RegistrarPagamento(vencimento, d.Valor, _formas["Boleto Bancário"].Id);
                db.ContasPagar.Add(conta);
            }

            if (_rnd.NextDouble() < 0.35)
                db.ContasPagar.Add(new ContaPagar($"Manutenção de equipamentos de cozinha - {mes:MM/yyyy}",
                    CategoriaDespesa.Manutencao, 600 + _rnd.Next(0, 1800), mes.AddDays(24)));
        }
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
