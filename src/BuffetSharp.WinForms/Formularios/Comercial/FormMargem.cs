using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Textos, cores e impressões da margem prevista do orçamento.</summary>
internal static class TextosMargem
{
    public static Color Cor(FaixaMargem faixa) => faixa switch
    {
        FaixaMargem.Boa => Tema.Sucesso,
        FaixaMargem.Atencao => Tema.Alerta,
        FaixaMargem.AbaixoDaMinima => Tema.Perigo,
        _ => Tema.TextoSuave
    };

    /// <summary>Semáforo para quem não vê os custos (o comercial).</summary>
    public static string Semaforo(FaixaMargem faixa) => faixa switch
    {
        FaixaMargem.Boa => "● boa",
        FaixaMargem.Atencao => "● atenção",
        FaixaMargem.AbaixoDaMinima => "● abaixo da mínima",
        _ => ""
    };

    public static string Percentual(decimal? margem) => margem is { } m ? $"{m:0.#}%" : "";

    /// <summary>Composição de custos e margem do orçamento, para a gerência (não vai ao cliente).</summary>
    public static DocumentoImpresso Interna(OrcamentoInput o, decimal total, CustosOrcamento custos, PercentuaisReceita percentuais,
        ParametrosMargem parametros, decimal subtotal, string empresa)
    {
        var margem = CalculadoraMargem.Percentual(total, custos);
        var doc = new DocumentoImpresso($"Custos e margem - {o.Codigo} (uso interno)", empresa);
        doc.Subtitulo($"Evento em {o.Data:dd/MM/yyyy}  •  {o.Convidados} convidados  •  não enviar ao cliente");
        doc.Tabela(["Item", "Base", "Valor"],
        [
            ["Insumos do cardápio", "ficha técnica × pessoas, pelo custo médio", Textos.Moeda(custos.Insumos)],
            ["Equipe estimada", "pelo tipo de evento", Textos.Moeda(custos.Equipe)],
            ["Serviços adicionais", "custo de cada serviço", Textos.Moeda(custos.Servicos)],
            ["Espaço", "custo por evento", Textos.Moeda(custos.Espaco)],
            ["Comissão do vendedor", $"{percentuais.Comissao:0.##}% da receita", Textos.Moeda(custos.Comissao)],
            ["Impostos", $"{percentuais.Impostos:0.##}% da receita", Textos.Moeda(custos.Impostos)],
            ["Taxa da forma de pagamento", $"{percentuais.Taxa:0.##}% da receita", Textos.Moeda(custos.Taxa)]
        ], [3, 4, 1.6f], [false, false, true]);
        doc.Total("Custos previstos", Textos.Moeda(custos.Total));
        doc.Total("Receita (total do orçamento)", Textos.Moeda(total));
        doc.Total("Margem", $"{Textos.Moeda(total - custos.Total)} ({Percentual(margem)})");
        var receitaMinima = CalculadoraMargem.ReceitaMinima(custos.Diretos, percentuais, parametros.MargemMinima);
        doc.Paragrafo($"Margem mínima {parametros.MargemMinima:0.#}% e meta {parametros.MargemMeta:0.#}%. " +
                      (receitaMinima is { } minima
                          ? $"Para a mínima, o total precisa ser de pelo menos {Textos.Moeda(minima)}: desconto máximo de " +
                            $"{CalculadoraMargem.DescontoMaximo(subtotal, custos.Diretos, percentuais, parametros.MargemMinima):0.##}% sobre o subtotal."
                          : "Os percentuais sobre a receita já consomem a margem mínima."));
        return doc;
    }

    /// <summary>Margem prevista no orçamento × a do evento.</summary>
    public static DocumentoImpresso Comparativo(ComparativoMargem c, string empresa)
    {
        var doc = new DocumentoImpresso("Margem prevista × realizada", empresa);
        doc.Subtitulo(c.Evento + (c.Orcamento is { } orcamento ? $"  •  previsto no {orcamento}" : "  •  evento sem orçamento"));
        if (!c.Encerrado)
            doc.Paragrafo("O evento ainda não foi encerrado: o realizado é parcial (insumos só saem do estoque no encerramento).");
        doc.Tabela(["Item", "Previsto", "Realizado", "Diferença"],
            c.Linhas.Select(l => new[] { l.Item, Textos.Moeda(l.Previsto), Textos.Moeda(l.Realizado), Textos.Moeda(l.Diferenca) }).ToList(),
            [3.4f, 1.4f, 1.4f, 1.4f], [false, true, true, true]);
        doc.Total("Margem prevista", $"{Textos.Moeda(c.ReceitaPrevista - c.CustoPrevisto)} ({Percentual(c.MargemPrevista)})");
        doc.Total("Margem realizada", $"{Textos.Moeda(c.ReceitaRealizada - c.CustoRealizado)} ({Percentual(c.MargemRealizada)})");
        doc.Paragrafo(c.Desperdicio is { } desperdicio
            ? $"Desperdício (sobras descartadas): {Textos.Moeda(desperdicio)}, já incluído nos insumos realizados. Os insumos que voltaram ao estoque já foram descontados."
            : "As sobras do evento ainda não foram registradas: o desperdício não aparece aqui.");
        return doc;
    }
}

/// <summary>Margem dos orçamentos do período agrupada por vendedor, pacote ou tipo de evento (administrador e financeiro).</summary>
internal sealed class FormRelatorioMargem : FormListagem<MargemGrupo>
{
    private readonly MargemService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _agrupar = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormRelatorioMargem(MargemService servico, Navegador navegador)
        : base("Relatório de margem", "Margem prevista dos orçamentos criados no período (custos congelados ao salvar). Vermelho: abaixo da margem mínima.")
    {
        _servico = servico;
        _navegador = navegador;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-6).AddDays(1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _agrupar.Items.AddRange(["Vendedor", "Pacote", "Tipo de evento"]);
        _agrupar.SelectedIndex = 0;
        Filtro("Agrupar por", _agrupar);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _agrupar.SelectedIndexChanged += async (_, _) => await RecarregarAsync();

        Botao("Orçamentos", Icones.Lista, () =>
        {
            _navegador.Abrir<FormMargemOrcamentos>();
            return Task.CompletedTask;
        }, "Cada orçamento com a receita, o custo e a margem");

        Grade.Texto("Grupo", g => g.Grupo, 30)
            .Numero("Orçamentos", g => g.Orcamentos, peso: 11)
            .Numero("Aprovados", g => g.Aprovados, peso: 10)
            .Moeda("Receita", g => g.Receita, 16)
            .Moeda("Custo previsto", g => g.Custo, 16)
            .Moeda("Margem", g => g.Margem, 16)
            .Texto("Margem %", g => TextosMargem.Percentual(g.Percentual), 10, g => g.Percentual is { } p && p < 0 ? Tema.Perigo : null)
            .Numero("Abaixo da mínima", g => g.AbaixoDaMinima, peso: 12, cor: g => g.AbaixoDaMinima > 0 ? Tema.Perigo : null)
            .Busca(g => g.Grupo);
    }

    protected override async Task CarregarAsync()
    {
        var orcamentos = await _servico.OrcamentosAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value));
        Exibir(MargemService.Agrupar(orcamentos, _agrupar.SelectedIndex switch
        {
            1 => o => o.Pacote,
            2 => o => o.TipoEvento,
            _ => o => o.Vendedor
        }));
    }

    protected override string TextoRodape(IReadOnlyList<MargemGrupo> visiveis)
    {
        var receita = visiveis.Sum(g => g.Receita);
        var margem = visiveis.Sum(g => g.Margem);
        return $"{visiveis.Sum(g => g.Orcamentos)} orçamento(s)  •  receita {Textos.Moeda(receita)}  •  margem {Textos.Moeda(margem)}" +
               (receita > 0 ? $" ({margem / receita * 100:0.#}%)" : "") + $"  •  abaixo da mínima: {visiveis.Sum(g => g.AbaixoDaMinima)}";
    }
}

/// <summary>Cada orçamento do período com a receita, o custo previsto e a margem.</summary>
internal sealed class FormMargemOrcamentos : FormListagem<MargemOrcamento>
{
    private readonly MargemService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormMargemOrcamentos(MargemService servico, Navegador navegador)
        : base("Margem dos orçamentos", "Receita, custo previsto e margem de cada orçamento criado no período.")
    {
        _servico = servico;
        _navegador = navegador;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-6).AddDays(1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Grade.Texto("Orçamento", o => o.Codigo, 12)
            .Data("Criado em", o => o.CriadoEm, "dd/MM/yyyy", 11)
            .Texto("Cliente", o => o.Cliente, 24)
            .Texto("Vendedor", o => o.Vendedor?.Split(' ')[0], 11)
            .Texto("Pacote", o => o.Pacote, 14)
            .Texto("Tipo", o => o.TipoEvento, 14)
            .Status("Status", o => o.Status, 11)
            .Moeda("Receita", o => o.Receita, 14)
            .Moeda("Custo previsto", o => o.Custo, 14)
            .Moeda("Margem", o => o.Margem, 14)
            .Texto("Margem %", o => TextosMargem.Percentual(o.Percentual), 9, o => TextosMargem.Cor(o.Faixa))
            .Busca(o => $"{o.Codigo} {o.Cliente} {o.Vendedor} {o.Pacote} {o.TipoEvento}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _servico.OrcamentosAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<MargemOrcamento> visiveis)
    {
        var receita = visiveis.Sum(o => o.Receita);
        return $"{visiveis.Count} orçamento(s)  •  receita {Textos.Moeda(receita)}  •  margem {Textos.Moeda(visiveis.Sum(o => o.Margem))}" +
               (receita > 0 ? $" ({visiveis.Sum(o => o.Margem) / receita * 100:0.#}%)" : "") +
               $"  •  abaixo da mínima: {visiveis.Count(o => o.Faixa == FaixaMargem.AbaixoDaMinima)}";
    }

    protected override Task AoAbrirAsync(MargemOrcamento item)
    {
        using (var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(item.OrcamentoId)))
            editor.ShowDialog(this);
        return RecarregarAsync();
    }
}
