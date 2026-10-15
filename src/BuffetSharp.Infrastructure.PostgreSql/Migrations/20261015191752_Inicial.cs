using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    datahora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    usuarioid = table.Column<int>(type: "integer", nullable: true),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    registroid = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    acao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    alteracoes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cargos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    valordiariapadrao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cargos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categoriascardapio",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categoriascardapio", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categoriasproduto",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categoriasproduto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipopessoa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nomefantasia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    celular = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    datanascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    endereco_bairro = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    endereco_cidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "espacos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    capacidademaxima = table.Column<int>(type: "integer", nullable: false),
                    valorlocacao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_espacos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "formaspagamento",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    maximoparcelas = table.Column<int>(type: "integer", nullable: false),
                    taxapercentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formaspagamento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    razaosocial = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nomefantasia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    contato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    endereco_bairro = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    endereco_cidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fornecedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pacotes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    precoporpessoa = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimoconvidados = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pacotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "servicosadicionais",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cobradoporpessoa = table.Column<bool>(type: "boolean", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_servicosadicionais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tiposevento",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tiposevento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidadesmedida",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sigla = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidadesmedida", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    login = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    senhahash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    perfil = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    ultimoacessoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    percentualcomissao = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funcionarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cargoid = table.Column<int>(type: "integer", nullable: false),
                    valordiaria = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dataadmissao = table.Column<DateOnly>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funcionarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_funcionarios_cargos_cargoid",
                        column: x => x.cargoid,
                        principalTable: "cargos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itenscardapio",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categoriacardapioid = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itenscardapio", x => x.id);
                    table.ForeignKey(
                        name: "fk_itenscardapio_categoriascardapio_categoriacardapioid",
                        column: x => x.categoriacardapioid,
                        principalTable: "categoriascardapio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materiais",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantidadepropria = table.Column<int>(type: "integer", nullable: false),
                    valorreposicao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    custolocacao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materiais", x => x.id);
                    table.ForeignKey(
                        name: "fk_materiais_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    categoriaprodutoid = table.Column<int>(type: "integer", nullable: false),
                    unidademedidaid = table.Column<int>(type: "integer", nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    customedio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estoqueatual = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    estoqueminimo = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtos_categoriasproduto_categoriaprodutoid",
                        column: x => x.categoriaprodutoid,
                        principalTable: "categoriasproduto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produtos_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produtos_unidadesmedida_unidademedidaid",
                        column: x => x.unidademedidaid,
                        principalTable: "unidadesmedida",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orcamentos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clienteid = table.Column<int>(type: "integer", nullable: false),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: false),
                    espacoid = table.Column<int>(type: "integer", nullable: true),
                    pacoteid = table.Column<int>(type: "integer", nullable: true),
                    vendedorid = table.Column<int>(type: "integer", nullable: true),
                    inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    quantidadeconvidados = table.Column<int>(type: "integer", nullable: false),
                    validoate = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valorporpessoa = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valorespaco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    percentualdesconto = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    valortotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    motivorecusa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    enviadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    decididoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orcamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_orcamentos_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orcamentos_espacos_espacoid",
                        column: x => x.espacoid,
                        principalTable: "espacos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orcamentos_pacotes_pacoteid",
                        column: x => x.pacoteid,
                        principalTable: "pacotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orcamentos_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orcamentos_usuarios_vendedorid",
                        column: x => x.vendedorid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pacoteitens",
                columns: table => new
                {
                    pacoteid = table.Column<int>(type: "integer", nullable: false),
                    itemcardapioid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pacoteitens", x => new { x.pacoteid, x.itemcardapioid });
                    table.ForeignKey(
                        name: "fk_pacoteitens_itenscardapio_itemcardapioid",
                        column: x => x.itemcardapioid,
                        principalTable: "itenscardapio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pacoteitens_pacotes_pacoteid",
                        column: x => x.pacoteid,
                        principalTable: "pacotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fichastecnicas",
                columns: table => new
                {
                    itemcardapioid = table.Column<int>(type: "integer", nullable: false),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    quantidadeporpessoa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fichastecnicas", x => new { x.itemcardapioid, x.produtoid });
                    table.ForeignKey(
                        name: "fk_fichastecnicas_itenscardapio_itemcardapioid",
                        column: x => x.itemcardapioid,
                        principalTable: "itenscardapio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fichastecnicas_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "degustacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clienteid = table.Column<int>(type: "integer", nullable: false),
                    pacoteid = table.Column<int>(type: "integer", nullable: true),
                    orcamentoid = table.Column<int>(type: "integer", nullable: true),
                    datahora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    pessoas = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    avaliacao = table.Column<int>(type: "integer", nullable: true),
                    comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    motivocancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_degustacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_degustacoes_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_degustacoes_orcamentos_orcamentoid",
                        column: x => x.orcamentoid,
                        principalTable: "orcamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_degustacoes_pacotes_pacoteid",
                        column: x => x.pacoteid,
                        principalTable: "pacotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enviosproposta",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orcamentoid = table.Column<int>(type: "integer", nullable: false),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    destinatario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    enviadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enviosproposta", x => x.id);
                    table.ForeignKey(
                        name: "fk_enviosproposta_orcamentos_orcamentoid",
                        column: x => x.orcamentoid,
                        principalTable: "orcamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    clienteid = table.Column<int>(type: "integer", nullable: false),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: false),
                    espacoid = table.Column<int>(type: "integer", nullable: false),
                    pacoteid = table.Column<int>(type: "integer", nullable: true),
                    orcamentoid = table.Column<int>(type: "integer", nullable: true),
                    vendedorid = table.Column<int>(type: "integer", nullable: true),
                    percentualcomissao = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    quantidadeconvidados = table.Column<int>(type: "integer", nullable: false),
                    valorporpessoa = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valortotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    motivocancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    realizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    estoquebaixado = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventos_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_espacos_espacoid",
                        column: x => x.espacoid,
                        principalTable: "espacos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_orcamentos_orcamentoid",
                        column: x => x.orcamentoid,
                        principalTable: "orcamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_pacotes_pacoteid",
                        column: x => x.pacoteid,
                        principalTable: "pacotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_usuarios_vendedorid",
                        column: x => x.vendedorid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orcamentoservicos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orcamentoid = table.Column<int>(type: "integer", nullable: false),
                    servicoadicionalid = table.Column<int>(type: "integer", nullable: true),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valorunitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orcamentoservicos", x => x.id);
                    table.ForeignKey(
                        name: "fk_orcamentoservicos_orcamentos_orcamentoid",
                        column: x => x.orcamentoid,
                        principalTable: "orcamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_orcamentoservicos_servicosadicionais_servicoadicionalid",
                        column: x => x.servicoadicionalid,
                        principalTable: "servicosadicionais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contaspagar",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    eventoid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    datapagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    valorpago = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    formapagamentoid = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contaspagar", x => x.id);
                    table.ForeignKey(
                        name: "fk_contaspagar_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contaspagar_formaspagamento_formapagamentoid",
                        column: x => x.formapagamentoid,
                        principalTable: "formaspagamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contaspagar_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contratos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    hashconteudo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    emitidoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    assinadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    nomesignatario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    documentosignatario = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    assinaturacliente = table.Column<byte[]>(type: "bytea", nullable: true),
                    nomerepresentante = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    assinaturaempresa = table.Column<byte[]>(type: "bytea", nullable: true),
                    canceladoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivocancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contratos", x => x.id);
                    table.ForeignKey(
                        name: "fk_contratos_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "convidados",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    acompanhantes = table.Column<int>(type: "integer", nullable: false),
                    mesa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rsvp = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    checkinem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convidados", x => x.id);
                    table.ForeignKey(
                        name: "fk_convidados_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventoequipe",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    funcionarioid = table.Column<int>(type: "integer", nullable: false),
                    funcao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    valordiaria = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    presente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventoequipe", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventoequipe_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_eventoequipe_funcionarios_funcionarioid",
                        column: x => x.funcionarioid,
                        principalTable: "funcionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventomateriais",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    materialid = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    quantidadealugada = table.Column<int>(type: "integer", nullable: false),
                    custolocacaounitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valorreposicaounitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantidadedevolvida = table.Column<int>(type: "integer", nullable: true),
                    quantidadeperdida = table.Column<int>(type: "integer", nullable: false),
                    devolvidoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventomateriais", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventomateriais_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_eventomateriais_materiais_materialid",
                        column: x => x.materialid,
                        principalTable: "materiais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventoservicos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    servicoadicionalid = table.Column<int>(type: "integer", nullable: true),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valorunitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventoservicos", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventoservicos_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_eventoservicos_servicosadicionais_servicoadicionalid",
                        column: x => x.servicoadicionalid,
                        principalTable: "servicosadicionais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentacoesestoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    custounitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    saldoapos = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    data = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    eventoid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentacoesestoque", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentacoesestoque_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentacoesestoque_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcelas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    datapagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    valorpago = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    formapagamentoid = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parcelas", x => x.id);
                    table.ForeignKey(
                        name: "fk_parcelas_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_parcelas_formaspagamento_formapagamentoid",
                        column: x => x.formapagamentoid,
                        principalTable: "formaspagamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fechamentoscomissao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vendedorid = table.Column<int>(type: "integer", nullable: false),
                    de = table.Column<DateOnly>(type: "date", nullable: false),
                    ate = table.Column<DateOnly>(type: "date", nullable: false),
                    basecalculo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    contapagarid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fechamentoscomissao", x => x.id);
                    table.ForeignKey(
                        name: "fk_fechamentoscomissao_contaspagar_contapagarid",
                        column: x => x.contapagarid,
                        principalTable: "contaspagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fechamentoscomissao_usuarios_vendedorid",
                        column: x => x.vendedorid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_datahora",
                table: "auditoria",
                column: "datahora");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_entidade_registroid",
                table: "auditoria",
                columns: new[] { "entidade", "registroid" });

            migrationBuilder.CreateIndex(
                name: "ix_cargos_nome",
                table: "cargos",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categoriascardapio_nome",
                table: "categoriascardapio",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categoriasproduto_nome",
                table: "categoriasproduto",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clientes_documento",
                table: "clientes",
                column: "documento",
                unique: true,
                filter: "\"documento\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_nome",
                table: "clientes",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_datapagamento",
                table: "contaspagar",
                column: "datapagamento");

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_eventoid",
                table: "contaspagar",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_formapagamentoid",
                table: "contaspagar",
                column: "formapagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_fornecedorid",
                table: "contaspagar",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_status_vencimento",
                table: "contaspagar",
                columns: new[] { "status", "vencimento" });

            migrationBuilder.CreateIndex(
                name: "ix_contratos_eventoid",
                table: "contratos",
                column: "eventoid",
                unique: true,
                filter: "\"status\" <> 'Cancelado'");

            migrationBuilder.CreateIndex(
                name: "ix_convidados_eventoid_nome",
                table: "convidados",
                columns: new[] { "eventoid", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_degustacoes_clienteid",
                table: "degustacoes",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_degustacoes_datahora",
                table: "degustacoes",
                column: "datahora");

            migrationBuilder.CreateIndex(
                name: "ix_degustacoes_orcamentoid",
                table: "degustacoes",
                column: "orcamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_degustacoes_pacoteid",
                table: "degustacoes",
                column: "pacoteid");

            migrationBuilder.CreateIndex(
                name: "ix_degustacoes_status_datahora",
                table: "degustacoes",
                columns: new[] { "status", "datahora" });

            migrationBuilder.CreateIndex(
                name: "ix_enviosproposta_orcamentoid_enviadoem",
                table: "enviosproposta",
                columns: new[] { "orcamentoid", "enviadoem" });

            migrationBuilder.CreateIndex(
                name: "ix_espacos_nome",
                table: "espacos",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventoequipe_eventoid_funcionarioid",
                table: "eventoequipe",
                columns: new[] { "eventoid", "funcionarioid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventoequipe_funcionarioid",
                table: "eventoequipe",
                column: "funcionarioid");

            migrationBuilder.CreateIndex(
                name: "ix_eventomateriais_eventoid_materialid",
                table: "eventomateriais",
                columns: new[] { "eventoid", "materialid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventomateriais_materialid",
                table: "eventomateriais",
                column: "materialid");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_clienteid",
                table: "eventos",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_espacoid_inicio",
                table: "eventos",
                columns: new[] { "espacoid", "inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_inicio",
                table: "eventos",
                column: "inicio");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_orcamentoid",
                table: "eventos",
                column: "orcamentoid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_pacoteid",
                table: "eventos",
                column: "pacoteid");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_status",
                table: "eventos",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_tipoeventoid",
                table: "eventos",
                column: "tipoeventoid");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_vendedorid",
                table: "eventos",
                column: "vendedorid");

            migrationBuilder.CreateIndex(
                name: "ix_eventoservicos_eventoid",
                table: "eventoservicos",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_eventoservicos_servicoadicionalid",
                table: "eventoservicos",
                column: "servicoadicionalid");

            migrationBuilder.CreateIndex(
                name: "ix_fechamentoscomissao_contapagarid",
                table: "fechamentoscomissao",
                column: "contapagarid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fechamentoscomissao_vendedorid_de_ate",
                table: "fechamentoscomissao",
                columns: new[] { "vendedorid", "de", "ate" });

            migrationBuilder.CreateIndex(
                name: "ix_fichastecnicas_produtoid",
                table: "fichastecnicas",
                column: "produtoid");

            migrationBuilder.CreateIndex(
                name: "ix_formaspagamento_nome",
                table: "formaspagamento",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fornecedores_documento",
                table: "fornecedores",
                column: "documento",
                unique: true,
                filter: "\"documento\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_cargoid",
                table: "funcionarios",
                column: "cargoid");

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_cpf",
                table: "funcionarios",
                column: "cpf",
                unique: true,
                filter: "\"cpf\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_itenscardapio_categoriacardapioid",
                table: "itenscardapio",
                column: "categoriacardapioid");

            migrationBuilder.CreateIndex(
                name: "ix_itenscardapio_nome",
                table: "itenscardapio",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_materiais_fornecedorid",
                table: "materiais",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_materiais_nome",
                table: "materiais",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_data",
                table: "movimentacoesestoque",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_eventoid",
                table: "movimentacoesestoque",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_produtoid_data",
                table: "movimentacoesestoque",
                columns: new[] { "produtoid", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_clienteid",
                table: "orcamentos",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_espacoid",
                table: "orcamentos",
                column: "espacoid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_inicio",
                table: "orcamentos",
                column: "inicio");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_pacoteid",
                table: "orcamentos",
                column: "pacoteid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_status",
                table: "orcamentos",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_tipoeventoid",
                table: "orcamentos",
                column: "tipoeventoid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_vendedorid",
                table: "orcamentos",
                column: "vendedorid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentoservicos_orcamentoid",
                table: "orcamentoservicos",
                column: "orcamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentoservicos_servicoadicionalid",
                table: "orcamentoservicos",
                column: "servicoadicionalid");

            migrationBuilder.CreateIndex(
                name: "ix_pacoteitens_itemcardapioid",
                table: "pacoteitens",
                column: "itemcardapioid");

            migrationBuilder.CreateIndex(
                name: "ix_pacotes_nome",
                table: "pacotes",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_datapagamento",
                table: "parcelas",
                column: "datapagamento");

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_eventoid",
                table: "parcelas",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_formapagamentoid",
                table: "parcelas",
                column: "formapagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_status_vencimento",
                table: "parcelas",
                columns: new[] { "status", "vencimento" });

            migrationBuilder.CreateIndex(
                name: "ix_produtos_categoriaprodutoid",
                table: "produtos",
                column: "categoriaprodutoid");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_codigo",
                table: "produtos",
                column: "codigo",
                unique: true,
                filter: "\"codigo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_fornecedorid",
                table: "produtos",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_nome",
                table: "produtos",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_unidademedidaid",
                table: "produtos",
                column: "unidademedidaid");

            migrationBuilder.CreateIndex(
                name: "ix_servicosadicionais_nome",
                table: "servicosadicionais",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tiposevento_nome",
                table: "tiposevento",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unidadesmedida_sigla",
                table: "unidadesmedida",
                column: "sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_login",
                table: "usuarios",
                column: "login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "contratos");

            migrationBuilder.DropTable(
                name: "convidados");

            migrationBuilder.DropTable(
                name: "degustacoes");

            migrationBuilder.DropTable(
                name: "enviosproposta");

            migrationBuilder.DropTable(
                name: "eventoequipe");

            migrationBuilder.DropTable(
                name: "eventomateriais");

            migrationBuilder.DropTable(
                name: "eventoservicos");

            migrationBuilder.DropTable(
                name: "fechamentoscomissao");

            migrationBuilder.DropTable(
                name: "fichastecnicas");

            migrationBuilder.DropTable(
                name: "movimentacoesestoque");

            migrationBuilder.DropTable(
                name: "orcamentoservicos");

            migrationBuilder.DropTable(
                name: "pacoteitens");

            migrationBuilder.DropTable(
                name: "parcelas");

            migrationBuilder.DropTable(
                name: "funcionarios");

            migrationBuilder.DropTable(
                name: "materiais");

            migrationBuilder.DropTable(
                name: "contaspagar");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "servicosadicionais");

            migrationBuilder.DropTable(
                name: "itenscardapio");

            migrationBuilder.DropTable(
                name: "cargos");

            migrationBuilder.DropTable(
                name: "eventos");

            migrationBuilder.DropTable(
                name: "formaspagamento");

            migrationBuilder.DropTable(
                name: "categoriasproduto");

            migrationBuilder.DropTable(
                name: "fornecedores");

            migrationBuilder.DropTable(
                name: "unidadesmedida");

            migrationBuilder.DropTable(
                name: "categoriascardapio");

            migrationBuilder.DropTable(
                name: "orcamentos");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "espacos");

            migrationBuilder.DropTable(
                name: "pacotes");

            migrationBuilder.DropTable(
                name: "tiposevento");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
