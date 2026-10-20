using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cardapio;

/// <summary>
/// Reajuste percentual das tabelas de preço (pacotes, serviços e espaços), na hora ou agendado para a data de vigência,
/// com simulação antes de aplicar e opção de desfazer.
/// </summary>
internal sealed class FormReajustes : FormListagem<ReajusteResumo>
{
    private readonly ReajusteService _servico;

    public FormReajustes(ReajusteService servico)
        : base("Reajuste de preços", "Reajuste das tabelas de pacotes, serviços e espaços. Com vigência futura, é aplicado sozinho na data. Orçamentos e eventos já feitos mantêm os valores negociados.")
    {
        _servico = servico;

        Botao("Novo reajuste", Icones.Adicionar, NovoAsync, "Simula e aplica (ou agenda) um reajuste", destaque: true);
        Botao("Ver itens", Icones.Lista, VerItensAsync, "Preços antes e depois do reajuste");
        Separador();
        Botao("Desfazer", Icones.Desfazer, DesfazerAsync, "Volta os preços ao valor anterior ao reajuste");
        Botao("Cancelar agendamento", Icones.Cancelar, CancelarAsync);

        Grade.Texto("Descrição", r => r.Descricao, 40)
            .Numero("Reajuste %", r => r.Percentual, "N2", 16, r => r.Percentual < 0 ? Tema.Sucesso : null)
            .Data("Vigência", r => r.VigenciaEm.ToDateTime(TimeOnly.MinValue), peso: 18)
            .Texto("Tabelas", r => Tabelas(r.Alcance), 30)
            .Texto("Arredondamento", r => Textos.Exibir(r.Arredondamento), 22)
            .Status("Situação", r => r.Status, 18)
            .Numero("Itens", r => r.Itens, peso: 10)
            .Data("Aplicado em", r => r.AplicadoEm, "dd/MM/yyyy HH:mm", 22)
            .Texto("Usuário", r => r.Usuario, 16)
            .Busca(r => r.Descricao);
    }

    private static string Tabelas(AlcanceReajuste alcance) => string.Join(", ",
        new[] { (AlcanceReajuste.Pacotes, "pacotes"), (AlcanceReajuste.Servicos, "serviços"), (AlcanceReajuste.Espacos, "espaços") }
            .Where(t => alcance.HasFlag(t.Item1)).Select(t => t.Item2));

    protected override async Task CarregarAsync() => Exibir(await _servico.ListarAsync());

    protected override Task AoAbrirAsync(ReajusteResumo item) => VerItensAsync(item);

    private async Task NovoAsync()
    {
        var input = new ReajusteInput
        {
            Descricao = $"Tabela {DateTime.Today.Year + (DateTime.Today.Month >= 10 ? 1 : 0)}",
            VigenciaEm = DateOnly.FromDateTime(DateTime.Today),
            Percentual = 5
        };
        var e = new EditorCampos();
        e.Texto("Descrição", () => input.Descricao, v => input.Descricao = v ?? "", nameof(input.Descricao), 100, span: 2);
        e.Decimal("Reajuste (%)", () => input.Percentual, v => input.Percentual = v, nameof(input.Percentual), 2, -50,
            ReajustePreco.PercentualMaximo);
        e.Data("Vigência", () => input.VigenciaEm, v => input.VigenciaEm = v ?? input.VigenciaEm, nameof(input.VigenciaEm));
        e.Opcoes("Arredondamento", () => input.Arredondamento, v => input.Arredondamento = v, span: 2);
        e.Marcador("Pacotes (preço por pessoa)", () => input.Pacotes, v => input.Pacotes = v);
        e.Marcador("Serviços adicionais", () => input.Servicos, v => input.Servicos = v);
        e.Marcador("Espaços (locação)", () => input.Espacos, v => input.Espacos = v);

        using var dialogo = new FormEdicao("Novo reajuste de preços",
            "Use \"Simular\" para conferir os novos preços. Com vigência até hoje, eles valem já ao salvar.", e,
            () => _servico.CriarAsync(input), 620);
        var simular = Tema.BotaoSecundario("Simular", Icones.Calculadora);
        simular.Click += async (_, _) =>
        {
            try
            {
                e.LimparErros();
                e.Aplicar();
                MostrarItens($"Simulação: {input.Percentual:+0.##;-0.##}%", await _servico.SimularAsync(input));
            }
            catch (Exception ex)
            {
                Mensagens.Erro(dialogo, ex);
            }
        };
        dialogo.AdicionarAcao(simular);
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            await CarregarAsync();
    }

    private Task VerItensAsync() => Selecionado() is { } item ? VerItensAsync(item) : Task.CompletedTask;

    private async Task VerItensAsync(ReajusteResumo reajuste)
    {
        if (reajuste.Status == StatusReajuste.Agendado)
        {
            var simulacao = await _servico.SimularAsync(new ReajusteInput
            {
                Descricao = reajuste.Descricao,
                Percentual = reajuste.Percentual,
                VigenciaEm = reajuste.VigenciaEm,
                Pacotes = reajuste.Alcance.HasFlag(AlcanceReajuste.Pacotes),
                Servicos = reajuste.Alcance.HasFlag(AlcanceReajuste.Servicos),
                Espacos = reajuste.Alcance.HasFlag(AlcanceReajuste.Espacos),
                Arredondamento = reajuste.Arredondamento
            });
            MostrarItens($"{reajuste.Descricao} (agendado para {reajuste.VigenciaEm:dd/MM/yyyy}, com os preços de hoje)", simulacao);
            return;
        }
        MostrarItens(reajuste.Descricao, await _servico.ItensAplicadosAsync(reajuste.Id));
    }

    private void MostrarItens(string titulo, IReadOnlyList<ItemReajusteInfo> itens)
    {
        var grade = new Grade<ItemReajusteInfo>();
        grade.Texto("Tabela", i => Textos.Exibir(i.Tipo), 18).Texto("Item", i => i.Nome, 50)
            .Moeda("Antes", i => i.ValorAnterior, 20).Moeda("Depois", i => i.ValorNovo, 20)
            .Moeda("Diferença", i => i.Diferenca, 20, i => i.Diferenca < 0 ? Tema.Sucesso : Tema.Alerta);
        grade.Carregar(itens);
        grade.Controle.Dock = DockStyle.Fill;

        using var janela = new FormBase { Text = titulo, ClientSize = new Size(Px(760), Px(460)) };
        var rodape = new Label
        {
            Dock = DockStyle.Bottom,
            Height = Px(30),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = itens.Count == 0 ? "Nenhum preço muda." : $"{itens.Count} preço(s) alterado(s)."
        };
        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        painel.Controls.Add(grade.Controle);
        painel.Controls.Add(rodape);
        janela.Controls.Add(painel);
        janela.ShowDialog(this);
    }

    private async Task DesfazerAsync()
    {
        if (Selecionado() is not { } reajuste)
            return;
        if (!Mensagens.Confirmar(this, $"Voltar os preços de \"{reajuste.Descricao}\" ao valor anterior ao reajuste? " +
                                       "Itens alterados depois do reajuste ficam como estão.", "Desfazer reajuste", perigoso: true))
            return;
        var resultado = await _servico.DesfazerAsync(reajuste.Id);
        await CarregarAsync();
        Mensagens.Info(this, $"{resultado.Restaurados} preço(s) voltaram ao valor anterior." +
                             (resultado.Mantidos.Count == 0 ? "" : Environment.NewLine + "Mantidos (alterados depois do reajuste): " +
                                                                    string.Join(", ", resultado.Mantidos)));
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } reajuste)
            return;
        if (!Mensagens.Confirmar(this, $"Cancelar o reajuste agendado \"{reajuste.Descricao}\"?", "Cancelar agendamento"))
            return;
        await _servico.CancelarAsync(reajuste.Id);
        await CarregarAsync();
    }
}
