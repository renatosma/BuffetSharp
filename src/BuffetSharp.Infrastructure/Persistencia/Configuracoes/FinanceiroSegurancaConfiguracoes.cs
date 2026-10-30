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
        b.Property(x => x.Descricao).HasMaxLength(ContaPagar.TamanhoDescricao).IsRequired();
        b.Property(x => x.Documento).HasMaxLength(ContaPagar.TamanhoDocumento);
        b.Property(x => x.Observacao).HasMaxLength(500);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FormaPagamento).WithMany().HasForeignKey(x => x.FormaPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.Vencimento });
        b.HasIndex(x => x.DataPagamento);
    }
}

internal sealed class ParcelamentoPagarConfig : IEntityTypeConfiguration<ParcelamentoPagar>
{
    public void Configure(EntityTypeBuilder<ParcelamentoPagar> b)
    {
        b.ToTable("ParcelamentosPagar");
        b.Property(x => x.Descricao).HasMaxLength(ContaPagar.TamanhoDescricao).IsRequired();
        b.Property(x => x.Documento).HasMaxLength(ContaPagar.TamanhoDocumento);
        b.Property(x => x.Observacao).HasMaxLength(ParcelamentoPagar.TamanhoObservacao);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Parcelas).WithOne(c => c.Parcelamento).HasForeignKey(c => c.ParcelamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Agrupadas).WithOne(c => c.AgrupadaEm).HasForeignKey(c => c.AgrupadaEmId).OnDelete(DeleteBehavior.Restrict);
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
        b.Property(x => x.PercentualComissao).HasPrecision(5, 2);
        b.Property(x => x.DescontoMaximo).HasPrecision(5, 2);
        b.Property(x => x.Email).HasMaxLength(Usuario.TamanhoEmail);
        b.Property(x => x.Celular).HasMaxLength(Usuario.TamanhoCelular);
        b.HasIndex(x => x.Login).IsUnique();
    }
}
