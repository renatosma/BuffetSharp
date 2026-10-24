using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class OrcamentoService(
    IBuffetDbContextFactory fabrica,
    DisponibilidadeService disponibilidade,
    BuffetOptions opcoes,
    ISessaoUsuario sessao,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    public OrcamentoInput Novo()
    {
        var data = Hoje.AddMonths(2);
        var usuario = sessao.Usuario;
        return new OrcamentoInput
        {
            Data = data,
            ValidoAte = Hoje.AddDays(opcoes.DiasValidadeOrcamento),
            VendedorId = usuario is not null && usuario.PossuiPerfil(Domain.Seguranca.PerfilUsuario.Comercial) ? usuario.Id : null
        };
    }

    public async Task<OrcamentoInput> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var o = await db.Orcamentos.AsNoTracking()
                    .Include(x => x.Servicos)
                    .Include(x => x.Envios)
                    .Include(x => x.Evento)
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Orçamento não encontrado.");

        return new OrcamentoInput
        {
            Id = o.Id,
            Versao = o.Versao,
            Codigo = o.Codigo,
            Status = o.Status,
            MotivoRecusa = o.MotivoRecusa,
            EventoId = o.Evento?.Id,
            ClienteId = o.ClienteId,
            TipoEventoId = o.TipoEventoId,
            EspacoId = o.EspacoId,
            PacoteId = o.PacoteId,
            VendedorId = o.VendedorId,
            Data = o.DataEvento,
            HoraInicio = o.HoraInicio,
            HoraFim = o.HoraFim,
            Convidados = o.QuantidadeConvidados,
            ConvidadosMeia = o.ConvidadosMeia,
            ConvidadosIsentos = o.ConvidadosIsentos,
            ValidoAte = o.ValidoAte,
            ValorPorPessoa = o.ValorPorPessoa,
            PercentualMeia = o.PercentualMeia,
            IdadeMaximaIsento = o.IdadeMaximaIsento,
            IdadeMaximaMeia = o.IdadeMaximaMeia,
            ValorEspaco = o.ValorEspaco,
            PercentualDesconto = o.PercentualDesconto,
            CondicaoPagamentoId = o.CondicaoPagamentoId,
            FormaPagamentoId = o.FormaPagamentoId,
            Observacoes = o.Observacoes,
            Servicos = o.Servicos.Select(s => new ServicoItemInput
            {
                ServicoAdicionalId = s.ServicoAdicionalId ?? 0,
                Descricao = s.Descricao,
                Quantidade = s.Quantidade,
                ValorUnitario = s.ValorUnitario
            }).ToList(),
            Envios = o.Envios.OrderByDescending(e => e.EnviadoEm)
                .Select(e => new EnvioPropostaInfo(e.Canal, e.Destinatario, e.Usuario, e.EnviadoEm)).ToList()
        };
    }

    public async Task<int> SalvarAsync(OrcamentoInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();

        Orcamento orcamento;
        if (input.Id == 0)
        {
            orcamento = new Orcamento(input.ClienteId, input.TipoEventoId, input.Data, input.HoraInicio, input.HoraFim,
                input.QuantidadesPorFaixa, input.ValidoAte);
            db.Orcamentos.Add(orcamento);
        }
        else
        {
            orcamento = await db.Orcamentos.Include(o => o.Servicos).FirstOrDefaultAsync(o => o.Id == input.Id, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");
            Concorrencia.Conferir(orcamento, input.Versao);
        }

        orcamento.AlterarDados(input.ClienteId, input.TipoEventoId, input.Data, input.HoraInicio, input.HoraFim,
            input.QuantidadesPorFaixa, input.ValidoAte, input.Observacoes);

        var pacote = input.PacoteId is { } pacoteId ? await db.Pacotes.FindAsync([pacoteId], ct) : null;
        orcamento.DefinirPacote(pacote, pacote is null ? null : input.ValorPorPessoa, pacote is null ? null : input.PercentualMeia);

        var espaco = input.EspacoId is { } espacoId ? await db.Espacos.FindAsync([espacoId], ct) : null;
        orcamento.DefinirEspaco(espaco, espaco is null ? null : input.ValorEspaco);

        var idsServicos = input.Servicos.Select(s => s.ServicoAdicionalId).Distinct().ToList();
        var servicos = await db.ServicosAdicionais.Where(s => idsServicos.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        orcamento.DefinirServicos(input.Servicos.Select(s =>
            (servicos.GetValueOrDefault(s.ServicoAdicionalId) ?? throw new RegraNegocioException("Serviço não encontrado."),
                s.Quantidade, s.ValorUnitario)));

        orcamento.AplicarDesconto(input.PercentualDesconto);

        if (input.CondicaoPagamentoId != orcamento.CondicaoPagamentoId)
        {
            if (input.CondicaoPagamentoId is { } condicaoId)
            {
                var condicao = await db.CondicoesPagamento.FindAsync([condicaoId], ct);
                Validador.Garantir(condicao?.Ativo == true, "A condição de pagamento não existe ou está inativa.");
            }
            orcamento.DefinirCondicaoPagamento(input.CondicaoPagamentoId);
        }

        if (input.FormaPagamentoId != orcamento.FormaPagamentoId)
        {
            await GarantirFormaAtivaAsync(db, input.FormaPagamentoId, ct);
            orcamento.DefinirFormaPagamento(input.FormaPagamentoId);
        }

        if (input.VendedorId != orcamento.VendedorId)
        {
            if (input.VendedorId is { } vendedorId)
            {
                var vendedor = await db.Usuarios.FindAsync([vendedorId], ct);
                Validador.Garantir(vendedor?.PodeVender == true, "O vendedor deve ser um usuário ativo com perfil Comercial ou Administrador.");
            }
            orcamento.DefinirVendedor(input.VendedorId);
        }

        await db.SaveChangesAsync(ct);
        input.Versao = orcamento.Versao;
        return orcamento.Id;
    }

    public Task EnviarAsync(int id, CancellationToken ct = default) =>
        AlterarAsync(id, o => o.Enviar(Agora), ct);

    public Task RecusarAsync(int id, string motivo, CancellationToken ct = default) =>
        AlterarAsync(id, o => o.Recusar(motivo, Agora), ct);

    public Task ReabrirAsync(int id, CancellationToken ct = default) =>
        AlterarAsync(id, o => o.Reabrir(), ct);

    /// <summary>Aprova o orçamento e gera o evento. Retorna o Id do evento criado.</summary>
    public async Task<int> AprovarAsync(int id, string tituloEvento, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var orcamento = await db.Orcamentos.Include(o => o.Servicos).FirstOrDefaultAsync(o => o.Id == id, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");

        if (orcamento.EspacoId is { } espacoId)
            await disponibilidade.GarantirDisponivelAsync(espacoId, orcamento.Inicio, orcamento.Fim, null, ct);

        var comissao = orcamento.VendedorId is { } vendedorId
            ? (await db.Usuarios.FindAsync([vendedorId], ct))?.PercentualComissao ?? 0
            : 0;
        var evento = orcamento.Aprovar(tituloEvento, Agora, comissao);
        await Checklists.AplicarSugeridoAsync(db, evento, ct);
        db.Eventos.Add(evento);
        await db.SaveChangesAsync(ct);
        return evento.Id;
    }

    public async Task<int> DuplicarAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var original = await db.Orcamentos.AsNoTracking().Include(o => o.Servicos).FirstOrDefaultAsync(o => o.Id == id, ct)
                       ?? throw new RegraNegocioException("Orçamento não encontrado.");
        var copia = original.Duplicar(Hoje, opcoes.DiasValidadeOrcamento);
        db.Orcamentos.Add(copia);
        await db.SaveChangesAsync(ct);
        return copia.Id;
    }

    public async Task ExcluirAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var orcamento = await db.Orcamentos.FirstOrDefaultAsync(o => o.Id == id, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");
        Validador.Garantir(orcamento.Status is StatusOrcamento.Rascunho or StatusOrcamento.Recusado,
            "Somente orçamentos em rascunho ou recusados podem ser excluídos.");
        db.Orcamentos.Remove(orcamento);
        await db.SaveChangesAsync(ct);
    }

    private async Task AlterarAsync(int id, Action<Orcamento> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var orcamento = await db.Orcamentos.Include(o => o.Servicos).FirstOrDefaultAsync(o => o.Id == id, ct)
                        ?? throw new RegraNegocioException("Orçamento não encontrado.");
        acao(orcamento);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task GarantirFormaAtivaAsync(IBuffetDbContext db, int? formaPagamentoId, CancellationToken ct)
    {
        if (formaPagamentoId is not { } id)
            return;
        var forma = await db.FormasPagamento.FindAsync([id], ct);
        Validador.Garantir(forma?.Ativo == true, "A forma de pagamento não existe ou está inativa.");
    }
}
