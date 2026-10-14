using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class ParcelaConfig : IEntityTypeConfiguration<Parcela>
{
    public void Configure(EntityTypeBuilder<Parcela> b)
    {
        b.ToTable("Parcelas");
        b.Property(x => x.Descricao).HasMaxLength(100).IsRequired();
        b.Property(x => x.Observacao).HasMaxLength(500);
        b.HasOne(x => x.FormaPagamento).WithMany().HasForeignKey(x => x.FormaPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.Vencimento });
        b.HasIndex(x => x.DataPagamento);
    }
}

internal sealed class ContaPagarConfig : IEntityTypeConfiguration<ContaPagar>
{
    public void Configure(EntityTypeBuilder<ContaPagar> b)
    {
        b.ToTable("ContasPagar");
        b.Property(x => x.Descricao).HasMaxLength(150).IsRequired();
        b.Property(x => x.Observacao).HasMaxLength(500);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FormaPagamento).WithMany().HasForeignKey(x => x.FormaPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.Vencimento });
        b.HasIndex(x => x.DataPagamento);
    }
}

internal sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        b.Property(x => x.Login).HasMaxLength(60).IsRequired();
        b.Property(x => x.SenhaHash).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Login).IsUnique();
    }
}
