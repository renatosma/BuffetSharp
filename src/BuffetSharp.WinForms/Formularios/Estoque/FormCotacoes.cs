using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Formularios.Comercial;
using BuffetSharp.WinForms.Impressao;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>
/// Cotações de compra: os mesmos produtos orçados com vários fornecedores (cadastrados ou não), para comprar melhor, por
/// exemplo para um evento grande. No mapa, o menor preço vence cada produto por padrão, mas dá para escolher outro (marca
/// ou qualidade pedida pelo cliente). Ao concluir, vira um pedido de compra por vencedor.
/// </summary>
internal sealed class FormCotacoes : FormListagem<CotacaoResumo>
{
    private readonly IConsultasCompras _consultas;
    private readonly IConsultasLookup _lookups;
    private readonly CotacaoService _servico;
    private readonly BuffetOptions _opcoes;
    private readonly Navegador _navegador;
    private readonly DateTimePicker _de;
    private readonly DateTimePicker _ate;
    private readonly ComboBox _status = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };

    public FormCotacoes(IConsultasCompras consultas, IConsultasLookup lookups, CotacaoService servico, BuffetOptions opcoes, Navegador navegador)
        : base("Cotações de compra", "Orce os produtos com vários fornecedores (não precisam estar cadastrados). No mapa, o menor preço vence por padrão; escolha outro quando o cliente pedir marca ou qualidade. Ao concluir, gera um pedido por fornecedor vencedor.")
    {
        _consultas = consultas;
        _lookups = lookups;
        _servico = servico;
        _opcoes = opcoes;
        _navegador = navegador;

        _de = FiltroData("De", DateTime.Today.AddMonths(-3));
        _ate = FiltroData("Até", null);
        _status.DataSource = new List<object> { "(todas)" }.Concat(Textos.Opcoes<StatusCotacao>()).ToList();
        Filtro("Situação", _status);
        _de.ValueChanged += async (_, _) => await RecarregarAsync();
        _ate.ValueChanged += async (_, _) => await RecarregarAsync();
        _status.SelectionChangeCommitted += async (_, _) => await RecarregarAsync();

        Botao("Nova cotação", Icones.Adicionar, () => EditarAsync(0), destaque: true);
        Botao("Editar", Icones.Editar, () => Selecionado() is { } c ? EditarAsync(c.Id) : Task.CompletedTask, "Produtos e participantes");
        Botao("Mapa de preços", Icones.Calculadora, MapaAsync, "Preços de cada participante e o vencedor de cada produto");
        Botao("Pedir preços (WhatsApp)", Icones.Mensagem, PedirPrecosAsync, "Envia a lista de produtos a um participante");
        Separador();
        Botao("Gerar pedidos", Icones.Confirmar, GerarPedidosAsync, "Conclui a cotação: um pedido de compra por fornecedor vencedor");
        Botao("Cancelar cotação", Icones.Cancelar, CancelarAsync);
        Separador();
        Botao("Imprimir mapa", Icones.Imprimir, ImprimirAsync);

        Grade.Texto("Código", c => c.Codigo, 15)
            .Data("Data", c => c.Data, peso: 14)
            .Texto("Descrição", c => c.Descricao, 40)
            .Texto("Evento", c => c.Evento, 26)
            .Data("Prazo", c => c.PrazoResposta, peso: 14,
                cor: c => c.Status == StatusCotacao.Aberta && c.PrazoResposta < DateTime.Today ? Tema.Perigo : null)
            .Numero("Produtos", c => c.Itens, peso: 12)
            .Numero("Participantes", c => c.Participantes, peso: 14)
            .Texto("Preços", c => c.Respostas, 12)
            .Moeda("Total (vencedores)", c => c.TotalVencedores, 20)
            .Numero("Escolhas", c => c.EscolhasManuais, peso: 11, cor: c => c.EscolhasManuais > 0 ? Tema.Alerta : null)
            .Status("Situação", c => c.Status, 14)
            .Numero("Pedidos", c => c.Pedidos, peso: 10)
            .Busca(c => $"{c.Codigo} {c.Descricao} {c.Evento} {c.Observacoes}");
    }

    protected override async Task CarregarAsync() =>
        Exibir(await _consultas.ListarCotacoesAsync(new FiltroCotacoes
        {
            De = ValorData(_de),
            Ate = ValorData(_ate),
            Status = (_status.SelectedItem as OpcaoEnum<StatusCotacao>)?.Valor
        }));

    protected override string TextoRodape(IReadOnlyList<CotacaoResumo> visiveis) =>
        $"{visiveis.Count} cotação(ões)  •  abertas: {visiveis.Count(c => c.Status == StatusCotacao.Aberta)}";

    protected override Task AoAbrirAsync(CotacaoResumo item) => item.Status == StatusCotacao.Aberta && item.Participantes == 0
        ? EditarAsync(item.Id)
        : AbrirMapaAsync(item.Id);

    private async Task EditarAsync(int id)
    {
        if (await DialogosCotacao.EditarAsync(this, _servico, _lookups, id) is not { } salvo)
            return;
        await CarregarAsync();
        Grade.Selecionar(c => c.Id == salvo);
        if (id == 0 && Selecionado() is { Itens: > 0, Participantes: > 0 } &&
            Mensagens.Confirmar(this, "Cotação gravada. Já tem preços para informar no mapa?", "Mapa de preços"))
            await AbrirMapaAsync(salvo);
    }

    private Task MapaAsync() => Selecionado() is { } cotacao ? AbrirMapaAsync(cotacao.Id) : Task.CompletedTask;

    private async Task AbrirMapaAsync(int id)
    {
        if (!await DialogosCotacao.MapaAsync(this, _servico, id))
            return;
        await CarregarAsync();
        Grade.Selecionar(c => c.Id == id);
    }

    private async Task GerarPedidosAsync()
    {
        if (Selecionado() is not { } cotacao)
            return;
        if (cotacao.Status != StatusCotacao.Aberta)
        {
            Mensagens.Aviso(this, "Selecione uma cotação aberta.");
            return;
        }

        var mapa = await _servico.ObterMapaAsync(cotacao.Id);
        var vencedores = mapa.Itens.GroupBy(mapa.Vencedor).Where(g => g.Key is not null)
            .Select(g =>
            {
                var participante = mapa.Participantes.First(p => p.Id == g.Key);
                var total = g.Sum(i => Math.Round(i.Quantidade * (mapa.Preco(i.ItemId, participante.Id)?.PrecoUnitario ?? 0), 2));
                return (Participante: participante, Produtos: g.Count(), Total: total);
            }).ToList();
        var semPreco = mapa.Itens.Where(i => mapa.Vencedor(i) is null).Select(i => i.Produto).ToList();
        if (semPreco.Count > 0)
        {
            Mensagens.Aviso(this, $"Sem preço para: {string.Join(", ", semPreco)}. Informe no mapa ou retire o produto da cotação.");
            return;
        }

        var resumo = string.Join("\n", vencedores.Select(v => $"• {v.Participante.Nome}: {v.Produtos} produto(s), {Textos.Moeda(v.Total)}"));
        var semCadastro = vencedores.Where(v => v.Participante.FornecedorId is null).Select(v => v.Participante.Nome).ToList();
        var cadastrar = false;
        if (semCadastro.Count > 0)
        {
            if (!Mensagens.Confirmar(this, $"{string.Join(", ", semCadastro)} ainda não {(semCadastro.Count == 1 ? "está cadastrado" : "estão cadastrados")} " +
                                           "como fornecedor. Para virar pedido, o cadastro é obrigatório: cadastrar agora com o nome e o contato " +
                                           "informados na cotação? (Depois você pode completar CNPJ e endereço em Cadastros › Fornecedores.)",
                    "Cadastrar fornecedores"))
                return;
            cadastrar = true;
        }
        if (!Mensagens.Confirmar(this, $"Serão gerados {vencedores.Count} pedido(s) de compra:\n\n{resumo}\n\nA cotação fica concluída.",
                "Gerar pedidos de compra"))
            return;

        var pedidos = await _servico.GerarPedidosAsync(cotacao.Id, cadastrar);
        await CarregarAsync();
        if (Mensagens.Confirmar(this, $"Pedidos gerados: {string.Join(", ", pedidos.Select(p => $"PC-{p:D5}"))}. Abrir os pedidos de compra?",
                "Cotação concluída"))
            _navegador.Abrir<FormPedidosCompra>();
    }

    private async Task CancelarAsync()
    {
        if (Selecionado() is not { } cotacao)
            return;
        var motivo = Dialogos.PedirTexto(this, "Cancelar cotação", "Motivo", subtitulo: $"{cotacao.Codigo} - {cotacao.Descricao}");
        if (motivo is null)
            return;
        await _servico.CancelarAsync(cotacao.Id, motivo);
        await CarregarAsync();
    }

    private async Task PedirPrecosAsync()
    {
        if (Selecionado() is not { } cotacao)
            return;
        var dados = await _servico.ObterAsync(cotacao.Id);
        if (dados.Participantes.Count == 0 || dados.Itens.Count == 0)
        {
            Mensagens.Aviso(this, "Inclua os produtos e os participantes da cotação (Editar).");
            return;
        }
        var opcoes = dados.Participantes.Select(p => new ItemLookup { Id = p.Id, Nome = p.Nome, Info = p.Telefone }).ToList();
        var escolhido = opcoes[0].Id;
        var e = new EditorCampos(1);
        e.Lista("Participante", opcoes, () => escolhido, v => escolhido = v ?? escolhido);
        using (var dialogo = new FormEdicao("Pedir preços", "Para quem enviar a lista de produtos?", e, () => Task.CompletedTask, 460))
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;

        var participante = dados.Participantes.First(p => p.Id == escolhido);
        var mapa = await _servico.ObterMapaAsync(cotacao.Id);
        var texto = $"Olá{(participante.Contato is { } contato ? $", {contato}" : "")}! Aqui é do *{_opcoes.NomeEmpresa}*. " +
                    $"Pode me passar o preço destes produtos (cotação *{dados.Codigo}*)?\n\n" +
                    string.Join("\n", mapa.Itens.Select(i => $"• {i.Produto}: {i.Quantidade:0.###} {i.Unidade}" +
                                                             (i.Especificacao is { } esp ? $" ({esp})" : ""))) +
                    (dados.PrazoResposta is { } prazo ? $"\n\nPreciso da resposta até {prazo:dd/MM/yyyy}." : "") +
                    "\n\nPor favor, informe o preço unitário, o prazo de entrega e a forma de pagamento. Obrigado!";
        DialogosEnvio.WhatsApp(this, new MensagemWhatsApp(participante.Telefone ?? "", texto), $"cotação para {participante.Nome}");
    }

    private async Task ImprimirAsync()
    {
        if (Selecionado() is not { } cotacao)
            return;
        var mapa = await _servico.ObterMapaAsync(cotacao.Id);
        var doc = new DocumentoImpresso($"Mapa da cotação {mapa.Codigo}", _opcoes.NomeEmpresa);
        doc.Campos(3,
            ("Descrição", mapa.Descricao),
            ("Data", cotacao.Data.ToString("dd/MM/yyyy")),
            ("Situação", Textos.Exibir(cotacao.Status)),
            ("Evento", cotacao.Evento ?? "-"),
            ("Participantes", mapa.Participantes.Count.ToString()),
            ("Total pelos vencedores", Textos.Moeda(cotacao.TotalVencedores)));
        string[] cabecalho = ["Produto", "Qtd.", .. mapa.Participantes.Select(p => p.Nome), "Vencedor"];
        var linhas = mapa.Itens.Select(i =>
        {
            var vencedor = mapa.Vencedor(i);
            string[] linha =
            [
                i.Produto + (i.Especificacao is { } esp ? $" ({esp})" : ""),
                $"{i.Quantidade:N3} {i.Unidade}",
                .. mapa.Participantes.Select(p => mapa.Preco(i.ItemId, p.Id) is { PrecoUnitario: > 0 } preco
                    ? preco.PrecoUnitario.ToString("C2") + (p.Id == vencedor ? " ✔" : "")
                    : "-"),
                (mapa.Participantes.FirstOrDefault(p => p.Id == vencedor)?.Nome ?? "-") + (i.Escolhido is not null && i.Motivo is { } motivo ? $" - {motivo}" : "")
            ];
            return linha;
        }).ToList();
        float[] pesos = [3, 1.2f, .. mapa.Participantes.Select(_ => 1.3f), 2.5f];
        bool[] direita = [false, true, .. mapa.Participantes.Select(_ => true), false];
        doc.Tabela(cabecalho, linhas, pesos, direita);
        doc.Paragrafo("Preços por unidade do estoque. ✔ = vencedor do produto.");
        doc.Visualizar(this, paisagem: mapa.Participantes.Count > 3);
    }
}
