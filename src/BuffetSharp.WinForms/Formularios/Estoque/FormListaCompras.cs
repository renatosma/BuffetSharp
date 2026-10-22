using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Lista de compras dos eventos confirmados de um período: consumo previsto pela ficha técnica × pessoas, menos o estoque,
/// na embalagem do fornecedor. Sai impressa por fornecedor ou como pedido pelo WhatsApp.
/// </summary>
internal sealed class FormListaCompras : FormListagem<ItemListaCompras>
{
    private readonly PlanejamentoService _servico;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly CheckBox _reporMinimo = new() { Text = "Repor o estoque mínimo", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
    private ListaCompras? _lista;

    public FormListaCompras(PlanejamentoService servico, BuffetOptions opcoes, Navegador navegador)
        : base("Lista de compras", "O que comprar para os eventos confirmados do período: ficha técnica × pessoas, menos o que já há em estoque. O estoque só é baixado quando o evento é realizado.")
    {
        _servico = servico;
        _opcoes = opcoes;
        _navegador = navegador;

        _de = FiltroData("Eventos de", DateTime.Today, opcional: false);
        _ate = FiltroData("até", DateTime.Today.AddDays(7), opcional: false);
        Filtros.Controls.Add(_reporMinimo);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _reporMinimo.CheckedChanged += async (_, _) => await RecarregarAsync();

        Botao("Imprimir por fornecedor", Icones.Imprimir, ImprimirAsync, "Lista agrupada por fornecedor", destaque: true);
        Botao("Gerar pedido de compra", Icones.Documento, GerarPedidoAsync, "Cria o pedido de compra com os itens do fornecedor selecionado");
        Botao("Pedido por WhatsApp", Icones.Mensagem, PedidoWhatsAppAsync, "Envia ao fornecedor do item selecionado os itens dele");
        Botao("Eventos considerados", Icones.Bandeira, EventosAsync);

        Grade.Texto("Fornecedor", i => i.Fornecedor, 32)
            .Texto("Produto", i => i.Produto, 40)
            .Texto("Categoria", i => i.Categoria, 22)
            .Numero("Necessário", i => i.Necessidade, "N3", 18)
            .Numero("Em estoque", i => i.Estoque, "N3", 18, i => i.Estoque < 0 ? Tema.Perigo : null)
            .Numero("Comprar", i => i.AComprar, "N3", 18, _ => Tema.Primaria)
            .Texto("Un.", i => i.Unidade, 8)
            .Texto("Embalagem sugerida", i => i.Embalagem, 26)
            .Moeda("Custo estimado", i => i.CustoEstimado, 20)
            .Texto("Eventos", i => string.Join(", ", i.Eventos), 30)
            .Busca(i => $"{i.Fornecedor} {i.Produto} {i.Categoria} {string.Join(" ", i.Eventos)}");
    }

    private DateOnly De => DateOnly.FromDateTime(_de.Value);
    private DateOnly Ate => DateOnly.FromDateTime(_ate.Value);

    protected override async Task CarregarAsync()
    {
        _lista = await _servico.ListaComprasAsync(De, Ate, _reporMinimo.Checked);
        Exibir(_lista.Itens);
    }

    protected override string TextoRodape(IReadOnlyList<ItemListaCompras> visiveis)
    {
        if (_lista is null)
            return "";
        var semPacote = _lista.SemPacote.Count();
        return $"{visiveis.Count} item(ns) a comprar  •  custo estimado: {Textos.Moeda(visiveis.Sum(i => i.CustoEstimado))}  •  " +
               $"{_lista.Eventos.Count} evento(s) no período ({_lista.Eventos.Sum(e => e.Pessoas):N0} pessoas)" +
               (semPacote > 0 ? $"  •  ⚠ {semPacote} evento(s) sem pacote não entram na conta" : "");
    }

    private Task ImprimirAsync()
    {
        if (_lista is null || Grade.Visiveis.Count() == 0)
            Mensagens.Aviso(this, "Não há nada a comprar no período.");
        else
            ImpressaoOperacao.PedidoCompra(this, _lista, Grade.Visiveis.ToList(), _opcoes.NomeEmpresa);
        return Task.CompletedTask;
    }

    private async Task GerarPedidoAsync()
    {
        if (Selecionado() is not { } item)
            return;
        if (item.FornecedorId is not { } fornecedorId)
        {
            Mensagens.Aviso(this, "Este produto não tem fornecedor padrão. Cadastre o fornecedor no produto ou faça o pedido em Estoque › Pedidos de compra.");
            return;
        }
        var itens = Grade.Visiveis.Where(i => i.FornecedorId == fornecedorId).ToList();
        if (!Mensagens.Confirmar(this, $"Criar um pedido de compra para {item.Fornecedor} com {itens.Count} item(ns) da lista " +
                                       $"(cerca de {Textos.Moeda(itens.Sum(i => i.CustoEstimado))})? Você poderá revisar quantidades e custos.",
                "Gerar pedido de compra"))
            return;
        var compras = _navegador.Servico<CompraService>();
        var id = await compras.GerarDaListaAsync(fornecedorId, itens);
        await DialogosPedidoCompra.EditarAsync(this, compras, _navegador.Servico<Application.Consultas.IConsultasLookup>(), id);
        _navegador.Abrir<FormPedidosCompra>();
    }

    private Task PedidoWhatsAppAsync()
    {
        if (Selecionado() is not { } item)
            return Task.CompletedTask;
        var itens = Grade.Visiveis.Where(i => i.FornecedorId == item.FornecedorId).ToList();
        var mensagem = new MensagemWhatsApp(item.TelefoneFornecedor ?? "", ImpressaoOperacao.TextoPedido(itens, _opcoes.NomeEmpresa));
        DialogosEnvio.WhatsApp(this, mensagem, $"pedido para {item.Fornecedor}");
        return Task.CompletedTask;
    }

    private Task EventosAsync()
    {
        if (_lista is null)
            return Task.CompletedTask;
        var grade = new Grade<EventoPlanejado>();
        grade.Texto("Código", e => e.Codigo, 18).Data("Data", e => e.Inicio, "dd/MM/yyyy HH:mm", 22).Texto("Evento", e => e.Titulo, 50)
            .Numero("Pessoas", e => e.Pessoas, peso: 14).Texto("Pacote", e => e.Pacote ?? "(sem pacote - fora da conta)", 30,
                e => e.Pacote is null ? Tema.Alerta : null);
        grade.Carregar(_lista.Eventos);
        grade.Controle.Height = Px(320);
        var e = new EditorCampos(1);
        e.Livre(grade.Controle);
        using var dialogo = new FormEdicao("Eventos considerados", $"Confirmados de {De:dd/MM/yyyy} a {Ate:dd/MM/yyyy}", e,
            () => Task.CompletedTask, 820, somenteLeitura: true);
        dialogo.ShowDialog(this);
        return Task.CompletedTask;
    }
}
