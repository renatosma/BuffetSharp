using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Financeiro;

/// <summary>
/// Comissões dos vendedores sobre os valores efetivamente recebidos no período, com o percentual congelado
/// em cada evento. O fechamento gera a conta a pagar ao vendedor.
/// </summary>
internal sealed class FormComissoes : FormListagem<ComissaoVendedor>
{
    private readonly IConsultasComissoes _consultas;
    private readonly ComissaoService _servico;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly Grade<FechamentoComissaoResumo> _fechamentos = new();

    public FormComissoes(IConsultasComissoes consultas, ComissaoService servico)
        : base("Comissões de vendas", "Comissão = valor recebido no período × percentual do vendedor na data da venda. Feche o período para gerar a conta a pagar.")
    {
        _consultas = consultas;
        _servico = servico;

        var inicioMesPassado = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        _de = FiltroData("Recebimentos de", inicioMesPassado, opcional: false);
        _ate = FiltroData("até", inicioMesPassado.AddMonths(1).AddDays(-1), opcional: false);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Detalhar", Icones.Lista, DetalharAsync, "Parcelas recebidas que compõem a comissão", destaque: true);
        Botao("Fechar comissão", Icones.Confirmar, FecharAsync, "Congela o valor e gera a conta a pagar ao vendedor");
        Botao("Desfazer fechamento", Icones.Desfazer, DesfazerAsync, "Somente se a conta ainda não foi paga");

        Grade.Texto("Vendedor", c => c.Vendedor, 40)
            .Numero("Eventos", c => c.QuantidadeEventos, peso: 14)
            .Numero("Parcelas", c => c.QuantidadeParcelas, peso: 14)
            .Moeda("Recebido no período", c => c.BaseRecebida, 26)
            .Moeda("Comissão", c => c.Comissao, 22, _ => Tema.Primaria)
            .Texto("Situação", c => Situacao(c), 50, c => c.Fechada ? Tema.Sucesso : Tema.Alerta)
            .Busca(c => c.Vendedor);

        _fechamentos.Texto("Vendedor", f => f.Vendedor, 35)
            .Texto("Período", f => $"{f.De:dd/MM/yyyy} a {f.Ate:dd/MM/yyyy}", 35)
            .Moeda("Base", f => f.BaseCalculo, 22)
            .Moeda("Comissão", f => f.Valor, 22)
            .Data("Fechado em", f => f.CriadoEm, peso: 20)
            .Texto("Conta a pagar", f => Textos.Exibir<Domain.Financeiro.StatusTitulo>(f.SituacaoConta), 20,
                f => f.SituacaoConta == "Pago" ? Tema.Sucesso : Tema.Info);
        var cartao = Tema.Cartao(new Padding(10));
        cartao.Dock = DockStyle.Fill;
        cartao.Controls.Add(_fechamentos.Controle);
        cartao.Controls.Add(new Label { Text = "Fechamentos realizados", Dock = DockStyle.Top, Height = 26, Font = Tema.FonteNegrito });
        var historico = new Panel { Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(20, 4, 20, 4) };
        historico.Controls.Add(cartao);
        Controls.Add(historico);
        Controls.SetChildIndex(historico, 1);
    }

    private DateOnly De => DateOnly.FromDateTime(_de.Value);
    private DateOnly Ate => DateOnly.FromDateTime(_ate.Value);

    private static string Situacao(ComissaoVendedor c) => c.Fechada
        ? $"Fechada ({c.FechamentoDe:dd/MM} a {c.FechamentoAte:dd/MM}: {Textos.Moeda(c.FechamentoValor ?? 0)}) - " +
          Textos.Exibir<Domain.Financeiro.StatusTitulo>(c.SituacaoConta).ToLowerInvariant()
        : "Em aberto";

    protected override async Task CarregarAsync()
    {
        Exibir(await _consultas.ApurarAsync(De, Ate));
        _fechamentos.Carregar(await _consultas.ListarFechamentosAsync());
    }

    protected override string TextoRodape(IReadOnlyList<ComissaoVendedor> visiveis) =>
        $"{visiveis.Count} vendedor(es)  •  recebido: {Textos.Moeda(visiveis.Sum(c => c.BaseRecebida))}  •  " +
        $"comissões: {Textos.Moeda(visiveis.Sum(c => c.Comissao))}  •  em aberto: {Textos.Moeda(visiveis.Where(c => !c.Fechada).Sum(c => c.Comissao))}";

    private async Task DetalharAsync()
    {
        if (Selecionado() is not { } vendedor)
            return;
        var parcelas = await _consultas.DetalharAsync(vendedor.VendedorId, De, Ate);
        var grade = new Grade<ComissaoParcela>();
        grade.Data("Recebido em", p => p.DataPagamento, peso: 18)
            .Texto("Evento", p => $"{p.EventoCodigo} - {p.Evento}", 55)
            .Texto("Cliente", p => p.Cliente, 35)
            .Texto("Parcela", p => p.Descricao, 25)
            .Moeda("Valor recebido", p => p.ValorPago, 22)
            .Numero("%", p => p.Percentual, "N2", 10)
            .Moeda("Comissão", p => p.Comissao, 20);
        grade.Carregar(parcelas);
        grade.Controle.Height = 360;

        var e = new EditorCampos(1);
        e.Livre(grade.Controle);
        e.Informacao("Total", () => $"{parcelas.Count} parcela(s)  •  recebido {Textos.Moeda(parcelas.Sum(p => p.ValorPago))}  •  " +
                                    $"comissão {Textos.Moeda(parcelas.Sum(p => p.Comissao))}");
        using var dialogo = new FormEdicao($"Comissão de {vendedor.Vendedor}", $"Recebimentos de {De:dd/MM/yyyy} a {Ate:dd/MM/yyyy}",
            e, () => Task.CompletedTask, 980, somenteLeitura: true);
        dialogo.ShowDialog(this);
    }

    private async Task FecharAsync()
    {
        if (Selecionado() is not { } vendedor)
            return;
        if (!Mensagens.Confirmar(this, $"Fechar a comissão de {vendedor.Vendedor} de {De:dd/MM/yyyy} a {Ate:dd/MM/yyyy} " +
                                       $"no valor de {Textos.Moeda(vendedor.Comissao)}? Será gerada uma conta a pagar (categoria Comissões).",
                "Fechar comissão"))
            return;
        await _servico.FecharAsync(vendedor.VendedorId, De, Ate);
        await CarregarAsync();
    }

    private async Task DesfazerAsync()
    {
        if (Selecionado() is not { FechamentoId: { } fechamentoId } vendedor)
        {
            Mensagens.Aviso(this, "Selecione um vendedor com comissão fechada no período.");
            return;
        }
        if (!Mensagens.Confirmar(this, $"Desfazer o fechamento de {vendedor.Vendedor}? A conta a pagar gerada será excluída.", "Desfazer", perigoso: true))
            return;
        await _servico.DesfazerAsync(fechamentoId);
        await CarregarAsync();
    }
}
