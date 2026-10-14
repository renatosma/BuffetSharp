using System.Globalization;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BuffetSharp.Infrastructure.Persistencia;

/// <summary>
/// Monta os registros de auditoria a partir do ChangeTracker: para cada entidade incluída, alterada ou
/// excluída, guarda quem fez, quando e os valores (anterior → novo) dos campos afetados.
/// </summary>
internal static class Auditoria
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Campos de controle técnico que não interessam ao histórico.</summary>
    private static readonly HashSet<string> Ignorados = [nameof(Entidade.CriadoEm), nameof(Entidade.AtualizadoEm), "UltimoAcessoEm"];

    /// <summary>Campos sensíveis: registra-se apenas que mudaram.</summary>
    private static readonly HashSet<string> Sigilosos = ["SenhaHash"];

    private const int TamanhoMaximoTexto = 300;

    internal sealed class Pendente(EntityEntry entrada, AcaoAuditoria acao, List<AlteracaoCampo> alteracoes)
    {
        public EntityEntry Entrada { get; } = entrada;
        public AcaoAuditoria Acao { get; } = acao;
        public List<AlteracaoCampo> Alteracoes { get; } = alteracoes;

        // Inclusões só têm chave depois de gravadas; exclusões perdem os valores depois de gravadas.
        public string? Chave { get; set; }
        public string? Descricao { get; set; }
    }

    public static List<Pendente> Coletar(DbContext contexto)
    {
        contexto.ChangeTracker.DetectChanges();
        var pendentes = new List<Pendente>();
        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.Entity is RegistroAuditoria || entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var acao = entrada.State switch
            {
                EntityState.Added => AcaoAuditoria.Inclusao,
                EntityState.Deleted => AcaoAuditoria.Exclusao,
                _ => AcaoAuditoria.Alteracao
            };

            var alteracoes = new List<AlteracaoCampo>();
            foreach (var propriedade in entrada.Properties)
                Comparar(alteracoes, entrada.State, propriedade, propriedade.Metadata.Name);
            foreach (var complexa in entrada.ComplexProperties)
                foreach (var propriedade in complexa.Properties)
                    Comparar(alteracoes, entrada.State, propriedade, $"{complexa.Metadata.Name}.{propriedade.Metadata.Name}");

            if (acao == AcaoAuditoria.Alteracao && alteracoes.Count == 0)
                continue;

            var pendente = new Pendente(entrada, acao, alteracoes);
            if (acao != AcaoAuditoria.Inclusao)
                Identificar(pendente);
            pendentes.Add(pendente);
        }
        return pendentes;
    }

    public static IEnumerable<RegistroAuditoria> Finalizar(IEnumerable<Pendente> pendentes, UsuarioLogado? usuario, DateTime agora)
    {
        foreach (var pendente in pendentes)
        {
            if (pendente.Chave is null)
                Identificar(pendente);
            yield return new RegistroAuditoria(agora, usuario?.Id, usuario?.Login ?? "sistema",
                pendente.Entrada.Metadata.ClrType.Name, pendente.Chave!, pendente.Descricao, pendente.Acao, pendente.Alteracoes);
        }
    }

    private static void Comparar(List<AlteracaoCampo> alteracoes, EntityState estado, PropertyEntry propriedade, string nome)
    {
        if (propriedade.Metadata.IsPrimaryKey() || Ignorados.Contains(propriedade.Metadata.Name))
            return;

        switch (estado)
        {
            case EntityState.Added when propriedade.CurrentValue is not null:
                alteracoes.Add(new AlteracaoCampo(nome, null, Formatar(propriedade.Metadata.Name, propriedade.CurrentValue)));
                break;
            case EntityState.Deleted when propriedade.OriginalValue is not null:
                alteracoes.Add(new AlteracaoCampo(nome, Formatar(propriedade.Metadata.Name, propriedade.OriginalValue), null));
                break;
            case EntityState.Modified when propriedade.IsModified && !Iguais(propriedade.OriginalValue, propriedade.CurrentValue):
                alteracoes.Add(new AlteracaoCampo(nome, Formatar(propriedade.Metadata.Name, propriedade.OriginalValue),
                    Formatar(propriedade.Metadata.Name, propriedade.CurrentValue)));
                break;
        }
    }

    private static bool Iguais(object? a, object? b) =>
        a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : Equals(a, b);

    private static string? Formatar(string campo, object? valor)
    {
        if (valor is null)
            return null;
        if (Sigilosos.Contains(campo))
            return "(oculto)";

        var texto = valor switch
        {
            byte[] bytes => $"(imagem, {bytes.Length:N0} bytes)",
            bool b => b ? "Sim" : "Não",
            decimal d => d.ToString("0.####", PtBr),
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd/MM/yyyy", PtBr) : dt.ToString("dd/MM/yyyy HH:mm", PtBr),
            DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
            TimeOnly t => t.ToString("HH:mm", PtBr),
            IFormattable f => f.ToString(null, PtBr),
            _ => valor.ToString() ?? string.Empty
        };
        return texto.Length > TamanhoMaximoTexto ? texto[..TamanhoMaximoTexto] + "…" : texto;
    }

    /// <summary>Chave e descrição legível (código, título, nome...) com o evento/orçamento de origem, quando houver.</summary>
    private static void Identificar(Pendente pendente)
    {
        var entrada = pendente.Entrada;
        var chave = entrada.Metadata.FindPrimaryKey()!.Properties.Select(p => entrada.Property(p.Name).CurrentValue?.ToString() ?? "?");
        pendente.Chave = string.Join("/", chave);

        var entidade = entrada.Entity;
        var tipo = entidade.GetType();
        string? Ler(string nome) => tipo.GetProperty(nome)?.GetValue(entidade)?.ToString();

        var descricao = Ler("Codigo") is { } codigo && codigo != "(novo)" ? $"{codigo} {Ler("Titulo")}".Trim() : null;
        descricao ??= Ler("Titulo") ?? Ler("Nome") ?? Ler("RazaoSocial") ?? Ler("Descricao") ?? Ler("Login") ?? Ler("Destinatario");
        if (descricao is null && tipo.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType != typeof(object))
            descricao = entidade.ToString();

        if (entidade is not Domain.Eventos.Evento && Ler("EventoId") is { } eventoId)
            descricao = $"{descricao} • EVT-{int.Parse(eventoId):D5}".TrimStart(' ', '•');
        else if (entidade is not Domain.Comercial.Orcamento && Ler("OrcamentoId") is { } orcamentoId)
            descricao = $"{descricao} • ORC-{int.Parse(orcamentoId):D5}".TrimStart(' ', '•');

        pendente.Descricao = descricao;
    }
}
