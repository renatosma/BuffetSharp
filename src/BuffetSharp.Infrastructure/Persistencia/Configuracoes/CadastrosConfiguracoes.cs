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
        b.HasIndex(x => x.Documento).IsUnique().HasFilter("[Documento] IS NOT NULL");
    }
}

internal sealed class FuncionarioConfig : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> b)
    {
        b.ToTable("Funcionarios");
        b.HasOne(x => x.Cargo).WithMany().HasForeignKey(x => x.CargoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Cpf).IsUnique().HasFilter("[Cpf] IS NOT NULL");
    }
}

internal sealed class EspacoConfig : IEntityTypeConfiguration<Espaco>
{
    public void Configure(EntityTypeBuilder<Espaco> b)
    {
        b.ToTable("Espacos");
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
