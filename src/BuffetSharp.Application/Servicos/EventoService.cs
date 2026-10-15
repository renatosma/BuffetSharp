using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

public sealed class EventoService(
    IBuffetDbContextFactory fabrica,
    DisponibilidadeService disponibilidade,
    IConsultasEstoqueCardapio estoque,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;


    // ------------------------- Consulta -------------------------

    public async Task<EventoDetalhe> ObterDetalheAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking()
                         .Include(e => e.Cliente)
                         .Include(e => e.TipoEvento)
                         .Include(e => e.Espaco)
                         .Include(e => e.Orcamento)
                         .Include(e => e.Vendedor)
                         .Include(e => e.Materiais).ThenInclude(m => m.Material)
                         .Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!)
                         .ThenInclude(i => i.Ingredientes).ThenInclude(f => f.Produto!).ThenInclude(p => p.UnidadeMedida)
                         .Include(e => e.Servicos)
                         .Include(e => e.Equipe).ThenInclude(f => f.Funcionario!).ThenInclude(f => f.Cargo)
                         .Include(e => e.Convidados)
                         .Include(e => e.Parcelas).ThenInclude(p => p.FormaPagamento)
                         .FirstOrDefaultAsync(e => e.Id == id, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");

        var despesas = await db.ContasPagar
            // Pessoal e locação de materiais ficam de fora: o próprio evento já os custeia (escala e reserva de materiais).
            .Where(c => c.EventoId == id && c.Status != StatusTitulo.Cancelado &&
                        c.Categoria != CategoriaDespesa.Pessoal && c.Categoria != CategoriaDespesa.LocacaoMateriais)
            .SumAsync(c => (decimal?)c.Valor, ct) ?? 0;

        return new EventoDetalhe
        {
            Evento = evento,
            CustoInsumosEstimado = Math.Round((evento.Pacote?.CustoPorPessoa ?? 0) * evento.QuantidadeConvidados, 2),
            DespesasVinculadas = despesas
        };
    }

    public async Task<EventoInput> ObterParaEdicaoAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var e = await db.Eventos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RegraNegocioException("Evento não encontrado.");
        return new EventoInput
        {
            Id = e.Id,
            Titulo = e.Titulo,
            ClienteId = e.ClienteId,
            TipoEventoId = e.TipoEventoId,
            EspacoId = e.EspacoId,
            VendedorId = e.VendedorId,
            Data = e.DataEvento,
            HoraInicio = e.HoraInicio,
            HoraFim = e.HoraFim,
            Convidados = e.QuantidadeConvidados,
            ValorTotal = e.ValorTotal,
            Observacoes = e.Observacoes
        };
    }

    // ------------------------- Cadastro do evento -------------------------

    /// <summary>Cria (venda direta, sem orçamento) ou altera os dados do evento.</summary>
    public async Task<int> SalvarAsync(EventoInput input, CancellationToken ct = default)
    {
        var (inicio, fim) = Domain.Comercial.PeriodoEvento.Calcular(input.Data, input.HoraInicio, input.HoraFim);

        await using var db = fabrica.Criar();
        var espaco = await db.Espacos.FindAsync([input.EspacoId], ct)
                     ?? throw new RegraNegocioException("Selecione o espaço do evento.");
        Validador.Garantir(input.Convidados <= espaco.CapacidadeMaxima,
            $"O espaço '{espaco.Nome}' comporta no máximo {espaco.CapacidadeMaxima} pessoas.");

        await disponibilidade.GarantirDisponivelAsync(input.EspacoId, inicio, fim, input.Id == 0 ? null : input.Id, ct);

        Evento evento;
        if (input.Id == 0)
        {
            evento = new Evento(input.Titulo, input.ClienteId, input.TipoEventoId, input.EspacoId, input.Data,
                input.HoraInicio, input.HoraFim, input.Convidados, input.ValorTotal);
            db.Eventos.Add(evento);
        }
        else
        {
            evento = await db.Eventos.Include(e => e.Parcelas).FirstOrDefaultAsync(e => e.Id == input.Id, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
            evento.AlterarDados(input.Titulo, input.EspacoId, input.Data, input.HoraInicio, input.HoraFim,
                input.Convidados, input.Observacoes);
            if (evento.ValorTotal != input.ValorTotal)
                evento.AjustarValorTotal(input.ValorTotal);
        }

        // A comissão fica congelada na venda; muda somente se o vendedor for trocado.
        if (evento.EhNovo || evento.VendedorId != input.VendedorId)
        {
            var percentual = 0m;
            if (input.VendedorId is { } vendedorId)
            {
                var vendedor = await db.Usuarios.FindAsync([vendedorId], ct);
                Validador.Garantir(vendedor?.PodeVender == true, "O vendedor deve ser um usuário ativo com perfil Comercial ou Administrador.");
                percentual = vendedor!.PercentualComissao;
            }
            evento.DefinirVendedor(input.VendedorId, percentual);
        }

        await db.SaveChangesAsync(ct);
        return evento.Id;
    }

    // ------------------------- Equipe -------------------------

    public Task EscalarFuncionarioAsync(int eventoId, int funcionarioId, string? funcao, decimal? valorDiaria,
        CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Equipe), async (db, evento) =>
        {
            var funcionario = await db.Funcionarios.Include(f => f.Cargo).FirstOrDefaultAsync(f => f.Id == funcionarioId, ct)
                              ?? throw new RegraNegocioException("Funcionário não encontrado.");
            evento.EscalarFuncionario(funcionario, funcao, valorDiaria);
        }, ct);

    public Task RemoverDaEquipeAsync(int eventoId, int funcionarioId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Equipe), (_, e) => e.RemoverDaEquipe(funcionarioId), ct);

    public Task RegistrarPresencaAsync(int eventoId, int funcionarioId, bool presente, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Equipe), (_, e) => e.RegistrarPresenca(funcionarioId, presente), ct);

    // ------------------------- Convidados -------------------------

    public Task AdicionarConvidadoAsync(int eventoId, string nome, string? telefone, string? email, int acompanhantes,
        string? mesa, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados),
            (_, e) => e.AdicionarConvidado(nome, telefone, email, acompanhantes, mesa), ct);

    /// <summary>
    /// Importa convidados colados de uma planilha/texto: uma pessoa por linha no formato
    /// "Nome;Telefone;Acompanhantes;Mesa" (somente o nome é obrigatório). Retorna a quantidade importada.
    /// </summary>
    public async Task<int> ImportarConvidadosAsync(int eventoId, string texto, CancellationToken ct = default)
    {
        var importados = 0;
        await AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, evento) =>
        {
            foreach (var linha in texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var partes = linha.Split([';', '\t'], StringSplitOptions.TrimEntries);
                var nome = partes[0];
                if (string.IsNullOrWhiteSpace(nome) ||
                    evento.Convidados.Any(c => c.Nome.Equals(nome, StringComparison.CurrentCultureIgnoreCase)))
                    continue;

                var telefone = partes.Length > 1 && partes[1].Length > 0 ? partes[1] : null;
                var acompanhantes = partes.Length > 2 && int.TryParse(partes[2], out var a) ? a : 0;
                var mesa = partes.Length > 3 && partes[3].Length > 0 ? partes[3] : null;
                evento.AdicionarConvidado(nome, telefone, null, acompanhantes, mesa);
                importados++;
            }
        }, ct);
        return importados;
    }

    public Task RemoverConvidadoAsync(int eventoId, int convidadoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.RemoverConvidado(convidadoId), ct);

    public Task DefinirRsvpAsync(int eventoId, int convidadoId, StatusRsvp rsvp, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.DefinirRsvp(convidadoId, rsvp), ct);

    public Task RegistrarCheckInAsync(int eventoId, int convidadoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.RegistrarCheckIn(convidadoId, Agora), ct);

    public Task DesfazerCheckInAsync(int eventoId, int convidadoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.DesfazerCheckIn(convidadoId), ct);

    // ------------------------- Financeiro -------------------------

    public Task GerarParcelasAsync(int eventoId, PlanoPagamentoInput plano, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Parcelas), (_, e) =>
            e.GerarParcelas(plano.Quantidade, plano.PrimeiroVencimento, plano.ValorEntrada, plano.VencimentoEntrada,
                plano.FormaPagamentoId), ct);

    // ------------------------- Materiais -------------------------

    /// <summary>
    /// Reserva o material para o evento. As unidades próprias precisam estar livres na data (veja
    /// <see cref="IConsultasEstoqueCardapio.DisponibilidadeMateriaisAsync"/>); o que faltar deve ser informado como alugado.
    /// </summary>
    public Task ReservarMaterialAsync(int eventoId, int materialId, int quantidade, int alugada, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Materiais), async (db, evento) =>
        {
            var material = await db.Materiais.FirstOrDefaultAsync(m => m.Id == materialId, ct)
                           ?? throw new RegraNegocioException("Material não encontrado.");

            var proprias = quantidade - alugada;
            if (proprias > 0)
            {
                var situacao = (await estoque.DisponibilidadeMateriaisAsync(evento.DataEvento, eventoId, ct))
                    .FirstOrDefault(d => d.Id == materialId);
                var disponivel = situacao?.Disponivel ?? material.QuantidadePropria;
                Validador.Garantir(proprias <= disponivel,
                    $"Patrimônio insuficiente de '{material.Nome}' em {evento.DataEvento:dd/MM/yyyy}: " +
                    $"{disponivel} disponível(is) de {material.QuantidadePropria} ({situacao?.Reservada ?? 0} reservada(s) em eventos próximos). " +
                    $"Informe ao menos {quantidade - disponivel} unidade(s) como alugadas.");
            }

            evento.ReservarMaterial(material, quantidade, alugada);
        }, ct);

    public Task RemoverMaterialAsync(int eventoId, int materialId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Materiais), (_, e) => e.RemoverMaterial(materialId), ct);

    /// <summary>Registra a devolução; as perdas de itens próprios são baixadas do patrimônio.</summary>
    public Task RegistrarDevolucaoMaterialAsync(int eventoId, int materialId, int devolvidas, int perdidas,
        CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Materiais), async (db, evento) =>
        {
            var perdasProprias = evento.RegistrarDevolucao(materialId, devolvidas, perdidas, Agora);
            if (perdasProprias > 0)
            {
                var material = await db.Materiais.FirstAsync(m => m.Id == materialId, ct);
                material.AjustarPatrimonio(-perdasProprias);
            }
        }, ct);

    /// <summary>
    /// Gera as contas a pagar da locação de materiais de terceiros (uma por locador). Retorna o total lançado.
    /// </summary>
    public async Task<decimal> LancarLocacaoMateriaisAsync(int eventoId, CancellationToken ct = default)
    {
        var total = 0m;
        await AlterarAsync(eventoId, q => q.Include(e => e.Materiais).ThenInclude(m => m.Material), async (db, evento) =>
        {
            Validador.Garantir(evento.Status != StatusEvento.Cancelado, "Evento cancelado.");
            var alugados = evento.Materiais.Where(m => m.QuantidadeAlugada > 0).ToList();
            Validador.Garantir(alugados.Count > 0, "Não há materiais alugados de terceiros neste evento.");
            Validador.Garantir(!await db.ContasPagar.AnyAsync(c => c.EventoId == eventoId &&
                                                                   c.Categoria == CategoriaDespesa.LocacaoMateriais &&
                                                                   c.Status != StatusTitulo.Cancelado, ct),
                "A locação de materiais deste evento já foi lançada em contas a pagar. Cancele a conta existente para lançar novamente.");

            foreach (var locador in alugados.GroupBy(m => m.Material!.FornecedorId))
            {
                var valor = locador.Sum(m => m.CustoLocacao);
                if (valor <= 0)
                    continue;
                var itens = string.Join(", ", locador.Select(m => $"{m.QuantidadeAlugada} {m.Material!.Nome}"));
                db.ContasPagar.Add(new ContaPagar($"Locação de materiais - {evento.Codigo}", CategoriaDespesa.LocacaoMateriais,
                    valor, evento.DataEvento, locador.Key, eventoId) { Observacao = itens.Length > 500 ? itens[..500] : itens });
                total += valor;
            }
            Validador.Garantir(total > 0, "Os materiais alugados não têm custo de locação cadastrado.");
        }, ct);
        return total;
    }

    // ------------------------- Ciclo de vida -------------------------

    /// <summary>
    /// Marca o evento como realizado e baixa do estoque os insumos do pacote (ficha técnica × convidados).
    /// </summary>
    public async Task<ResultadoRealizacao> RealizarAsync(int eventoId, CancellationToken ct = default)
    {
        var avisos = new List<string>();
        await AlterarAsync(eventoId,
            q => q.Include(e => e.Pacote!).ThenInclude(p => p.Itens).ThenInclude(i => i.ItemCardapio!)
                .ThenInclude(i => i.Ingredientes),
            async (db, evento) =>
            {
                var agora = Agora;
                evento.Realizar(agora);
                if (evento.Pacote is null)
                {
                    avisos.Add("Evento sem pacote: nenhum insumo foi baixado do estoque.");
                    return;
                }

                var consumo = evento.Pacote.CalcularConsumo(evento.QuantidadeConvidados);
                var produtos = await db.Produtos.Where(p => consumo.Keys.Contains(p.Id)).ToListAsync(ct);
                foreach (var produto in produtos)
                {
                    var movimentacao = produto.RegistrarSaida(consumo[produto.Id], agora,
                        $"Consumo do evento {evento.Codigo}", evento.Id, permitirSaldoNegativo: true);
                    db.MovimentacoesEstoque.Add(movimentacao);
                    if (produto.EstoqueAtual < 0)
                        avisos.Add($"{produto.Nome}: saldo ficou negativo ({produto.EstoqueAtual:N3}). Registre a entrada pendente.");
                    else if (produto.AbaixoDoMinimo)
                        avisos.Add($"{produto.Nome}: abaixo do estoque mínimo ({produto.EstoqueAtual:N3}).");
                }
                evento.MarcarEstoqueBaixado();
            }, ct);
        return new ResultadoRealizacao(avisos);
    }

    public Task CancelarAsync(int eventoId, string motivo, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Parcelas), (_, e) => e.Cancelar(motivo), ct);

    // ------------------------- Infra -------------------------

    private Task AlterarAsync(int eventoId, Func<IQueryable<Evento>, IQueryable<Evento>> incluir,
        Action<IBuffetDbContext, Evento> acao, CancellationToken ct) =>
        AlterarAsync(eventoId, incluir, (db, e) =>
        {
            acao(db, e);
            return Task.CompletedTask;
        }, ct);

    private async Task AlterarAsync(int eventoId, Func<IQueryable<Evento>, IQueryable<Evento>> incluir,
        Func<IBuffetDbContext, Evento, Task> acao, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var evento = await incluir(db.Eventos).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        await acao(db, evento);
        await db.SaveChangesAsync(ct);
    }
}
