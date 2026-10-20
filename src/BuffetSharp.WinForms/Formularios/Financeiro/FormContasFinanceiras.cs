using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>Caixa e contas bancárias com o saldo atual de cada uma.</summary>
internal sealed class FormContasFinanceiras : FormListagem<ContaFinanceiraResumo>
{
    private readonly IConsultasContas _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly ICadastroService<ContaFinanceira> _cadastro;
    private readonly ContaFinanceiraService _servico;
    private readonly Navegador _navegador;
    private readonly CheckBox _inativas = new() { Text = "Mostrar inativas", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

    public FormContasFinanceiras(IConsultasContas consultas, IConsultasLookup lookups, ICadastroService<ContaFinanceira> cadastro,
        ContaFinanceiraService servico, Navegador navegador)
        : base("Caixa e contas bancárias",
            "Recebimentos e pagamentos entram e saem daqui: dinheiro no caixa, as demais formas na conta bancária escolhida.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _cadastro = cadastro;
        _servico = servico;
        _navegador = navegador;

        Botao("Extrato", Icones.Lista, () => Selecionado() is { } c ? AoAbrirAsync(c) : Task.CompletedTask, destaque: true);
        Botao("Nova conta", Icones.Adicionar, () => EditarAsync(0));
        Botao("Editar", Icones.Editar, () => Selecionado() is { } c ? EditarAsync(c.Id) : Task.CompletedTask);
        Separador();
        Botao("Lançamento avulso", Icones.Dinheiro, async () =>
        {
            if (await DialogosContas.LancarAsync(this, _servico, _lookups, Grade.Selecionado?.Id))
                await CarregarAsync();
        }, "Aporte, retirada, tarifa, rendimento...");
        Botao("Transferência", Icones.Enviar, async () =>
        {
            if (await DialogosContas.TransferirAsync(this, _servico, _lookups, Grade.Selecionado?.Id))
                await CarregarAsync();
        }, "Entre contas (ex.: depósito do caixa no banco)");
        Botao("Fechar / conferir", Icones.Cadeado, async () =>
        {
            if (Selecionado() is { } c && await DialogosContas.FecharAsync(this, _servico, c))
                await CarregarAsync();
        }, "Fechamento do caixa ou conciliação com o extrato do banco");
        Botao("Conciliar extrato (OFX)", Icones.Banco, async () =>
        {
            if (Selecionado() is not { } c)
                return;
            using var conciliacao = _navegador.Criar<FormConciliacao>(c.Id, c.Nome);
            conciliacao.ShowDialog(this);
            if (conciliacao.Alterou)
                await CarregarAsync();
        }, "Confere os movimentos da conta com o extrato do banco");

        Filtros.Controls.Add(_inativas);
        _inativas.CheckedChanged += async (_, _) => await RecarregarAsync();

        Grade.Texto("Conta", c => c.Nome, 45)
            .Texto("Tipo", c => Textos.Exibir(c.Tipo), 22)
            .Texto("Banco / agência / número", c => string.Join("  ", new[] { c.Banco, c.Agencia, c.Numero }.Where(x => !string.IsNullOrWhiteSpace(x))), 50)
            .Moeda("Saldo atual", c => c.Saldo, 26, c => c.Saldo < 0 ? Tema.Perigo : Tema.Sucesso)
            .Data("Conferida até", c => c.FechadaAte, peso: 22)
            .Data("Último movimento", c => c.UltimoMovimento, peso: 22)
            .SimNao("Ativa", c => c.Ativo, 14)
            .Busca(c => $"{c.Nome} {c.Banco} {c.Agencia} {c.Numero}");
    }

    protected override async Task CarregarAsync() => Exibir(await _consultas.ListarContasAsync(!_inativas.Checked));

    protected override string TextoRodape(IReadOnlyList<ContaFinanceiraResumo> v) =>
        $"{v.Count} conta(s)  •  caixa: {Textos.Moeda(v.Where(c => c.Tipo == TipoContaFinanceira.Caixa).Sum(c => c.Saldo))}" +
        $"  •  bancos: {Textos.Moeda(v.Where(c => c.Tipo != TipoContaFinanceira.Caixa).Sum(c => c.Saldo))}" +
        $"  •  total: {Textos.Moeda(v.Sum(c => c.Saldo))}";

    protected override Task AoAbrirAsync(ContaFinanceiraResumo item)
    {
        _navegador.Abrir($"extrato:{item.Id}", () => _navegador.Criar<FormExtratoConta>(item.Id));
        return Task.CompletedTask;
    }

    private async Task EditarAsync(int id)
    {
        var conta = id == 0 ? new ContaFinanceira() : await _cadastro.ObterAsync(id) ?? throw new Domain.Comum.RegraNegocioException("Conta não encontrada.");
        var e = new EditorCampos();
        e.Texto("Nome", () => conta.Nome, v => conta.Nome = v ?? "", nameof(conta.Nome), 80, dica: "Ex.: Caixa, Itaú PJ");
        e.Opcoes("Tipo", () => conta.Tipo, v => conta.Tipo = v, nameof(conta.Tipo));
        e.Texto("Banco", () => conta.Banco, v => conta.Banco = v, nameof(conta.Banco), 60, dica: "Ex.: 341 - Itaú");
        e.Texto("Agência", () => conta.Agencia, v => conta.Agencia = v, nameof(conta.Agencia), 20);
        e.Texto("Número da conta", () => conta.Numero, v => conta.Numero = v, nameof(conta.Numero), 30);
        e.Decimal("Saldo inicial (R$)", () => conta.SaldoInicial, v => conta.SaldoInicial = v, nameof(conta.SaldoInicial), minimo: -99_999_999);
        e.Data("Data do saldo inicial", () => conta.DataSaldoInicial, v => conta.DataSaldoInicial = v ?? conta.DataSaldoInicial, nameof(conta.DataSaldoInicial));
        e.Marcador("Ativa", () => conta.Ativo, v => conta.Ativo = v);

        using var dialogo = new FormEdicao(id == 0 ? "Nova conta" : "Editar conta",
            "O saldo inicial é o saldo da conta no fim do dia anterior à data informada; a partir dela, o sistema controla os movimentos.",
            e, () => _cadastro.SalvarAsync(conta), 620);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }
}

/// <summary>Extrato de uma conta: movimentos no período com o saldo dia a dia.</summary>
internal sealed class FormExtratoConta : FormListagem<MovimentoExtrato>
{
    private readonly IConsultasContas _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly ContaFinanceiraService _servico;
    private readonly Navegador _navegador;
    private readonly ComboBox _conta = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = nameof(ItemLookup.Nome) };
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly int _contaInicial;
    private ExtratoConta? _extrato;

    public FormExtratoConta(IConsultasContas consultas, IConsultasLookup lookups, ContaFinanceiraService servico, Navegador navegador,
        int contaId)
        : base("Extrato da conta", "Entradas e saídas com o saldo após cada movimento. Linhas em cinza estão em período já conferido.")
    {
        _navegador = navegador;
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;
        _contaInicial = contaId;

        Filtro("Conta", _conta);
        var hoje = DateTime.Today;
        _de = FiltroData("De", new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-1), opcional: false);
        _ate = FiltroData("até", hoje, opcional: false);
        _conta.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Lançamento avulso", Icones.Dinheiro, async () =>
        {
            if (await DialogosContas.LancarAsync(this, _servico, _lookups, ContaId))
                await CarregarAsync();
        }, "Aporte, retirada, tarifa, rendimento...", destaque: true);
        Botao("Transferência", Icones.Enviar, async () =>
        {
            if (await DialogosContas.TransferirAsync(this, _servico, _lookups, ContaId))
                await CarregarAsync();
        });
        Botao("Excluir movimento", Icones.Excluir, ExcluirAsync, "Somente lançamentos avulsos e transferências");
        Separador();
        Botao("Fechar / conferir", Icones.Cadeado, async () =>
        {
            if (_extrato is not null && await DialogosContas.FecharAsync(this, _servico, _extrato.Conta))
                await CarregarAsync();
        }, "Fechamento do caixa ou conciliação com o extrato do banco");
        Botao("Desfazer último fechamento", Icones.Desfazer, DesfazerFechamentoAsync);
        Botao("Conciliar extrato (OFX)", Icones.Banco, async () =>
        {
            if (_extrato?.Conta is not { } conta)
                return;
            using var conciliacao = _navegador.Criar<FormConciliacao>(conta.Id, conta.Nome);
            conciliacao.ShowDialog(this);
            if (conciliacao.Alterou)
                await CarregarAsync();
        }, "Confere os movimentos com o extrato do banco (arquivo OFX)");
        Botao("Desfazer conciliação", Icones.Desfazer, async () =>
        {
            if (Selecionado() is not { } m)
                return;
            if (!m.Conciliado)
            {
                Mensagens.Aviso(this, "Selecione um movimento conciliado.");
                return;
            }
            await _navegador.Servico<ConciliacaoService>().DesfazerAsync(m.Id);
            await CarregarAsync();
        });
        Botao("Fechamentos", Icones.Historico, FechamentosAsync, "Histórico de conferências da conta");

        Grade.Data("Data", m => m.Data, peso: 18)
            .Texto("Descrição", m => m.Descricao, 90)
            .Texto("Origem", m => Textos.Exibir(m.Origem), 22)
            .Moeda("Entrada", m => m.Entrada, 22, _ => Tema.Sucesso)
            .Moeda("Saída", m => m.Saida, 22, _ => Tema.Perigo)
            .Moeda("Saldo", m => m.Saldo, 24, m => m.Saldo < 0 ? Tema.Perigo : null)
            .Texto("Banco", m => m.Conciliado ? "✓ conciliado" : "", 16, _ => Tema.Sucesso)
            .CorDaLinha(m => m.Conferido ? Tema.TextoSuave : null)
            .Busca(m => m.Descricao);
    }

    private int? ContaId => (_conta.SelectedItem as ItemLookup)?.Id;

    protected override async Task CarregarAsync()
    {
        if (_conta.DataSource is null)
        {
            var contas = await _lookups.ListarAsync(TipoLookup.ContasFinanceiras, somenteAtivos: false);
            _conta.DataSource = contas.ToList();
            _conta.SelectedIndex = Math.Max(0, contas.ToList().FindIndex(c => c.Id == _contaInicial));
        }
        if (ContaId is not { } id)
            return;

        _extrato = await _consultas.ExtratoAsync(id, DateOnly.FromDateTime(_de.Value), DateOnly.FromDateTime(_ate.Value));
        Text = $"Extrato - {_extrato.Conta.Nome}";
        Exibir(_extrato.Movimentos);
    }

    protected override string TextoRodape(IReadOnlyList<MovimentoExtrato> v) => _extrato is null
        ? string.Empty
        : $"Saldo anterior: {Textos.Moeda(_extrato.SaldoAnterior)}  •  entradas: {Textos.Moeda(_extrato.Movimentos.Sum(m => m.Entrada ?? 0))}" +
          $"  •  saídas: {Textos.Moeda(_extrato.Movimentos.Sum(m => m.Saida ?? 0))}  •  saldo final: {Textos.Moeda(_extrato.SaldoFinal)}" +
          (_extrato.Conta.FechadaAte is { } fechada ? $"  •  conferida até {fechada:dd/MM/yyyy}" : string.Empty);

    private async Task ExcluirAsync()
    {
        if (Selecionado() is not { } m)
            return;
        var texto = m.Origem == OrigemMovimento.Transferencia
            ? $"Excluir a transferência \"{m.Descricao}\"? A outra ponta (na outra conta) também será excluída."
            : $"Excluir o movimento \"{m.Descricao}\" ({Textos.Moeda(m.Valor)})?";
        if (!Mensagens.Confirmar(this, texto, "Excluir movimento", perigoso: true))
            return;
        await _servico.ExcluirMovimentoAsync(m.Id);
        await CarregarAsync();
    }

    private async Task DesfazerFechamentoAsync()
    {
        if (_extrato?.Conta is not { } conta)
            return;
        if (conta.FechadaAte is null)
        {
            Mensagens.Info(this, "Esta conta não tem fechamento.");
            return;
        }
        if (!Mensagens.Confirmar(this, $"Desfazer o fechamento de {conta.FechadaAte:dd/MM/yyyy} da conta '{conta.Nome}'? O ajuste de diferença, se houver, também é excluído.",
                "Desfazer fechamento", perigoso: true))
            return;
        await _servico.ReabrirAsync(conta.Id);
        await CarregarAsync();
    }

    private async Task FechamentosAsync()
    {
        if (_extrato?.Conta is not { } conta)
            return;
        var fechamentos = await _consultas.ListarFechamentosAsync(conta.Id);
        if (fechamentos.Count == 0)
        {
            Mensagens.Info(this, "Esta conta ainda não foi fechada (conferida).");
            return;
        }
        var linhas = fechamentos.Select(f =>
            $"{f.Data:dd/MM/yyyy}: sistema {Textos.Moeda(f.SaldoSistema)}, informado {Textos.Moeda(f.SaldoInformado)}" +
            (f.Diferenca != 0 ? $", diferença {Textos.Moeda(f.Diferenca)}" : "") +
            $" - {f.Usuario} em {DateTime.SpecifyKind(f.CriadoEm, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy HH:mm}" + (f.Observacao is null ? "" : $" ({f.Observacao})"));
        Mensagens.Info(this, string.Join(Environment.NewLine, linhas), $"Fechamentos - {conta.Nome}");
    }
}

/// <summary>Lançamento avulso, transferência e fechamento de conta.</summary>
internal static class DialogosContas
{
    private static readonly ItemLookup Entrada = new() { Id = 1, Nome = "Entrada (aporte, rendimento, estorno...)" };
    private static readonly ItemLookup Saida = new() { Id = 2, Nome = "Saída (retirada, tarifa, sangria...)" };

    public static async Task<bool> LancarAsync(IWin32Window dono, ContaFinanceiraService servico, IConsultasLookup lookups, int? contaId)
    {
        var contas = await lookups.ListarAsync(TipoLookup.ContasFinanceiras);
        var input = new MovimentoInput { ContaFinanceiraId = contaId ?? contas.FirstOrDefault()?.Id ?? 0, Data = DateOnly.FromDateTime(DateTime.Today) };
        var sentido = 2;

        var e = new EditorCampos();
        e.Lista("Conta", contas, () => input.ContaFinanceiraId, v => input.ContaFinanceiraId = v ?? 0, span: 2);
        e.Lista("Tipo", [Entrada, Saida], () => sentido, v => sentido = v ?? 2);
        e.Data("Data", () => input.Data, v => input.Data = v ?? input.Data);
        e.Decimal("Valor (R$)", () => input.Valor, v => input.Valor = v);
        e.Texto("Descrição", () => input.Descricao, v => input.Descricao = v ?? "", maximo: 200, span: 2, dica: "Ex.: Tarifa de manutenção da conta");

        using var dialogo = new FormEdicao("Lançamento avulso",
            "Para receber ou pagar contas use Contas a receber / Contas a pagar: assim o título também é quitado.", e, () =>
            {
                input.Entrada = sentido == 1;
                return servico.LancarAsync(input);
            }, 560);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    public static async Task<bool> TransferirAsync(IWin32Window dono, ContaFinanceiraService servico, IConsultasLookup lookups, int? origemId)
    {
        var contas = await lookups.ListarAsync(TipoLookup.ContasFinanceiras);
        var input = new TransferenciaInput
        {
            OrigemId = origemId ?? contas.FirstOrDefault()?.Id ?? 0,
            Data = DateOnly.FromDateTime(DateTime.Today)
        };
        input.DestinoId = contas.FirstOrDefault(c => c.Id != input.OrigemId)?.Id ?? 0;

        var e = new EditorCampos();
        e.Lista("De (sai da conta)", contas, () => input.OrigemId, v => input.OrigemId = v ?? 0);
        e.Lista("Para (entra na conta)", contas, () => input.DestinoId, v => input.DestinoId = v ?? 0);
        e.Data("Data", () => input.Data, v => input.Data = v ?? input.Data);
        e.Decimal("Valor (R$)", () => input.Valor, v => input.Valor = v);
        e.Texto("Descrição (opcional)", () => input.Descricao, v => input.Descricao = v, maximo: 120, span: 2, dica: "Ex.: Depósito do caixa");

        using var dialogo = new FormEdicao("Transferência entre contas", null, e, () => servico.TransferirAsync(input), 560);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>Fechamento do caixa ou conciliação: compara o saldo do sistema na data com o contado/extrato.</summary>
    public static async Task<bool> FecharAsync(IWin32Window dono, ContaFinanceiraService servico, ContaFinanceiraResumo conta)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var input = new FechamentoInput { ContaFinanceiraId = conta.Id, Ate = hoje };
        var saldoSistema = await servico.SaldoAsync(conta.Id, hoje);
        input.SaldoInformado = saldoSistema;

        var e = new EditorCampos();
        e.Informacao("Conta", () => conta.Nome);
        e.Informacao("Conferida até", () => conta.FechadaAte is { } f ? f.ToString("dd/MM/yyyy") : "(nunca)");
        var data = e.Data("Fechar até (inclusive)", () => input.Ate, v => input.Ate = v ?? hoje);
        var sistema = e.Informacao("Saldo do sistema nessa data", () => Textos.Moeda(saldoSistema));
        e.Decimal(conta.Tipo == TipoContaFinanceira.Caixa ? "Valor contado no caixa (R$)" : "Saldo do extrato do banco (R$)",
            () => input.SaldoInformado, v => input.SaldoInformado = v, minimo: -99_999_999);
        e.Texto("Observação (obrigatória se houver diferença)", () => input.Observacao, v => input.Observacao = v, maximo: 500, span: 2, linhas: 2);
        data.ValueChanged += async (_, _) =>
        {
            try
            {
                saldoSistema = await servico.SaldoAsync(conta.Id, DateOnly.FromDateTime(data.Value));
                sistema.Text = Textos.Moeda(saldoSistema);
            }
            catch (Exception ex)
            {
                Mensagens.Erro(dono, ex);
            }
        };

        using var dialogo = new FormEdicao(conta.Tipo == TipoContaFinanceira.Caixa ? "Fechamento do caixa" : "Conciliação da conta",
            "Uma diferença vira um ajuste (sobra ou falta). Depois de fechada, a conta não aceita movimentos até a data.",
            e, () => servico.FecharAsync(input), 600);
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }
}
