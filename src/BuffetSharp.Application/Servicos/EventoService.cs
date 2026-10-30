using BuffetSharp.Domain.Cardapio;
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
    EscalaService escalas,
    IConsultasEstoqueCardapio estoque,
    ISessaoUsuario sessao,
    TimeProvider relogio)
{
    private DateTime Agora => relogio.GetLocalNow().DateTime;


    // ------------------------- Consulta -------------------------

    public async Task<EventoDetalhe> ObterDetalheAsync(int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();

        // Com resolução de identidade, a mesa de cada convidado é a mesma instância das mesas do mapa.
        var evento = await db.Eventos.AsNoTrackingWithIdentityResolution()
                         .Include(e => e.Cliente)
                         .Include(e => e.TipoEvento)
                         .Include(e => e.Espaco)
                         .Include(e => e.Orcamento)
                         .Include(e => e.Vendedor)
                         .Include(e => e.Materiais).ThenInclude(m => m.Material)
                         .ComCardapioCompleto()
                         .Include(e => e.Servicos)
                         .Include(e => e.Equipe).ThenInclude(f => f.Funcionario!).ThenInclude(f => f.Cargo)
                         .Include(e => e.Convidados).ThenInclude(c => c.Mesa)
                         .Include(e => e.Mesas)
                         .Include(e => e.Fornecedores).ThenInclude(f => f.Fornecedor)
                         .Include(e => e.Fornecedores).ThenInclude(f => f.Pagamento!).ThenInclude(p => p.Parcelas)
                         .Include(e => e.Fornecedores).ThenInclude(f => f.EtapaChegada)
                         .Include(e => e.Parcelas).ThenInclude(p => p.FormaPagamento)
                         .Include(e => e.Tarefas)
                         .Include(e => e.Etapas)
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
            CustoInsumosEstimado = Math.Round((evento.Pacote?.CustoPorPessoa ?? 0) * evento.PessoasAtendidas, 2),
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
            Versao = e.Versao,
            Titulo = e.Titulo,
            ClienteId = e.ClienteId,
            TipoEventoId = e.TipoEventoId,
            EspacoId = e.EspacoId,
            VendedorId = e.VendedorId,
            Data = e.DataEvento,
            HoraInicio = e.HoraInicio,
            HoraFim = e.HoraFim,
            Convidados = e.QuantidadeConvidados,
            ConvidadosMeia = e.ConvidadosMeia,
            ConvidadosIsentos = e.ConvidadosIsentos,
            ValorTotal = e.ValorTotal,
            CondicaoPagamentoId = e.CondicaoPagamentoId,
            FormaPagamentoId = e.FormaPagamentoId,
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
                input.HoraInicio, input.HoraFim, input.QuantidadesPorFaixa, input.ValorTotal);
            await Checklists.AplicarSugeridoAsync(db, evento, ct);
            db.Eventos.Add(evento);
        }
        else
        {
            evento = await db.Eventos.Include(e => e.Parcelas).Include(e => e.Etapas).FirstOrDefaultAsync(e => e.Id == input.Id, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
            Concorrencia.Conferir(evento, input.Versao);
            evento.AlterarDados(input.Titulo, input.EspacoId, input.Data, input.HoraInicio, input.HoraFim,
                input.QuantidadesPorFaixa, input.Observacoes);
            if (evento.ValorTotal != input.ValorTotal)
                evento.AjustarValorTotal(input.ValorTotal);
        }

        if (evento.CondicaoPagamentoId != input.CondicaoPagamentoId)
            evento.DefinirCondicaoPagamento(input.CondicaoPagamentoId);
        if (evento.FormaPagamentoId != input.FormaPagamentoId)
        {
            await OrcamentoService.GarantirFormaAtivaAsync(db, input.FormaPagamentoId, ct);
            evento.DefinirFormaPagamento(input.FormaPagamentoId);
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
        input.Versao = evento.Versao;
        return evento.Id;
    }

    // ------------------------- Equipe -------------------------

    /// <summary>
    /// Escala o funcionário. Indisponível (folga, férias, atestado) não pode; em conflito com outro evento ou sem o descanso
    /// mínimo, lança <see cref="ConflitoEscalaException"/> (use <see cref="EscalarComExcecaoAsync"/> para autorizar).
    /// </summary>
    public Task EscalarFuncionarioAsync(int eventoId, int funcionarioId, string? funcao, decimal? valorDiaria,
        CancellationToken ct = default) =>
        EscalarAsync(eventoId, funcionarioId, funcao, valorDiaria, null, ct);

    /// <summary>Escala apesar do conflito com outro evento (ou do descanso), com o motivo registrado. Somente administradores.</summary>
    public Task EscalarComExcecaoAsync(int eventoId, int funcionarioId, string? funcao, decimal? valorDiaria, string motivo,
        CancellationToken ct = default)
    {
        Validador.Garantir(sessao.Usuario?.EhAdministrador == true, "Somente um administrador pode escalar com conflito de horário.");
        return EscalarAsync(eventoId, funcionarioId, funcao, valorDiaria, motivo, ct);
    }

    private Task EscalarAsync(int eventoId, int funcionarioId, string? funcao, decimal? valorDiaria, string? excecao, CancellationToken ct) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Equipe).Include(e => e.Etapas), async (db, evento) =>
        {
            var funcionario = await db.Funcionarios.Include(f => f.Cargo).FirstOrDefaultAsync(f => f.Id == funcionarioId, ct)
                              ?? throw new RegraNegocioException("Funcionário não encontrado.");
            var escala = evento.EscalarFuncionario(funcionario, funcao, valorDiaria);

            var conflitos = await escalas.ConflitosAsync(db, evento, funcionarioId, ct);
            if (conflitos.FirstOrDefault(c => !c.AdmiteExcecao) is { } indisponivel)
                throw new RegraNegocioException($"{funcionario.Nome} está indisponível: {indisponivel.Descricao}.");
            if (conflitos.Count == 0)
                return;
            if (excecao is null)
                throw new ConflitoEscalaException(funcionario.Nome, conflitos);
            escala.AutorizarConflito(excecao, sessao.Usuario!.Login);
        }, ct);

    public Task RemoverDaEquipeAsync(int eventoId, int funcionarioId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Equipe).Include(e => e.Etapas), (_, e) => e.RemoverDaEquipe(funcionarioId), ct);

    public Task RegistrarPresencaAsync(int eventoId, int funcionarioId, bool presente, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Equipe), (_, e) => e.RegistrarPresenca(funcionarioId, presente), ct);

    // ------------------------- Convidados -------------------------

    /// <param name="mesaId">Mesa do mapa onde o convidado senta (opcional).</param>
    public Task AdicionarConvidadoAsync(int eventoId, string nome, string? telefone, string? email, int acompanhantes,
        int? mesaId = null, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComMesas, (_, e) =>
        {
            var convidado = e.AdicionarConvidado(nome, telefone, email, acompanhantes);
            if (mesaId is { } id)
                e.SentarConvidado(convidado, e.Mesas.FirstOrDefault(m => m.Id == id) ?? throw new RegraNegocioException("Mesa não encontrada."));
        }, ct);

    /// <summary>
    /// Importa convidados colados de uma planilha/texto: uma pessoa por linha no formato
    /// "Nome;Telefone;Acompanhantes;Mesa;Restrições" (somente o nome é obrigatório). A mesa ("5", "Mesa 5" ou um nome, como
    /// "Noivos") entra no mapa se ainda não existir; quem não couber nela fica sem mesa. As restrições ("vegano, sem lactose",
    /// "celíaco") são reconhecidas e o que não for vira observação ("alergia a kiwi"). Retorna a quantidade importada.
    /// </summary>
    public async Task<int> ImportarConvidadosAsync(int eventoId, string texto, CancellationToken ct = default)
    {
        var importados = 0;
        await AlterarAsync(eventoId, ComMesas, (_, evento) =>
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
                var convidado = evento.AdicionarConvidado(nome, telefone, null, acompanhantes);
                if (partes.Length > 3 && partes[3].Length > 0 && evento.ObterOuCriarMesa(partes[3]) is var mesa &&
                    evento.LugaresLivres(mesa) >= convidado.Pessoas)
                    evento.SentarConvidado(convidado, mesa);
                if (partes.Length > 4 && RestricoesAlimentares.Ler(partes[4]) is var (restricoes, observacao) &&
                    (restricoes != RestricaoAlimentar.Nenhuma || observacao is not null))
                    evento.DefinirRestricoesConvidado(convidado, restricoes, observacao);
                importados++;
            }
        }, ct);
        return importados;
    }

    public Task DefinirRestricoesConvidadoAsync(int eventoId, int convidadoId, RestricaoAlimentar restricoes, string? observacao,
        CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.DefinirRestricoesConvidado(convidadoId, restricoes, observacao), ct);

    /// <summary>Restrições sem nome na lista (ex.: 6 vegetarianos), informadas pelo cliente.</summary>
    public Task DefinirRestricoesAvulsasAsync(int eventoId, IEnumerable<(RestricaoAlimentar Restricoes, int Pessoas, string? Descricao)> restricoes,
        CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.RestricoesAvulsas), (_, e) => e.DefinirRestricoesAvulsas(restricoes), ct);

    public Task RemoverConvidadoAsync(int eventoId, int convidadoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.RemoverConvidado(convidadoId), ct);

    public Task DefinirRsvpAsync(int eventoId, int convidadoId, StatusRsvp rsvp, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComMesas, (_, e) => e.DefinirRsvp(convidadoId, rsvp), ct);

    public Task RegistrarCheckInAsync(int eventoId, int convidadoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.RegistrarCheckIn(convidadoId, Agora), ct);

    public Task DesfazerCheckInAsync(int eventoId, int convidadoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Convidados), (_, e) => e.DesfazerCheckIn(convidadoId), ct);

    /// <summary>
    /// Fecha a contagem de quem veio e lança a cobrança dos excedentes numa parcela própria. Retorna a apuração e o Id da
    /// parcela (nulo quando ninguém passou do contratado).
    /// </summary>
    public async Task<(ExcedentesConvidados Excedentes, int? ParcelaId)> ApurarExcedentesAsync(int eventoId,
        ApuracaoExcedentesInput input, CancellationToken ct = default)
    {
        Parcela? parcela = null;
        var excedentes = ExcedentesConvidados.Nenhum;
        await AlterarAsync(eventoId, q => q.Include(e => e.Convidados).Include(e => e.Parcelas), async (db, evento) =>
        {
            await OrcamentoService.GarantirFormaAtivaAsync(db, input.FormaPagamentoId, ct);
            parcela = evento.ApurarExcedentes(input.QuantidadesPorFaixa, input.ValorPorPessoa, input.Vencimento,
                input.FormaPagamentoId, Agora);
            excedentes = evento.Excedentes;
        }, ct);
        return (excedentes, parcela?.Id);
    }

    public Task DesfazerApuracaoExcedentesAsync(int eventoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Parcelas), (_, e) => e.DesfazerApuracaoExcedentes(), ct);

    // ------------------------- Checklist -------------------------

    public Task AdicionarTarefaAsync(int eventoId, TarefaInput input, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Tarefas), (_, e) => e.AdicionarTarefa(input.Descricao, input.Prazo, input.Responsavel), ct);

    public Task ConcluirTarefaAsync(int eventoId, int tarefaId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Tarefas), (_, e) => e.ConcluirTarefa(tarefaId, sessao.Usuario?.Login, Agora), ct);

    public Task ReabrirTarefaAsync(int eventoId, int tarefaId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Tarefas), (_, e) => e.ReabrirTarefa(tarefaId), ct);

    public Task RemoverTarefaAsync(int eventoId, int tarefaId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Tarefas), (_, e) => e.RemoverTarefa(tarefaId), ct);

    /// <summary>Inclui as tarefas do modelo (o sugerido para o tipo do evento, se não for informado). Retorna quantas entraram.</summary>
    public async Task<int> AplicarChecklistAsync(int eventoId, int? modeloId = null, CancellationToken ct = default)
    {
        var incluidas = 0;
        await AlterarAsync(eventoId, q => q.Include(e => e.Tarefas), async (db, evento) =>
        {
            var modelo = modeloId is { } id
                ? await db.ModelosChecklist.Include(m => m.Itens).FirstOrDefaultAsync(m => m.Id == id, ct)
                : await Checklists.ModeloSugeridoAsync(db, evento.TipoEventoId, ct);
            Validador.Garantir(modelo is not null, "Não há modelo de checklist ativo. Cadastre um em Cadastros › Modelos de checklist.");
            incluidas = evento.AplicarChecklist(modelo!);
        }, ct);
        return incluidas;
    }

    // ------------------------- Cronograma -------------------------

    /// <summary>Inclui ou altera uma etapa do cronograma. Com funcionário da escala, o nome dele vira o responsável.</summary>
    public Task SalvarEtapaAsync(int eventoId, EtapaInput input, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Etapas).Include(e => e.Equipe).ThenInclude(f => f.Funcionario), (_, e) =>
        {
            var responsavel = input.FuncionarioId is { } funcionarioId
                ? e.Equipe.FirstOrDefault(f => f.FuncionarioId == funcionarioId)?.Funcionario?.Nome ?? input.Responsavel
                : input.Responsavel;
            if (input.Id == 0)
                e.AdicionarEtapa(input.Horario, input.DuracaoMinutos, input.Atividade, input.Local, responsavel, input.FuncionarioId, input.Observacao);
            else
                e.AlterarEtapa(input.Id, input.Horario, input.DuracaoMinutos, input.Atividade, input.Local, responsavel, input.FuncionarioId,
                    input.Observacao);
        }, ct);

    public Task RemoverEtapaAsync(int eventoId, int etapaId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Etapas).Include(e => e.Fornecedores), (_, e) => e.RemoverEtapa(etapaId), ct);

    /// <summary>Inclui as etapas do modelo (o sugerido para o tipo do evento, se não for informado). Retorna quantas entraram.</summary>
    public async Task<int> AplicarCronogramaAsync(int eventoId, int? modeloId = null, CancellationToken ct = default)
    {
        var incluidas = 0;
        await AlterarAsync(eventoId, q => q.Include(e => e.Etapas), async (db, evento) =>
        {
            var modelo = modeloId is { } id
                ? await db.ModelosCronograma.Include(m => m.Itens).FirstOrDefaultAsync(m => m.Id == id, ct)
                : await Checklists.CronogramaSugeridoAsync(db, evento.TipoEventoId, ct);
            Validador.Garantir(modelo is not null, "Não há modelo de cronograma ativo. Cadastre um em Cadastros › Modelos de cronograma.");
            incluidas = evento.AplicarCronograma(modelo!);
        }, ct);
        return incluidas;
    }

    // ------------------------- Mapa de mesas -------------------------

    /// <summary>Inclui ou altera uma mesa do mapa (a posição só muda em <see cref="MoverMesaAsync"/>).</summary>
    public Task SalvarMesaAsync(int eventoId, MesaInput input, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComMesas, (_, e) =>
        {
            if (input.Id == 0)
                e.AdicionarMesa(input.Numero, input.Nome, input.Lugares, input.Formato, input.X, input.Y);
            else
                e.AlterarMesa(input.Id, input.Numero, input.Nome, input.Lugares, input.Formato);
        }, ct);

    public Task MoverMesaAsync(int eventoId, int mesaId, int x, int y, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Mesas), (_, e) => e.MoverMesa(mesaId, x, y), ct);

    public Task RemoverMesaAsync(int eventoId, int mesaId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComMesas, (_, e) => e.RemoverMesa(mesaId), ct);

    /// <summary>Senta o convidado na mesa (nula: tira da mesa).</summary>
    public Task SentarConvidadoAsync(int eventoId, int convidadoId, int? mesaId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComMesas, (_, e) => e.SentarConvidado(convidadoId, mesaId), ct);

    /// <summary>Distribui os convidados sem mesa pelos lugares livres. Retorna quantos foram sentados.</summary>
    public async Task<int> DistribuirConvidadosAsync(int eventoId, CancellationToken ct = default)
    {
        var sentados = 0;
        await AlterarAsync(eventoId, ComMesas, (_, e) => sentados = e.DistribuirConvidados(), ct);
        return sentados;
    }

    /// <summary>Troca o mapa do evento pelo mapa padrão do espaço. Retorna quantos convidados ficaram sem mesa.</summary>
    public async Task<int> UsarMapaDoEspacoAsync(int eventoId, CancellationToken ct = default)
    {
        var semMesa = 0;
        await AlterarAsync(eventoId, q => ComMesas(q).Include(e => e.Espaco), async (db, e) =>
        {
            var planta = await db.EspacoMesas.Where(m => m.EspacoId == e.EspacoId).ToListAsync(ct);
            Validador.Garantir(planta.Count > 0,
                $"O espaço '{e.Espaco?.Nome}' ainda não tem mapa padrão. Monte as mesas aqui e use \"Salvar como padrão do salão\".");
            semMesa = e.UsarPlanta(planta);
        }, ct);
        return semMesa;
    }

    private static IQueryable<Evento> ComMesas(IQueryable<Evento> eventos) => eventos.Include(e => e.Convidados).Include(e => e.Mesas);

    // ------------------------- Fornecedores do evento -------------------------

    /// <summary>Contrata o fornecedor, gerando as contas a pagar pelo plano, ou altera a contratação. Retorna o Id.</summary>
    public async Task<int> SalvarFornecedorAsync(int eventoId, ContratacaoFornecedorInput input, CancellationToken ct = default)
    {
        EventoFornecedor? contratacao = null;
        await AlterarAsync(eventoId, ComFornecedores, async (db, e) =>
        {
            if (input.Id == 0)
            {
                var fornecedor = await db.Fornecedores.FindAsync([input.FornecedorId], ct) ?? throw new RegraNegocioException("Selecione o fornecedor.");
                contratacao = e.ContratarFornecedor(fornecedor, input.Servico, input.Valor, input.EventoServicoId, input.Chegada,
                    input.Responsavel, input.Telefone, input.Observacao);
                input.Plano.GerarParcelas(contratacao.Pagamento!);
            }
            else
            {
                e.AlterarFornecedor(input.Id, input.Servico, input.EventoServicoId, input.Chegada, input.Responsavel, input.Telefone,
                    input.Observacao);
                contratacao = e.Fornecedores.First(f => f.Id == input.Id);
            }
        }, ct);
        return contratacao!.Id;
    }

    /// <summary>Troca o valor combinado e refaz as contas a pagar (nenhuma pode ter sido paga).</summary>
    public Task RefazerPagamentoFornecedorAsync(int eventoId, int contratacaoId, decimal valor, PlanoPagamentoInput plano,
        CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComFornecedores, (db, e) =>
        {
            foreach (var item in e.RefazerPagamentoFornecedor(contratacaoId, valor))
            {
                switch (item)
                {
                    case ContaPagar conta: db.ContasPagar.Remove(conta); break;
                    case ParcelamentoPagar p: db.ParcelamentosPagar.Remove(p); break;
                }
            }
            plano.GerarParcelas(e.Fornecedores.First(f => f.Id == contratacaoId).Pagamento!);
        }, ct);

    /// <summary>Cancela a contratação e as contas a pagar em aberto. Retorna quanto já tinha sido pago ao fornecedor.</summary>
    public async Task<decimal> CancelarFornecedorAsync(int eventoId, int contratacaoId, CancellationToken ct = default)
    {
        var pago = 0m;
        await AlterarAsync(eventoId, ComFornecedores, (_, e) => pago = e.CancelarFornecedor(contratacaoId), ct);
        return pago;
    }

    public Task ConfirmarFornecedorAsync(int eventoId, int contratacaoId, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComFornecedores, (_, e) => e.ConfirmarFornecedor(contratacaoId, Agora), ct);

    public Task AvaliarFornecedorAsync(int eventoId, int contratacaoId, int nota, string? comentario, CancellationToken ct = default) =>
        AlterarAsync(eventoId, ComFornecedores, (_, e) => e.AvaliarFornecedor(contratacaoId, nota, comentario, Agora), ct);

    /// <summary>Notas que o fornecedor recebeu nos eventos, da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<AvaliacaoFornecedor>> AvaliacoesDoFornecedorAsync(int fornecedorId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.Eventos.AsNoTracking()
            .SelectMany(e => e.Fornecedores.Where(f => f.FornecedorId == fornecedorId && f.Nota != null),
                (e, f) => new { e.Id, e.Titulo, e.Inicio, f.Servico, Nota = f.Nota!.Value, f.Comentario })
            .OrderByDescending(a => a.Inicio)
            .Select(a => new AvaliacaoFornecedor(a.Id, a.Titulo, a.Inicio, a.Servico, a.Nota, a.Comentario))
            .ToListAsync(ct);
    }

    /// <summary>Média das notas de cada fornecedor avaliado.</summary>
    public async Task<IReadOnlyDictionary<int, MediaAvaliacao>> MediasAvaliacaoAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var medias = await db.Eventos.AsNoTracking().SelectMany(e => e.Fornecedores).Where(f => f.Nota != null)
            .GroupBy(f => f.FornecedorId)
            .Select(g => new { FornecedorId = g.Key, Soma = g.Sum(f => f.Nota!.Value), Quantidade = g.Count() })
            .ToListAsync(ct);
        return medias.ToDictionary(m => m.FornecedorId,
            m => new MediaAvaliacao(m.FornecedorId, Math.Round((decimal)m.Soma / m.Quantidade, 1), m.Quantidade));
    }

    private static IQueryable<Evento> ComFornecedores(IQueryable<Evento> eventos) =>
        eventos.Include(e => e.Servicos).Include(e => e.Etapas)
            .Include(e => e.Fornecedores).ThenInclude(f => f.Fornecedor)
            .Include(e => e.Fornecedores).ThenInclude(f => f.Pagamento!).ThenInclude(p => p.Parcelas)
            .Include(e => e.Fornecedores).ThenInclude(f => f.EtapaChegada);

    // ------------------------- Financeiro -------------------------

    public Task GerarParcelasAsync(int eventoId, PlanoPagamentoInput plano, CancellationToken ct = default) =>
        AlterarAsync(eventoId, q => q.Include(e => e.Parcelas), async (db, e) =>
        {
            await GarantirSemCobrancaPendenteAsync(db, eventoId, "refazer o plano de pagamento", ct);
            if (plano.Vencimentos is { Count: > 0 } vencimentos)
                e.GerarParcelas(vencimentos.Select(v => v.ParaPlanejado()).ToList(), plano.FormaPagamentoId);
            else
                e.GerarParcelas(plano.Quantidade, plano.PrimeiroVencimento, plano.ValorEntrada, plano.VencimentoEntrada,
                    plano.FormaPagamentoId, plano.IntervaloDias is > 0 ? plano.IntervaloDias : null);
            if (plano.CondicaoPagamentoId is { } condicaoId && condicaoId != e.CondicaoPagamentoId)
                e.DefinirCondicaoPagamento(condicaoId);
        }, ct);

    /// <summary>
    /// Plano de pagamento sugerido pela condição para o saldo ainda não pago do evento (as parcelas em aberto são
    /// substituídas ao gerar o plano). Os vencimentos contam a partir de hoje.
    /// </summary>
    public async Task<PlanoPagamentoInput> SugerirPlanoAsync(int eventoId, int condicaoPagamentoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Parcelas).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        var condicao = await db.CondicoesPagamento.AsNoTracking().Include(c => c.Parcelas).FirstOrDefaultAsync(c => c.Id == condicaoPagamentoId, ct)
                       ?? throw new RegraNegocioException("Condição de pagamento não encontrada.");

        if (condicao.ParcelasDefinidas)
            return PlanoPagamentoInput.DaCondicao(condicao, condicao.Vencimentos(evento.SaldoAParcelar, DateOnly.FromDateTime(Agora), evento.DataEvento),
                evento.FormaPagamentoId ?? condicao.FormaPagamentoId);
        var plano = condicao.Simular(evento.SaldoAParcelar, DateOnly.FromDateTime(Agora), evento.DataEvento);
        return new PlanoPagamentoInput
        {
            Quantidade = plano.Quantidade,
            PrimeiroVencimento = plano.PrimeiroVencimento,
            ValorEntrada = plano.ValorEntrada,
            VencimentoEntrada = plano.VencimentoEntrada ?? plano.PrimeiroVencimento,
            FormaPagamentoId = evento.FormaPagamentoId ?? condicao.FormaPagamentoId,
            IntervaloDias = plano.IntervaloDias ?? 0,
            CondicaoPagamentoId = condicao.Id
        };
    }

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
    /// Marca o evento como realizado e baixa do estoque os insumos do pacote (ficha técnica × convidados, contando os
    /// excedentes já apurados).
    /// </summary>
    public async Task<ResultadoRealizacao> RealizarAsync(int eventoId, CancellationToken ct = default)
    {
        var avisos = new List<string>();
        await AlterarAsync(eventoId,
            q => q.ComCardapioCompleto(),
            async (db, evento) =>
            {
                var agora = Agora;
                evento.Realizar(agora);
                if (evento.Pacote is null)
                {
                    avisos.Add("Evento sem pacote: nenhum insumo foi baixado do estoque.");
                    return;
                }

                // Quem tem restrição comeu o substituto: os insumos dele saem no lugar dos do prato.
                var consumo = evento.PlanejarCardapio(evento.PessoasAtendidas).Consumo();
                var produtos = await EstoqueService.ComLotes(db.Produtos).Where(p => consumo.Keys.Contains(p.Id)).ToListAsync(ct);
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
        AlterarAsync(eventoId, q => ComFornecedores(q).Include(e => e.Parcelas), async (db, e) =>
        {
            await GarantirSemCobrancaPendenteAsync(db, eventoId, "cancelar o evento", ct);
            e.Cancelar(motivo);
        }, ct);

    /// <summary>Parcela em aberto com boleto/PIX registrado pendente: o cliente ainda pode pagar, então a cobrança sai antes.</summary>
    private static async Task GarantirSemCobrancaPendenteAsync(IBuffetDbContext db, int eventoId, string acao, CancellationToken ct)
    {
        var pendentes = await db.CobrancasRegistradas.Where(c => c.EventoId == eventoId && c.Status == StatusCobranca.Pendente)
            .Select(c => c.Descricao).ToListAsync(ct);
        Validador.Garantir(pendentes.Count == 0,
            $"Há boleto/PIX registrado pendente ({string.Join(", ", pendentes)}). Cancele a cobrança em Financeiro › Cobranças (boleto e PIX) " +
            $"antes de {acao}.");
    }

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
