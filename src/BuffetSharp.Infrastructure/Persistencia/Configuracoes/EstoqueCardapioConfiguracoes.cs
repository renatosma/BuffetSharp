using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class ProdutoConfig : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> b)
    {
        b.ToTable("Produtos");
        b.Property(x => x.CustoMedio).HasPrecision(18, 4);
        b.Property(x => x.EstoqueAtual).HasPrecision(18, 3);
        b.Property(x => x.EstoqueMinimo).HasPrecision(18, 3);
        b.HasOne(x => x.CategoriaProduto).WithMany().HasForeignKey(x => x.CategoriaProdutoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UnidadeMedida).WithMany().HasForeignKey(x => x.UnidadeMedidaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Embalagens).WithOne().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Lotes).WithOne(l => l.Produto).HasForeignKey(l => l.ProdutoId).OnDelete(DeleteBehavior.Cascade);
        b.Ignore(x => x.SaldoSemLote);
        b.HasIndex(x => x.Nome);
        b.HasIndex(x => x.Codigo).IsUnique().HasFilter("[Codigo] IS NOT NULL");
    }
}

internal sealed class ProdutoEmbalagemConfig : IEntityTypeConfiguration<ProdutoEmbalagem>
{
    public void Configure(EntityTypeBuilder<ProdutoEmbalagem> b)
    {
        b.ToTable("ProdutoEmbalagens");
        b.Property(x => x.Fator).HasPrecision(18, 4);
        b.Property(x => x.Descricao).HasMaxLength(60);
        b.Property(x => x.CodigoFornecedor).HasMaxLength(30);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UnidadeMedida).WithMany().HasForeignKey(x => x.UnidadeMedidaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MovimentacaoEstoqueConfig : IEntityTypeConfiguration<MovimentacaoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoque> b)
    {
        b.ToTable("MovimentacoesEstoque");
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.Property(x => x.CustoUnitario).HasPrecision(18, 4);
        b.Property(x => x.SaldoApos).HasPrecision(18, 3);
        b.Property(x => x.Observacao).HasMaxLength(300);
        b.Property(x => x.UnidadeCompra).HasMaxLength(10);
        b.Property(x => x.QuantidadeCompra).HasPrecision(18, 3);
        b.Property(x => x.FatorConversao).HasPrecision(18, 4);
        b.Property(x => x.CustoUnitarioCompra).HasPrecision(18, 4);
        b.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Evento>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);

        // Conta a pagar da compra. Excluir a conta ou desfazer o parcelamento desfaz o vínculo antes (serviço financeiro).
        b.HasOne(x => x.ContaPagar).WithMany().HasForeignKey(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ParcelamentoPagar).WithMany().HasForeignKey(x => x.ParcelamentoPagarId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EstornoDe).WithMany().HasForeignKey(x => x.EstornoDeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Fornecedor>().WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProdutoId, x.Data });
        b.HasIndex(x => x.Data);
        b.HasMany(x => x.Lotes).WithOne().HasForeignKey(l => l.MovimentacaoEstoqueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LoteEstoqueConfig : IEntityTypeConfiguration<LoteEstoque>
{
    public void Configure(EntityTypeBuilder<LoteEstoque> b)
    {
        b.ToTable("LotesEstoque");
        b.Property(x => x.Numero).HasMaxLength(LoteEstoque.TamanhoNumero);
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.Property(x => x.Saldo).HasPrecision(18, 3);
        b.Property(x => x.CustoUnitario).HasPrecision(18, 4);
        b.Ignore(x => x.Descricao);
        b.HasIndex(x => new { x.ProdutoId, x.Validade });
        b.HasIndex(x => x.Validade);
    }
}

internal sealed class MovimentacaoLoteConfig : IEntityTypeConfiguration<MovimentacaoLote>
{
    public void Configure(EntityTypeBuilder<MovimentacaoLote> b)
    {
        b.ToTable("MovimentacaoLotes");
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.HasOne(x => x.Lote).WithMany().HasForeignKey(x => x.LoteEstoqueId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ItemCardapioConfig : IEntityTypeConfiguration<ItemCardapio>
{
    public void Configure(EntityTypeBuilder<ItemCardapio> b)
    {
        b.ToTable("ItensCardapio");
        b.HasOne(x => x.CategoriaCardapio).WithMany().HasForeignKey(x => x.CategoriaCardapioId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Ingredientes).WithOne().HasForeignKey(x => x.ItemCardapioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class FichaTecnicaItemConfig : IEntityTypeConfiguration<FichaTecnicaItem>
{
    public void Configure(EntityTypeBuilder<FichaTecnicaItem> b)
    {
        b.ToTable("FichasTecnicas");
        b.HasKey(x => new { x.ItemCardapioId, x.ProdutoId });
        b.Property(x => x.QuantidadePorPessoa).HasPrecision(18, 4);
        b.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PacoteConfig : IEntityTypeConfiguration<Pacote>
{
    public void Configure(EntityTypeBuilder<Pacote> b)
    {
        b.ToTable("Pacotes");
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.PacoteId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class PacoteItemConfig : IEntityTypeConfiguration<PacoteItem>
{
    public void Configure(EntityTypeBuilder<PacoteItem> b)
    {
        b.ToTable("PacoteItens");
        b.HasKey(x => new { x.PacoteId, x.ItemCardapioId });
        b.HasOne(x => x.ItemCardapio).WithMany().HasForeignKey(x => x.ItemCardapioId).OnDelete(DeleteBehavior.Restrict);
    }
}
