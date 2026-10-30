using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>Sobras do evento: o que sobrou de cada prato (e o destino) e os insumos que voltam ao estoque.</summary>
internal static class DialogosSobras
{
    public static string Destino(DestinoSobra destino) => destino switch
    {
        DestinoSobra.Descarte => "Descarte (desperdício)",
        DestinoSobra.Doacao => "Doação",
        DestinoSobra.RefeicaoEquipe => "Refeição da equipe",
        _ => "Cliente levou"
    };

    /// <returns>Verdadeiro se as sobras foram registradas.</returns>
    public static bool Registrar(IWin32Window dono, SobrasService servico, SobrasInput input)
    {
        var pratos = new GradeEditavel<SobraLinha>(() => new SobraLinha(), "", "", altura: 260)
            .Texto("Seção", nameof(SobraLinha.Secao), 22, somenteLeitura: true)
            .Texto("Prato", nameof(SobraLinha.Prato), 40, somenteLeitura: true)
            .Numero("Porções", nameof(SobraLinha.Porcoes), "N0", 12, somenteLeitura: true)
            .Numero("Sobrou (%)", nameof(SobraLinha.Percentual), "N1", 12)
            .Opcoes("Destino", nameof(SobraLinha.DestinoValor), Enum.GetValues<DestinoSobra>().Select(d => new OpcaoGrade<int>((int)d, Destino(d))), 26)
            .Texto("Instituição (doação)", nameof(SobraLinha.Instituicao), 30, SobraPrato.TamanhoInstituicao)
            .Numero("Custo da sobra", nameof(SobraLinha.Custo), "C2", 16, somenteLeitura: true);
        pratos.Incluir.Visible = pratos.Remover.Visible = false;
        pratos.Carregar(input.Pratos);

        var insumos = new GradeEditavel<RetornoLinha>(() => new RetornoLinha(), "", "", altura: 180)
            .Texto("Insumo", nameof(RetornoLinha.Produto), 40, somenteLeitura: true)
            .Texto("Un.", nameof(RetornoLinha.Unidade), 8, somenteLeitura: true)
            .Numero("Baixado no evento", nameof(RetornoLinha.Consumido), "N3", 16, somenteLeitura: true)
            .Numero("Já voltou", nameof(RetornoLinha.JaVoltou), "N3", 14, somenteLeitura: true)
            .Numero("Volta agora", nameof(RetornoLinha.Voltar), "N3", 14);
        insumos.Incluir.Visible = insumos.Remover.Visible = false;
        insumos.Carregar(input.Insumos.Where(i => !i.Perecivel && i.NaoDevolvido > 0));

        var resumo = new Label { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(4, 8, 4, 4) };
        void Atualizar()
        {
            var linhas = pratos.Itens;
            resumo.Text = $"Sobra: {Textos.Moeda(linhas.Sum(l => l.Custo))}  •  desperdício (descarte): " +
                          $"{Textos.Moeda(linhas.Where(l => l.Destino == DestinoSobra.Descarte).Sum(l => l.Custo))}";
        }
        pratos.Alterado += Atualizar;

        var e = new EditorCampos(1);
        e.Secao($"Pratos ({input.Pessoas} pessoas) - quanto do produzido sobrou e o destino");
        e.Livre(pratos.Painel);
        e.Livre(resumo);
        e.Secao("Insumos levados e não usados que voltam ao estoque (os perecíveis não voltam)");
        e.Livre(insumos.Painel);
        e.AoCarregar(Atualizar);

        using var dialogo = new FormEdicao($"Sobras - {input.Evento}",
            input.RegistradasEm is { } em ? $"Registradas em {em:dd/MM/yyyy HH:mm}: salvar de novo substitui as sobras (o que já voltou ao estoque fica)."
                : "Só o descarte conta como desperdício. Doação, refeição da equipe e o que o cliente levou são sobra aproveitada.",
            e, () =>
            {
                pratos.ConcluirEdicao();
                insumos.ConcluirEdicao();
                return servico.RegistrarAsync(input);
            }, 1000);
        dialogo.TextoSalvar("Registrar sobras");
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>Folha para a cozinha preencher no fim do evento.</summary>
    public static DocumentoImpresso Ficha(SobrasInput input, string empresa)
    {
        var doc = new DocumentoImpresso($"Ficha de sobras - {input.Evento}", empresa);
        doc.Subtitulo($"{input.Inicio:dd/MM/yyyy HH:mm}  •  {input.Pessoas} pessoas  •  preencher no fim do evento");
        doc.Tabela(["Seção", "Prato", "Porções", "Sobrou (%)", "Destino (descarte / doação / equipe / cliente)"],
            input.Pratos.Select(p => new[] { p.Secao, p.Prato, p.Porcoes.ToString(), "[        ]", "" }).ToList(),
            [1.6f, 3, 0.9f, 1.1f, 3.4f], [false, false, true, false, false]);
        doc.Paragrafo("Instituição da doação: ______________________________________");
        if (input.Insumos.Count > 0)
            doc.Tabela(["Insumo não usado", "Un.", "Baixado", "Voltou ao estoque"],
                input.Insumos.Where(i => !i.Perecivel).Select(i => new[] { i.Produto, i.Unidade, i.Consumido.ToString("N3"), "[        ]" }).ToList(),
                [3.5f, 0.8f, 1.2f, 1.6f], [false, false, true, false]);
        doc.Paragrafo("Responsável: ______________________________    Data: ____/____/______");
        return doc;
    }
}

/// <summary>Desperdício dos eventos com as sobras registradas, agrupado por evento, prato, pacote, tipo de evento ou mês.</summary>
internal sealed class FormRelatorioDesperdicio : FormListagem<DesperdicioGrupo>
{
    private readonly SobrasService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _agrupar = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };

    private static readonly (AgrupamentoDesperdicio Valor, string Texto)[] Agrupamentos =
    [
        (AgrupamentoDesperdicio.Evento, "Evento"), (AgrupamentoDesperdicio.Prato, "Prato"), (AgrupamentoDesperdicio.Pacote, "Pacote"),
        (AgrupamentoDesperdicio.TipoEvento, "Tipo de evento"), (AgrupamentoDesperdicio.Mes, "Mês")
    ];

    public FormRelatorioDesperdicio(SobrasService servico, Navegador navegador)
        : base("Sobras e desperdício", "Eventos com as sobras registradas. Desperdício é só o descarte; doação, refeição da equipe e o que o cliente levou são sobra aproveitada.")
    {
        _servico = servico;
        _navegador = navegador;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-6).AddDays(1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _agrupar.Items.AddRange(Agrupamentos.Select(a => (object)a.Texto).ToArray());
        _agrupar.SelectedIndex = 0;
        Filtro("Agrupar por", _agrupar);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _agrupar.SelectedIndexChanged += async (_, _) => await RecarregarAsync();

        Botao("Ajustar fichas", Icones.Editar, () =>
        {
            _navegador.Abrir<FormSugestoesFicha>();
            return Task.CompletedTask;
        }, "Pratos que sobram muito: sugestão de reduzir a ficha técnica");

        Grade.Texto("Grupo", g => g.Grupo, 36)
            .Numero("Eventos", g => g.Eventos, peso: 9)
            .Moeda("Insumos", g => g.CustoInsumos, 14)
            .Moeda("Sobra", g => g.Sobra, 13)
            .Moeda("Desperdício", g => g.Desperdicio, 13, g => g.Desperdicio > 0 ? Tema.Perigo : null)
            .Texto("% dos insumos", g => g.PercentualDesperdicio is { } p ? $"{p:0.#}%" : "", 11)
            .Moeda("Doado", g => g.Doado, 12)
            .Texto("Sobra média", g => $"{g.SobraMedia:0.#}%", 10, g => g.SobraMedia >= 15 ? Tema.Alerta : null)
            .Busca(g => g.Grupo);
    }

    protected override async Task CarregarAsync() =>
        Exibir(SobrasService.Agrupar(await _servico.EventosAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)),
            Agrupamentos[Math.Max(0, _agrupar.SelectedIndex)].Valor));

    protected override string TextoRodape(IReadOnlyList<DesperdicioGrupo> visiveis)
    {
        var insumos = visiveis.Sum(g => g.CustoInsumos);
        var desperdicio = visiveis.Sum(g => g.Desperdicio);
        return $"Sobra {Textos.Moeda(visiveis.Sum(g => g.Sobra))}  •  desperdício {Textos.Moeda(desperdicio)}" +
               (insumos > 0 ? $" ({desperdicio / insumos * 100:0.#}% dos insumos)" : "") + $"  •  doado {Textos.Moeda(visiveis.Sum(g => g.Doado))}";
    }
}

/// <summary>Pratos com sobra média acima do limite nos últimos eventos: a ficha técnica reduzida, para aplicar com um clique.</summary>
internal sealed class FormSugestoesFicha : FormListagem<SugestaoFicha>
{
    private readonly SobrasService _servico;

    public FormSugestoesFicha(SobrasService servico)
        : base("Ajuste da ficha pelas sobras", "Pratos que sobram muito nos últimos eventos. Aplicar reduz todas as quantidades da ficha técnica pelo fator (com uma folga de segurança).")
    {
        _servico = servico;
        Botao("Aplicar", Icones.Aceitar, AplicarAsync, destaque: true);

        Grade.Texto("Prato", s => s.Prato, 26)
            .Numero("Eventos", s => s.Eventos, peso: 8)
            .Texto("Sobra média", s => $"{s.SobraMedia:0.#}%", 10, _ => Tema.Alerta)
            .Texto("Reduzir", s => $"{(1 - s.Fator) * 100:0}%", 8)
            .Texto("Ficha atual → sugerida", s => s.Resumo, 70)
            .Busca(s => s.Prato);
    }

    protected override async Task CarregarAsync() => Exibir(await _servico.SugestoesAsync());

    private async Task AplicarAsync()
    {
        if (Selecionado() is not { } sugestao ||
            !Mensagens.Confirmar(this, $"Reduzir em {(1 - sugestao.Fator) * 100:0}% as quantidades da ficha técnica de \"{sugestao.Prato}\"?\n\n{sugestao.Resumo}",
                "Ajustar ficha técnica"))
            return;
        await _servico.AplicarAsync(sugestao.ItemCardapioId, sugestao.Fator);
        await RecarregarAsync();
        Mensagens.Info(this, $"Ficha técnica de \"{sugestao.Prato}\" ajustada. Ela volta a aparecer aqui só se continuar sobrando.", "Ficha ajustada");
    }
}
