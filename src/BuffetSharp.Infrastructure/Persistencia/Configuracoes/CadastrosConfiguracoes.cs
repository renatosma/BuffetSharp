using BuffetSharp.Domain.Cadastros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

// Tamanhos e obrigatoriedade vêm das DataAnnotations das entidades; aqui ficam tabelas,
// índices, relacionamentos e precisões específicas.

internal sealed class TipoEventoConfig : IEntityTypeConfiguration<TipoEvento>
{
    public void Configure(EntityTypeBuilder<TipoEvento> b)
    {
        b.ToTable("TiposEvento");
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class CategoriaProdutoConfig : IEntityTypeConfiguration<CategoriaProduto>
{
    public void Configure(EntityTypeBuilder<CategoriaProduto> b)
    {
        b.ToTable("CategoriasProduto");
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class CategoriaCardapioConfig : IEntityTypeConfiguration<CategoriaCardapio>
{
    public void Configure(EntityTypeBuilder<CategoriaCardapio> b)
    {
        b.ToTable("CategoriasCardapio");
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class UnidadeMedidaConfig : IEntityTypeConfiguration<UnidadeMedida>
{
    public void Configure(EntityTypeBuilder<UnidadeMedida> b)
    {
        b.ToTable("UnidadesMedida");
        b.HasIndex(x => x.Sigla).IsUnique();
    }
}

internal sealed class FormaPagamentoConfig : IEntityTypeConfiguration<FormaPagamento>
{
    public void Configure(EntityTypeBuilder<FormaPagamento> b)
    {
        b.ToTable("FormasPagamento");
        b.HasIndex(x => x.Nome).IsUnique();
        b.Property(x => x.TaxaPercentual).HasPrecision(5, 2);
    }
}

internal sealed class CondicaoPagamentoConfig : IEntityTypeConfiguration<CondicaoPagamento>
{
    public void Configure(EntityTypeBuilder<CondicaoPagamento> b)
    {
        b.ToTable("CondicoesPagamento");
        b.HasIndex(x => x.Nome).IsUnique();
        b.Property(x => x.PercentualEntrada).HasPrecision(5, 2);
        b.HasOne(x => x.FormaPagamento).WithMany().HasForeignKey(x => x.FormaPagamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Parcelas).WithOne().HasForeignKey(x => x.CondicaoPagamentoId).OnDelete(DeleteBehavior.Cascade);
        b.Ignore(x => x.AVista);
        b.Ignore(x => x.ParcelasDefinidas);
        b.Ignore(x => x.Resumo);
        b.Ignore(x => x.Texto);
    }
}

internal sealed class CondicaoPagamentoParcelaConfig : IEntityTypeConfiguration<CondicaoPagamentoParcela>
{
    public void Configure(EntityTypeBuilder<CondicaoPagamentoParcela> b)
    {
        b.ToTable("CondicaoPagamentoParcelas");
        b.Property(x => x.Percentual).HasPrecision(5, 2);
        b.HasOne(x => x.FormaPagamento).WithMany().HasForeignKey(x => x.FormaPagamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CargoConfig : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> b)
    {
        b.ToTable("Cargos");
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.ComplexProperty(x => x.Endereco);
        b.HasMany(x => x.Contatos).WithOne().HasForeignKey("ClienteId").OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Enderecos).WithOne().HasForeignKey("ClienteId").OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Nome);
        b.HasIndex(x => x.Documento).IsUnique().HasFilter("[Documento] IS NOT NULL");
    }
}

internal sealed class FornecedorConfig : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> b)
    {
        b.ToTable("Fornecedores");
        b.ComplexProperty(x => x.Endereco);
        b.HasMany(x => x.Contatos).WithOne().HasForeignKey("FornecedorId").OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Enderecos).WithOne().HasForeignKey("FornecedorId").OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Documento).IsUnique().HasFilter("[Documento] IS NOT NULL");
    }
}

internal sealed class FuncionarioConfig : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> b)
    {
        b.ToTable("Funcionarios");
        b.ComplexProperty(x => x.Endereco);
        b.HasMany(x => x.Contatos).WithOne().HasForeignKey("FuncionarioId").OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Enderecos).WithOne().HasForeignKey("FuncionarioId").OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Cargo).WithMany().HasForeignKey(x => x.CargoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Cpf).IsUnique().HasFilter("[Cpf] IS NOT NULL");
    }
}

internal sealed class EspacoConfig : IEntityTypeConfiguration<Espaco>
{
    public void Configure(EntityTypeBuilder<Espaco> b)
    {
        b.ToTable("Espacos");
        b.ComplexProperty(x => x.Endereco);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class ServicoAdicionalConfig : IEntityTypeConfiguration<ServicoAdicional>
{
    public void Configure(EntityTypeBuilder<ServicoAdicional> b)
    {
        b.ToTable("ServicosAdicionais");
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

/// <summary>
/// Contatos adicionais de clientes, fornecedores e funcionários numa única tabela: cada linha pertence a um só dono
/// (uma das chaves ClienteId, FornecedorId ou FuncionarioId preenchida) e é excluída junto com ele.
/// </summary>
internal sealed class ContatoAdicionalConfig : IEntityTypeConfiguration<ContatoAdicional>
{
    public void Configure(EntityTypeBuilder<ContatoAdicional> b)
    {
        b.ToTable("ContatosAdicionais");
        b.Ignore(x => x.EhEmail);
    }
}

internal sealed class EnderecoAdicionalConfig : IEntityTypeConfiguration<EnderecoAdicional>
{
    public void Configure(EntityTypeBuilder<EnderecoAdicional> b)
    {
        b.ToTable("EnderecosAdicionais");
        b.ComplexProperty(x => x.Endereco);
    }
}
