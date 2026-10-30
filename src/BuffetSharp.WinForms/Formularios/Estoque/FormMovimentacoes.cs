using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Kardex: histórico de entradas, saídas, ajustes e consumo dos eventos.</summary>
internal sealed class FormMovimentacoes : FormListagem<MovimentacaoResumo>
{
    private readonly IConsultasEstoqueCardapio _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly EstoqueService _estoque;
    private readonly int _produtoInicial;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _produto = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tipo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _carregouFiltros;

    public FormMovimentacoes(IConsultasEstoqueCardapio consultas, IConsultasLookup lookups, EstoqueService estoque, int produtoId = 0)
        : base("Movimentações de estoque", "Kardex com todas as entradas, saídas, ajustes de inventário e consumo dos eventos.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _estoque = estoque;
        Botao("Estornar entrada", Icones.Desfazer, EstornarAsync, "Desfaz uma entrada lançada por engano e cancela a conta a pagar dela");
        _produtoInicial = produtoId;

        _de = FiltroData("De", DateTime.Today.AddMonths(-3), opcional: false);
        _ate = FiltroData("Até", DateTime.Today, opcional: false);
        Filtro("Produto", _produto);
        Filtro("Tipo", _tipo);
        var aplicar = Tema.BotaoSecundario("Aplicar", Icones.Filtro);
        aplicar.Click += async (_, _) => await RecarregarAsync();
        Filtros.Controls.Add(aplicar);

        Grade.Data("Data", m => m.Data, "dd/MM/yyyy HH:mm", 30)
            .Texto("Produto", m => m.Produto, 50)
            .Texto("Tipo", m => Textos.Exibir(m.Tipo) + (m.EstornadaEm is not null ? " (estornada)" : ""), 22,
                m => m.EstornadaEm is not null ? Tema.Neutro : Textos.Cor(m.Tipo))
            .Numero("Quantidade", m => m.Quantidade, "N3", 22,
                m => m.Quantidade < 0 || m.Tipo is TipoMovimentacao.Saida or TipoMovimentacao.Estorno or TipoMovimentacao.Perda or TipoMovimentacao.Devolucao
                    ? Tema.Perigo : Tema.Sucesso)
            .Texto("Un.", m => m.Unidade, 10)
            .Texto("Compra", m => m.Compra, 16)
            .Texto("Fornecedor", m => m.Fornecedor, 30)
            .Texto("Conta a pagar", m => m.ContaPagar, 26,
                m => m.SituacaoConta == Domain.Financeiro.StatusTitulo.Pago || (m.ParcelasConta > 0 && m.ParcelasPagas == m.ParcelasConta) ? Tema.Sucesso : Tema.Info)
            .Numero("Custo unit.", m => m.CustoUnitario, "C4", 22)
            .Moeda("Valor", m => m.ValorTotal, 22)
            .Numero("Saldo após", m => m.SaldoApos, "N3", 22)
            .Texto("Evento", m => m.EventoId is { } id ? $"EVT-{id:D5}" : "", 18)
            .Texto("Observação", m => m.Observacao, 60)
            .Busca(m => $"{m.Produto} {m.Observacao} {m.EventoId} {m.Fornecedor} {m.ContaPagar}");
    }

    protected override async Task CarregarAsync()
    {
        if (!_carregouFiltros)
        {
            var produtos = (await _lookups.ListarAsync(TipoLookup.Produtos, somenteAtivos: false)).ToList();
            produtos.Insert(0, new ItemLookup { Id = 0, Nome = "(todos)" });
            _produto.DataSource = produtos;
            _produto.SelectedIndex = Math.Max(0, produtos.FindIndex(p => p.Id == _produtoInicial));
            _tipo.DataSource = new List<object> { "(todos)" }.Concat(Textos.Opcoes<TipoMovimentacao>()).ToList();
            _carregouFiltros = true;
            if (_produtoInicial > 0)
                Text = $"Movimentações - {(_produto.SelectedItem as ItemLookup)?.Nome}";
        }

        var produtoId = (_produto.SelectedItem as ItemLookup)?.Id;
        var tipo = (_tipo.SelectedItem as OpcaoEnum<TipoMovimentacao>)?.Valor;
        Exibir(await _consultas.ListarMovimentacoesAsync(_de.Value.Date, _ate.Value.Date, produtoId is 0 ? null : produtoId, tipo));
    }

    protected override string TextoRodape(IReadOnlyList<MovimentacaoResumo> visiveis) =>
        $"{visiveis.Count} movimentação(ões)  •  entradas: {Textos.Moeda(visiveis.Where(m => m.Tipo == TipoMovimentacao.Entrada).Sum(m => m.ValorTotal))}" +
        $"  •  saídas: {Textos.Moeda(visiveis.Where(m => m.Tipo == TipoMovimentacao.Saida).Sum(m => m.ValorTotal))}" +
        (visiveis.Any(m => m.Tipo == TipoMovimentacao.Estorno)
            ? $"  •  estornos: {Textos.Moeda(visiveis.Where(m => m.Tipo == TipoMovimentacao.Estorno).Sum(m => m.ValorTotal))}"
            : "");

    private async Task EstornarAsync()
    {
        if (Selecionado() is not { } entrada)
            return;
        if (entrada.Tipo != TipoMovimentacao.Entrada || entrada.EstornadaEm is not null)
        {
            Mensagens.Aviso(this, "Selecione uma entrada que ainda não foi estornada.");
            return;
        }
        if (entrada.RecebimentoCompraId is { } recebimento)
        {
            Mensagens.Aviso(this, $"Esta entrada veio do recebimento REC-{recebimento:D5} (pedido PC-{entrada.PedidoCompraId:D5}). " +
                                  "Estorne o recebimento em Compras › Recebimentos: os itens saem juntos e voltam ao saldo do pedido.");
            return;
        }
        var conta = entrada.ContaPagar.Length > 0 ? " A conta a pagar da compra será cancelada." : "";
        var motivo = Dialogos.PedirTexto(this, "Estornar entrada", "Motivo do estorno",
            subtitulo: $"{entrada.Quantidade:N3} {entrada.Unidade} de {entrada.Produto} em {entrada.Data:dd/MM/yyyy} " +
                       $"({Textos.Moeda(entrada.ValorTotal)}) saem do estoque e o custo médio é recalculado.{conta}");
        if (motivo is null)
            return;
        await _estoque.EstornarEntradaAsync(entrada.Id, motivo);
        await CarregarAsync();
    }
}
