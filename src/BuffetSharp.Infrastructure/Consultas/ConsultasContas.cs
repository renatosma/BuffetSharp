using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasContas(FabricaConexao conexoes) : IConsultasContas
{
    private const string SelectConta = """
        SELECT c.Id, c.Nome, c.Tipo, c.Banco, c.Agencia, c.Numero, c.SaldoInicial, c.DataSaldoInicial, c.FechadaAte, c.Ativo,
               c.SaldoInicial + COALESCE(m.Total, 0) AS Saldo, m.Ultimo AS UltimoMovimento
          FROM ContasFinanceiras c
          LEFT JOIN (SELECT ContaFinanceiraId, SUM(Valor) AS Total, MAX(Data) AS Ultimo
                       FROM MovimentosConta GROUP BY ContaFinanceiraId) m ON m.ContaFinanceiraId = c.Id
        """;

    public async Task<IReadOnlyList<ContaFinanceiraResumo>> ListarContasAsync(bool somenteAtivas = true, CancellationToken ct = default) =>
        await conexoes.ListarAsync<ContaFinanceiraResumo>(SelectConta + """
             WHERE @Todas = 1 OR c.Ativo = {verdadeiro}
             ORDER BY CASE WHEN c.Tipo = 'Caixa' THEN 0 ELSE 1 END, c.Nome
            """, new { Todas = somenteAtivas ? 0 : 1 }, ct);

    private sealed class Movimento
    {
        public int Id { get; init; }
        public DateTime Data { get; init; }
        public string Descricao { get; init; } = string.Empty;
        public OrigemMovimento Origem { get; init; }
        public decimal Valor { get; init; }
        public DateTime? ConciliadoEm { get; init; }
    }

    public async Task<ExtratoConta> ExtratoAsync(int contaId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var parametros = new { ContaId = contaId, De = Filtros.Data(de), Ate = Filtros.Data(ate) };
        await using var lote = await conexoes.LoteAsync(
        [
            SelectConta + " WHERE c.Id = @ContaId",
            "SELECT COALESCE(SUM(Valor), 0) FROM MovimentosConta WHERE ContaFinanceiraId = @ContaId AND Data < @De",
            """
            SELECT Id, Data, Descricao, Origem, Valor, ConciliadoEm FROM MovimentosConta
             WHERE ContaFinanceiraId = @ContaId AND Data >= @De AND Data <= @Ate
             ORDER BY Data, Id
            """
        ], parametros, ct);
        var conta = (await lote.LerAsync<ContaFinanceiraResumo>()).SingleOrDefault()
                    ?? throw new Domain.Comum.RegraNegocioException("Conta não encontrada.");
        var anteriores = await lote.LerUnicoAsync<decimal>();
        var movimentos = await lote.LerAsync<Movimento>();

        // Antes da data do saldo inicial a conta não existia para o sistema: o saldo anterior é o próprio inicial.
        var saldo = conta.SaldoInicial + anteriores;
        var linhas = new List<MovimentoExtrato>(movimentos.Count);
        foreach (var m in movimentos)
        {
            saldo += m.Valor;
            linhas.Add(new MovimentoExtrato
            {
                Id = m.Id,
                Data = m.Data,
                Descricao = m.Descricao,
                Origem = m.Origem,
                Valor = m.Valor,
                Saldo = saldo,
                Conferido = conta.FechadaAte is { } fechada && m.Data <= fechada,
                Conciliado = m.ConciliadoEm is not null
            });
        }
        return new ExtratoConta(conta, conta.SaldoInicial + anteriores, linhas);
    }

    public async Task<IReadOnlyList<FechamentoContaResumo>> ListarFechamentosAsync(int contaId, CancellationToken ct = default) =>
        await conexoes.ListarAsync<FechamentoContaResumo>("""
            SELECT Id, Data, SaldoSistema, SaldoInformado, Observacao, Usuario, CriadoEm
              FROM FechamentosConta WHERE ContaFinanceiraId = @ContaId ORDER BY Data DESC
            """, new { ContaId = contaId }, ct);

    public async Task<IReadOnlyList<TituloEmAberto>> TitulosEmAbertoAsync(TipoBaixa tipo, int? pessoaId, CancellationToken ct = default)
    {
        const string receber = """
            SELECT p.Id, p.Descricao, p.EventoId, e.Titulo AS EventoTitulo, p.Vencimento, p.Valor,
                   e.ClienteId AS PessoaId, c.Nome AS Pessoa
              FROM Parcelas p
              JOIN Eventos e ON e.Id = p.EventoId
              JOIN Clientes c ON c.Id = e.ClienteId
             WHERE p.Status = 'Aberto' AND e.ClienteId = @PessoaId
             ORDER BY p.Vencimento, p.EventoId, p.Numero, p.Id
            """;
        const string pagar = """
            SELECT cp.Id, cp.Descricao, cp.EventoId, e.Titulo AS EventoTitulo, cp.Vencimento, cp.Valor,
                   cp.FornecedorId AS PessoaId, COALESCE(fo.NomeFantasia, fo.RazaoSocial) AS Pessoa, cp.Categoria, cp.Documento
              FROM ContasPagar cp
              LEFT JOIN Fornecedores fo ON fo.Id = cp.FornecedorId
              LEFT JOIN Eventos e ON e.Id = cp.EventoId
             WHERE cp.Status = 'Aberto'
               AND ((@PessoaId IS NULL AND cp.FornecedorId IS NULL) OR cp.FornecedorId = @PessoaId)
             ORDER BY cp.Vencimento, cp.Id
            """;
        return await conexoes.ListarAsync<TituloEmAberto>(tipo == TipoBaixa.Recebimento ? receber : pagar,
            new { PessoaId = pessoaId }, ct);
    }
}
