using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Funil comercial: interessados (leads) desde o primeiro contato até virarem clientes com orçamento, com o histórico
/// das conversas e os retornos combinados.
/// </summary>
public sealed class LeadService(IBuffetDbContextFactory fabrica, ISessaoUsuario sessao, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);
    private string? Usuario => sessao.Usuario?.Login;

    public LeadInput Novo() => new() { ResponsavelId = sessao.Usuario?.Id, ProximoContato = Hoje };

    public async Task<LeadInput> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var l = await db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Lead não encontrado.");
        return new LeadInput
        {
            Id = l.Id,
            Versao = l.Versao,
            Nome = l.Nome,
            Telefone = l.Telefone,
            Email = l.Email,
            Origem = l.Origem,
            DetalheOrigem = l.DetalheOrigem,
            TipoEventoId = l.TipoEventoId,
            DataPrevista = l.DataPrevista,
            Convidados = l.Convidados,
            ResponsavelId = l.ResponsavelId,
            ProximoContato = l.ProximoContato,
            Observacoes = l.Observacoes,
            Status = l.Status
        };
    }

    public async Task<int> SalvarAsync(LeadInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        Lead lead;
        if (input.Id == 0)
        {
            lead = new Lead(input.Nome, input.Telefone, input.Email, input.Origem);
            db.Leads.Add(lead);
        }
        else
        {
            lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == input.Id, ct) ?? throw new RegraNegocioException("Lead não encontrado.");
            Concorrencia.Conferir(lead, input.Versao);
        }

        lead.Alterar(input.Nome, input.Telefone, input.Email, input.Origem, input.DetalheOrigem, input.TipoEventoId,
            input.DataPrevista, input.Convidados, input.ResponsavelId, input.Observacoes);
        if (lead.EmAberto && lead.ProximoContato != input.ProximoContato)
            lead.AgendarRetorno(input.ProximoContato, Hoje);

        await db.SaveChangesAsync(ct);
        input.Versao = lead.Versao;
        return lead.Id;
    }

    public Task RegistrarContatoAsync(int leadId, string descricao, DateOnly? proximoContato, CancellationToken ct = default) =>
        AlterarAsync(leadId, l => l.RegistrarContato(descricao, Usuario, Agora, proximoContato), ct);

    public Task MarcarPerdidoAsync(int leadId, string motivo, CancellationToken ct = default) =>
        AlterarAsync(leadId, l => l.MarcarPerdido(motivo, Usuario, Agora), ct);

    public Task ReabrirAsync(int leadId, CancellationToken ct = default) =>
        AlterarAsync(leadId, l => l.Reabrir(Usuario, Agora), ct);

    /// <summary>
    /// O lead vira cliente: cadastra um cliente com o nome, o telefone e o e-mail do lead (ou liga a um cliente existente).
    /// </summary>
    /// <returns>O Id do cliente, para abrir o orçamento.</returns>
    public async Task<int> ConverterAsync(ConversaoLeadInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var lead = await db.Leads.Include(l => l.Interacoes).FirstOrDefaultAsync(l => l.Id == input.LeadId, ct)
                   ?? throw new RegraNegocioException("Lead não encontrado.");

        Cliente cliente;
        if (input.ClienteExistenteId is { } clienteId)
        {
            cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, ct)
                      ?? throw new RegraNegocioException("Cliente não encontrado.");
        }
        else
        {
            var celular = lead.Telefone is { } telefone && new string(telefone.Where(char.IsDigit).ToArray()) is { Length: 11 } ? telefone : null;
            cliente = new Cliente
            {
                Nome = lead.Nome,
                Celular = celular,
                Telefone = celular is null ? lead.Telefone : null,
                Email = lead.Email,
                Observacoes = $"Cliente vindo do lead LEAD-{lead.Id:D5} ({lead.Origem}" +
                              (lead.DetalheOrigem is { } detalhe ? $": {detalhe}" : "") + ")."
            };
            Validador.Validar(cliente);
            db.Clientes.Add(cliente);
        }

        lead.Converter(cliente, Usuario, Agora);
        await db.SaveChangesAsync(ct);
        return cliente.Id;
    }

    public async Task ExcluirAsync(int leadId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct) ?? throw new RegraNegocioException("Lead não encontrado.");
        Validador.Garantir(lead.Status != StatusLead.Convertido,
            "Leads convertidos em cliente fazem parte do histórico de conversão e não podem ser excluídos.");
        db.Leads.Remove(lead);
        await db.SaveChangesAsync(ct);
    }

    private async Task AlterarAsync(int leadId, Action<Lead> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var lead = await db.Leads.Include(l => l.Interacoes).FirstOrDefaultAsync(l => l.Id == leadId, ct)
                   ?? throw new RegraNegocioException("Lead não encontrado.");
        acao(lead);
        await db.SaveChangesAsync(ct);
    }
}
