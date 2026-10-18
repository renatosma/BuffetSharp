using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
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

        if (!await db.CondicoesPagamento.AnyAsync(ct))
        {
            progresso?.Report("Criando condições de pagamento...");
            db.CondicoesPagamento.AddRange(CondicoesIniciais());
            await db.SaveChangesAsync(ct);
        }

        if (dadosDemonstracao && !await db.Clientes.AnyAsync(ct))
        {
            progresso?.Report("Gerando dados de demonstração (cadastros, eventos, financeiro)...");
            await new DadosDemonstracao(db, relogio, hasher, opcoes).PopularAsync(ct);
        }
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
