using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>Item do cardápio com o custo dos insumos, para montar a composição no simulador.</summary>
public sealed record ItemSimulado(int Id, string Nome, string Secao, int OrdemSecao, CustoInsumos Custo)
{
    public override string ToString() => $"{Nome} ({Secao})";
}

public sealed record PacoteSimulado(int Id, string Nome, decimal Preco, IReadOnlyList<int> Itens)
{
    public override string ToString() => Nome;
}

/// <summary>O que o simulador precisa: os parâmetros da margem, os itens do cardápio e os pacotes.</summary>
public sealed record DadosSimulador(ParametrosMargem Parametros, IReadOnlyList<ItemSimulado> Itens, IReadOnlyList<PacoteSimulado> Pacotes,
    decimal ComissaoMedia);

/// <summary>Cenário montado na tela.</summary>
public sealed class SimulacaoInput
{
    public List<int> Itens { get; set; } = [];
    public int TipoEventoId { get; set; }
    public int Convidados { get; set; } = 100;
    public decimal Horas { get; set; } = ContextoConsumo.HorasReferencia;

    /// <summary>Parte dos convidados que é adulta (para as bebidas só de adultos).</summary>
    public decimal PercentualAdultos { get; set; } = 100;

    public decimal Comissao { get; set; }
    public decimal Taxa { get; set; }
    public decimal MargemAlvo { get; set; }

    /// <summary>Preço por pessoa praticado (o do pacote escolhido), para mostrar a margem que ele dá.</summary>
    public decimal? PrecoAtual { get; set; }
}

/// <summary>
/// Simulador de preço do pacote: a partir da composição (um pacote ou itens escolhidos), do tipo de evento (equipe pelo
/// dimensionamento), da duração e dos adultos, mostra o custo por pessoa, o preço mínimo (margem mínima) e o sugerido
/// (margem alvo) por número de convidados, e a margem do preço atual. Aplica o preço ao pacote ou cria um pacote novo.
/// Só para administrador e financeiro (mostra custos).
/// </summary>
public sealed class SimuladorPrecoService(IBuffetDbContextFactory fabrica, MargemService margens, CardapioService cardapio)
{
    public async Task<DadosSimulador> DadosAsync(CancellationToken ct = default)
    {
        GarantirCustos();
        await using var db = fabrica.Criar();
        var parametros = await margens.ParametrosAsync(db, ct);
        var itens = await db.ItensCardapio.AsNoTracking().Include(i => i.CategoriaCardapio)
            .Include(i => i.Ingredientes).ThenInclude(f => f.Produto)
            .Where(i => i.Ativo).ToListAsync(ct);
        var pacotes = await db.Pacotes.AsNoTracking().Include(p => p.Itens).Where(p => p.Ativo).OrderBy(p => p.Nome).ToListAsync(ct);
        var comissoes = await db.Usuarios.AsNoTracking()
            .Where(u => u.Ativo && (u.Perfil == Domain.Seguranca.PerfilUsuario.Comercial || u.Perfil == Domain.Seguranca.PerfilUsuario.Administrador) &&
                        u.PercentualComissao > 0)
            .Select(u => u.PercentualComissao).ToListAsync(ct);
        return new DadosSimulador(parametros,
            itens.OrderBy(i => i.CategoriaCardapio?.Ordem ?? 99).ThenBy(i => i.Nome)
                .Select(i => new ItemSimulado(i.Id, i.Nome, i.CategoriaCardapio?.Nome ?? "", i.CategoriaCardapio?.Ordem ?? 99, i.CustoInsumos)).ToList(),
            pacotes.Select(p => new PacoteSimulado(p.Id, p.Nome, p.PrecoPorPessoa, p.Itens.Select(i => i.ItemCardapioId).ToList())).ToList(),
            comissoes.Count == 0 ? 0 : Math.Round(comissoes.Average(), 2));
    }

    /// <summary>Custo e preços do cenário (com outra quantidade de convidados, se informada).</summary>
    public static ResultadoSimulacao Simular(DadosSimulador dados, SimulacaoInput input, int? convidados = null)
    {
        var pessoas = Math.Max(1, convidados ?? input.Convidados);
        var custo = dados.Itens.Where(i => input.Itens.Contains(i.Id)).Select(i => i.Custo)
            .Aggregate(CustoInsumos.Nenhum, (soma, c) => new CustoInsumos(soma.PorPessoa + c.PorPessoa, soma.PorPessoaHora + c.PorPessoaHora,
                soma.PorAdulto + c.PorAdulto, soma.PorAdultoHora + c.PorAdultoHora));
        var adultos = (int)Math.Round(pessoas * Math.Clamp(input.PercentualAdultos, 0, 100) / 100);
        var cenario = new CenarioPreco(new ContextoConsumo(pessoas, adultos, Math.Max(1, input.Horas)), custo,
            dados.Parametros.CustoEquipe(input.TipoEventoId, pessoas),
            new PercentuaisReceita(input.Comissao, dados.Parametros.AliquotaImpostos, input.Taxa));
        return SimuladorPreco.Simular(cenario, dados.Parametros.MargemMinima, input.MargemAlvo, input.PrecoAtual);
    }

    /// <summary>O cenário para cada faixa de convidados (mais a do próprio cenário).</summary>
    public static IReadOnlyList<ResultadoSimulacao> Tabela(DadosSimulador dados, SimulacaoInput input) =>
        SimuladorPreco.Faixas.Append(input.Convidados).Distinct().Order().Select(n => Simular(dados, input, n)).ToList();

    /// <summary>Passa a usar o preço por pessoa no pacote (os orçamentos já feitos não mudam).</summary>
    public async Task AplicarPrecoAsync(int pacoteId, decimal preco, CancellationToken ct = default)
    {
        GarantirCustos();
        Validador.Garantir(preco > 0, "O preço por pessoa deve ser maior que zero.");
        var pacote = await cardapio.ObterPacoteAsync(pacoteId, ct);
        pacote.PrecoPorPessoa = preco;
        await cardapio.SalvarPacoteAsync(pacote, ct);
    }

    /// <summary>Cria um pacote com a composição e o preço simulados.</summary>
    public async Task<int> CriarPacoteAsync(string nome, IReadOnlyList<int> itens, decimal preco, CancellationToken ct = default)
    {
        GarantirCustos();
        Validador.Garantir(!string.IsNullOrWhiteSpace(nome), "Informe o nome do pacote.");
        Validador.Garantir(preco > 0, "O preço por pessoa deve ser maior que zero.");
        return await cardapio.SalvarPacoteAsync(new PacoteInput
        {
            Nome = nome.Trim(),
            PrecoPorPessoa = preco,
            ItensCardapioIds = itens.ToList(),
            Descricao = "Criado pelo simulador de preço."
        }, ct);
    }

    private void GarantirCustos() =>
        Validador.Garantir(margens.PodeVerCustos, "Somente o administrador e o financeiro usam o simulador de preço (ele mostra os custos).");
}
