using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class LeadConfig : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("Leads");
        b.Property(x => x.Nome).HasMaxLength(Lead.TamanhoNome).IsRequired();
        b.Property(x => x.Telefone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.DetalheOrigem).HasMaxLength(150);
        b.Property(x => x.Observacoes).HasMaxLength(2000);
        b.Property(x => x.MotivoPerda).HasMaxLength(500);
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Responsavel).WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Interacoes).WithOne().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.ProximoContato });
        b.HasIndex(x => x.CriadoEm);
    }
}

internal sealed class LeadInteracaoConfig : IEntityTypeConfiguration<LeadInteracao>
{
    public void Configure(EntityTypeBuilder<LeadInteracao> b)
    {
        b.ToTable("LeadInteracoes");
        b.Property(x => x.Usuario).HasMaxLength(60);
        b.Property(x => x.Descricao).HasMaxLength(LeadInteracao.TamanhoDescricao).IsRequired();
        b.HasIndex(x => new { x.LeadId, x.Data });
    }
}

internal sealed class LembreteTratadoConfig : IEntityTypeConfiguration<LembreteTratado>
{
    public void Configure(EntityTypeBuilder<LembreteTratado> b)
    {
        b.ToTable("LembretesTratados");
        b.Property(x => x.Chave).HasMaxLength(LembreteTratado.TamanhoChave).IsRequired();
        b.Property(x => x.Usuario).HasMaxLength(60);
        b.HasIndex(x => x.Chave).IsUnique();
        b.HasIndex(x => x.TratadoEm);
    }
}

internal sealed class ReajustePrecoConfig : IEntityTypeConfiguration<ReajustePreco>
{
    public void Configure(EntityTypeBuilder<ReajustePreco> b)
    {
        b.ToTable("ReajustesPreco");
        b.Property(x => x.Descricao).HasMaxLength(100).IsRequired();
        b.Property(x => x.Percentual).HasPrecision(5, 2);
        b.Property(x => x.Usuario).HasMaxLength(60);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ReajustePrecoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.VigenciaEm });
    }
}

internal sealed class ReajusteItemConfig : IEntityTypeConfiguration<ReajusteItem>
{
    public void Configure(EntityTypeBuilder<ReajusteItem> b)
    {
        b.ToTable("ReajusteItens");
        b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.ReajustePrecoId, x.Tipo, x.ItemId }).IsUnique();
    }
}
