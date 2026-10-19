using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// "Como foi pago": data, quem registrou, formas (conta, caixa ou cartão com as faturas), juros, desconto, as outras
/// contas quitadas no mesmo pagamento e, no pagamento parcial, para onde foi o restante.
/// </summary>
internal static class PainelPagamento
{
    public sealed class LinhaForma
    {
        public string Tipo { get; init; } = string.Empty;
        public string Forma { get; init; } = string.Empty;
        public decimal Valor { get; init; }
        public string? Onde { get; init; }
    }

    public sealed class LinhaTitulo
    {
        public string Descricao { get; init; } = string.Empty;
        public decimal Abatido { get; init; }
        public decimal Juros { get; init; }
        public decimal Desconto { get; init; }
        public decimal Pago { get; init; }
        public string? Restante { get; init; }
    }

    /// <summary>
    /// Coluna "Pago com" das listagens: o tipo da forma usada (cartão de crédito, débito, PIX...) quando pago; em aberto,
    /// a forma prevista.
    /// </summary>
    public static string? PagoCom(StatusTitulo status, string? forma, TipoFormaPagamento? tipo) => status switch
    {
        StatusTitulo.Pago => tipo is { } t ? Textos.Exibir(t) : forma,
        StatusTitulo.Aberto when forma is not null => $"{forma} (prevista)",
        _ => null
    };

    /// <summary>"Cartão de crédito - Cartão Itaú" (só o tipo quando o nome da forma é o próprio tipo).</summary>
    public static string FormaComTipo(string? forma, TipoFormaPagamento? tipo)
    {
        var rotulo = tipo is { } t ? Textos.Exibir(t) : null;
        if (string.IsNullOrWhiteSpace(forma))
            return rotulo ?? "-";
        return rotulo is null || string.Equals(rotulo, forma, StringComparison.CurrentCultureIgnoreCase) ? forma : $"{rotulo} - {forma}";
    }

    /// <summary>
    /// Pagamento lançado antes do controle de caixa e contas: só há data, valor e a forma guardados na própria conta.
    /// </summary>
    public static void AdicionarResumo(EditorCampos e, bool recebimento, DateTime? data, decimal? valorPago, string? forma,
        TipoFormaPagamento? tipo)
    {
        e.Secao(recebimento ? "Como foi recebido" : "Como foi pago");
        e.Informacao(recebimento ? "Recebido em" : "Pago em", () => data?.ToString("dd/MM/yyyy (dddd)", Textos.PtBr) ?? "-");
        e.Informacao(recebimento ? "Valor recebido" : "Valor pago", () => valorPago is { } v ? Textos.Moeda(v) : "-");
        e.Informacao(recebimento ? "Recebido com" : "Pago com", () => FormaComTipo(forma, tipo), span: 2);
        e.Livre(new Label
        {
            Text = "Lançamento anterior ao controle de caixa e contas: não há o detalhe de conta, cartão, parcelas, juros ou desconto.",
            AutoSize = true,
            ForeColor = Tema.TextoSuave
        });
    }

    /// <summary>Acrescenta ao editor as seções do pagamento.</summary>
    public static void Adicionar(EditorCampos e, DetalhePagamento d)
    {
        var recebimento = d.Tipo == TipoBaixa.Recebimento;
        e.Secao(recebimento ? "Como foi recebido" : "Como foi pago");
        e.Informacao(recebimento ? "Data do recebimento" : "Data do pagamento", () => d.Data.ToString("dd/MM/yyyy (dddd)", Textos.PtBr));
        e.Informacao("Total", () => Textos.Moeda(d.Total));
        e.Informacao("Registrado por", () => $"{d.Usuario ?? "-"} em {d.RegistradoEm:dd/MM/yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(d.Observacao))
            e.Informacao("Observação", () => d.Observacao!);

        var formas = Grade<LinhaForma>(Math.Clamp(d.Formas.Count, 1, 4))
            .Texto(recebimento ? "Recebido com" : "Pago com", nameof(LinhaForma.Tipo), 25, somenteLeitura: true)
            .Texto("Forma de pagamento", nameof(LinhaForma.Forma), 30, somenteLeitura: true)
            .Numero("Valor", nameof(LinhaForma.Valor), "C2", 20, somenteLeitura: true)
            .Texto(recebimento ? "Conta" : "Conta / cartão", nameof(LinhaForma.Onde), 45, somenteLeitura: true);
        formas.Carregar(d.Formas.Select(f => new LinhaForma { Tipo = Textos.Exibir(f.Tipo), Forma = f.Forma, Valor = f.Valor, Onde = f.Onde }));
        e.Livre(formas.Painel);

        var faturas = d.Formas.Where(f => f.Faturas.Count > 0).ToList();
        if (faturas.Count > 0)
        {
            var texto = string.Join(Environment.NewLine, faturas.SelectMany(f =>
                new[] { $"Fatura do {f.Onde?.Replace("Cartão ", "cartão ")}:" }.Concat(f.Faturas.Select(x => "   " + x))));
            e.Livre(new Label { Text = texto, AutoSize = true, ForeColor = Tema.TextoSuave });
        }

        // A tabela das contas aparece quando há algo além do valor: outras contas, juros, desconto ou restante.
        var maisDetalhes = d.Titulos.Count > 1 || d.Titulos.Any(t => t.Juros != 0 || t.Desconto != 0 || t.Restante is not null);
        if (!maisDetalhes)
            return;
        e.Secao(d.Titulos.Count > 1
            ? recebimento ? "Parcelas recebidas juntas" : "Contas pagas juntas"
            : "Valores");
        var titulos = Grade<LinhaTitulo>(Math.Clamp(d.Titulos.Count, 1, 6))
            .Texto("Descrição", nameof(LinhaTitulo.Descricao), 50, somenteLeitura: true)
            .Numero("Abatido", nameof(LinhaTitulo.Abatido), "N2", 18, somenteLeitura: true)
            .Numero("Juros/multa", nameof(LinhaTitulo.Juros), "N2", 18, somenteLeitura: true)
            .Numero("Desconto", nameof(LinhaTitulo.Desconto), "N2", 18, somenteLeitura: true)
            .Numero("Pago", nameof(LinhaTitulo.Pago), "N2", 18, somenteLeitura: true)
            .Texto("Pagamento parcial", nameof(LinhaTitulo.Restante), 50, somenteLeitura: true);
        titulos.Carregar(d.Titulos.Select(t => new LinhaTitulo
        {
            Descricao = t.Descricao,
            Abatido = t.Abatido,
            Juros = t.Juros,
            Desconto = t.Desconto,
            Pago = t.Pago,
            Restante = t.Restante
        }));
        e.Livre(titulos.Painel);
    }

    /// <summary>Diálogo só com o pagamento do título (ex.: parcela recebida).</summary>
    /// <param name="data">Data, valor e forma guardados no título: usados quando o pagamento é anterior às baixas.</param>
    public static async Task ExibirAsync(IWin32Window dono, BaixaService baixas, TipoBaixa tipo, int tituloId, string descricao,
        DateTime? data, decimal? valorPago, string? forma, TipoFormaPagamento? tipoForma)
    {
        var detalhe = await baixas.DetalharDoTituloAsync(tipo, tituloId);
        var e = new EditorCampos(2);
        if (detalhe is not null)
            Adicionar(e, detalhe);
        else
            AdicionarResumo(e, tipo == TipoBaixa.Recebimento, data, valorPago, forma, tipoForma);
        using var dialogo = new FormEdicao(tipo == TipoBaixa.Recebimento ? "Recebimento" : "Pagamento", descricao, e,
            () => Task.CompletedTask, 860, somenteLeitura: true);
        dialogo.ShowDialog(dono);
    }

    private static GradeEditavel<T> Grade<T>(int linhas) where T : class, new()
    {
        var grade = new GradeEditavel<T>(() => new T(), "", "", altura: 34 + linhas * 28);
        grade.Incluir.Visible = false;
        grade.Remover.Visible = false;
        grade.Controle.ReadOnly = true;
        grade.Controle.AllowUserToAddRows = false;
        return grade;
    }
}
