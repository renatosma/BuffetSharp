using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Validades dos perecíveis: os lotes com saldo, do que vence primeiro para o que vence depois, com a situação (ok, vencendo,
/// vencido). O vencido não sai para eventos: é descartado como perda.
/// </summary>
internal sealed class FormValidades : FormListagem<LoteResumo>
{
    private readonly ValidadeService _validades;
    private readonly Navegador _navegador;
    private readonly CheckBox _consumidos = new() { Text = "Mostrar lotes já consumidos", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    public FormValidades(ValidadeService validades, Navegador navegador)
        : base("Validades", "Lotes dos produtos que controlam validade. As saídas usam primeiro o que vence antes; o vencido é descartado como perda.")
    {
        _validades = validades;
        _navegador = navegador;
        Filtros.Controls.Add(_consumidos);
        _consumidos.CheckedChanged += async (_, _) => await RecarregarAsync();

        Botao("Descartar (perda)", Icones.Excluir, () => Selecionado() is { Saldo: > 0 } lote
            ? FormMovimentarEstoque.DescartarAsync(this, _navegador, lote.ProdutoId, lote.Id, CarregarAsync)
            : Task.CompletedTask, "Baixa o lote vencido ou estragado como perda, pelo custo", destaque: true);
        Botao("Perdas do período", Icones.Historico, () =>
        {
            _navegador.Abrir<FormPerdasEstoque>();
            return Task.CompletedTask;
        });

        Grade.Data("Validade", l => l.Validade.ToDateTime(TimeOnly.MinValue), "dd/MM/yyyy", 14, Cor)
            .Texto("Situação", l => l.Saldo == 0 ? "Consumido" : l.Situacao switch
            {
                SituacaoLote.Vencido => $"Vencido há {-l.DiasParaVencer} dia(s)",
                SituacaoLote.Vencendo => l.DiasParaVencer == 0 ? "Vence hoje" : $"Vence em {l.DiasParaVencer} dia(s)",
                _ => "Ok"
            }, 18, Cor)
            .Texto("Produto", l => l.Produto, 34)
            .Texto("Lote", l => l.Numero ?? "(sem número)", 16)
            .Numero("Entrou", l => l.Quantidade, "N3", 12)
            .Numero("Saldo", l => l.Saldo, "N3", 12)
            .Texto("Un.", l => l.Unidade, 7)
            .Moeda("Valor", l => l.Valor, 14)
            .Data("Entrada", l => l.EntradaEm, "dd/MM/yyyy", 12)
            .CorDaLinha(l => l.Saldo == 0 ? Tema.Neutro : null)
            .Busca(l => $"{l.Produto} {l.Numero}");
    }

    private static Color? Cor(LoteResumo l) => l.Saldo == 0 ? null : l.Situacao switch
    {
        SituacaoLote.Vencido => Tema.Perigo,
        SituacaoLote.Vencendo => Tema.Alerta,
        _ => Tema.Sucesso
    };

    protected override async Task CarregarAsync() => Exibir(await _validades.ListarLotesAsync(somenteComSaldo: !_consumidos.Checked));

    protected override string TextoRodape(IReadOnlyList<LoteResumo> visiveis)
    {
        var comSaldo = visiveis.Where(l => l.Saldo > 0).ToList();
        var vencidos = comSaldo.Where(l => l.Situacao == SituacaoLote.Vencido).ToList();
        return $"{comSaldo.Count} lote(s) com saldo ({Textos.Moeda(comSaldo.Sum(l => l.Valor))})  •  " +
               $"{comSaldo.Count(l => l.Situacao == SituacaoLote.Vencendo)} vencendo  •  " +
               $"{vencidos.Count} vencido(s) no estoque ({Textos.Moeda(vencidos.Sum(l => l.Valor))})";
    }
}

/// <summary>Perdas por descarte (vencido, estragado...) no período, com o valor pelo custo.</summary>
internal sealed class FormPerdasEstoque : FormListagem<PerdaResumo>
{
    private readonly ValidadeService _validades;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormPerdasEstoque(ValidadeService validades)
        : base("Perdas do estoque", "Descartes de produto vencido ou estragado no período, pelo custo.")
    {
        _validades = validades;
        var hoje = DateTime.Today;
        _de = FiltroData("De", new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-2), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Grade.Data("Data", p => p.Data, "dd/MM/yyyy HH:mm", 16)
            .Texto("Produto", p => p.Produto, 30)
            .Numero("Quantidade", p => p.Quantidade, "N3", 12)
            .Texto("Un.", p => p.Unidade, 7)
            .Numero("Custo", p => p.CustoUnitario, "C4", 12)
            .Moeda("Valor", p => p.Valor, 14, _ => Tema.Perigo)
            .Texto("Motivo", p => p.Observacao, 50)
            .Busca(p => $"{p.Produto} {p.Observacao}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _validades.PerdasAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<PerdaResumo> visiveis) =>
        $"{visiveis.Count} descarte(s)  •  perdas: {Textos.Moeda(visiveis.Sum(p => p.Valor))}" +
        (visiveis.Count == 0 ? "" : "  •  maiores: " + string.Join(", ", visiveis.GroupBy(p => p.Produto)
            .OrderByDescending(g => g.Sum(p => p.Valor)).Take(3).Select(g => $"{g.Key} ({Textos.Moeda(g.Sum(p => p.Valor))})")));
}
