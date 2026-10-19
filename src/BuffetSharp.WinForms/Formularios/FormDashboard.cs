using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Seguranca;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios;

/// <summary>Painel gerencial carregado em uma única consulta Dapper (QueryMultiple).</summary>
internal sealed class FormDashboard : FormBase
{
    private readonly IConsultasDashboard _consultas;
    private readonly Navegador _navegador;

    private readonly CartaoIndicador _eventosMes = new("Eventos no mês", "-", Tema.Primaria);
    private readonly CartaoIndicador _faturamentoMes = new("Faturamento do mês", "-", Tema.Info);
    private readonly CartaoIndicador _recebidoMes = new("Recebido no mês", "-", Tema.Sucesso);
    private readonly CartaoIndicador _vencido = new("A receber vencido", "-", Tema.Perigo);
    private readonly CartaoIndicador _aPagar = new("A pagar (7 dias)", "-", Tema.Alerta);
    private readonly CartaoIndicador _orcamentos = new("Orçamentos em aberto", "-", Tema.Destaque);
    private readonly CartaoIndicador _conversao = new("Conversão (90 dias)", "-", Tema.Info);
    private readonly CartaoIndicador _estoque = new("Estoque a repor", "-", Tema.Alerta);
    private readonly CartaoIndicador _saldoContas = new("Caixa e bancos", "-", Tema.Sucesso);

    private readonly GraficoBarras _grafico = new() { Dock = DockStyle.Fill, Titulo = "Faturamento: contratado (data do evento) x recebido" };
    private readonly Grade<ProximoEvento> _proximos = new();
    private readonly Grade<TituloVencido> _vencidas = new();
    private readonly Grade<ProdutoAlerta> _alertas = new();

    public FormDashboard(IConsultasDashboard consultas, Navegador navegador, ISessaoUsuario sessao)
    {
        _consultas = consultas;
        _navegador = navegador;
        Text = "Painel";

        _proximos.Data("Data", e => e.Inicio, "dd/MM HH:mm", 30)
            .Texto("Evento", e => e.Titulo, 50)
            .Texto("Espaço", e => e.Espaco, 30)
            .Numero("Conv.", e => e.QuantidadeConvidados, peso: 17)
            .Numero("Equipe", e => e.EquipeEscalada, peso: 18, cor: e => e.EquipeEscalada == 0 ? Tema.Perigo : null)
            .Moeda("Saldo", e => e.SaldoAReceber, 32, e => e.SaldoAReceber > 0 ? Tema.Alerta : Tema.Sucesso);
        _proximos.DuploClique += e => _navegador.Abrir($"evento:{e.Id}", () => _navegador.Criar<FormEventoDetalhe>(e.Id));

        _vencidas.Texto("Cliente", t => t.Cliente, 40)
            .Texto("Parcela", t => t.Descricao, 60)
            .Data("Vencimento", t => t.Vencimento, peso: 24)
            .Numero("Dias", t => t.DiasAtraso, peso: 12, cor: _ => Tema.Perigo)
            .Moeda("Valor", t => t.Valor, 22);
        _vencidas.DuploClique += t => _navegador.Abrir($"evento:{t.EventoId}", () => _navegador.Criar<FormEventoDetalhe>(t.EventoId));

        _alertas.Texto("Produto", p => p.Nome, 50)
            .Numero("Estoque", p => p.EstoqueAtual, "N3", 20, _ => Tema.Perigo)
            .Numero("Mínimo", p => p.EstoqueMinimo, "N3", 20)
            .Texto("Un.", p => p.Unidade, 10);

        var cartoes = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 4, 14, 4) };
        cartoes.Controls.AddRange([_eventosMes, _faturamentoMes, _recebidoMes, _vencido, _aPagar, _orcamentos, _conversao, _estoque]);

        // Saldo de caixa e bancos: só para quem cuida do financeiro.
        if (sessao.Usuario?.PossuiPerfil(PerfilUsuario.Financeiro) == true)
            cartoes.Controls.Add(_saldoContas);

        var grade = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(14, 0, 14, 14) };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        grade.Controls.Add(Bloco(null, _grafico), 0, 0);
        grade.Controls.Add(Bloco("Próximos eventos", _proximos.Controle), 1, 0);
        grade.Controls.Add(Bloco("Parcelas vencidas (inadimplência)", _vencidas.Controle), 0, 1);
        grade.Controls.Add(Bloco("Estoque abaixo do mínimo", _alertas.Controle), 1, 1);

        var topo = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(20, 14, 20, 0) };
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titulos = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        titulos.Controls.Add(Tema.Titulo("Painel"));
        titulos.Controls.Add(Tema.Subtitulo("Visão geral do negócio. Dê um duplo clique em um evento ou parcela para abrir os detalhes."));
        var atualizar = Tema.BotaoSecundario("Atualizar", Icones.Atualizar);
        atualizar.Click += async (_, _) => await CarregarAsync();
        topo.Controls.Add(titulos, 0, 0);
        topo.Controls.Add(atualizar, 1, 0);

        Controls.Add(grade);
        Controls.Add(cartoes);
        Controls.Add(topo);
    }

    private static Control Bloco(string? titulo, Control conteudo)
    {
        var cartao = Tema.Cartao(new Padding(10));
        cartao.Dock = DockStyle.Fill;
        conteudo.Dock = DockStyle.Fill;
        cartao.Controls.Add(conteudo);
        if (titulo is not null)
            cartao.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Top, Font = Tema.FonteNegrito, Height = 28 });
        return cartao;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await CarregarAsync();
    }

    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var r = await _consultas.ObterAsync(DateOnly.FromDateTime(DateTime.Today));

        _eventosMes.Atualizar(r.EventosNoMes.ToString(), DateTime.Today.ToString("MMMM 'de' yyyy", Textos.PtBr));
        _faturamentoMes.Atualizar(GraficoBarras.FormatarCompacto(r.FaturamentoPrevistoMes), Textos.Moeda(r.FaturamentoPrevistoMes));
        _recebidoMes.Atualizar(GraficoBarras.FormatarCompacto(r.RecebidoNoMes), Textos.Moeda(r.RecebidoNoMes));
        _vencido.Atualizar(GraficoBarras.FormatarCompacto(r.AReceberVencido), $"{r.ParcelasVencidas.Count} parcela(s) em atraso",
            r.AReceberVencido > 0 ? Tema.Perigo : Tema.Sucesso);
        _aPagar.Atualizar(GraficoBarras.FormatarCompacto(r.APagarProximos7Dias), "inclui contas vencidas");
        _orcamentos.Atualizar(r.OrcamentosEmAberto.ToString(), $"{Textos.Moeda(r.ValorOrcamentosEmAberto)} em negociação");
        _conversao.Atualizar($"{r.TaxaConversao90Dias:0.#}%", "orçamentos aprovados / decididos");
        _saldoContas.Atualizar(GraficoBarras.FormatarCompacto(r.SaldoContas), "saldo atual de todas as contas",
            r.SaldoContas < 0 ? Tema.Perigo : Tema.Sucesso);
        _estoque.Atualizar(r.ProdutosAbaixoMinimo.ToString(), "produtos abaixo do mínimo", r.ProdutosAbaixoMinimo > 0 ? Tema.Alerta : Tema.Sucesso);

        _grafico.Definir(r.FaturamentoMensal.Select(m => m.Rotulo).ToList(),
            new GraficoBarras.Serie("Contratado", Tema.SerieA, r.FaturamentoMensal.Select(m => m.Valor).ToList()),
            new GraficoBarras.Serie("Recebido", Tema.SerieB, r.FaturamentoMensal.Select(m => m.Valor2).ToList()));

        _proximos.Carregar(r.ProximosEventos);
        _vencidas.Carregar(r.ParcelasVencidas);
        _alertas.Carregar(r.ProdutosEmAlerta);
    });
}
