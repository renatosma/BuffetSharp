using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Infrastructure.Persistencia;
using BuffetSharp.Infrastructure.Persistencia.Provedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuffetSharp.Tests.Bancos;

/// <summary>
/// Verificações que não precisam de servidor: o modelo de cada banco (nomes físicos, tipos e índices), a coerência
/// entre o modelo e as migrations de cada provedor e a tradução do dialeto SQL. Valem inclusive para o Oracle, que
/// não roda no ambiente de testes local.
/// </summary>
public class ProvedoresTests
{
    private static readonly Dictionary<TipoBancoDados, string> ConexoesFicticias = new()
    {
        [TipoBancoDados.SqlServer] = "Server=localhost;Database=buffetsharp;Trusted_Connection=True",
        [TipoBancoDados.PostgreSql] = "Host=localhost;Database=buffetsharp;Username=x;Password=x",
        [TipoBancoDados.MySql] = "Server=localhost;Database=buffetsharp;User Id=x;Password=x",
        [TipoBancoDados.Oracle] = "User Id=x;Password=x;Data Source=localhost:1521/FREEPDB1"
    };

    private static BuffetDbContext Contexto(TipoBancoDados tipo) =>
        ((ProvedorBanco)CatalogoProvedores.Obter(tipo)).CriarContexto(ConexoesFicticias[tipo]);

    private static IEntityType Entidade<T>(DbContext contexto) => contexto.Model.FindEntityType(typeof(T))!;

    private static IProperty Propriedade<T>(DbContext contexto, string nome) => Entidade<T>(contexto).FindProperty(nome)!;

    [Theory]
    [InlineData("SqlServer", TipoBancoDados.SqlServer)]
    [InlineData("mssql", TipoBancoDados.SqlServer)]
    [InlineData("Postgres", TipoBancoDados.PostgreSql)]
    [InlineData("MySQL", TipoBancoDados.MySql)]
    [InlineData("oracle", TipoBancoDados.Oracle)]
    [InlineData(null, TipoBancoDados.SqlServer)]
    public void Catalogo_interpreta_o_nome_e_carrega_o_provedor(string? nome, TipoBancoDados esperado)
    {
        var tipo = CatalogoProvedores.Interpretar(nome);
        Assert.Equal(esperado, tipo);
        Assert.Equal(esperado, CatalogoProvedores.Obter(tipo).Tipo);
    }

    [Fact]
    public void Banco_desconhecido_gera_mensagem_clara() =>
        Assert.Contains("não suportado", Assert.Throws<InvalidOperationException>(() => CatalogoProvedores.Interpretar("db2")).Message);

    [Theory]
    [InlineData(TipoBancoDados.SqlServer)]
    [InlineData(TipoBancoDados.PostgreSql)]
    [InlineData(TipoBancoDados.MySql)]
    [InlineData(TipoBancoDados.Oracle)]
    public void Migrations_do_banco_estao_atualizadas_com_o_modelo(TipoBancoDados tipo)
    {
        using var contexto = Contexto(tipo);
        Assert.False(contexto.Database.HasPendingModelChanges(),
            $"O modelo mudou e falta uma migration para {tipo}: dotnet ef migrations add <Nome> --project src/BuffetSharp.Infrastructure.{tipo}");
        Assert.NotEmpty(contexto.Database.GetMigrations());
    }

    [Theory]
    [InlineData(TipoBancoDados.SqlServer, "Clientes", "Endereco_Cep")]
    [InlineData(TipoBancoDados.PostgreSql, "clientes", "endereco_cep")]
    [InlineData(TipoBancoDados.MySql, "Clientes", "Endereco_Cep")]
    [InlineData(TipoBancoDados.Oracle, "CLIENTES", "ENDERECO_CEP")]
    public void Nomes_fisicos_seguem_a_regra_do_banco(TipoBancoDados tipo, string tabela, string colunaEndereco)
    {
        using var contexto = Contexto(tipo);
        var cliente = Entidade<Cliente>(contexto);
        Assert.Equal(tabela, cliente.GetTableName());
        var cep = cliente.GetComplexProperties().Single().ComplexType.FindProperty("Cep")!;
        Assert.Equal(colunaEndereco, cep.GetColumnName(StoreObjectIdentifier.Table(tabela, cliente.GetSchema())));
    }

    [Theory]
    [InlineData(TipoBancoDados.SqlServer, true)]
    [InlineData(TipoBancoDados.PostgreSql, true)]
    [InlineData(TipoBancoDados.MySql, false)]
    [InlineData(TipoBancoDados.Oracle, false)]
    public void Contrato_vigente_unico_por_evento_usa_indice_filtrado_onde_existe(TipoBancoDados tipo, bool unico)
    {
        using var contexto = Contexto(tipo);
        var indice = Entidade<Contrato>(contexto).GetIndexes().Single(i => i.Properties.Single().Name == nameof(Contrato.EventoId));
        Assert.Equal(unico, indice.IsUnique);
        Assert.Equal(unico, indice.GetFilter() is not null);

        // "Único entre os não nulos" continua único em todos (MySQL e Oracle já aceitam vários NULL).
        Assert.True(Entidade<Cliente>(contexto).GetIndexes().Single(i => i.Properties.Single().Name == nameof(Cliente.Documento)).IsUnique);
    }

    [Fact]
    public void Oracle_usa_tipos_suportados_pelo_19c()
    {
        using var contexto = Contexto(TipoBancoDados.Oracle);
        Assert.Equal("NCLOB", Propriedade<Contrato>(contexto, nameof(Contrato.Conteudo)).GetColumnType());
        Assert.Equal("BLOB", Propriedade<Contrato>(contexto, nameof(Contrato.AssinaturaCliente)).GetColumnType());
        Assert.Equal("DATE", Propriedade<Parcela>(contexto, nameof(Parcela.Vencimento)).GetColumnType());
        Assert.Equal("NUMBER(1)", Propriedade<Cliente>(contexto, nameof(Cliente.Ativo)).GetColumnType());
    }

    [Fact]
    public void PostgreSql_guarda_data_hora_sem_fuso()
    {
        using var contexto = Contexto(TipoBancoDados.PostgreSql);
        Assert.Equal("timestamp without time zone", Propriedade<Orcamento>(contexto, nameof(Orcamento.Inicio)).GetColumnType());
    }

    private const string Consulta = """
        SELECT Id FROM Clientes WHERE (@Todos = 1 OR Ativo = {verdadeiro}) AND Nome <> @Nome
         ORDER BY Nome
         {limite:@Limite};
        """;

    [Theory]
    [InlineData(TipoBancoDados.SqlServer, "Ativo = 1", "@Nome", "OFFSET 0 ROWS FETCH NEXT @Limite ROWS ONLY")]
    [InlineData(TipoBancoDados.PostgreSql, "Ativo = TRUE", "@Nome", "OFFSET 0 ROWS FETCH NEXT @Limite ROWS ONLY")]
    [InlineData(TipoBancoDados.MySql, "Ativo = 1", "@Nome", "LIMIT @Limite")]
    [InlineData(TipoBancoDados.Oracle, "Ativo = 1", ":Nome", "OFFSET 0 ROWS FETCH NEXT :Limite ROWS ONLY")]
    public void Dialeto_traduz_marcadores_e_parametros(TipoBancoDados tipo, string booleano, string parametro, string limite)
    {
        var sql = CatalogoProvedores.Obter(tipo).Dialeto.Preparar(Consulta);
        Assert.Contains(booleano, sql);
        Assert.Contains(parametro, sql);
        Assert.EndsWith(limite, sql);
        Assert.DoesNotContain("{", sql);
        Assert.False(sql.EndsWith(';'));
        if (tipo == TipoBancoDados.Oracle)
            Assert.DoesNotContain("@", sql);
    }

    [Fact]
    public void Oracle_seleciona_sem_tabela_a_partir_de_dual_e_nao_usa_lote()
    {
        var dialeto = CatalogoProvedores.Obter(TipoBancoDados.Oracle).Dialeto;
        Assert.Equal("SELECT 1 FROM DUAL", dialeto.Preparar("SELECT 1{semTabela}"));
        Assert.False(dialeto.SuportaLote);
    }
}
