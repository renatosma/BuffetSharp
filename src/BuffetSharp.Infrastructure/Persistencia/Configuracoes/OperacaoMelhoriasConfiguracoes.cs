using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class ModeloChecklistConfig : IEntityTypeConfiguration<ModeloChecklist>
{
    public void Configure(EntityTypeBuilder<ModeloChecklist> b)
    {
        b.ToTable("ModelosChecklist");
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ModeloChecklistId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class ModeloChecklistItemConfig : IEntityTypeConfiguration<ModeloChecklistItem>
{
    public void Configure(EntityTypeBuilder<ModeloChecklistItem> b)
    {
        b.ToTable("ModeloChecklistItens");
        b.Property(x => x.Descricao).HasMaxLength(ModeloChecklistItem.TamanhoDescricao).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(60);
    }
}

internal sealed class EventoTarefaConfig : IEntityTypeConfiguration<EventoTarefa>
{
    public void Configure(EntityTypeBuilder<EventoTarefa> b)
    {
        b.ToTable("EventoTarefas");
        b.Property(x => x.Descricao).HasMaxLength(EventoTarefa.TamanhoDescricao).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(60);
        b.Property(x => x.ConcluidaPor).HasMaxLength(60);
        b.HasOne<Evento>().WithMany(e => e.Tarefas).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EventoId, x.Prazo });
    }
}

internal sealed class FechamentoEquipeConfig : IEntityTypeConfiguration<FechamentoEquipe>
{
    public void Configure(EntityTypeBuilder<FechamentoEquipe> b)
    {
        b.ToTable("FechamentosEquipe");
        b.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ContaPagar).WithOne().HasForeignKey<FechamentoEquipe>(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);

        // Escalas pagas: ao desfazer, o serviço solta as escalas antes de excluir o fechamento.
        b.HasMany(x => x.Escalas).WithOne().HasForeignKey(x => x.FechamentoEquipeId).OnDelete(DeleteBehavior.ClientSetNull);
        b.HasIndex(x => new { x.FuncionarioId, x.De, x.Ate });
    }
}
