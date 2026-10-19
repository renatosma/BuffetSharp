using BuffetSharp.Application.Consultas;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasLookup(FabricaConexao conexoes) : IConsultasLookup
{
    // Cada tipo tem um SQL fixo (lista branca) — nada vindo da tela é concatenado.
    // @Todos é 0/1 (inteiro) e "ativo" usa o marcador de booleano: funciona em todos os bancos.
    private static readonly Dictionary<TipoLookup, string> Sql = new()
    {
        [TipoLookup.Clientes] = """
            SELECT Id, Nome, Celular AS Info FROM Clientes
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """,
        [TipoLookup.TiposEvento] = "SELECT Id, Nome FROM TiposEvento WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome",
        [TipoLookup.Espacos] = """
            SELECT Id, Nome, ValorLocacao AS Valor, CapacidadeMaxima AS Numero FROM Espacos
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """,
        [TipoLookup.Pacotes] = """
            SELECT Id, Nome, PrecoPorPessoa AS Valor, MinimoConvidados AS Numero FROM Pacotes
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY PrecoPorPessoa
            """,
        [TipoLookup.ServicosAdicionais] = """
            SELECT Id, Nome, Preco AS Valor, CobradoPorPessoa AS Marcador FROM ServicosAdicionais
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """,
        [TipoLookup.FormasPagamento] = """
            SELECT Id, Nome, TaxaPercentual AS Valor, MaximoParcelas AS Numero, Tipo AS Info FROM FormasPagamento
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """,
        [TipoLookup.Fornecedores] = """
            SELECT Id, COALESCE(NomeFantasia, RazaoSocial) AS Nome, Telefone AS Info FROM Fornecedores
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY 2
            """,
        [TipoLookup.Funcionarios] = """
            SELECT f.Id, f.Nome, f.ValorDiaria AS Valor, c.Nome AS Info
            FROM Funcionarios f JOIN Cargos c ON c.Id = f.CargoId
            WHERE @Todos = 1 OR f.Ativo = {verdadeiro} ORDER BY c.Nome, f.Nome
            """,
        [TipoLookup.Cargos] = "SELECT Id, Nome, ValorDiariaPadrao AS Valor FROM Cargos WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome",
        [TipoLookup.CategoriasProduto] = "SELECT Id, Nome FROM CategoriasProduto WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome",
        [TipoLookup.CategoriasCardapio] = """
            SELECT Id, Nome, Ordem AS Numero FROM CategoriasCardapio
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Ordem, Nome
            """,
        [TipoLookup.UnidadesMedida] = """
            SELECT Id, CONCAT(CONCAT(Sigla, ' - '), Descricao) AS Nome, Sigla AS Info FROM UnidadesMedida
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Sigla
            """,
        [TipoLookup.Produtos] = """
            SELECT p.Id, p.Nome, p.CustoMedio AS Valor, u.Sigla AS Info
            FROM Produtos p JOIN UnidadesMedida u ON u.Id = p.UnidadeMedidaId
            WHERE @Todos = 1 OR p.Ativo = {verdadeiro} ORDER BY p.Nome
            """,
        [TipoLookup.ItensCardapio] = """
            SELECT i.Id, i.Nome, c.Nome AS Info, c.Ordem AS Numero
            FROM ItensCardapio i JOIN CategoriasCardapio c ON c.Id = i.CategoriaCardapioId
            WHERE @Todos = 1 OR i.Ativo = {verdadeiro} ORDER BY c.Ordem, i.Nome
            """,
        [TipoLookup.Vendedores] = """
            SELECT Id, Nome, PercentualComissao AS Valor, Login AS Info FROM Usuarios
            WHERE (@Todos = 1 OR Ativo = {verdadeiro}) AND Perfil IN ('Comercial', 'Administrador') ORDER BY Nome
            """,
        [TipoLookup.Materiais] = """
            SELECT Id, Nome, CustoLocacao AS Valor, QuantidadePropria AS Numero, Categoria AS Info FROM Materiais
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Categoria, Nome
            """,
        [TipoLookup.CondicoesPagamento] = """
            SELECT Id, Nome, PercentualEntrada AS Valor, QuantidadeParcelas AS Numero FROM CondicoesPagamento
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """,
        [TipoLookup.ModelosContrato] = """
            SELECT Id, Nome, COALESCE(TipoEventoId, 0) AS Numero, Padrao AS Marcador FROM ModelosContrato
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """,
        [TipoLookup.ContasFinanceiras] = """
            SELECT Id, Nome, Tipo AS Info, CASE WHEN Tipo = 'Caixa' THEN {verdadeiro} ELSE {falso} END AS Marcador FROM ContasFinanceiras
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY CASE WHEN Tipo = 'Caixa' THEN 0 ELSE 1 END, Nome
            """,
        [TipoLookup.CartoesCredito] = """
            SELECT Id, CASE WHEN Final IS NULL THEN Nome ELSE CONCAT(CONCAT(Nome, ' - final '), Final) END AS Nome,
                   DiaVencimento AS Numero FROM CartoesCredito
            WHERE @Todos = 1 OR Ativo = {verdadeiro} ORDER BY Nome
            """
    };

    // Propriedades (e não construtor posicional): o Dapper converte os tipos numéricos de cada banco
    // (ex.: NUMBER do Oracle) para int ao preencher propriedades.
    private sealed class EventoLookup
    {
        public int Id { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public DateTime Inicio { get; init; }
    }

    public async Task<IReadOnlyList<ItemLookup>> ListarAsync(TipoLookup tipo, bool somenteAtivos = true, CancellationToken ct = default)
    {
        if (tipo == TipoLookup.EventosConfirmados)
        {
            // O texto exibido (código + título + data) é montado aqui, sem funções de formatação do banco.
            const string sql = """
                SELECT Id, Titulo, Inicio FROM Eventos
                WHERE @Todos = 1 OR Status <> 'Cancelado' ORDER BY Inicio DESC
                """;
            var eventos = await conexoes.ListarAsync<EventoLookup>(sql, new { Todos = somenteAtivos ? 0 : 1 }, ct);
            return eventos.Select(e => new ItemLookup { Id = e.Id, Nome = $"EVT-{e.Id:D5} - {e.Titulo} ({e.Inicio:dd/MM/yyyy})" }).ToList();
        }

        return await conexoes.ListarAsync<ItemLookup>(Sql[tipo], new { Todos = somenteAtivos ? 0 : 1 }, ct);
    }
}
