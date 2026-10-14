using BuffetSharp.Application.Abstracoes;
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

        if (dadosDemonstracao && !await db.Clientes.AnyAsync(ct))
        {
            progresso?.Report("Gerando dados de demonstração (cadastros, eventos, financeiro)...");
            await new DadosDemonstracao(db, relogio, hasher, opcoes).PopularAsync(ct);
        }
    }
}
