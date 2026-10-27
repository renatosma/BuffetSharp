using BuffetSharp.Domain.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

// Fiscal: configuração do buffet para a NFS-e e as notas de serviço emitidas pelos eventos.

internal sealed class ConfiguracaoFiscalConfig : IEntityTypeConfiguration<ConfiguracaoFiscal>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoFiscal> b)
    {
        b.ToTable("ConfiguracoesFiscais");
        b.Property(x => x.AliquotaIss).HasPrecision(5, 2);
        b.Property(x => x.Discriminacao).HasMaxLength(2000).IsRequired();
    }
}

internal sealed class NotaFiscalServicoConfig : IEntityTypeConfiguration<NotaFiscalServico>
{
    public void Configure(EntityTypeBuilder<NotaFiscalServico> b)
    {
        b.ToTable("NotasFiscaisServico");
        b.Property(x => x.Referencia).HasMaxLength(30).IsRequired();
        b.Property(x => x.AliquotaIss).HasPrecision(5, 2);
        b.Property(x => x.Discriminacao).HasMaxLength(NotaFiscalServico.TamanhoDiscriminacao).IsRequired();
        b.Property(x => x.TomadorDocumento).HasMaxLength(14).IsRequired();
        b.Property(x => x.TomadorNome).HasMaxLength(150).IsRequired();
        b.Property(x => x.TomadorEmail).HasMaxLength(150);
        b.Property(x => x.Numero).HasMaxLength(20);
        b.Property(x => x.CodigoVerificacao).HasMaxLength(60);
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.CaminhoXml).HasMaxLength(500);
        b.Property(x => x.Mensagem).HasMaxLength(NotaFiscalServico.TamanhoMensagem);
        b.Property(x => x.Usuario).HasMaxLength(50);
        b.Property(x => x.MotivoCancelamento).HasMaxLength(255);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Referencia).IsUnique();
        b.HasIndex(x => new { x.EventoId, x.Status });
        b.HasIndex(x => x.SolicitadaEm);
    }
}
