using System.Globalization;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Infrastructure.Persistencia;

namespace BuffetSharp.Infrastructure.Consultas;

internal sealed class ConsultasLeads(FabricaConexao conexoes) : IConsultasLeads
{
    public async Task<IReadOnlyList<LeadResumo>> ListarAsync(FiltroLeads filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT l.Id, l.Nome, l.Telefone, l.Email, l.Origem, l.DetalheOrigem, l.TipoEventoId, t.Nome AS TipoEvento, l.DataPrevista,
                   l.Convidados,
                   l.Status, l.ProximoContato, u.Nome AS Responsavel, l.ResponsavelId, l.CriadoEm, l.ClienteId, c.Nome AS Cliente,
                   l.MotivoPerda,
                   (SELECT MAX(i.Data) FROM LeadInteracoes i WHERE i.LeadId = l.Id) AS UltimoContato,
                   (SELECT COUNT(*) FROM LeadInteracoes i WHERE i.LeadId = l.Id) AS Contatos
              FROM Leads l
              LEFT JOIN TiposEvento t ON t.Id = l.TipoEventoId
              LEFT JOIN Usuarios u ON u.Id = l.ResponsavelId
              LEFT JOIN Clientes c ON c.Id = l.ClienteId
             WHERE (@Status IS NULL OR l.Status = @Status)
               AND (@SomenteEmAberto = 0 OR l.Status IN ('Novo', 'EmContato'))
               AND (@RetornoAte IS NULL OR l.ProximoContato <= @RetornoAte)
               AND (@CriadoDe IS NULL OR l.CriadoEm >= @CriadoDe)
               AND (@CriadoAteExclusivo IS NULL OR l.CriadoEm < @CriadoAteExclusivo)
             ORDER BY CASE WHEN l.ProximoContato IS NULL THEN 1 ELSE 0 END, l.ProximoContato, l.CriadoEm DESC
            """;
        return await conexoes.ListarAsync<LeadResumo>(sql, new
        {
            Status = filtro.Status?.ToString(),
            SomenteEmAberto = filtro.SomenteEmAberto ? 1 : 0,
            RetornoAte = filtro.RetornoAte?.Date,
            CriadoDe = filtro.CriadoDe?.Date,
            CriadoAteExclusivo = Filtros.FimDoDia(filtro.CriadoAte)
        }, ct);
    }

    public async Task<IReadOnlyList<InteracaoLeadResumo>> InteracoesAsync(int leadId, CancellationToken ct = default) =>
        await conexoes.ListarAsync<InteracaoLeadResumo>(
            "SELECT Data, Usuario, Descricao FROM LeadInteracoes WHERE LeadId = @LeadId ORDER BY Data DESC, Id DESC",
            new { LeadId = leadId }, ct);

    private sealed class LinhaFunil
    {
        public string Origem { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int Eventos { get; init; }
        public decimal Valor { get; init; }
    }

    /// <summary>Leads criados no período por canal, com quantos viraram clientes e quantos eventos esses clientes fecharam.</summary>
    public async Task<IReadOnlyList<FunilOrigem>> FunilPorOrigemAsync(DateTime de, DateTime ate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT l.Origem, l.Status,
                   (SELECT COUNT(*) FROM Eventos e
                     WHERE e.ClienteId = l.ClienteId AND e.CriadoEm >= l.CriadoEm AND e.Status <> 'Cancelado') AS Eventos,
                   COALESCE((SELECT SUM(e.ValorTotal) FROM Eventos e
                              WHERE e.ClienteId = l.ClienteId AND e.CriadoEm >= l.CriadoEm AND e.Status <> 'Cancelado'), 0) AS Valor
              FROM Leads l
             WHERE l.CriadoEm >= @De AND l.CriadoEm < @AteExclusivo
            """;
        var linhas = await conexoes.ListarAsync<LinhaFunil>(sql, new { De = de.Date, AteExclusivo = ate.Date.AddDays(1) }, ct);
        return linhas
            .GroupBy(l => Enum.Parse<OrigemLead>(l.Origem))
            .Select(g => new FunilOrigem
            {
                Origem = g.Key,
                Leads = g.Count(),
                EmAberto = g.Count(l => l.Status is nameof(StatusLead.Novo) or nameof(StatusLead.EmContato)),
                Convertidos = g.Count(l => l.Status == nameof(StatusLead.Convertido)),
                Perdidos = g.Count(l => l.Status == nameof(StatusLead.Perdido)),
                EventosFechados = g.Sum(l => l.Eventos),
                ValorFechado = g.Sum(l => l.Valor)
            })
            .OrderByDescending(f => f.Leads)
            .ToList();
    }
}

/// <summary>
/// Lembretes do dia, calculados dos próprios dados em uma ida ao banco (lote de consultas). Datas de aniversário e
/// semanas de cobrança são resolvidas em C#, sem depender de funções de data de cada banco.
/// </summary>
internal sealed class ConsultasLembretes(FabricaConexao conexoes) : IConsultasLembretes
{
    private sealed class Linha
    {
        public int Id { get; init; }
        public DateTime Data { get; init; }
        public decimal? Valor { get; init; }
        public string? Descricao { get; init; }
        public int? EventoId { get; init; }
        public string? Evento { get; init; }
        public int? ClienteId { get; init; }
        public string? Nome { get; init; }
        public string? Celular { get; init; }
        public string? Telefone { get; init; }
        public string? Email { get; init; }
        public string? TipoEvento { get; init; }
        public DateTime? Inicio { get; init; }
        public int? Quantidade { get; init; }
        public string? Status { get; init; }
    }

    public async Task<IReadOnlyList<Lembrete>> CalcularAsync(DateTime agora, LembretesOptionsConsulta opcoes, CancellationToken ct = default)
    {
        var hoje = agora.Date;
        await using var lote = await conexoes.LoteAsync(
        [
            """
            SELECT o.Id, o.ValidoAte AS Data, o.ValorTotal AS Valor, o.Inicio, o.ClienteId, c.Nome, c.Celular, c.Telefone, c.Email,
                   t.Nome AS TipoEvento
              FROM Orcamentos o
              JOIN Clientes c ON c.Id = o.ClienteId
              JOIN TiposEvento t ON t.Id = o.TipoEventoId
             WHERE o.Status = 'Enviado' AND o.ValidoAte >= @Hoje AND o.ValidoAte <= @LimiteOrcamento
            """,
            """
            SELECT d.Id, d.DataHora AS Data, d.Pessoas AS Quantidade, d.ClienteId, c.Nome, c.Celular, c.Telefone, c.Email
              FROM Degustacoes d
              JOIN Clientes c ON c.Id = d.ClienteId
             WHERE d.Status = 'Agendada' AND d.DataHora >= @Hoje AND d.DataHora < @DepoisDeAmanha
            """,
            """
            SELECT c.Id, c.DataNascimento AS Data, c.Id AS ClienteId, c.Nome, c.Celular, c.Telefone, c.Email
              FROM Clientes c
             WHERE c.Ativo = {verdadeiro} AND c.DataNascimento IS NOT NULL
            """,
            """
            SELECT p.Id, p.Vencimento AS Data, p.Valor, p.Descricao, p.EventoId, e.Titulo AS Evento, e.ClienteId, c.Nome,
                   c.Celular, c.Telefone, c.Email
              FROM Parcelas p
              JOIN Eventos e ON e.Id = p.EventoId
              JOIN Clientes c ON c.Id = e.ClienteId
             WHERE p.Status = 'Aberto' AND e.Status <> 'Cancelado' AND p.Vencimento <= @LimiteParcela
            """,
            """
            SELECT l.Id, COALESCE(l.ProximoContato, l.CriadoEm) AS Data, l.Nome, l.Telefone AS Celular, l.Email, l.Status,
                   t.Nome AS TipoEvento
              FROM Leads l
              LEFT JOIN TiposEvento t ON t.Id = l.TipoEventoId
             WHERE (l.Status = 'EmContato' AND l.ProximoContato <= @Hoje)
                OR (l.Status = 'Novo' AND (l.ProximoContato IS NULL OR l.ProximoContato <= @Hoje))
            """,
            """
            SELECT cp.Id, cp.Vencimento AS Data, cp.Valor, cp.Descricao, cp.EventoId, f.RazaoSocial AS Nome, f.Telefone, f.Email
              FROM ContasPagar cp
              LEFT JOIN Fornecedores f ON f.Id = cp.FornecedorId
             WHERE cp.Status = 'Aberto' AND cp.Vencimento <= @LimiteContaPagar
            """,
            """
            SELECT m.Id, e.Fim AS Data, m.Quantidade, mt.Nome AS Descricao, e.Id AS EventoId, e.Titulo AS Evento
              FROM EventoMateriais m
              JOIN Eventos e ON e.Id = m.EventoId
              JOIN Materiais mt ON mt.Id = m.MaterialId
             WHERE m.DevolvidoEm IS NULL AND e.Status <> 'Cancelado' AND e.Fim < @Agora
            """,
            """
            SELECT t.Id, t.Prazo AS Data, t.Descricao, t.Responsavel AS Nome, e.Id AS EventoId, e.Titulo AS Evento, e.Inicio
              FROM EventoTarefas t
              JOIN Eventos e ON e.Id = t.EventoId
             WHERE t.ConcluidaEm IS NULL AND e.Status <> 'Cancelado' AND t.Prazo <= @Hoje AND t.Prazo >= @TarefasDesde
            """
        ], new
        {
            Hoje = hoje,
            Agora = agora,
            TarefasDesde = hoje.AddDays(-30),
            DepoisDeAmanha = hoje.AddDays(2),
            LimiteOrcamento = hoje.AddDays(Math.Max(0, opcoes.DiasOrcamento)),
            LimiteParcela = hoje.AddDays(Math.Max(0, opcoes.DiasParcela)),
            LimiteContaPagar = hoje.AddDays(Math.Max(0, opcoes.DiasContaPagar))
        }, ct);

        var orcamentos = await lote.LerAsync<Linha>();
        var degustacoes = await lote.LerAsync<Linha>();
        var aniversariantes = await lote.LerAsync<Linha>();
        var parcelas = await lote.LerAsync<Linha>();
        var leads = await lote.LerAsync<Linha>();
        var contas = await lote.LerAsync<Linha>();
        var materiais = await lote.LerAsync<Linha>();
        var tarefas = await lote.LerAsync<Linha>();

        var lembretes = new List<Lembrete>();
        static string Dia(DateTime data) => data.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        lembretes.AddRange(orcamentos.Select(o => new Lembrete
        {
            Tipo = TipoLembrete.OrcamentoVencendo,
            ReferenciaId = o.Id,
            Data = o.Data,
            Titulo = $"Proposta ORC-{o.Id:D5} vence {(o.Data.Date == hoje ? "hoje" : $"em {o.Data:dd/MM}")}",
            Detalhe = $"{o.TipoEvento} em {o.Inicio:dd/MM/yyyy}",
            Pessoa = o.Nome, Telefone = o.Celular ?? o.Telefone, Email = o.Email, Valor = o.Valor, ClienteId = o.ClienteId,
            Chave = $"ORC:{o.Id}:{Dia(o.Data)}"
        }));

        lembretes.AddRange(degustacoes.Select(d => new Lembrete
        {
            Tipo = TipoLembrete.Degustacao,
            ReferenciaId = d.Id,
            Data = d.Data,
            Titulo = $"Degustação {(d.Data.Date == hoje ? "hoje" : "amanhã")} às {d.Data:HH:mm}",
            Detalhe = $"{d.Quantidade} pessoa(s)",
            Pessoa = d.Nome, Telefone = d.Celular ?? d.Telefone, Email = d.Email, ClienteId = d.ClienteId,
            Chave = $"DEG:{d.Id}:{d.Data:yyyyMMddHHmm}"
        }));

        foreach (var c in aniversariantes)
        {
            // Quem nasceu em 29/02 comemora em 28/02 nos anos que não são bissextos.
            var dia = Math.Min(c.Data.Day, DateTime.DaysInMonth(hoje.Year, c.Data.Month));
            if (c.Data.Month != hoje.Month || dia != hoje.Day)
                continue;
            lembretes.Add(new Lembrete
            {
                Tipo = TipoLembrete.Aniversario,
                ReferenciaId = c.Id,
                Data = hoje,
                Titulo = "Aniversário de cliente",
                Detalhe = c.Data.Year > 1900 ? $"{hoje.Year - c.Data.Year} anos" : null,
                Pessoa = c.Nome, Telefone = c.Celular ?? c.Telefone, Email = c.Email, ClienteId = c.Id,
                Chave = $"ANIV:{c.Id}:{hoje.Year}"
            });
        }

        foreach (var p in parcelas)
        {
            var vencida = p.Data.Date < hoje;
            var semana = $"{ISOWeek.GetYear(hoje)}{ISOWeek.GetWeekOfYear(hoje):D2}";
            lembretes.Add(new Lembrete
            {
                Tipo = vencida ? TipoLembrete.ParcelaVencida : TipoLembrete.ParcelaAVencer,
                ReferenciaId = p.Id,
                Data = p.Data,
                Titulo = vencida
                    ? $"Parcela vencida há {(hoje - p.Data.Date).Days} dia(s)"
                    : $"Parcela vence {(p.Data.Date == hoje ? "hoje" : $"em {p.Data:dd/MM}")}",
                Detalhe = $"{p.Descricao} • EVT-{p.EventoId:D5} {p.Evento}",
                Pessoa = p.Nome, Telefone = p.Celular ?? p.Telefone, Email = p.Email, Valor = p.Valor, EventoId = p.EventoId,
                ClienteId = p.ClienteId,
                // Cobrança de parcela vencida volta a aparecer toda semana enquanto não for paga.
                Chave = vencida ? $"VENC:{p.Id}:{semana}" : $"PARC:{p.Id}:{Dia(p.Data)}"
            });
        }

        lembretes.AddRange(leads.Select(l => new Lembrete
        {
            Tipo = TipoLembrete.RetornoLead,
            ReferenciaId = l.Id,
            Data = l.Data,
            Titulo = l.Status == nameof(StatusLead.Novo) ? "Lead novo aguardando o primeiro contato" : "Retorno combinado com lead",
            Detalhe = l.TipoEvento,
            Pessoa = l.Nome, Telefone = l.Celular, Email = l.Email,
            Chave = $"LEAD:{l.Id}:{Dia(l.Data)}"
        }));

        lembretes.AddRange(contas.Select(c => new Lembrete
        {
            Tipo = TipoLembrete.ContaPagarAVencer,
            ReferenciaId = c.Id,
            Data = c.Data,
            Titulo = c.Data.Date < hoje ? $"Conta vencida há {(hoje - c.Data.Date).Days} dia(s)"
                : $"Conta a pagar vence {(c.Data.Date == hoje ? "hoje" : $"em {c.Data:dd/MM}")}",
            Detalhe = c.Descricao,
            Pessoa = c.Nome, Telefone = c.Telefone, Email = c.Email, Valor = c.Valor, EventoId = c.EventoId,
            Chave = $"PAGAR:{c.Id}:{Dia(c.Data)}"
        }));

        lembretes.AddRange(materiais.GroupBy(m => m.EventoId!.Value).Select(g => new Lembrete
        {
            Tipo = TipoLembrete.MaterialNaoDevolvido,
            ReferenciaId = g.Key,
            Data = g.First().Data,
            Titulo = "Materiais do evento ainda não devolvidos",
            Detalhe = $"EVT-{g.Key:D5} {g.First().Evento}: " + string.Join(", ", g.Select(m => $"{m.Quantidade} {m.Descricao}")),
            EventoId = g.Key,
            Chave = $"MAT:{g.Key}:{Dia(hoje)}"
        }));

        lembretes.AddRange(tarefas.Select(t => new Lembrete
        {
            Tipo = TipoLembrete.TarefaEvento,
            ReferenciaId = t.Id,
            Data = t.Data,
            Titulo = t.Data.Date < hoje ? $"Tarefa atrasada há {(hoje - t.Data.Date).Days} dia(s)" : "Tarefa para hoje",
            Detalhe = $"{t.Descricao} • EVT-{t.EventoId:D5} {t.Evento} ({t.Inicio:dd/MM})",
            Pessoa = t.Nome,
            EventoId = t.EventoId,
            Chave = $"TAREFA:{t.Id}:{Dia(hoje)}"
        }));

        return lembretes.OrderBy(l => l.Data).ToList();
    }
}
