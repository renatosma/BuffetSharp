using System.Net;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Desconto acima da alçada de quem está salvando: a tela pede o motivo e, se houver alguém com alçada por perto, o login e a
/// senha dele para aprovar na hora.
/// </summary>
public sealed class AlcadaDescontoException(decimal percentual, decimal limite, decimal valor, string mensagem) : RegraNegocioException(mensagem)
{
    public decimal Percentual { get; } = percentual;
    public decimal Limite { get; } = limite;

    /// <summary>Desconto em reais (no orçamento, abaixo da tabela; no recebimento, o maior desconto dado).</summary>
    public decimal Valor { get; } = valor;
}

public sealed record AprovacaoDescontoResumo(int OrcamentoId, string Codigo, string Cliente, DateTime DataEvento, string? Vendedor,
    string? SolicitadoPor, DateTime? SolicitadoEm, decimal ValorTabela, decimal ValorTotal, decimal Percentual, string? Motivo,
    bool PodeAprovar)
{
    public decimal Valor => Math.Max(0, ValorTabela - ValorTotal);
}

public sealed record DescontoOrcamento(int OrcamentoId, string Codigo, DateTime CriadoEm, string Cliente, DateTime DataEvento,
    int? VendedorId, string? Vendedor, StatusOrcamento Status, decimal ValorTabela, decimal ValorTotal, decimal PercentualCampo,
    SituacaoDesconto Situacao, string? ConcedidoPor, string? DecididoPor, string? Motivo, string? Resposta)
{
    public decimal Valor => ValorTabela <= 0 ? 0 : Math.Max(0, ValorTabela - ValorTotal);
    public decimal Percentual => ValorTabela <= 0 ? 0 : Math.Round(Valor / ValorTabela * 100, 2);

    /// <summary>Passou da alçada de quem deu (aguardando, aprovado ou recusado).</summary>
    public bool PassouDaAlcada => Situacao != SituacaoDesconto.DentroDaAlcada;
}

public sealed record DescontosVendedor(string Vendedor, int Orcamentos, int ComDesconto, decimal ValorTabela, decimal Valor,
    decimal MaiorPercentual, int PassaramDaAlcada, int Aprovados, int Recusados, int Aguardando, string? Aprovadores)
{
    /// <summary>Desconto médio ponderado pelo valor: quanto ficou abaixo da tabela, somando todos os orçamentos.</summary>
    public decimal PercentualMedio => ValorTabela <= 0 ? 0 : Math.Round(Valor / ValorTabela * 100, 2);
}

public sealed record DescontoRecebimento(int BaixaId, DateOnly Data, string Descricao, string? Usuario, string? AprovadoPor,
    string? Motivo, decimal Valor, decimal Desconto)
{
    public decimal Percentual => Valor <= 0 ? 0 : Math.Round(Desconto / Valor * 100, 2);
}

/// <summary>
/// Alçada de desconto: cada usuário pode dar até o seu limite (do cadastro ou do perfil). Acima dele, o orçamento fica
/// aguardando a aprovação de quem tem alçada (pela fila, com lembrete e aviso por e-mail/WhatsApp) ou é aprovado na hora com
/// a senha de quem pode; no recebimento, só com a senha. O desconto do orçamento é o efetivo: o campo de desconto somado ao
/// preço negociado abaixo da tabela.
/// </summary>
public sealed class DescontoService(
    IBuffetDbContextFactory fabrica,
    IHasherSenha hasher,
    ISessaoUsuario sessao,
    IEnvioEmail email,
    IEnvioWhatsApp whatsApp,
    BuffetOptions opcoes,
    TimeProvider relogio,
    ILogger<DescontoService> log)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    internal decimal Limite(Usuario usuario) => usuario.DescontoMaximo ?? opcoes.AlcadaDesconto.Limite(usuario.Perfil);

    /// <summary>Limite de quem está logado; sem sessão (rotinas automáticas), não há limite.</summary>
    internal async Task<decimal> LimiteDaSessaoAsync(IBuffetDbContext db, CancellationToken ct)
    {
        if (sessao.Usuario is not { } logado)
            return 100;
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == logado.Id, ct);
        return usuario is null ? opcoes.AlcadaDesconto.Limite(logado.Perfil) : Limite(usuario);
    }

    public async Task<decimal> MeuLimiteAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await LimiteDaSessaoAsync(db, ct);
    }

    /// <summary>
    /// Confere login e senha de quem aprova na hora (num contexto à parte, para a senha errada contar mesmo sem gravar o
    /// resto) e devolve o login e o limite dele.
    /// </summary>
    internal async Task<(string Login, decimal Limite)> AutorizarAsync(string? login, string? senha, CancellationToken ct)
    {
        Validador.Garantir(!string.IsNullOrWhiteSpace(login) && !string.IsNullOrEmpty(senha), "Informe o login e a senha de quem aprova o desconto.");
        await using var db = fabrica.Criar();
        var normalizado = login!.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Login == normalizado && u.Ativo, ct)
                      ?? throw new RegraNegocioException("O login ou a senha de quem aprova não conferem.");
        Validador.Garantir(!usuario.EstaBloqueado(Agora), $"O login de {usuario.Nome} está bloqueado por senhas erradas.");
        if (!hasher.Verificar(senha!, usuario.SenhaHash))
        {
            usuario.RegistrarFalha(Agora);
            await db.SaveChangesAsync(ct);
            log.LogWarning("Senha errada na aprovação de desconto por {Login}", usuario.Login);
            throw new RegraNegocioException("O login ou a senha de quem aprova não conferem.");
        }
        return (usuario.Login, Limite(usuario));
    }

    /// <summary>
    /// Confere a alçada do orçamento que está sendo salvo. Acima do limite e sem pedido, avisa a tela
    /// (<see cref="AlcadaDescontoException"/>); com o pedido, aprova na hora (com a senha) ou deixa aguardando.
    /// </summary>
    /// <returns>Verdadeiro se o orçamento passou a aguardar aprovação (avisar os aprovadores depois de gravar).</returns>
    internal async Task<bool> ConferirOrcamentoAsync(IBuffetDbContext db, Orcamento orcamento, PedidoDesconto? pedido, CancellationToken ct)
    {
        var limite = await LimiteDaSessaoAsync(db, ct);
        var usuario = sessao.Usuario?.Login;
        if (orcamento.ConferirAlcada(limite, usuario, Agora) != ConferenciaAlcada.PrecisaAprovacao)
            return false;

        var percentual = orcamento.DescontoEfetivoPercentual;
        if (pedido is null)
            throw new AlcadaDescontoException(percentual, limite, orcamento.DescontoEfetivoValor,
                $"O desconto efetivo de {percentual:0.##}% sobre a tabela passa da sua alçada ({limite:0.##}%).");

        if (!pedido.AprovarNaHora)
        {
            orcamento.SolicitarAprovacaoDesconto(pedido.Motivo, usuario, Agora);
            return true;
        }
        Validador.Garantir(!string.IsNullOrWhiteSpace(pedido.Motivo), "Informe o motivo do desconto acima da sua alçada.");
        var (aprovador, limiteAprovador) = await AutorizarAsync(pedido.Login, pedido.Senha, ct);
        Validador.Garantir(percentual <= limiteAprovador,
            $"O desconto de {percentual:0.##}% também passa da alçada de {aprovador} ({limiteAprovador:0.##}%).");
        orcamento.SolicitarAprovacaoDesconto(pedido.Motivo, usuario, Agora);
        orcamento.AprovarDesconto(aprovador, limiteAprovador, "Aprovado na hora, com a senha", Agora);
        return false;
    }

    /// <summary>Desconto no recebimento acima da alçada de quem registra: só com a senha de quem pode (e o motivo).</summary>
    internal async Task ConferirRecebimentoAsync(IBuffetDbContext db, Baixa baixa, PedidoDesconto? pedido, CancellationToken ct)
    {
        var percentual = baixa.MaiorDescontoPercentual;
        if (baixa.Tipo != TipoBaixa.Recebimento || percentual == 0)
            return;
        var limite = await LimiteDaSessaoAsync(db, ct);
        if (percentual <= limite)
            return;
        if (pedido is not { AprovarNaHora: true })
            throw new AlcadaDescontoException(percentual, limite, baixa.Titulos.Sum(t => t.Desconto),
                $"O desconto de {percentual:0.##}% no recebimento passa da sua alçada ({limite:0.##}%): precisa da senha de quem pode aprovar.");
        Validador.Garantir(!string.IsNullOrWhiteSpace(pedido.Motivo), "Informe o motivo do desconto acima da sua alçada.");
        var (aprovador, limiteAprovador) = await AutorizarAsync(pedido.Login, pedido.Senha, ct);
        Validador.Garantir(percentual <= limiteAprovador,
            $"O desconto de {percentual:0.##}% também passa da alçada de {aprovador} ({limiteAprovador:0.##}%).");
        baixa.AprovarDesconto(aprovador, pedido.Motivo);
    }

    // ---------------- Fila de aprovação ----------------

    /// <summary>Descontos aguardando aprovação, com a indicação de quais o usuário logado pode aprovar.</summary>
    public async Task<IReadOnlyList<AprovacaoDescontoResumo>> PendentesAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var limite = await LimiteDaSessaoAsync(db, ct);
        var pendentes = await db.Orcamentos.AsNoTracking()
            .Where(o => o.SituacaoDesconto == SituacaoDesconto.Aguardando)
            .OrderBy(o => o.DescontoConcedidoEm)
            .Select(o => new
            {
                o.Id, Cliente = o.Cliente!.Nome, o.Inicio, Vendedor = o.Vendedor != null ? o.Vendedor.Nome : null, o.DescontoConcedidoPor,
                o.DescontoConcedidoEm, o.ValorTabela, o.ValorTotal, o.PercentualDescontoAutorizado, o.MotivoDesconto
            }).ToListAsync(ct);
        return pendentes.Select(o => new AprovacaoDescontoResumo(o.Id, $"ORC-{o.Id:D5}", o.Cliente, o.Inicio, o.Vendedor,
            o.DescontoConcedidoPor, o.DescontoConcedidoEm, o.ValorTabela, o.ValorTotal, o.PercentualDescontoAutorizado, o.MotivoDesconto,
            o.PercentualDescontoAutorizado <= limite)).ToList();
    }

    public Task AprovarAsync(int orcamentoId, string? observacao, CancellationToken ct = default) =>
        DecidirAsync(orcamentoId, (o, login, limite) => o.AprovarDesconto(login, limite, observacao, Agora), ct);

    public Task RecusarAsync(int orcamentoId, string? motivo, CancellationToken ct = default) =>
        DecidirAsync(orcamentoId, (o, login, _) => o.RecusarDesconto(login, motivo, Agora), ct);

    private async Task DecidirAsync(int orcamentoId, Action<Orcamento, string, decimal> decidir, CancellationToken ct)
    {
        var logado = sessao.Usuario ?? throw new RegraNegocioException("Nenhum usuário autenticado.");
        await using var db = fabrica.Criar();
        var orcamento = await db.Orcamentos.Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == orcamentoId, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");
        var limite = await LimiteDaSessaoAsync(db, ct);
        Validador.Garantir(orcamento.PercentualDescontoAutorizado <= limite,
            $"O desconto de {orcamento.PercentualDescontoAutorizado:0.##}% passa da sua alçada ({limite:0.##}%).");
        decidir(orcamento, logado.Login, limite);
        await db.SaveChangesAsync(ct);
        await AvisarSolicitanteAsync(db, orcamento, ct);
    }

    /// <summary>Lembrete de aprovação para quem tem alçada sobre o desconto pedido.</summary>
    public async Task<IReadOnlyList<Lembrete>> LembretesAsync(CancellationToken ct = default) =>
        (await PendentesAsync(ct)).Where(p => p.PodeAprovar).Select(p => new Lembrete
        {
            Tipo = TipoLembrete.AprovacaoDesconto,
            ReferenciaId = p.OrcamentoId,
            Data = p.SolicitadoEm ?? Agora,
            Titulo = $"{p.Codigo}: desconto de {p.Percentual:0.##}% aguardando a sua aprovação",
            Detalhe = $"{p.Cliente} • pedido por {p.SolicitadoPor ?? "?"}: {p.Motivo}",
            Pessoa = p.Cliente,
            Valor = p.Valor,
            Chave = $"desconto:{p.OrcamentoId}:{p.Percentual:0.##}"
        }).ToList();

    // ---------------- Avisos ----------------

    /// <summary>
    /// Avisa por e-mail e WhatsApp (quando o envio é direto) quem pode aprovar o desconto pedido. Falhas de envio não
    /// impedem nada: o pedido continua na fila e nos lembretes.
    /// </summary>
    /// <returns>Quantas pessoas foram avisadas.</returns>
    public async Task<int> AvisarAprovadoresAsync(int orcamentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var orcamento = await db.Orcamentos.AsNoTracking().Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == orcamentoId, ct);
        if (orcamento is not { SituacaoDesconto: SituacaoDesconto.Aguardando })
            return 0;
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => u.Ativo && (u.Email != null || u.Celular != null)).ToListAsync(ct);
        var texto = $"""
            Pedido de aprovação de desconto no {orcamento.Codigo} ({orcamento.Cliente?.Nome}, evento em {orcamento.DataEvento:dd/MM/yyyy}).
            Desconto efetivo de {orcamento.PercentualDescontoAutorizado:0.##}% sobre a tabela ({Moeda(orcamento.DescontoEfetivoValor)}): tabela {Moeda(orcamento.ValorTabela)}, proposta {Moeda(orcamento.ValorTotal)}.
            Pedido por {orcamento.DescontoConcedidoPor}: {orcamento.MotivoDesconto}
            Aprove ou recuse em Comercial › Aprovações de desconto.
            """;
        var avisados = 0;
        foreach (var usuario in usuarios.Where(u => u.Login != orcamento.DescontoConcedidoPor && Limite(u) >= orcamento.PercentualDescontoAutorizado))
            if (await AvisarAsync(usuario, $"Aprovação de desconto: {orcamento.Codigo}", texto, ct))
                avisados++;
        return avisados;
    }

    private async Task AvisarSolicitanteAsync(IBuffetDbContext db, Orcamento orcamento, CancellationToken ct)
    {
        if (orcamento.DescontoConcedidoPor is not { } login || login == sessao.Usuario?.Login)
            return;
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login && u.Ativo, ct);
        if (usuario is null)
            return;
        var aprovado = orcamento.SituacaoDesconto == SituacaoDesconto.Aprovado;
        var texto = $"""
            O desconto de {orcamento.PercentualDescontoAutorizado:0.##}% no {orcamento.Codigo} ({orcamento.Cliente?.Nome}) foi {(aprovado ? "aprovado" : "recusado")} por {orcamento.DescontoDecididoPor}.
            {(orcamento.RespostaDesconto is { } resposta ? (aprovado ? "Observação: " : "Motivo: ") + resposta : "")}
            {(aprovado ? "A proposta já pode ser enviada ao cliente." : "Baixe o desconto para poder enviar a proposta.")}
            """;
        await AvisarAsync(usuario, $"Desconto {(aprovado ? "aprovado" : "recusado")}: {orcamento.Codigo}", texto, ct);
    }

    private async Task<bool> AvisarAsync(Usuario usuario, string assunto, string texto, CancellationToken ct)
    {
        var avisado = false;
        try
        {
            if (usuario.Email is { } destino)
            {
                var corpo = "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2b2b2b\">" +
                            string.Concat(texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>")) +
                            $"<p style=\"color:#6e6a66\">{WebUtility.HtmlEncode(opcoes.NomeEmpresa)}</p></div>";
                await email.EnviarAsync(new MensagemEmail(destino, assunto, corpo, []), ct);
                avisado = true;
            }
            if (usuario.Celular is { } celular && whatsApp.Direto)
            {
                await whatsApp.EnviarAsync(celular, $"*{assunto}*\n{texto.Trim()}", ct: ct);
                avisado = true;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Não foi possível avisar {Login} sobre o desconto", usuario.Login);
        }
        return avisado;
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", PrecoCompraService.PtBr);

    // ---------------- Relatórios ----------------

    /// <summary>Orçamentos criados no período, com o desconto efetivo sobre a tabela e a alçada.</summary>
    public async Task<IReadOnlyList<DescontoOrcamento>> OrcamentosAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var (inicio, fim) = (de.ToDateTime(TimeOnly.MinValue), ate.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var linhas = await db.Orcamentos.AsNoTracking()
            .Where(o => o.CriadoEm >= inicio && o.CriadoEm < fim)
            .OrderBy(o => o.CriadoEm)
            .Select(o => new
            {
                o.Id, o.CriadoEm, Cliente = o.Cliente!.Nome, o.Inicio, o.VendedorId, Vendedor = o.Vendedor != null ? o.Vendedor.Nome : null,
                o.Status, o.ValorTabela, o.ValorTotal, o.PercentualDesconto, o.SituacaoDesconto, o.DescontoConcedidoPor,
                o.DescontoDecididoPor, o.MotivoDesconto, o.RespostaDesconto
            }).ToListAsync(ct);
        return linhas.Select(o => new DescontoOrcamento(o.Id, $"ORC-{o.Id:D5}", o.CriadoEm, o.Cliente, o.Inicio, o.VendedorId, o.Vendedor,
            o.Status, o.ValorTabela, o.ValorTotal, o.PercentualDesconto, o.SituacaoDesconto, o.DescontoConcedidoPor, o.DescontoDecididoPor,
            o.MotivoDesconto, o.RespostaDesconto)).ToList();
    }

    /// <summary>Resumo por vendedor: quanto ficou abaixo da tabela e quantos descontos passaram da alçada.</summary>
    public static IReadOnlyList<DescontosVendedor> PorVendedor(IEnumerable<DescontoOrcamento> orcamentos) =>
        orcamentos.GroupBy(o => o.Vendedor ?? "(sem vendedor)")
            .Select(g => new DescontosVendedor(g.Key, g.Count(), g.Count(o => o.Valor > 0), g.Sum(o => o.ValorTabela > 0 ? o.ValorTabela : o.ValorTotal),
                g.Sum(o => o.Valor), g.Max(o => o.Percentual), g.Count(o => o.PassouDaAlcada),
                g.Count(o => o.Situacao == SituacaoDesconto.Aprovado), g.Count(o => o.Situacao == SituacaoDesconto.Recusado),
                g.Count(o => o.Situacao == SituacaoDesconto.Aguardando),
                g.Select(o => o.DecididoPor).OfType<string>().Distinct().Order().ToList() is { Count: > 0 } aprovadores ? string.Join(", ", aprovadores) : null))
            .OrderByDescending(v => v.PercentualMedio).ToList();

    /// <summary>Recebimentos do período com desconto, com quem deu e quem autorizou.</summary>
    public async Task<IReadOnlyList<DescontoRecebimento>> RecebimentosAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var baixas = await db.Baixas.AsNoTracking().Include(b => b.Titulos).ThenInclude(t => t.Parcela!).ThenInclude(p => p.Evento!).ThenInclude(e => e.Cliente)
            .Where(b => b.Tipo == TipoBaixa.Recebimento && b.Data >= de && b.Data <= ate && b.Titulos.Any(t => t.Desconto > 0))
            .OrderBy(b => b.Data).ToListAsync(ct);
        return baixas.SelectMany(b => b.Titulos.Where(t => t.Desconto > 0 && t.Parcela is not null).Select(t => new DescontoRecebimento(b.Id, b.Data,
            $"{t.Parcela!.Evento?.Cliente?.Nome} - {t.Parcela.Evento?.Codigo} {t.Parcela.Descricao}", b.Usuario, b.DescontoAprovadoPor,
            b.MotivoDesconto, t.Valor, t.Desconto))).ToList();
    }
}
