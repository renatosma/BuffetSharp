using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuffetSharp.Infrastructure.Persistencia.Configuracoes;

internal sealed class ModeloChecklistConfig : IEntityTypeConfiguration<ModeloChecklist>
{
    public void Configure(EntityTypeBuilder<ModeloChecklist> b)
    {
        b.ToTable("ModelosChecklist");
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ModeloChecklistId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class ModeloChecklistItemConfig : IEntityTypeConfiguration<ModeloChecklistItem>
{
    public void Configure(EntityTypeBuilder<ModeloChecklistItem> b)
    {
        b.ToTable("ModeloChecklistItens");
        b.Property(x => x.Descricao).HasMaxLength(ModeloChecklistItem.TamanhoDescricao).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(60);
    }
}

internal sealed class EventoTarefaConfig : IEntityTypeConfiguration<EventoTarefa>
{
    public void Configure(EntityTypeBuilder<EventoTarefa> b)
    {
        b.ToTable("EventoTarefas");
        b.Property(x => x.Descricao).HasMaxLength(EventoTarefa.TamanhoDescricao).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(60);
        b.Property(x => x.ConcluidaPor).HasMaxLength(60);
        b.HasOne<Evento>().WithMany(e => e.Tarefas).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EventoId, x.Prazo });
    }
}

internal sealed class ModeloCronogramaConfig : IEntityTypeConfiguration<ModeloCronograma>
{
    public void Configure(EntityTypeBuilder<ModeloCronograma> b)
    {
        b.ToTable("ModelosCronograma");
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ModeloCronogramaId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Nome).IsUnique();
    }
}

internal sealed class ModeloCronogramaItemConfig : IEntityTypeConfiguration<ModeloCronogramaItem>
{
    public void Configure(EntityTypeBuilder<ModeloCronogramaItem> b)
    {
        b.ToTable("ModeloCronogramaItens");
        b.Property(x => x.Atividade).HasMaxLength(EventoEtapa.TamanhoAtividade).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(EventoEtapa.TamanhoResponsavel);
        b.Property(x => x.Local).HasMaxLength(EventoEtapa.TamanhoLocal);
    }
}

internal sealed class EventoEtapaConfig : IEntityTypeConfiguration<EventoEtapa>
{
    public void Configure(EntityTypeBuilder<EventoEtapa> b)
    {
        b.ToTable("EventoEtapas");
        b.Property(x => x.Atividade).HasMaxLength(EventoEtapa.TamanhoAtividade).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(EventoEtapa.TamanhoResponsavel);
        b.Property(x => x.Local).HasMaxLength(EventoEtapa.TamanhoLocal);
        b.Property(x => x.Observacao).HasMaxLength(EventoEtapa.TamanhoObservacao);
        b.HasOne<Evento>().WithMany(e => e.Etapas).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);

        // Sem cascata: a escala desfaz o vínculo ao tirar o funcionário do evento.
        b.HasOne<Funcionario>().WithMany().HasForeignKey(x => x.FuncionarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EventoId, x.Horario });
    }
}

internal sealed class EventoMesaConfig : IEntityTypeConfiguration<EventoMesa>
{
    public void Configure(EntityTypeBuilder<EventoMesa> b)
    {
        b.ToTable("EventoMesas");
        b.Property(x => x.Nome).HasMaxLength(PlantaSalao.TamanhoNome);
        b.HasOne<Evento>().WithMany(e => e.Mesas).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EventoId, x.Numero }).IsUnique();
    }
}

internal sealed class EventoFornecedorConfig : IEntityTypeConfiguration<EventoFornecedor>
{
    public void Configure(EntityTypeBuilder<EventoFornecedor> b)
    {
        b.ToTable("EventoFornecedores");
        b.Property(x => x.Servico).HasMaxLength(EventoFornecedor.TamanhoServico).IsRequired();
        b.Property(x => x.Responsavel).HasMaxLength(EventoFornecedor.TamanhoResponsavel);
        b.Property(x => x.Telefone).HasMaxLength(EventoFornecedor.TamanhoTelefone);
        b.Property(x => x.Observacao).HasMaxLength(EventoFornecedor.TamanhoObservacao);
        b.Property(x => x.Comentario).HasMaxLength(EventoFornecedor.TamanhoComentario);
        b.HasOne<Evento>().WithMany(e => e.Fornecedores).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EventoServico>().WithMany().HasForeignKey(x => x.EventoServicoId).OnDelete(DeleteBehavior.Restrict);

        // Sem cascata: o evento desfaz os vínculos (pagamento refeito, etapa removida do cronograma).
        b.HasOne(x => x.Pagamento).WithMany().HasForeignKey("PagamentoId").OnDelete(DeleteBehavior.ClientSetNull);
        b.HasOne(x => x.EtapaChegada).WithMany().HasForeignKey("EtapaChegadaId").OnDelete(DeleteBehavior.ClientSetNull);
        b.HasIndex(x => x.FornecedorId);
    }
}

internal sealed class IndisponibilidadeFuncionarioConfig : IEntityTypeConfiguration<IndisponibilidadeFuncionario>
{
    public void Configure(EntityTypeBuilder<IndisponibilidadeFuncionario> b)
    {
        b.ToTable("IndisponibilidadesFuncionario");
        b.Ignore(x => x.Descricao);
        b.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.FuncionarioId, x.Inicio });
    }
}

internal sealed class PesquisaSatisfacaoConfig : IEntityTypeConfiguration<PesquisaSatisfacao>
{
    public void Configure(EntityTypeBuilder<PesquisaSatisfacao> b)
    {
        b.ToTable("PesquisasSatisfacao");
        b.Property(x => x.Comentario).HasMaxLength(PesquisaSatisfacao.TamanhoComentario);
        b.Property(x => x.TratativaDetrator).HasMaxLength(PesquisaSatisfacao.TamanhoTratativa);
        b.Property(x => x.RegistradaPor).HasMaxLength(50);
        b.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.EventoId).IsUnique();
    }
}

internal sealed class EsperaDataConfig : IEntityTypeConfiguration<EsperaData>
{
    public void Configure(EntityTypeBuilder<EsperaData> b)
    {
        b.ToTable("EsperasData");
        b.Property(x => x.Observacao).HasMaxLength(EsperaData.TamanhoObservacao);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Espaco).WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TipoEvento).WithMany().HasForeignKey(x => x.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Orcamento>().WithMany().HasForeignKey(x => x.OrcamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Situacao, x.Inicio });
    }
}

internal sealed class RegraPrecoConfig : IEntityTypeConfiguration<RegraPreco>
{
    public void Configure(EntityTypeBuilder<RegraPreco> b)
    {
        b.ToTable("RegrasPreco");
        b.Property(x => x.Percentual).HasPrecision(6, 2);
        b.Ignore(x => x.Quando);
        b.Ignore(x => x.Ajuste);
        b.HasOne(x => x.Pacote).WithMany().HasForeignKey(x => x.PacoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Espaco).WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EspacoMesaConfig : IEntityTypeConfiguration<EspacoMesa>
{
    public void Configure(EntityTypeBuilder<EspacoMesa> b)
    {
        b.ToTable("EspacoMesas");
        b.Property(x => x.Nome).HasMaxLength(PlantaSalao.TamanhoNome);
        b.HasOne<Espaco>().WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EspacoId, x.Numero }).IsUnique();
    }
}

internal sealed class FechamentoEquipeConfig : IEntityTypeConfiguration<FechamentoEquipe>
{
    public void Configure(EntityTypeBuilder<FechamentoEquipe> b)
    {
        b.ToTable("FechamentosEquipe");
        b.HasOne(x => x.Funcionario).WithMany().HasForeignKey(x => x.FuncionarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ContaPagar).WithOne().HasForeignKey<FechamentoEquipe>(x => x.ContaPagarId).OnDelete(DeleteBehavior.Restrict);

        // Escalas pagas: ao desfazer, o serviço solta as escalas antes de excluir o fechamento.
        b.HasMany(x => x.Escalas).WithOne().HasForeignKey(x => x.FechamentoEquipeId).OnDelete(DeleteBehavior.ClientSetNull);
        b.HasIndex(x => new { x.FuncionarioId, x.De, x.Ate });
    }
}
