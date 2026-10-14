using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;
using Dapper;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasLookup(FabricaConexao conexoes) : IConsultasLookup
{
    // Cada tipo tem um SQL fixo (lista branca) — nada vindo da tela é concatenado.
    private static readonly Dictionary<TipoLookup, string> Sql = new()
    {
        [TipoLookup.Clientes] = """
            SELECT Id, Nome, Celular AS Info FROM Clientes
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome
            """,
        [TipoLookup.TiposEvento] = "SELECT Id, Nome FROM TiposEvento WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome",
        [TipoLookup.Espacos] = """
            SELECT Id, Nome, ValorLocacao AS Valor, CapacidadeMaxima AS Numero FROM Espacos
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome
            """,
        [TipoLookup.Pacotes] = """
            SELECT Id, Nome, PrecoPorPessoa AS Valor, MinimoConvidados AS Numero FROM Pacotes
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY PrecoPorPessoa
            """,
        [TipoLookup.ServicosAdicionais] = """
            SELECT Id, Nome, Preco AS Valor, CobradoPorPessoa AS Marcador FROM ServicosAdicionais
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome
            """,
        [TipoLookup.FormasPagamento] = """
            SELECT Id, Nome, TaxaPercentual AS Valor, MaximoParcelas AS Numero FROM FormasPagamento
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome
            """,
        [TipoLookup.Fornecedores] = """
            SELECT Id, COALESCE(NomeFantasia, RazaoSocial) AS Nome, Telefone AS Info FROM Fornecedores
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY 2
            """,
        [TipoLookup.Funcionarios] = """
            SELECT f.Id, f.Nome, f.ValorDiaria AS Valor, c.Nome AS Info
            FROM Funcionarios f JOIN Cargos c ON c.Id = f.CargoId
            WHERE @Todos = 1 OR f.Ativo = 1 ORDER BY c.Nome, f.Nome
            """,
        [TipoLookup.Cargos] = "SELECT Id, Nome, ValorDiariaPadrao AS Valor FROM Cargos WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome",
        [TipoLookup.CategoriasProduto] = "SELECT Id, Nome FROM CategoriasProduto WHERE @Todos = 1 OR Ativo = 1 ORDER BY Nome",
        [TipoLookup.CategoriasCardapio] = """
            SELECT Id, Nome, Ordem AS Numero FROM CategoriasCardapio
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY Ordem, Nome
            """,
        [TipoLookup.UnidadesMedida] = """
            SELECT Id, CONCAT(Sigla, ' - ', Descricao) AS Nome, Sigla AS Info FROM UnidadesMedida
            WHERE @Todos = 1 OR Ativo = 1 ORDER BY Sigla
            """,
        [TipoLookup.Produtos] = """
            SELECT p.Id, p.Nome, p.CustoMedio AS Valor, u.Sigla AS Info
            FROM Produtos p JOIN UnidadesMedida u ON u.Id = p.UnidadeMedidaId
            WHERE @Todos = 1 OR p.Ativo = 1 ORDER BY p.Nome
            """,
        [TipoLookup.ItensCardapio] = """
            SELECT i.Id, i.Nome, c.Nome AS Info, c.Ordem AS Numero
            FROM ItensCardapio i JOIN CategoriasCardapio c ON c.Id = i.CategoriaCardapioId
            WHERE @Todos = 1 OR i.Ativo = 1 ORDER BY c.Ordem, i.Nome
            """,
        [TipoLookup.EventosConfirmados] = """
            SELECT Id, CONCAT('EVT-', FORMAT(Id, 'D5'), ' - ', Titulo, ' (', FORMAT(Inicio, 'dd/MM/yyyy'), ')') AS Nome
            FROM Eventos
            WHERE @Todos = 1 OR Status <> 'Cancelado' ORDER BY Inicio DESC
            """
    };

    public async Task<IReadOnlyList<ItemLookup>> ListarAsync(TipoLookup tipo, bool somenteAtivos = true, CancellationToken ct = default)
    {
        await using var conexao = conexoes.Criar();
        var itens = await conexao.QueryAsync<ItemLookup>(
            new CommandDefinition(Sql[tipo], new { Todos = !somenteAtivos }, cancellationToken: ct));
        return itens.AsList();
    }
}
