using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Cardapio;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Fiscal;
using BuffetSharp.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BuffetSharp.Application.Abstracoes;

/// <summary>
/// Unidade de trabalho do EF Core usada pelos serviços (lado de escrita).
/// Em aplicações desktop, cada operação cria e descarta o seu próprio contexto.
/// </summary>
public interface IBuffetDbContext : IDisposable, IAsyncDisposable
{
    DbSet<TipoEvento> TiposEvento { get; }
    DbSet<CategoriaProduto> CategoriasProduto { get; }
    DbSet<CategoriaCardapio> CategoriasCardapio { get; }
    DbSet<UnidadeMedida> UnidadesMedida { get; }
    DbSet<FormaPagamento> FormasPagamento { get; }
    DbSet<CondicaoPagamento> CondicoesPagamento { get; }
    DbSet<Cargo> Cargos { get; }
    DbSet<Cliente> Clientes { get; }
    DbSet<Fornecedor> Fornecedores { get; }
    DbSet<Funcionario> Funcionarios { get; }
    DbSet<Espaco> Espacos { get; }
    DbSet<ServicoAdicional> ServicosAdicionais { get; }
    DbSet<Produto> Produtos { get; }
    DbSet<MovimentacaoEstoque> MovimentacoesEstoque { get; }
    DbSet<CotacaoCompra> CotacoesCompra { get; }
    DbSet<PedidoCompra> PedidosCompra { get; }
    DbSet<RecebimentoCompra> RecebimentosCompra { get; }
    DbSet<NotaFiscalEntrada> NotasFiscaisEntrada { get; }
    DbSet<CodigoProdutoFornecedor> CodigosProdutoFornecedor { get; }
    DbSet<ProdutoEmbalagem> ProdutoEmbalagens { get; }
    DbSet<ItemCardapio> ItensCardapio { get; }
    DbSet<Pacote> Pacotes { get; }
    DbSet<Orcamento> Orcamentos { get; }
    DbSet<Evento> Eventos { get; }
    DbSet<Parcela> Parcelas { get; }
    DbSet<ContaPagar> ContasPagar { get; }
    DbSet<ParcelamentoPagar> ParcelamentosPagar { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<Material> Materiais { get; }
    DbSet<Contrato> Contratos { get; }
    DbSet<ModeloContrato> ModelosContrato { get; }
    DbSet<Degustacao> Degustacoes { get; }
    DbSet<FechamentoComissao> FechamentosComissao { get; }
    DbSet<ContaFinanceira> ContasFinanceiras { get; }
    DbSet<MovimentoConta> MovimentosConta { get; }
    DbSet<FechamentoConta> FechamentosConta { get; }
    DbSet<CartaoCredito> CartoesCredito { get; }
    DbSet<Baixa> Baixas { get; }
    DbSet<Lead> Leads { get; }
    DbSet<LembreteTratado> LembretesTratados { get; }
    DbSet<ReajustePreco> ReajustesPreco { get; }
    DbSet<ModeloChecklist> ModelosChecklist { get; }
    DbSet<FechamentoEquipe> FechamentosEquipe { get; }
    DbSet<ConfiguracaoCobranca> ConfiguracoesCobranca { get; }
    DbSet<CobrancaRegistrada> CobrancasRegistradas { get; }
    DbSet<ConfiguracaoFiscal> ConfiguracoesFiscais { get; }
    DbSet<NotaFiscalServico> NotasFiscaisServico { get; }
    DbSet<RegistroAuditoria> RegistrosAuditoria { get; }

    ChangeTracker ChangeTracker { get; }

    DbSet<TEntidade> Set<TEntidade>() where TEntidade : class;

    EntityEntry<TEntidade> Entry<TEntidade>(TEntidade entidade) where TEntidade : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IBuffetDbContextFactory
{
    IBuffetDbContext Criar();
}
