using System.Net.Mail;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Domain.Estoque;

public enum StatusCotacao
{
    /// <summary>Recebendo preços dos participantes.</summary>
    Aberta,

    /// <summary>Vencedores escolhidos e pedidos de compra gerados.</summary>
    Concluida,

    Cancelada
}

/// <summary>
/// Cotação de compra: os mesmos produtos orçados com vários participantes (fornecedores cadastrados ou não), para comprar
/// melhor, por exemplo para um evento grande. O vencedor é escolhido por produto: por padrão o menor preço, mas pode ser
/// outro (marca ou qualidade pedida pelo cliente), com o motivo. Ao concluir, gera um pedido de compra por vencedor; só
/// então o participante precisa estar cadastrado como fornecedor.
/// </summary>
public class CotacaoCompra : Entidade
{
    public const int TamanhoDescricao = 150;

    private readonly List<CotacaoItem> _itens = [];
    private readonly List<CotacaoParticipante> _participantes = [];
    private readonly List<CotacaoPreco> _precos = [];

    private CotacaoCompra()
    {
    }

    public CotacaoCompra(string descricao, DateOnly data)
    {
        Alterar(descricao, data, null, null, null);
    }

    public string Codigo => EhNovo ? "(nova)" : $"COT-{Id:D5}";

    public string Descricao { get; private set; } = string.Empty;
    public DateOnly Data { get; private set; }

    /// <summary>Até quando os participantes podem mandar preço.</summary>
    public DateOnly? PrazoResposta { get; private set; }

    /// <summary>Evento para o qual se compra (opcional).</summary>
    public int? EventoId { get; private set; }
    public Evento? Evento { get; private set; }

    public string? Observacoes { get; private set; }
    public StatusCotacao Status { get; private set; } = StatusCotacao.Aberta;
    public string? MotivoCancelamento { get; private set; }
    public DateTime? ConcluidaEm { get; private set; }

    public IReadOnlyCollection<CotacaoItem> Itens => _itens;
    public IReadOnlyCollection<CotacaoParticipante> Participantes => _participantes;
    public IReadOnlyCollection<CotacaoPreco> Precos => _precos;

    /// <summary>Total da compra pelos vencedores escolhidos.</summary>
    public decimal TotalVencedores => _itens.Sum(i => i.Vencedor is { } v ? PrecoDe(i, v)?.Total ?? 0 : 0);

    public void Alterar(string descricao, DateOnly data, DateOnly? prazoResposta, int? eventoId, string? observacoes)
    {
        GarantirAberta();
        Validador.Garantir(!string.IsNullOrWhiteSpace(descricao), "Informe a descrição da cotação (ex.: compras do casamento de sábado).");
        Validador.Garantir(descricao.Trim().Length <= TamanhoDescricao, $"A descrição pode ter no máximo {TamanhoDescricao} caracteres.");
        Validador.Garantir(prazoResposta is null || prazoResposta >= data, "O prazo de resposta não pode ser antes da cotação.");
        Validador.Garantir(observacoes is null || observacoes.Trim().Length <= 1000, "As observações podem ter no máximo 1.000 caracteres.");
        Descricao = descricao.Trim();
        Data = data;
        PrazoResposta = prazoResposta;
        EventoId = eventoId is > 0 ? eventoId : null;
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
    }

    /// <summary>
    /// Substitui os produtos cotados (quantidade na unidade do estoque). Produtos que continuam guardam os preços já
    /// informados; os retirados levam os seus preços junto.
    /// </summary>
    public void DefinirItens(IEnumerable<(Produto Produto, decimal Quantidade, string? Especificacao)> itens)
    {
        GarantirAberta();
        var novos = itens.ToList();
        Validador.Garantir(novos.Count > 0, "Inclua ao menos um produto na cotação.");
        Validador.Garantir(novos.Select(n => n.Produto).Distinct().Count() == novos.Count, "Cada produto pode aparecer uma vez só na cotação.");
        foreach (var (produto, quantidade, especificacao) in novos)
        {
            Validador.Garantir(quantidade > 0, $"Informe a quantidade de '{produto.Nome}'.");
            Validador.Garantir(especificacao is null || especificacao.Trim().Length <= CotacaoItem.TamanhoEspecificacao,
                $"A especificação de '{produto.Nome}' pode ter no máximo {CotacaoItem.TamanhoEspecificacao} caracteres.");
        }

        foreach (var retirado in _itens.Where(i => novos.All(n => !MesmoProduto(i, n.Produto))).ToList())
        {
            _precos.RemoveAll(p => p.Item == retirado);
            _itens.Remove(retirado);
        }
        foreach (var (produto, quantidade, especificacao) in novos)
        {
            var item = _itens.FirstOrDefault(i => MesmoProduto(i, produto));
            if (item is null)
                _itens.Add(new CotacaoItem(produto, quantidade, especificacao));
            else
                item.Alterar(quantidade, especificacao);
        }
    }

    private static bool MesmoProduto(CotacaoItem item, Produto produto) =>
        item.Produto == produto || (produto.Id > 0 && item.ProdutoId == produto.Id);

    public CotacaoParticipante AdicionarParticipante(string nome, string? contato, string? telefone, string? email, int? fornecedorId)
    {
        GarantirAberta();
        var participante = new CotacaoParticipante();
        participante.Alterar(nome, contato, telefone, email, fornecedorId);
        Validador.Garantir(_participantes.All(p => !p.Nome.Equals(participante.Nome, StringComparison.CurrentCultureIgnoreCase)),
            $"'{participante.Nome}' já participa da cotação.");
        Validador.Garantir(fornecedorId is null || _participantes.All(p => p.FornecedorId != fornecedorId),
            "Este fornecedor já participa da cotação.");
        _participantes.Add(participante);
        return participante;
    }

    public void AlterarParticipante(CotacaoParticipante participante, string nome, string? contato, string? telefone, string? email,
        int? fornecedorId)
    {
        GarantirAberta();
        GarantirParticipante(participante);
        Validador.Garantir(_participantes.All(p => p == participante || !p.Nome.Equals(nome.Trim(), StringComparison.CurrentCultureIgnoreCase)),
            $"'{nome.Trim()}' já participa da cotação.");
        Validador.Garantir(fornecedorId is null || _participantes.All(p => p == participante || p.FornecedorId != fornecedorId),
            "Este fornecedor já participa da cotação.");
        participante.Alterar(nome, contato, telefone, email, fornecedorId);
    }

    /// <summary>Retira o participante com os preços dele; os produtos que ele ganhava voltam ao menor preço.</summary>
    public void RemoverParticipante(CotacaoParticipante participante)
    {
        GarantirAberta();
        GarantirParticipante(participante);
        _precos.RemoveAll(p => p.Participante == participante);
        foreach (var item in _itens.Where(i => i.Vencedor == participante))
            item.LimparVencedor();
        _participantes.Remove(participante);
        AtualizarVencedores();
    }

    /// <summary>Condições da proposta do participante (prazo de entrega, pagamento, observações).</summary>
    public void DefinirCondicoes(CotacaoParticipante participante, DateOnly? previsaoEntrega, string? condicaoPagamento, string? observacoes)
    {
        GarantirAberta();
        GarantirParticipante(participante);
        participante.DefinirCondicoes(previsaoEntrega, condicaoPagamento, observacoes);
    }

    /// <summary>Preço unitário (na unidade do estoque) que o participante deu para o produto. Nulo = não cotou.</summary>
    public void DefinirPreco(CotacaoItem item, CotacaoParticipante participante, decimal? precoUnitario, string? observacao)
    {
        GarantirAberta();
        Validador.Garantir(_itens.Contains(item), "O produto não é desta cotação.");
        GarantirParticipante(participante);
        var atual = PrecoDe(item, participante);
        if (precoUnitario is null)
        {
            if (atual is not null)
                _precos.Remove(atual);
            return;
        }

        Validador.Garantir(precoUnitario > 0, $"O preço de '{item.Produto?.Nome}' precisa ser maior que zero (deixe em branco se não cotou).");
        if (atual is null)
            _precos.Add(new CotacaoPreco(item, participante, precoUnitario.Value, observacao));
        else
            atual.Alterar(precoUnitario.Value, observacao);
    }

    public CotacaoPreco? PrecoDe(CotacaoItem item, CotacaoParticipante participante) =>
        _precos.FirstOrDefault(p => p.Item == item && p.Participante == participante);

    /// <summary>Menor preço do produto (empate: o participante incluído primeiro).</summary>
    public CotacaoPreco? MenorPreco(CotacaoItem item) =>
        _precos.Where(p => p.Item == item)
            .OrderBy(p => p.PrecoUnitario)
            .ThenBy(p => _participantes.IndexOf(p.Participante!))
            .FirstOrDefault();

    /// <summary>
    /// Vencedores automáticos ficam com o menor preço. Uma escolha manual continua valendo enquanto o escolhido tiver preço
    /// para o produto.
    /// </summary>
    public void AtualizarVencedores()
    {
        foreach (var item in _itens)
        {
            if (item.EscolhaManual && item.Vencedor is { } escolhido && PrecoDe(item, escolhido) is not null)
                continue;
            item.DefinirVencedor(MenorPreco(item)?.Participante, manual: false, motivo: null);
        }
    }

    /// <summary>Volta todos os produtos ao menor preço, desfazendo as escolhas manuais.</summary>
    public void EscolherMenoresPrecos()
    {
        GarantirAberta();
        foreach (var item in _itens)
            item.DefinirVencedor(MenorPreco(item)?.Participante, manual: false, motivo: null);
    }

    /// <summary>
    /// Escolhe quem fornece o produto. Se não for o menor preço, o motivo é obrigatório (ex.: marca pedida pelo cliente,
    /// qualidade, prazo). Participante nulo volta ao menor preço.
    /// </summary>
    public void EscolherVencedor(CotacaoItem item, CotacaoParticipante? participante, string? motivo)
    {
        GarantirAberta();
        Validador.Garantir(_itens.Contains(item), "O produto não é desta cotação.");
        var menor = MenorPreco(item);
        if (participante is null || participante == menor?.Participante)
        {
            item.DefinirVencedor(menor?.Participante, manual: false, motivo: null);
            return;
        }

        GarantirParticipante(participante);
        var preco = PrecoDe(item, participante);
        Validador.Garantir(preco is not null, $"{participante.Nome} não deu preço para '{item.Produto?.Nome}'.");
        Validador.Garantir(menor is null || preco!.PrecoUnitario <= menor.PrecoUnitario || !string.IsNullOrWhiteSpace(motivo),
            $"'{item.Produto?.Nome}': {participante.Nome} não tem o menor preço. Informe o motivo da escolha (ex.: marca pedida pelo cliente).");
        Validador.Garantir(motivo is null || motivo.Trim().Length <= CotacaoItem.TamanhoMotivo,
            $"O motivo pode ter no máximo {CotacaoItem.TamanhoMotivo} caracteres.");
        item.DefinirVencedor(participante, manual: true, motivo);
    }

    /// <summary>Vencedores que ainda não estão cadastrados como fornecedor (precisam estar para virar pedido).</summary>
    public IReadOnlyList<CotacaoParticipante> VencedoresSemCadastro() =>
        _itens.Select(i => i.Vencedor).OfType<CotacaoParticipante>().Distinct().Where(p => p.FornecedorId is null).ToList();

    /// <summary>
    /// Conclui a cotação com um pedido de compra por vencedor (cada um com os produtos que ganhou, ao preço cotado). Todo
    /// produto precisa de vencedor e todo vencedor precisa estar cadastrado como fornecedor.
    /// </summary>
    public IReadOnlyList<PedidoCompra> GerarPedidos(DateOnly hoje, DateTime agora)
    {
        GarantirAberta();
        Validador.Garantir(_itens.Count > 0, "A cotação não tem produtos.");
        var semVencedor = _itens.Where(i => i.Vencedor is null).Select(i => i.Produto?.Nome).ToList();
        Validador.Garantir(semVencedor.Count == 0,
            $"Sem preço (ou sem vencedor) para: {string.Join(", ", semVencedor)}. Informe o preço ou retire o produto da cotação.");
        var semCadastro = VencedoresSemCadastro();
        Validador.Garantir(semCadastro.Count == 0,
            $"Cadastre como fornecedor antes de gerar os pedidos: {string.Join(", ", semCadastro.Select(p => p.Nome))}.");

        var pedidos = new List<PedidoCompra>();
        foreach (var grupo in _itens.GroupBy(i => i.Vencedor!))
        {
            var participante = grupo.Key;
            var fornecedorId = participante.FornecedorId!.Value;
            var pedido = new PedidoCompra(fornecedorId, hoje);
            var previsao = participante.PrevisaoEntrega is { } p && p >= hoje ? p : (DateOnly?)null;
            var observacoes = $"Gerado pela cotação {Codigo} - {Descricao}." +
                              (participante.CondicaoPagamento is { } condicao ? $" Pagamento: {condicao}." : "");
            pedido.Alterar(fornecedorId, hoje, previsao, CategoriaDespesa.Insumos, observacoes);
            pedido.DefinirItens(grupo.Select(i => (i.Produto!, (ProdutoEmbalagem?)null, i.Quantidade, PrecoDe(i, participante)!.PrecoUnitario)));
            pedido.VincularCotacao(this);
            pedidos.Add(pedido);
        }

        Status = StatusCotacao.Concluida;
        ConcluidaEm = agora;
        return pedidos;
    }

    public void Cancelar(string motivo)
    {
        GarantirAberta();
        Validador.Garantir(!string.IsNullOrWhiteSpace(motivo), "Informe o motivo do cancelamento.");
        Status = StatusCotacao.Cancelada;
        MotivoCancelamento = motivo.Trim()[..Math.Min(motivo.Trim().Length, 500)];
    }

    private void GarantirParticipante(CotacaoParticipante participante) =>
        Validador.Garantir(_participantes.Contains(participante), "O participante não é desta cotação.");

    private void GarantirAberta() =>
        Validador.Garantir(Status == StatusCotacao.Aberta,
            Status == StatusCotacao.Concluida ? "A cotação já foi concluída (os pedidos foram gerados)." : "A cotação foi cancelada.");
}

/// <summary>Produto cotado, na unidade do estoque, com o vencedor escolhido.</summary>
public class CotacaoItem
{
    public const int TamanhoEspecificacao = 200;
    public const int TamanhoMotivo = 300;

    private CotacaoItem()
    {
    }

    internal CotacaoItem(Produto produto, decimal quantidade, string? especificacao)
    {
        Produto = produto;
        ProdutoId = produto.Id;
        Alterar(quantidade, especificacao);
    }

    public int Id { get; private set; }
    public int CotacaoCompraId { get; private set; }
    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Quantidade na unidade do estoque.</summary>
    public decimal Quantidade { get; private set; }

    /// <summary>Marca, qualidade ou detalhe pedido (ex.: "picanha maturada", "marca X a pedido do cliente").</summary>
    public string? Especificacao { get; private set; }

    public int? VencedorId { get; private set; }
    public CotacaoParticipante? Vencedor { get; private set; }

    /// <summary>Vencedor escolhido pelo usuário (não segue o menor preço quando os preços mudam).</summary>
    public bool EscolhaManual { get; private set; }

    /// <summary>Por que o vencedor não é o menor preço.</summary>
    public string? MotivoEscolha { get; private set; }

    internal void Alterar(decimal quantidade, string? especificacao)
    {
        Quantidade = quantidade;
        Especificacao = string.IsNullOrWhiteSpace(especificacao) ? null : especificacao.Trim();
    }

    internal void DefinirVencedor(CotacaoParticipante? participante, bool manual, string? motivo)
    {
        Vencedor = participante;
        if (participante is null)
            VencedorId = null;
        EscolhaManual = manual && participante is not null;
        MotivoEscolha = EscolhaManual && !string.IsNullOrWhiteSpace(motivo) ? motivo.Trim() : null;
    }

    internal void LimparVencedor() => DefinirVencedor(null, false, null);
}

/// <summary>Quem foi consultado: um fornecedor cadastrado ou só um nome e contato (o cadastro é exigido ao virar pedido).</summary>
public class CotacaoParticipante
{
    public const int TamanhoNome = 150;

    internal CotacaoParticipante()
    {
    }

    public int Id { get; private set; }
    public int CotacaoCompraId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Contato { get; private set; }
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }

    public int? FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }

    public DateOnly? PrevisaoEntrega { get; private set; }
    public string? CondicaoPagamento { get; private set; }
    public string? Observacoes { get; private set; }

    internal void Alterar(string nome, string? contato, string? telefone, string? email, int? fornecedorId)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(nome), "Informe o nome do participante (fornecedor, loja, atacadista...).");
        Validador.Garantir(nome.Trim().Length <= TamanhoNome, $"O nome pode ter no máximo {TamanhoNome} caracteres.");
        Validador.Garantir(contato is null || contato.Trim().Length <= 100, "O contato pode ter no máximo 100 caracteres.");
        Validador.Garantir(telefone is null || telefone.Trim().Length <= 20, "O telefone pode ter no máximo 20 caracteres.");
        Validador.Garantir(string.IsNullOrWhiteSpace(email) || (email.Trim().Length <= 150 && MailAddress.TryCreate(email.Trim(), out _)),
            $"E-mail inválido para '{nome.Trim()}'.");
        Nome = nome.Trim();
        Contato = string.IsNullOrWhiteSpace(contato) ? null : contato.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (fornecedorId != FornecedorId)
            Fornecedor = null;
        FornecedorId = fornecedorId is > 0 ? fornecedorId : null;
    }

    internal void DefinirCondicoes(DateOnly? previsaoEntrega, string? condicaoPagamento, string? observacoes)
    {
        Validador.Garantir(condicaoPagamento is null || condicaoPagamento.Trim().Length <= 100,
            "A condição de pagamento pode ter no máximo 100 caracteres.");
        Validador.Garantir(observacoes is null || observacoes.Trim().Length <= 500, "As observações podem ter no máximo 500 caracteres.");
        PrevisaoEntrega = previsaoEntrega;
        CondicaoPagamento = string.IsNullOrWhiteSpace(condicaoPagamento) ? null : condicaoPagamento.Trim();
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
    }

    /// <summary>Cadastra o participante como fornecedor, com os dados dele, e liga os dois (o Id vem ao gravar).</summary>
    public Fornecedor CadastrarFornecedor()
    {
        Validador.Garantir(FornecedorId is null && Fornecedor is null, $"{Nome} já está cadastrado como fornecedor.");
        Fornecedor = new Fornecedor { RazaoSocial = Nome, Contato = Contato, Telefone = Telefone, Email = Email };
        return Fornecedor;
    }
}

/// <summary>Preço que um participante deu para um produto da cotação.</summary>
public class CotacaoPreco
{
    public const int TamanhoObservacao = 200;

    private CotacaoPreco()
    {
    }

    internal CotacaoPreco(CotacaoItem item, CotacaoParticipante participante, decimal precoUnitario, string? observacao)
    {
        Item = item;
        Participante = participante;
        Alterar(precoUnitario, observacao);
    }

    public int Id { get; private set; }
    public int CotacaoCompraId { get; private set; }
    public int CotacaoItemId { get; private set; }
    public CotacaoItem? Item { get; private set; }
    public int CotacaoParticipanteId { get; private set; }
    public CotacaoParticipante? Participante { get; private set; }

    /// <summary>Preço de uma unidade do estoque.</summary>
    public decimal PrecoUnitario { get; private set; }

    /// <summary>Marca, qualidade, validade... do que foi oferecido.</summary>
    public string? Observacao { get; private set; }

    public decimal Total => Math.Round((Item?.Quantidade ?? 0) * PrecoUnitario, 2);

    internal void Alterar(decimal precoUnitario, string? observacao)
    {
        Validador.Garantir(observacao is null || observacao.Trim().Length <= TamanhoObservacao,
            $"A observação do preço pode ter no máximo {TamanhoObservacao} caracteres.");
        PrecoUnitario = precoUnitario;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }
}
