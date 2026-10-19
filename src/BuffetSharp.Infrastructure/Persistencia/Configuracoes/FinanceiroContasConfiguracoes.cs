using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class ContaFinanceiraConfig : IEntityTypeConfiguration<ContaFinanceira>
{
    public void Configure(EntityTypeBuilder<ContaFinanceira> b)
    {
        b.ToTable("ContasFinanceiras");
        b.Ignore(x => x.CamposControladosPeloSistema);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class MovimentoContaConfig : IEntityTypeConfiguration<MovimentoConta>
{
    public void Configure(EntityTypeBuilder<MovimentoConta> b)
    {
        b.ToTable("MovimentosConta");
        b.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaFinanceiraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contrapartida).WithMany().HasForeignKey(x => x.ContrapartidaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ContaFinanceiraId, x.Data });
    }
}

internal sealed class FechamentoContaConfig : IEntityTypeConfiguration<FechamentoConta>
{
    public void Configure(EntityTypeBuilder<FechamentoConta> b)
    {
        b.ToTable("FechamentosConta");
        b.Property(x => x.Observacao).HasMaxLength(500);
        b.Property(x => x.Usuario).HasMaxLength(60);
        b.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaFinanceiraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Ajuste).WithMany().HasForeignKey(x => x.AjusteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ContaFinanceiraId, x.Data }).IsUnique();
    }
}

internal sealed class CartaoCreditoConfig : IEntityTypeConfiguration<CartaoCredito>
{
    public void Configure(EntityTypeBuilder<CartaoCredito> b)
    {
        b.ToTable("CartoesCredito");
        b.Property(x => x.Final).HasMaxLength(4);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class BaixaConfig : IEntityTypeConfiguration<Baixa>
{
    public void Configure(EntityTypeBuilder<Baixa> b)
    {
        b.ToTable("Baixas");
        b.Property(x => x.Observacao).HasMaxLength(500);
        b.Property(x => x.Usuario).HasMaxLength(60);
        b.HasMany(x => x.Titulos).WithOne().HasForeignKey(x => x.BaixaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Formas).WithOne().HasForeignKey(x => x.BaixaId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Titulos).HasField("_titulos");
        b.Navigation(x => x.Formas).HasField("_formas");
        b.HasIndex(x => x.Data);
    }
}

internal sealed class BaixaTituloConfig : IEntityTypeConfiguration<BaixaTitulo>
{
    public void Configure(EntityTypeBuilder<BaixaTitulo> b)
    {
        b.ToTable("BaixaTitulos");
        b.HasOne(x => x.Parcela).WithMany().HasForeignKey(x => x.ParcelaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ContaPagar).WithMany().HasForeignKey(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);

        // O saldo de um pagamento parcial pode ser substituído (ex.: novo plano de pagamento do evento): o vínculo some.
        b.HasOne(x => x.SaldoParcela).WithMany().HasForeignKey(x => x.SaldoParcelaId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.SaldoContaPagar).WithMany().HasForeignKey(x => x.SaldoContaPagarId).OnDelete(DeleteBehavior.SetNull);
        b.Ignore(x => x.Titulo);
        b.Ignore(x => x.Saldo);
    }
}

internal sealed class BaixaFormaConfig : IEntityTypeConfiguration<BaixaForma>
{
    public void Configure(EntityTypeBuilder<BaixaForma> b)
    {
        b.ToTable("BaixaFormas");
        b.HasOne(x => x.FormaPagamento).WithMany().HasForeignKey(x => x.FormaPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaFinanceiraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cartao).WithMany().HasForeignKey(x => x.CartaoCreditoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Movimento).WithOne().HasForeignKey<MovimentoConta>(x => x.BaixaFormaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Faturas).WithOne(x => x.OrigemBaixaForma).HasForeignKey(x => x.OrigemBaixaFormaId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Faturas).HasField("_faturas");
    }
}
