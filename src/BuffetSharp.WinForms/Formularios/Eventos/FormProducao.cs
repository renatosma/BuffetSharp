using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Eventos;

/// <summary>Produção da cozinha: eventos confirmados do dia com os pratos e os insumos já multiplicados pelas pessoas.</summary>
internal sealed class FormProducao : FormListagem<OrdemProducao>
{
    private readonly PlanejamentoService _servico;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _data;

    public FormProducao(PlanejamentoService servico, BuffetOptions opcoes, Navegador navegador)
        : base("Produção da cozinha", "Ordens de produção dos eventos confirmados do dia: cada prato do pacote com as porções e os insumos (ficha técnica × pessoas).")
    {
        _servico = servico;
        _opcoes = opcoes;
        _navegador = navegador;

        _data = FiltroData("Dia", DateTime.Today, opcional: false);
        _data.ValueChanged += async (_, _) => await RecarregarAsync();

        Botao("Imprimir ordens do dia", Icones.Imprimir, () =>
        {
            ImpressaoOperacao.OrdensProducao(this, Grade.Visiveis.ToList(), _opcoes.NomeEmpresa);
            return Task.CompletedTask;
        }, destaque: true);
        Botao("Imprimir selecionada", Icones.Pagina, () =>
        {
            if (Selecionado() is { } ordem)
                ImpressaoOperacao.OrdensProducao(this, [ordem], _opcoes.NomeEmpresa);
            return Task.CompletedTask;
        });
        Botao("Abrir evento", Icones.Editar, () => Selecionado() is { } o ? AoAbrirAsync(o) : Task.CompletedTask);

        Grade.Texto("Código", o => o.Codigo, 18)
            .Data("Início", o => o.Inicio, "HH:mm", 12)
            .Texto("Evento", o => o.Titulo, 45)
            .Texto("Espaço", o => o.Espaco, 22)
            .Texto("Pessoas", o => o.Convidados, 40)
            .Texto("Pacote", o => o.Pacote ?? "(sem pacote)", 22, o => o.Pacote is null ? Tema.Alerta : null)
            .Numero("Pratos", o => o.Pratos.Count, peso: 12)
            .Busca(o => $"{o.Codigo} {o.Titulo} {o.Espaco} {o.Pacote}");
    }

    protected override async Task CarregarAsync() => Exibir(await _servico.OrdensDoDiaAsync(DateOnly.FromDateTime(_data.Value)));

    protected override string TextoRodape(IReadOnlyList<OrdemProducao> visiveis) =>
        visiveis.Count == 0 ? "Nenhum evento confirmado neste dia." : $"{visiveis.Count} evento(s)  •  {visiveis.Sum(o => o.Pessoas):N0} pessoas a servir";

    protected override Task AoAbrirAsync(OrdemProducao item)
    {
        _navegador.Abrir($"evento:{item.EventoId}", () => _navegador.Criar<FormEventoDetalhe>(item.EventoId));
        return Task.CompletedTask;
    }
}
