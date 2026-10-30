using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Desconto acima da alçada: o motivo e, para aprovar na hora, o login e a senha de quem pode.</summary>
internal static class DialogosAlcada
{
    /// <param name="permitePedido">
    /// Orçamento: sem a senha, fica aguardando a aprovação. Recebimento: só com a senha (o dinheiro está sendo recebido agora).
    /// </param>
    /// <returns>O pedido, ou nulo se o usuário desistiu.</returns>
    public static PedidoDesconto? Pedir(IWin32Window dono, AlcadaDescontoException alcada, bool permitePedido)
    {
        var pedido = new PedidoDesconto();
        var e = new EditorCampos(2);
        e.Informacao("Desconto", () => $"{alcada.Percentual:0.##}% ({Textos.Moeda(alcada.Valor)})");
        e.Informacao("Sua alçada", () => $"{alcada.Limite:0.##}%");
        if (alcada.Margem is not null)
            e.Informacao("Margem", () => "abaixo da mínima: só o administrador aprova", span: 2);
        e.Texto("Motivo do desconto", () => null, v => pedido.Motivo = v, maximo: Orcamento.TamanhoMotivoDesconto, span: 2, linhas: 2);
        e.Secao(permitePedido
            ? "Aprovar agora (opcional): quem tem alçada informa o login e a senha"
            : "Quem tem alçada aprova com o login e a senha");
        e.Texto("Login de quem aprova", () => null, v => pedido.Login = v, maximo: 60);
        e.Senha("Senha", v => pedido.Senha = v);

        using var dialogo = new FormEdicao("Desconto acima da sua alçada",
            alcada.Message + (permitePedido ? " Sem a senha, o orçamento fica aguardando a aprovação e quem pode aprovar é avisado." : ""),
            e, () =>
            {
                Validador.Garantir(!string.IsNullOrWhiteSpace(pedido.Motivo), "Informe o motivo do desconto.");
                Validador.Garantir(permitePedido || pedido.AprovarNaHora, "Informe o login e a senha de quem aprova o desconto.");
                return Task.CompletedTask;
            }, 560);
        dialogo.TextoSalvar(permitePedido ? "Confirmar desconto" : "Aprovar desconto");
        return dialogo.ShowDialog(dono) == DialogResult.OK ? pedido : null;
    }

    /// <summary>
    /// Tenta gravar; se o desconto passar da alçada, pede o motivo (e a senha de quem aprova) e tenta de novo. Desistir cancela
    /// a gravação sem mensagem de erro.
    /// </summary>
    public static async Task<T> GravarAsync<T>(IWin32Window dono, bool permitePedido, Action<PedidoDesconto?> definir, Func<Task<T>> gravar)
    {
        try
        {
            return await gravar();
        }
        catch (AlcadaDescontoException alcada)
        {
            var pedido = Pedir(dono, alcada, permitePedido) ?? throw new OperationCanceledException("Desconto não confirmado.");
            definir(pedido);
            try
            {
                return await gravar();
            }
            finally
            {
                definir(null);
            }
        }
    }
}

/// <summary>Descontos acima da alçada aguardando decisão: quem tem alçada aprova ou recusa (com o motivo).</summary>
internal sealed class FormAprovacoesDesconto : FormListagem<AprovacaoDescontoResumo>
{
    private readonly DescontoService _servico;
    private readonly Navegador _navegador;

    public FormAprovacoesDesconto(DescontoService servico, Navegador navegador)
        : base("Aprovações de desconto", "Orçamentos com desconto acima da alçada de quem vendeu. Enquanto ninguém decide, a proposta não pode ser enviada nem aprovada.")
    {
        _servico = servico;
        _navegador = navegador;
        Botao("Aprovar", Icones.Aceitar, AprovarAsync, destaque: true);
        Botao("Recusar", Icones.Cancelar, RecusarAsync, "O vendedor precisa baixar o desconto");
        Botao("Abrir orçamento", Icones.Documento, () => Selecionado() is { } p ? AoAbrirAsync(p) : Task.CompletedTask);

        Grade.Texto("Orçamento", p => p.Codigo, 13)
            .Texto("Cliente", p => p.Cliente, 26)
            .Data("Evento", p => p.DataEvento, "dd/MM/yyyy", 11)
            .Texto("Vendedor", p => p.Vendedor?.Split(' ')[0], 12)
            .Texto("Pedido por", p => p.SolicitadoPor, 11)
            .Data("Pedido em", p => p.SolicitadoEm, "dd/MM HH:mm", 11)
            .Moeda("Tabela", p => p.ValorTabela, 13)
            .Moeda("Proposta", p => p.ValorTotal, 13)
            .Texto("Desconto", p => $"{p.Percentual:0.##}%", 9, p => p.PodeAprovar ? Tema.Alerta : Tema.Perigo)
            .Moeda("Abaixo da tabela", p => p.Valor, 13)
            .Texto("Margem", p => p.Margem is { } m ? $"{m:0.#}% (baixa)" : "", 11, p => p.Margem is null ? null : Tema.Perigo)
            .Texto("Motivo", p => p.Motivo, 32)
            .Texto("Sua alçada", p => p.PodeAprovar ? "pode aprovar" : "acima da sua", 12, p => p.PodeAprovar ? null : Tema.Neutro)
            .Busca(p => $"{p.Codigo} {p.Cliente} {p.Vendedor} {p.SolicitadoPor} {p.Motivo}");
    }

    protected override async Task CarregarAsync() => Exibir(await _servico.PendentesAsync());

    protected override string TextoRodape(IReadOnlyList<AprovacaoDescontoResumo> visiveis) =>
        $"{visiveis.Count} pedido(s) aguardando  •  {visiveis.Count(p => p.PodeAprovar)} dentro da sua alçada  •  " +
        $"abaixo da tabela: {Textos.Moeda(visiveis.Sum(p => p.Valor))}";

    protected override Task AoAbrirAsync(AprovacaoDescontoResumo item)
    {
        using (var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(item.OrcamentoId)))
            editor.ShowDialog(this);
        return RecarregarAsync();
    }

    private async Task AprovarAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        string? observacao = null;
        var e = new EditorCampos(1);
        e.Texto("Observação (opcional)", () => null, v => observacao = v, maximo: Orcamento.TamanhoMotivoDesconto, linhas: 2);
        using var dialogo = new FormEdicao($"Aprovar o desconto do {pedido.Codigo}",
            $"Desconto de {pedido.Percentual:0.##}% ({Textos.Moeda(pedido.Valor)} abaixo da tabela) pedido por {pedido.SolicitadoPor}: {pedido.Motivo}",
            e, () => _servico.AprovarAsync(pedido.OrcamentoId, observacao), 560);
        dialogo.TextoSalvar("Aprovar desconto");
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await RecarregarAsync();
    }

    private async Task RecusarAsync()
    {
        if (Selecionado() is not { } pedido)
            return;
        var motivo = Dialogos.PedirTexto(this, $"Recusar o desconto do {pedido.Codigo}", "Motivo da recusa", linhas: 2,
            subtitulo: "O vendedor é avisado e precisa baixar o desconto para enviar a proposta.");
        if (motivo is null)
            return;
        await _servico.RecusarAsync(pedido.OrcamentoId, motivo);
        await RecarregarAsync();
    }
}

/// <summary>Descontos por vendedor no período: quanto ficou abaixo da tabela e quantos passaram da alçada.</summary>
internal sealed class FormRelatorioDescontos : FormListagem<DescontosVendedor>
{
    private readonly DescontoService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormRelatorioDescontos(DescontoService servico, Navegador navegador)
        : base("Relatório de descontos", "Orçamentos criados no período, por vendedor. O desconto é o efetivo: o campo de desconto somado ao preço negociado abaixo da tabela.")
    {
        _servico = servico;
        _navegador = navegador;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-6).AddDays(1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Orçamentos", Icones.Lista, () => AbrirAsync<FormDescontosOrcamentos>(), "Cada orçamento com desconto, com quem deu e quem aprovou");
        Botao("Recebimentos", Icones.Dinheiro, () => AbrirAsync<FormDescontosRecebimento>(), "Descontos dados ao receber parcelas");

        Grade.Texto("Vendedor", v => v.Vendedor, 22)
            .Numero("Orçamentos", v => v.Orcamentos, peso: 10)
            .Numero("Com desconto", v => v.ComDesconto, peso: 10)
            .Texto("Desconto médio", v => $"{v.PercentualMedio:0.##}%", 11)
            .Texto("Maior", v => $"{v.MaiorPercentual:0.##}%", 8)
            .Moeda("Abaixo da tabela", v => v.Valor, 14)
            .Numero("Acima da alçada", v => v.PassaramDaAlcada, peso: 11)
            .Numero("Aprovados", v => v.Aprovados, peso: 9)
            .Numero("Recusados", v => v.Recusados, peso: 9, cor: v => v.Recusados > 0 ? Tema.Perigo : null)
            .Numero("Aguardando", v => v.Aguardando, peso: 9, cor: v => v.Aguardando > 0 ? Tema.Alerta : null)
            .Texto("Aprovado por", v => v.Aprovadores, 16)
            .Busca(v => v.Vendedor);
    }

    private Task AbrirAsync<T>() where T : Form
    {
        _navegador.Abrir<T>();
        return Task.CompletedTask;
    }

    protected override async Task CarregarAsync() =>
        Exibir(DescontoService.PorVendedor(await _servico.OrcamentosAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value))));

    protected override string TextoRodape(IReadOnlyList<DescontosVendedor> visiveis)
    {
        var tabela = visiveis.Sum(v => v.ValorTabela);
        return $"{visiveis.Sum(v => v.Orcamentos)} orçamento(s)  •  abaixo da tabela: {Textos.Moeda(visiveis.Sum(v => v.Valor))}" +
               (tabela > 0 ? $" ({visiveis.Sum(v => v.Valor) / tabela * 100:0.##}%)" : "") +
               $"  •  acima da alçada: {visiveis.Sum(v => v.PassaramDaAlcada)}";
    }
}

/// <summary>Cada orçamento do período com o desconto efetivo e a alçada (quem deu, quem decidiu e por quê).</summary>
internal sealed class FormDescontosOrcamentos : FormListagem<DescontoOrcamento>
{
    private readonly DescontoService _servico;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormDescontosOrcamentos(DescontoService servico, Navegador navegador)
        : base("Descontos nos orçamentos", "Orçamentos criados no período com desconto efetivo sobre a tabela.")
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
            .Status("Status", o => o.Status, 11)
            .Moeda("Tabela", o => o.ValorTabela, 13)
            .Moeda("Proposta", o => o.ValorTotal, 13)
            .Texto("Desconto", o => $"{o.Percentual:0.##}%", 9)
            .Moeda("Abaixo da tabela", o => o.Valor, 13)
            .Texto("Alçada", o => Textos.Exibir(o.Situacao), 12, o => Textos.Cor(o.Situacao))
            .Texto("Deu", o => o.ConcedidoPor, 10)
            .Texto("Decidiu", o => o.DecididoPor, 10)
            .Texto("Motivo / resposta", o => string.Join(" → ", new[] { o.Motivo, o.Resposta }.OfType<string>()), 30)
            .Busca(o => $"{o.Codigo} {o.Cliente} {o.Vendedor} {o.ConcedidoPor} {o.DecididoPor} {o.Motivo}");
    }

    protected override async Task CarregarAsync() =>
        Exibir((await _servico.OrcamentosAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value))).Where(o => o.Valor > 0).ToList());

    protected override string TextoRodape(IReadOnlyList<DescontoOrcamento> visiveis) =>
        $"{visiveis.Count} orçamento(s) com desconto  •  abaixo da tabela: {Textos.Moeda(visiveis.Sum(o => o.Valor))}  •  " +
        $"acima da alçada: {visiveis.Count(o => o.PassouDaAlcada)}";

    protected override Task AoAbrirAsync(DescontoOrcamento item)
    {
        using (var editor = _navegador.Criar<FormOrcamentoEdicao>(new AberturaOrcamento(item.OrcamentoId)))
            editor.ShowDialog(this);
        return RecarregarAsync();
    }
}

/// <summary>Descontos dados no recebimento de parcelas: quem registrou e quem autorizou o que passou da alçada.</summary>
internal sealed class FormDescontosRecebimento : FormListagem<DescontoRecebimento>
{
    private readonly DescontoService _servico;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;

    public FormDescontosRecebimento(DescontoService servico)
        : base("Descontos no recebimento", "Parcelas recebidas com desconto no período.")
    {
        _servico = servico;
        var hoje = DateTime.Today;
        _de = FiltroData("De", hoje.AddMonths(-6).AddDays(1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Grade.Data("Data", r => r.Data.ToDateTime(TimeOnly.MinValue), peso: 10)
            .Texto("Parcela", r => r.Descricao, 36)
            .Moeda("Valor", r => r.Valor, 13)
            .Moeda("Desconto", r => r.Desconto, 12)
            .Texto("%", r => $"{r.Percentual:0.##}%", 7)
            .Texto("Registrado por", r => r.Usuario, 12)
            .Texto("Autorizado por", r => r.AprovadoPor, 12, r => r.AprovadoPor is null ? null : Tema.Alerta)
            .Texto("Motivo", r => r.Motivo, 28)
            .Busca(r => $"{r.Descricao} {r.Usuario} {r.AprovadoPor} {r.Motivo}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _servico.RecebimentosAsync(DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value)));

    protected override string TextoRodape(IReadOnlyList<DescontoRecebimento> visiveis) =>
        $"{visiveis.Count} parcela(s) com desconto  •  descontos: {Textos.Moeda(visiveis.Sum(r => r.Desconto))}  •  " +
        $"acima da alçada (com senha): {visiveis.Count(r => r.AprovadoPor is not null)}";
}
