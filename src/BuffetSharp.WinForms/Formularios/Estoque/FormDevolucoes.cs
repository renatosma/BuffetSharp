using System.ComponentModel;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Devolução de parte de um recebimento ao fornecedor e o comprovante dela.</summary>
internal static class DialogosDevolucao
{
    private sealed class LinhaDevolucao : INotifyPropertyChanged
    {
        public required ItemDevolvivel Item { get; init; }
        public string Produto => Item.Produto;
        public string Unidade => Item.Unidade;
        public decimal Recebido => Item.Recebido;
        public decimal Devolvido => Item.Devolvido;
        public decimal Disponivel => Item.Disponivel;
        public decimal Custo => Item.CustoUnitario;
        public decimal Devolver { get; set; }
        public decimal Total => Math.Round(Devolver * Custo, 2);

        public event PropertyChangedEventHandler? PropertyChanged;
        public void Atualizar() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
    }

    /// <summary>Abre a devolução do recebimento. Retorna o Id da devolução, ou nulo se cancelar.</summary>
    public static async Task<int?> DevolverAsync(IWin32Window dono, DevolucaoService servico, int recebimentoId, string empresa)
    {
        var situacao = await servico.SituacaoAsync(recebimentoId);
        Validador.Garantir(situacao.Itens.Any(i => i.Disponivel > 0), "Tudo deste recebimento já foi devolvido.");
        var input = servico.Nova(recebimentoId);
        var linhas = new BindingList<LinhaDevolucao>(situacao.Itens.Where(i => i.Disponivel > 0).Select(i => new LinhaDevolucao { Item = i }).ToList());

        var e = new EditorCampos();
        e.Informacao("Recebimento", () => $"{situacao.Recebimento} do pedido {situacao.Pedido} - {situacao.Fornecedor}", span: 2);
        e.Data("Data da devolução", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        e.Opcoes("Motivo", () => input.Motivo, v => input.Motivo = v, nameof(input.Motivo));
        e.Marcador("O fornecedor vai repor (volta ao saldo do pedido)", () => input.FornecedorRepoe, v => input.FornecedorRepoe = v,
            nameof(input.FornecedorRepoe), span: 2);
        e.Texto("NF de devolução", () => input.NotaDevolucao, v => input.NotaDevolucao = v, nameof(input.NotaDevolucao),
            DevolucaoCompra.TamanhoNotaDevolucao, dica: "Número da nota emitida ao fornecedor (opcional)");
        e.Texto("Observações", () => input.Observacoes, v => input.Observacoes = v, nameof(input.Observacoes), DevolucaoCompra.TamanhoObservacoes,
            span: 2, dica: "O que aconteceu (ex.: 3 caixas amassadas, embalagem violada)");

        e.Secao("O que volta ao fornecedor");
        var grade = new DataGridView { AutoGenerateColumns = false, Dock = DockStyle.Fill };
        Tema.EstilizarGrade(grade);
        grade.ReadOnly = false;
        grade.AllowUserToAddRows = false;
        grade.EditMode = DataGridViewEditMode.EditOnEnter;
        DataGridViewTextBoxColumn Coluna(string titulo, string propriedade, float peso, string? formato = null, bool editavel = false) => new()
        {
            HeaderText = titulo, DataPropertyName = propriedade, FillWeight = peso, ReadOnly = !editavel,
            DefaultCellStyle = formato is null
                ? new DataGridViewCellStyle()
                : new DataGridViewCellStyle { Format = formato, FormatProvider = Textos.PtBr, Alignment = DataGridViewContentAlignment.MiddleRight }
        };
        grade.Columns.AddRange(
            Coluna("Produto", nameof(LinhaDevolucao.Produto), 34),
            Coluna("Un.", nameof(LinhaDevolucao.Unidade), 8),
            Coluna("Recebido", nameof(LinhaDevolucao.Recebido), 11, "N3"),
            Coluna("Já devolvido", nameof(LinhaDevolucao.Devolvido), 11, "N3"),
            Coluna("Custo", nameof(LinhaDevolucao.Custo), 11, "N4"),
            Coluna("Devolver", nameof(LinhaDevolucao.Devolver), 12, "N3", editavel: true),
            Coluna("Valor", nameof(LinhaDevolucao.Total), 12, "C2"));
        grade.Columns[^2].DefaultCellStyle.BackColor = Tema.Fundo;
        grade.Columns[^2].DefaultCellStyle.Font = Tema.FonteNegrito;
        grade.DataSource = linhas;
        grade.DataError += (_, ev) => ev.ThrowException = false;

        var destino = new Label { AutoSize = true, MaximumSize = new Size(820, 0), Dock = DockStyle.Bottom, Font = Tema.FonteNegrito, Padding = new Padding(0, 6, 0, 0) };
        void Conferir()
        {
            var total = linhas.Sum(l => l.Total);
            destino.Text = total == 0
                ? "Informe na coluna \"Devolver\" quanto volta de cada produto (na unidade do pedido)."
                : $"Devolução de {Textos.Moeda(total)}. {situacao.Destino(total)}";
            destino.ForeColor = total == 0 ? Tema.TextoSuave : Tema.Texto;
        }
        grade.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex >= 0 && ev.RowIndex < linhas.Count)
                linhas[ev.RowIndex].Atualizar();
            Conferir();
        };
        var painel = new Panel { Height = 230, Dock = DockStyle.Top };
        painel.Controls.Add(grade);
        painel.Controls.Add(destino);
        e.Livre(painel);
        e.Associar(nameof(input.Itens), grade);
        e.AoCarregar(Conferir);
        e.AoAplicar(() =>
        {
            grade.EndEdit();
            input.Itens = linhas.Where(l => l.Devolver != 0).Select(l => new DevolucaoItemInput(l.Item.RecebimentoItemId, l.Devolver)).ToList();
        });

        int? salva = null;
        using var dialogo = new FormEdicao($"Devolver itens do {situacao.Recebimento}",
            "Os itens saem do estoque pelo custo da entrada (nos perecíveis, do lote deste recebimento). O valor sai da nota: antes " +
            "dela, a nota já vem sem o devolvido; depois, é abatido das contas em aberto e o que passar vira crédito com o fornecedor.", e,
            async () => salva = await servico.DevolverAsync(input), 860);
        dialogo.TextoSalvar("Registrar devolução");
        if (dialogo.ShowDialog(dono) != DialogResult.OK || salva is not { } id)
            return null;

        var devolucao = await servico.ObterAsync(id);
        if (Mensagens.Confirmar(dono, $"Devolução {devolucao.Codigo} registrada ({Textos.Moeda(devolucao.Valor)}). Abrir o comprovante para o fornecedor?",
                "Devolução registrada"))
            Comprovante(devolucao, await servico.ItensAsync(id), empresa).Visualizar(dono);
        return id;
    }

    /// <summary>Comprovante da devolução para o fornecedor.</summary>
    public static DocumentoImpresso Comprovante(DevolucaoResumo devolucao, IReadOnlyList<DevolucaoItemResumo> itens, string empresa)
    {
        var doc = new DocumentoImpresso($"Devolução {devolucao.Codigo}", empresa);
        doc.Campos(3,
            ("Fornecedor", devolucao.Fornecedor),
            ("Data", devolucao.Data.ToString("dd/MM/yyyy")),
            ("Motivo", Textos.Exibir(devolucao.Motivo)),
            ("Recebimento", devolucao.Recebimento),
            ("Pedido", devolucao.Pedido),
            ("Nota fiscal da compra", devolucao.NotaFiscal ?? "-"),
            ("NF de devolução", devolucao.NotaDevolucao ?? "-"),
            ("Reposição", devolucao.FornecedorRepoe ? "O fornecedor vai repor" : "Sem reposição"),
            ("Registrado por", devolucao.Usuario ?? "-"));
        doc.Tabela(["Produto", "Quantidade", "Un.", "Custo unitário", "Total"],
            itens.Select(i => new[] { i.Produto, i.Quantidade.ToString("N3"), i.Unidade, i.CustoUnitario.ToString("C4"), Textos.Moeda(i.Total) }).ToList(),
            [4, 1.3f, 0.8f, 1.5f, 1.5f], [false, true, false, true, true]);
        doc.Total("Total devolvido", Textos.Moeda(devolucao.Valor));
        if (devolucao.ValorAbatido > 0)
            doc.Paragrafo($"Abatido das contas da {devolucao.NotaFiscal}: {Textos.Moeda(devolucao.ValorAbatido)}.");
        if (devolucao.ValorCredito > 0)
            doc.Paragrafo($"Crédito para a próxima compra: {Textos.Moeda(devolucao.ValorCredito)}.", negrito: true);
        if (!string.IsNullOrWhiteSpace(devolucao.Observacoes))
            doc.Paragrafo(devolucao.Observacoes);
        doc.Assinaturas("Entregue por", "Recebido pelo fornecedor");
        return doc;
    }
}

/// <summary>
/// Devoluções ao fornecedor: o que voltou de cada recebimento, o motivo e o destino do valor (antes da nota, abatido das contas
/// ou crédito com o fornecedor), com o comprovante impresso e o envio pelo WhatsApp ou por e-mail.
/// </summary>
internal sealed class FormDevolucoesCompra : FormListagem<DevolucaoResumo>
{
    private readonly DevolucaoService _servico;
    private readonly IEnvioEmail _email;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormDevolucoesCompra(DevolucaoService servico, IEnvioEmail email, BuffetOptions opcoes, Navegador navegador)
        : base("Devoluções ao fornecedor", "Itens devolvidos dos recebimentos (registre pela tela de Recebimentos). O valor sai da nota: antes dela, abatido das contas em aberto ou como crédito para a próxima nota.")
    {
        _servico = servico;
        _email = email;
        _opcoes = opcoes;
        _navegador = navegador;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-3), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Comprovante", Icones.Imprimir, () => Selecionado() is { } d ? AoAbrirAsync(d) : Task.CompletedTask, destaque: true);
        Botao("WhatsApp", Icones.Mensagem, WhatsAppAsync, "Envia a devolução ao fornecedor");
        Botao("E-mail", Icones.Email, EmailAsync, "Envia a devolução ao fornecedor com o comprovante em PDF");
        Separador();
        Botao("Por fornecedor", Icones.Lista, () =>
        {
            _navegador.Abrir<FormDevolucoesPorFornecedor>();
            return Task.CompletedTask;
        }, "Quem mais teve devolução, com a parte do recebido que voltou");

        Grade.Texto("Código", d => d.Codigo, 13)
            .Data("Data", d => d.Data.ToDateTime(TimeOnly.MinValue), peso: 11)
            .Texto("Fornecedor", d => d.Fornecedor, 24)
            .Texto("Recebimento", d => d.Recebimento, 13)
            .Texto("Pedido", d => d.Pedido, 11)
            .Texto("Motivo", d => Textos.Exibir(d.Motivo), 14)
            .Numero("Itens", d => d.Itens, peso: 7)
            .Moeda("Valor", d => d.Valor, 13)
            .Texto("Destino do valor", d => d.Destino, 18)
            .Moeda("Crédito a usar", d => d.CreditoDisponivel, 13, d => d.CreditoDisponivel > 0 ? Tema.Alerta : null)
            .SimNao("Repõe", d => d.FornecedorRepoe, 8)
            .Texto("NF devolução", d => d.NotaDevolucao, 12)
            .Texto("Observações", d => d.Observacoes, 30)
            .Busca(d => $"{d.Codigo} {d.Fornecedor} {d.Recebimento} {d.Pedido} {d.NotaFiscal} {d.NotaDevolucao} {d.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _servico.ListarAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<DevolucaoResumo> visiveis) =>
        $"{visiveis.Count} devolução(ões)  •  devolvido: {Textos.Moeda(visiveis.Sum(d => d.Valor))}  •  " +
        $"abatido das notas: {Textos.Moeda(visiveis.Sum(d => d.ValorAbatido))}  •  crédito a usar: {Textos.Moeda(visiveis.Sum(d => d.CreditoDisponivel))}";

    protected override async Task AoAbrirAsync(DevolucaoResumo item) =>
        DialogosDevolucao.Comprovante(item, await _servico.ItensAsync(item.Id), _opcoes.NomeEmpresa).Visualizar(this);

    private async Task WhatsAppAsync()
    {
        if (Selecionado() is not { } devolucao)
            return;
        var itens = await _servico.ItensAsync(devolucao.Id);
        DialogosEnvio.WhatsApp(this, new MensagemWhatsApp(devolucao.TelefoneFornecedor ?? "", _servico.Mensagem(devolucao, itens)),
            $"devolução para {devolucao.Fornecedor}", "o comprovante",
            () => DialogosEnvio.Pdf($"Devolução {devolucao.Codigo}", DialogosDevolucao.Comprovante(devolucao, itens, _opcoes.NomeEmpresa).GerarPdf()));
    }

    private async Task EmailAsync()
    {
        if (Selecionado() is not { } devolucao)
            return;
        var itens = await _servico.ItensAsync(devolucao.Id);
        var email = new EmailInput
        {
            Para = devolucao.EmailFornecedor ?? "",
            Assunto = $"Devolução {devolucao.Codigo} - {_opcoes.NomeEmpresa}",
            Mensagem = _servico.Mensagem(devolucao, itens)
        };
        DialogosEnvio.Email(this, $"Enviar devolução {devolucao.Codigo}", email, "o comprovante",
            () => DialogosEnvio.Pdf($"Devolução {devolucao.Codigo}", DialogosDevolucao.Comprovante(devolucao, itens, _opcoes.NomeEmpresa).GerarPdf()),
            m => _servico.EnviarEmailAsync(m), _email.Destino);
    }
}

/// <summary>Devoluções por fornecedor no período: quantas, quanto, a parte do recebido que voltou e o motivo mais comum.</summary>
internal sealed class FormDevolucoesPorFornecedor : FormListagem<DevolucoesFornecedor>
{
    private readonly DevolucaoService _servico;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormDevolucoesPorFornecedor(DevolucaoService servico)
        : base("Devoluções por fornecedor", "Quem mais teve mercadoria devolvida no período e quanto isso representa do que foi recebido dele.")
    {
        _servico = servico;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-12).AddDays(1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Grade.Texto("Fornecedor", f => f.Fornecedor, 30)
            .Numero("Devoluções", f => f.Devolucoes, peso: 11)
            .Moeda("Devolvido", f => f.Valor, 14)
            .Moeda("Recebido no período", f => f.Recebido, 16)
            .Texto("% devolvido", f => $"{f.Percentual:0.0}%", 11, f => f.Percentual >= 5 ? Tema.Perigo : f.Percentual >= 2 ? Tema.Alerta : null)
            .Texto("Motivo mais comum", f => Textos.Exibir(f.MotivoPrincipal), 18)
            .Moeda("Crédito a usar", f => f.CreditoDisponivel, 14)
            .Busca(f => f.Fornecedor);
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _servico.PorFornecedorAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<DevolucoesFornecedor> visiveis) =>
        $"{visiveis.Count} fornecedor(es) com devolução  •  devolvido: {Textos.Moeda(visiveis.Sum(f => f.Valor))}  •  " +
        $"crédito a usar: {Textos.Moeda(visiveis.Sum(f => f.CreditoDisponivel))}";
}
