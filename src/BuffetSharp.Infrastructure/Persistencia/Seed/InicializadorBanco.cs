using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Infrastructure.Persistencia.Seed;

/// <summary>
/// Aplica as migrations pendentes e cria os dados iniciais (usuário administrador e,
/// opcionalmente, uma base de demonstração completa).
/// </summary>
public sealed class InicializadorBanco(IDbContextFactory<BuffetDbContext> fabrica, IHasherSenha hasher, BuffetOptions opcoes,
    TimeProvider relogio)
{
    public async Task InicializarAsync(bool dadosDemonstracao, IProgress<string>? progresso = null, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);

        // A carga inicial não gera histórico de auditoria (milhares de registros sem valor para o usuário).
        db.AuditoriaHabilitada = false;

        progresso?.Report("Atualizando estrutura do banco de dados...");
        await db.Database.MigrateAsync(ct);

        if (!await db.Usuarios.AnyAsync(ct))
        {
            progresso?.Report("Criando usuários iniciais...");
            db.Usuarios.AddRange(
                new Usuario("Administrador", "admin", PerfilUsuario.Administrador, hasher.Gerar("Admin@123")),
                new Usuario("Carla Comercial", "comercial", PerfilUsuario.Comercial, hasher.Gerar("Comercial@123")),
                new Usuario("Fábio Financeiro", "financeiro", PerfilUsuario.Financeiro, hasher.Gerar("Financeiro@123")),
                new Usuario("Otávio Operacional", "operacional", PerfilUsuario.Operacional, hasher.Gerar("Operacional@123")));
            await db.SaveChangesAsync(ct);
        }

        // Cadastros que o sistema precisa para funcionar desde o início (também em bancos de versões anteriores).
        if (!await db.ModelosContrato.AnyAsync(ct))
        {
            progresso?.Report("Criando o modelo de contrato padrão...");
            db.ModelosContrato.Add(new ModeloContrato
            {
                Nome = "Contrato padrão",
                Padrao = true,
                Conteudo = GeradorContrato.ModeloPadrao.ReplaceLineEndings("\n")
            });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.ContasFinanceiras.AnyAsync(ct))
        {
            progresso?.Report("Criando o caixa...");
            db.ContasFinanceiras.Add(new ContaFinanceira { Nome = "Caixa", Tipo = TipoContaFinanceira.Caixa });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.CondicoesPagamento.AnyAsync(ct))
        {
            progresso?.Report("Criando condições de pagamento...");
            db.CondicoesPagamento.AddRange(CondicoesIniciais());
            await db.SaveChangesAsync(ct);
        }

        if (!await db.ModelosChecklist.AnyAsync(ct))
        {
            progresso?.Report("Criando o checklist padrão dos eventos...");
            db.ModelosChecklist.Add(ChecklistInicial());
            await db.SaveChangesAsync(ct);
        }

        if (!await db.ModelosCronograma.AnyAsync(ct))
        {
            progresso?.Report("Criando o cronograma padrão dos eventos...");
            db.ModelosCronograma.Add(CronogramaInicial());
            await db.SaveChangesAsync(ct);
        }

        if (dadosDemonstracao && !await db.Clientes.AnyAsync(ct))
        {
            progresso?.Report("Gerando dados de demonstração (cadastros, eventos, financeiro)...");
            await new DadosDemonstracao(db, relogio, hasher, opcoes).PopularAsync(ct);
        }
    }

    /// <summary>Preparação típica de um evento, do contrato à pesquisa de satisfação; pode ser alterada no cadastro.</summary>
    public static ModeloChecklist ChecklistInicial()
    {
        (int DiasAntes, string Descricao, string Responsavel)[] tarefas =
        [
            (60, "Assinar o contrato e receber a entrada", "Comercial"),
            (30, "Confirmar cardápio e serviços adicionais com o cliente", "Comercial"),
            (30, "Reservar louças, enxoval e materiais (alugar o que faltar)", "Operacional"),
            (15, "Definir o layout das mesas e a decoração", "Decoração"),
            (10, "Receber a lista final de convidados", "Comercial"),
            (7, "Escalar a equipe (garçons, cozinha, recepção)", "Operacional"),
            (5, "Comprar os insumos (lista de compras)", "Cozinha"),
            (3, "Confirmar som, iluminação e fornecedores terceirizados", "Operacional"),
            (1, "Montar o salão e conferir os materiais", "Operacional"),
            (0, "Recepção dos convidados e check-in", "Recepção"),
            (-2, "Conferir a devolução dos materiais", "Operacional"),
            (-3, "Enviar a pesquisa de satisfação ao cliente", "Comercial")
        ];
        return new ModeloChecklist
        {
            Nome = "Checklist padrão",
            Itens = tarefas.Select((t, i) => new ModeloChecklistItem
            {
                DiasAntes = t.DiasAntes,
                Descricao = t.Descricao,
                Responsavel = t.Responsavel,
                Ordem = i + 1
            }).ToList()
        };
    }

    /// <summary>Minuto a minuto típico de uma festa (minutos a partir do início do evento); pode ser alterado no cadastro.</summary>
    public static ModeloCronograma CronogramaInicial()
    {
        (int Minutos, int Duracao, string Atividade, string Responsavel, string? Local)[] etapas =
        [
            (-240, 180, "Montagem do salão e da decoração", "Operacional", "Salão"),
            (-60, 20, "Reunião com a equipe (briefing)", "Maître", "Cozinha"),
            (-30, 30, "Equipe a postos, buffet e bar prontos", "Maître", null),
            (0, 60, "Recepção dos convidados", "Recepção", "Entrada"),
            (0, 120, "Serviço de bebidas e salgados", "Garçons", "Salão"),
            (90, 15, "Abertura e homenagem aos anfitriões", "Cerimonial", "Pista"),
            (120, 90, "Jantar", "Cozinha", "Salão"),
            (210, 20, "Parabéns e corte do bolo", "Cerimonial", "Mesa do bolo"),
            (230, 60, "Doces e café", "Copa", null),
            (300, 0, "Encerramento do serviço", "Maître", null),
            (330, 90, "Desmontagem e conferência dos materiais", "Operacional", "Salão")
        ];
        return new ModeloCronograma
        {
            Nome = "Cronograma padrão",
            Itens = etapas.Select((e, i) => new ModeloCronogramaItem
            {
                MinutosDoInicio = e.Minutos,
                DuracaoMinutos = e.Duracao,
                Atividade = e.Atividade,
                Responsavel = e.Responsavel,
                Local = e.Local,
                Ordem = i + 1
            }).ToList()
        };
    }

    /// <summary>Condições mais comuns em buffets; podem ser alteradas ou inativadas no cadastro.</summary>
    public static IEnumerable<CondicaoPagamento> CondicoesIniciais() =>
    [
        new CondicaoPagamento
        {
            Nome = "À vista",
            PercentualEntrada = 100,
            QuantidadeParcelas = 0,
            DiasEntrada = 2
        },
        new CondicaoPagamento
        {
            Nome = "30% de entrada + 5x",
            PercentualEntrada = 30,
            DiasEntrada = 2,
            QuantidadeParcelas = 5,
            DiasPrimeiraParcela = 30,
            QuitarDiasAntesEvento = 10
        },
        new CondicaoPagamento
        {
            Nome = "50% na assinatura + 50% antes do evento",
            Descricao = "50% na assinatura do contrato e 50% até 10 dias antes do evento",
            PercentualEntrada = 50,
            DiasEntrada = 2,
            QuantidadeParcelas = 1,
            DiasPrimeiraParcela = 730,
            QuitarDiasAntesEvento = 10
        }
    ];
}
