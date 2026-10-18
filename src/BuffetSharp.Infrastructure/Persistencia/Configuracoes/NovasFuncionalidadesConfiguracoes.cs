using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class MaterialConfig : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> b)
    {
        b.ToTable("Materiais");
        b.Property(x => x.Observacoes).HasMaxLength(500);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class EventoMaterialConfig : IEntityTypeConfiguration<EventoMaterial>
{
    public void Configure(EntityTypeBuilder<EventoMaterial> b)
    {
        b.ToTable("EventoMateriais");
        b.Property(x => x.CustoLocacaoUnitario).HasPrecision(18, 2);
        b.Property(x => x.ValorReposicaoUnitario).HasPrecision(18, 2);
        b.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Evento>().WithMany(e => e.Materiais).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EventoId, x.MaterialId }).IsUnique();
    }
}

internal sealed class ContratoConfig : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> b)
    {
        b.ToTable("Contratos");
        b.Property(x => x.Conteudo).IsRequired();
        b.Property(x => x.HashConteudo).HasMaxLength(64).IsFixedLength().IsRequired();
        b.Property(x => x.NomeSignatario).HasMaxLength(150);
        b.Property(x => x.DocumentoSignatario).HasMaxLength(14);
        b.Property(x => x.NomeRepresentante).HasMaxLength(150);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(500);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ModeloContrato>().WithMany().HasForeignKey(x => x.ModeloContratoId).OnDelete(DeleteBehavior.Restrict);

        // Um único contrato vigente (não cancelado) por evento.
        b.HasIndex(x => x.EventoId).IsUnique().HasFilter("[Status] <> 'Cancelado'");
    }
}

internal sealed class ModeloContratoConfig : IEntityTypeConfiguration<ModeloContrato>
{
    public void Configure(EntityTypeBuilder<ModeloContrato> b)
    {
        b.ToTable("ModelosContrato");
        b.Property(x => x.Conteudo).IsRequired();
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class DegustacaoConfig : IEntityTypeConfiguration<Degustacao>
{
    public void Configure(EntityTypeBuilder<Degustacao> b)
    {
        b.ToTable("Degustacoes");
        b.Property(x => x.Observacoes).HasMaxLength(1000);
        b.Property(x => x.Comentario).HasMaxLength(1000);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(500);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Pacote).WithMany().HasForeignKey(x => x.PacoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Orcamento).WithMany().HasForeignKey(x => x.OrcamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.DataHora);
        b.HasIndex(x => new { x.Status, x.DataHora });
    }
}

internal sealed class EnvioPropostaConfig : IEntityTypeConfiguration<EnvioProposta>
{
    public void Configure(EntityTypeBuilder<EnvioProposta> b)
    {
        b.ToTable("EnviosProposta");
        b.Property(x => x.Destinatario).HasMaxLength(150).IsRequired();
        b.Property(x => x.Usuario).HasMaxLength(60);
        b.HasOne<Orcamento>().WithMany(o => o.Envios).HasForeignKey(x => x.OrcamentoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OrcamentoId, x.EnviadoEm });
    }
}

internal sealed class FechamentoComissaoConfig : IEntityTypeConfiguration<FechamentoComissao>
{
    public void Configure(EntityTypeBuilder<FechamentoComissao> b)
    {
        b.ToTable("FechamentosComissao");
        b.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ContaPagar).WithOne().HasForeignKey<FechamentoComissao>(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.VendedorId, x.De, x.Ate });
    }
}

internal sealed class RegistroAuditoriaConfig : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        b.ToTable("Auditoria");
        b.Property(x => x.Usuario).HasMaxLength(60).IsRequired();
        b.Property(x => x.Entidade).HasMaxLength(60).IsRequired();
        b.Property(x => x.RegistroId).HasMaxLength(40).IsRequired();
        b.Property(x => x.Descricao).HasMaxLength(200);
        b.Property(x => x.Alteracoes).IsRequired();
        b.HasIndex(x => x.DataHora);
        b.HasIndex(x => new { x.Entidade, x.RegistroId });
    }
}
