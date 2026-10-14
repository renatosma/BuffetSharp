using BuffetSharp.Application.Consultas;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>Visão 360º do cliente: totais, orçamentos e eventos (consulta Dapper com múltiplos resultados).</summary>
internal sealed class FormHistoricoCliente : FormBase
{
    private readonly IConsultasComercial _consultas;
    private readonly int _clienteId;
    private readonly FlowLayoutPanel _indicadores = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 4, 14, 4) };
    private readonly Grade<OrcamentoResumo> _orcamentos = new();
    private readonly Grade<EventoResumo> _eventos = new();

    public FormHistoricoCliente(IConsultasComercial consultas, int clienteId, string nomeCliente)
    {
        _consultas = consultas;
        _clienteId = clienteId;
        Text = $"Histórico - {nomeCliente}";
        ClientSize = new Size(1000, 640);
        MinimizeBox = false;

        _orcamentos.Texto("Código", o => o.Codigo, 20).Data("Data", o => o.Inicio, peso: 20).Texto("Tipo", o => o.TipoEvento, 30)
            .Numero("Convidados", o => o.QuantidadeConvidados, peso: 18).Moeda("Valor", o => o.ValorTotal, 25).Status("Status", o => o.Status, 20);
        _eventos.Texto("Código", e => e.Codigo, 20).Data("Data", e => e.Inicio, peso: 20).Texto("Título", e => e.Titulo, 50)
            .Moeda("Valor", e => e.ValorTotal, 25).Moeda("Recebido", e => e.Recebido, 25).Moeda("Saldo", e => e.SaldoAReceber, 25)
            .Status("Status", e => e.Status, 20);

        var abas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        abas.TabPages.Add(new TabPage("Eventos") { Controls = { _eventos.Controle }, Padding = new Padding(8) });
        abas.TabPages.Add(new TabPage("Orçamentos") { Controls = { _orcamentos.Controle }, Padding = new Padding(8) });

        var cabecalho = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(18, 14, 18, 4), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        cabecalho.Controls.Add(Tema.Titulo(nomeCliente));
        cabecalho.Controls.Add(Tema.Subtitulo("Relacionamento com o buffet"));

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 6, 16, 16) };
        corpo.Controls.Add(abas);
        Controls.Add(corpo);
        Controls.Add(_indicadores);
        Controls.Add(cabecalho);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(async () =>
        {
            var historico = await _consultas.HistoricoClienteAsync(_clienteId);
            _indicadores.Controls.AddRange(
            [
                new Controles.CartaoIndicador("Eventos", historico.QuantidadeEventos.ToString(), Tema.Primaria),
                new Controles.CartaoIndicador("Total contratado", Textos.Moeda(historico.TotalContratado), Tema.Info),
                new Controles.CartaoIndicador("Total recebido", Textos.Moeda(historico.TotalRecebido), Tema.Sucesso),
                new Controles.CartaoIndicador("Em aberto", Textos.Moeda(historico.SaldoEmAberto), historico.SaldoEmAberto > 0 ? Tema.Alerta : Tema.Neutro),
                new Controles.CartaoIndicador("Último evento", historico.UltimoEvento?.ToString("dd/MM/yyyy") ?? "-", Tema.Destaque)
            ]);
            _orcamentos.Carregar(historico.Orcamentos);
            _eventos.Carregar(historico.Eventos);
        });
    }
}
