using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

// Compras: cotação -> pedido de compra -> recebimento (estoque) -> nota fiscal de entrada (contas a pagar).

internal sealed class CotacaoCompraConfig : IEntityTypeConfiguration<CotacaoCompra>
{
    public void Configure(EntityTypeBuilder<CotacaoCompra> b)
    {
        b.ToTable("CotacoesCompra");
        b.Property(x => x.Descricao).HasMaxLength(CotacaoCompra.TamanhoDescricao).IsRequired();
        b.Property(x => x.Observacoes).HasMaxLength(1000);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(500);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.CotacaoCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Participantes).WithOne().HasForeignKey(x => x.CotacaoCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Precos).WithOne().HasForeignKey(x => x.CotacaoCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.Data });
    }
}

internal sealed class CotacaoItemConfig : IEntityTypeConfiguration<CotacaoItem>
{
    public void Configure(EntityTypeBuilder<CotacaoItem> b)
    {
        b.ToTable("CotacaoItens");
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.Property(x => x.Especificacao).HasMaxLength(CotacaoItem.TamanhoEspecificacao);
        b.Property(x => x.MotivoEscolha).HasMaxLength(CotacaoItem.TamanhoMotivo);
        b.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);

        // Sem cascata (o SQL Server não aceita vários caminhos): a cotação limpa o vencedor ao retirar o participante.
        b.HasOne(x => x.Vencedor).WithMany().HasForeignKey(x => x.VencedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CotacaoParticipanteConfig : IEntityTypeConfiguration<CotacaoParticipante>
{
    public void Configure(EntityTypeBuilder<CotacaoParticipante> b)
    {
        b.ToTable("CotacaoParticipantes");
        b.Property(x => x.Nome).HasMaxLength(CotacaoParticipante.TamanhoNome).IsRequired();
        b.Property(x => x.Contato).HasMaxLength(100);
        b.Property(x => x.Telefone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.CondicaoPagamento).HasMaxLength(100);
        b.Property(x => x.Observacoes).HasMaxLength(500);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CotacaoPrecoConfig : IEntityTypeConfiguration<CotacaoPreco>
{
    public void Configure(EntityTypeBuilder<CotacaoPreco> b)
    {
        b.ToTable("CotacaoPrecos");
        b.Property(x => x.PrecoUnitario).HasPrecision(18, 4);
        b.Property(x => x.Observacao).HasMaxLength(CotacaoPreco.TamanhoObservacao);

        // Os preços são apagados pela cotação (que os retira junto com o produto ou o participante).
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.CotacaoItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Participante).WithMany().HasForeignKey(x => x.CotacaoParticipanteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CotacaoItemId, x.CotacaoParticipanteId }).IsUnique();
    }
}

internal sealed class PedidoCompraConfig : IEntityTypeConfiguration<PedidoCompra>
{
    public void Configure(EntityTypeBuilder<PedidoCompra> b)
    {
        b.ToTable("PedidosCompra");
        b.Property(x => x.Observacoes).HasMaxLength(1000);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(500);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CotacaoCompra).WithMany().HasForeignKey(x => x.CotacaoCompraId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.PedidoCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.Data });
    }
}

internal sealed class PedidoCompraItemConfig : IEntityTypeConfiguration<PedidoCompraItem>
{
    public void Configure(EntityTypeBuilder<PedidoCompraItem> b)
    {
        b.ToTable("PedidoCompraItens");
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.Property(x => x.QuantidadeRecebida).HasPrecision(18, 3);
        b.Property(x => x.CustoUnitario).HasPrecision(18, 4);
        b.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Embalagem).WithMany().HasForeignKey(x => x.ProdutoEmbalagemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RecebimentoCompraConfig : IEntityTypeConfiguration<RecebimentoCompra>
{
    public void Configure(EntityTypeBuilder<RecebimentoCompra> b)
    {
        b.ToTable("RecebimentosCompra");
        b.Property(x => x.Observacoes).HasMaxLength(RecebimentoCompra.TamanhoObservacoes);
        b.Property(x => x.MotivoEstorno).HasMaxLength(RecebimentoCompra.TamanhoObservacoes);
        b.Property(x => x.Usuario).HasMaxLength(50);
        b.HasOne(x => x.PedidoCompra).WithMany().HasForeignKey(x => x.PedidoCompraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Fornecedor>().WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.NotaFiscalEntrada).WithMany(x => x.Recebimentos).HasForeignKey(x => x.NotaFiscalEntradaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.RecebimentoCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.Data });
        b.HasIndex(x => new { x.FornecedorId, x.Status });
    }
}

internal sealed class RecebimentoItemConfig : IEntityTypeConfiguration<RecebimentoItem>
{
    public void Configure(EntityTypeBuilder<RecebimentoItem> b)
    {
        b.ToTable("RecebimentoItens");
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.Property(x => x.CustoUnitario).HasPrecision(18, 4);
        b.HasOne(x => x.PedidoCompraItem).WithMany().HasForeignKey(x => x.PedidoCompraItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Entrada).WithMany().HasForeignKey(x => x.MovimentacaoEstoqueId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotaFiscalEntradaConfig : IEntityTypeConfiguration<NotaFiscalEntrada>
{
    public void Configure(EntityTypeBuilder<NotaFiscalEntrada> b)
    {
        b.ToTable("NotasFiscaisEntrada");
        b.Property(x => x.Numero).HasMaxLength(NotaFiscalEntrada.TamanhoNumero).IsRequired();
        b.Property(x => x.Serie).HasMaxLength(NotaFiscalEntrada.TamanhoSerie);
        b.Property(x => x.ChaveAcesso).HasMaxLength(NotaFiscalEntrada.TamanhoChave);
        b.Property(x => x.Observacoes).HasMaxLength(500);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(500);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);

        // Excluir a conta ou desfazer o parcelamento desfaz o vínculo antes (serviço financeiro).
        b.HasOne(x => x.ContaPagar).WithMany().HasForeignKey(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ParcelamentoPagar).WithMany().HasForeignKey(x => x.ParcelamentoPagarId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.FornecedorId, x.Numero });
        b.HasIndex(x => x.DataEmissao);
        b.HasIndex(x => x.ChaveAcesso);
    }
}

internal sealed class CodigoProdutoFornecedorConfig : IEntityTypeConfiguration<CodigoProdutoFornecedor>
{
    public void Configure(EntityTypeBuilder<CodigoProdutoFornecedor> b)
    {
        b.ToTable("CodigosProdutoFornecedor");
        b.Property(x => x.Codigo).HasMaxLength(CodigoProdutoFornecedor.TamanhoCodigo).IsRequired();
        b.Property(x => x.Descricao).HasMaxLength(CodigoProdutoFornecedor.TamanhoDescricao);
        b.HasOne<Fornecedor>().WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        // Embalagem retirada do produto: o vínculo continua, na unidade do estoque.
        b.HasOne(x => x.Embalagem).WithMany().HasForeignKey(x => x.ProdutoEmbalagemId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.FornecedorId, x.Codigo }).IsUnique();
    }
}
