using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class OrcamentoConfig : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> b)
    {
        b.ToTable("Orcamentos");
        b.Property(x => x.PercentualDesconto).HasPrecision(5, 2);
        b.Property(x => x.Observacoes).HasMaxLength(2000);
        b.Property(x => x.MotivoRecusa).HasMaxLength(500);

        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Espaco).WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Pacote).WithMany().HasForeignKey(x => x.PacoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Servicos).WithOne().HasForeignKey(x => x.OrcamentoId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Inicio);
    }
}

internal sealed class OrcamentoServicoConfig : IEntityTypeConfiguration<OrcamentoServico>
{
    public void Configure(EntityTypeBuilder<OrcamentoServico> b)
    {
        b.ToTable("OrcamentoServicos");
        b.Property(x => x.Descricao).HasMaxLength(150).IsRequired();
        b.HasOne<ServicoAdicional>().WithMany().HasForeignKey(x => x.ServicoAdicionalId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EventoConfig : IEntityTypeConfiguration<Evento>
{
    public void Configure(EntityTypeBuilder<Evento> b)
    {
        b.ToTable("Eventos");
        b.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
        b.Property(x => x.Observacoes).HasMaxLength(2000);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(500);

        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Espaco).WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Pacote).WithMany().HasForeignKey(x => x.PacoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Orcamento).WithOne(o => o.Evento).HasForeignKey<Evento>(x => x.OrcamentoId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PercentualComissao).HasPrecision(5, 2);

        b.HasMany(x => x.Servicos).WithOne().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Equipe).WithOne().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Convidados).WithOne().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Parcelas).WithOne(p => p.Evento).HasForeignKey(p => p.EventoId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Inicio);
        b.HasIndex(x => new { x.EspacoId, x.Inicio });
        b.HasIndex(x => x.Status);
    }
}

internal sealed class EventoServicoConfig : IEntityTypeConfiguration<EventoServico>
{
    public void Configure(EntityTypeBuilder<EventoServico> b)
    {
        b.ToTable("EventoServicos");
        b.Property(x => x.Descricao).HasMaxLength(150).IsRequired();
        b.HasOne<ServicoAdicional>().WithMany().HasForeignKey(x => x.ServicoAdicionalId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EventoFuncionarioConfig : IEntityTypeConfiguration<EventoFuncionario>
{
    public void Configure(EntityTypeBuilder<EventoFuncionario> b)
    {
        b.ToTable("EventoEquipe");
        b.Property(x => x.Funcao).HasMaxLength(80).IsRequired();
        b.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EventoId, x.FuncionarioId }).IsUnique();
    }
}

internal sealed class ConvidadoConfig : IEntityTypeConfiguration<Convidado>
{
    public void Configure(EntityTypeBuilder<Convidado> b)
    {
        b.ToTable("Convidados");
        b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        b.Property(x => x.Telefone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Mesa).HasMaxLength(20);
        b.HasIndex(x => new { x.EventoId, x.Nome });
    }
}
