using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDITORIA",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DATAHORA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    USUARIOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    ENTIDADE = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    REGISTROID = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    ACAO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ALTERACOES = table.Column<string>(type: "NCLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDITORIA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CARGOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    VALORDIARIAPADRAO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARGOS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CATEGORIASCARDAPIO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    ORDEM = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIASCARDAPIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CATEGORIASPRODUTO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIASPRODUTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CLIENTES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIPOPESSOA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    NOMEFANTASIA = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    DOCUMENTO = table.Column<string>(type: "NVARCHAR2(14)", maxLength: 14, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    CELULAR = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    DATANASCIMENTO = table.Column<DateTime>(type: "DATE", nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ENDERECO_BAIRRO = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ENDERECO_CEP = table.Column<string>(type: "NVARCHAR2(9)", maxLength: 9, nullable: true),
                    ENDERECO_CIDADE = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ENDERECO_COMPLEMENTO = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ENDERECO_LOGRADOURO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    ENDERECO_NUMERO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    ENDERECO_UF = table.Column<string>(type: "NVARCHAR2(2)", maxLength: 2, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLIENTES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ESPACOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CAPACIDADEMAXIMA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALORLOCACAO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESPACOS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FORMASPAGAMENTO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    MAXIMOPARCELAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TAXAPERCENTUAL = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORMASPAGAMENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FORNECEDORES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RAZAOSOCIAL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    NOMEFANTASIA = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    DOCUMENTO = table.Column<string>(type: "NVARCHAR2(14)", maxLength: 14, nullable: true),
                    CONTATO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ENDERECO_BAIRRO = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ENDERECO_CEP = table.Column<string>(type: "NVARCHAR2(9)", maxLength: 9, nullable: true),
                    ENDERECO_CIDADE = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ENDERECO_COMPLEMENTO = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ENDERECO_LOGRADOURO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    ENDERECO_NUMERO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    ENDERECO_UF = table.Column<string>(type: "NVARCHAR2(2)", maxLength: 2, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FORNECEDORES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PACOTES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    PRECOPORPESSOA = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    MINIMOCONVIDADOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PACOTES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SERVICOSADICIONAIS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    PRECO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    COBRADOPORPESSOA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SERVICOSADICIONAIS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TIPOSEVENTO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TIPOSEVENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "UNIDADESMEDIDA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    SIGLA = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UNIDADESMEDIDA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "USUARIOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    LOGIN = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    SENHAHASH = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PERFIL = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ULTIMOACESSOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PERCENTUALCOMISSAO = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIOS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FUNCIONARIOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CPF = table.Column<string>(type: "NVARCHAR2(11)", maxLength: 11, nullable: true),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    CARGOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALORDIARIA = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    DATAADMISSAO = table.Column<DateTime>(type: "DATE", nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FUNCIONARIOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FUNCIONARIOS_CARGOS_CARGOID",
                        column: x => x.CARGOID,
                        principalTable: "CARGOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ITENSCARDAPIO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CATEGORIACARDAPIOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ITENSCARDAPIO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ITENSCARDAPIO_CATEGORIASCARDAPIO_CATEGORIACARDAPIOID",
                        column: x => x.CATEGORIACARDAPIOID,
                        principalTable: "CATEGORIASCARDAPIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MATERIAIS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CATEGORIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    QUANTIDADEPROPRIA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALORREPOSICAO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    CUSTOLOCACAO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATERIAIS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MATERIAIS_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRODUTOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    CODIGO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    CATEGORIAPRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    UNIDADEMEDIDAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CUSTOMEDIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    ESTOQUEATUAL = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    ESTOQUEMINIMO = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUTOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRODUTOS_CATEGORIASPRODUTO_CATEGORIAPRODUTOID",
                        column: x => x.CATEGORIAPRODUTOID,
                        principalTable: "CATEGORIASPRODUTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRODUTOS_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRODUTOS_UNIDADESMEDIDA_UNIDADEMEDIDAID",
                        column: x => x.UNIDADEMEDIDAID,
                        principalTable: "UNIDADESMEDIDA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORCAMENTOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ESPACOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PACOTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    VENDEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    INICIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    QUANTIDADECONVIDADOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALIDOATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VALORPORPESSOA = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORESPACO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    PERCENTUALDESCONTO = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    VALORTOTAL = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    MOTIVORECUSA = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    ENVIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DECIDIDOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORCAMENTOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOS_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOS_ESPACOS_ESPACOID",
                        column: x => x.ESPACOID,
                        principalTable: "ESPACOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOS_PACOTES_PACOTEID",
                        column: x => x.PACOTEID,
                        principalTable: "PACOTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOS_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOS_USUARIOS_VENDEDORID",
                        column: x => x.VENDEDORID,
                        principalTable: "USUARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PACOTEITENS",
                columns: table => new
                {
                    PACOTEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ITEMCARDAPIOID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PACOTEITENS", x => new { x.PACOTEID, x.ITEMCARDAPIOID });
                    table.ForeignKey(
                        name: "FK_PACOTEITENS_ITENSCARDAPIO_ITEMCARDAPIOID",
                        column: x => x.ITEMCARDAPIOID,
                        principalTable: "ITENSCARDAPIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PACOTEITENS_PACOTES_PACOTEID",
                        column: x => x.PACOTEID,
                        principalTable: "PACOTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FICHASTECNICAS",
                columns: table => new
                {
                    ITEMCARDAPIOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADEPORPESSOA = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FICHASTECNICAS", x => new { x.ITEMCARDAPIOID, x.PRODUTOID });
                    table.ForeignKey(
                        name: "FK_FICHASTECNICAS_ITENSCARDAPIO_ITEMCARDAPIOID",
                        column: x => x.ITEMCARDAPIOID,
                        principalTable: "ITENSCARDAPIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FICHASTECNICAS_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DEGUSTACOES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PACOTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ORCAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DATAHORA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    PESSOAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    AVALIACAO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    COMENTARIO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEGUSTACOES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DEGUSTACOES_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DEGUSTACOES_ORCAMENTOS_ORCAMENTOID",
                        column: x => x.ORCAMENTOID,
                        principalTable: "ORCAMENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DEGUSTACOES_PACOTES_PACOTEID",
                        column: x => x.PACOTEID,
                        principalTable: "PACOTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ENVIOSPROPOSTA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ORCAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CANAL = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DESTINATARIO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    ENVIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ENVIOSPROPOSTA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ENVIOSPROPOSTA_ORCAMENTOS_ORCAMENTOID",
                        column: x => x.ORCAMENTOID,
                        principalTable: "ORCAMENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EVENTOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TITULO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ESPACOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PACOTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ORCAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    VENDEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PERCENTUALCOMISSAO = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    INICIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    QUANTIDADECONVIDADOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALORPORPESSOA = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORTOTAL = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    REALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ESTOQUEBAIXADO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOS_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOS_ESPACOS_ESPACOID",
                        column: x => x.ESPACOID,
                        principalTable: "ESPACOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOS_ORCAMENTOS_ORCAMENTOID",
                        column: x => x.ORCAMENTOID,
                        principalTable: "ORCAMENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOS_PACOTES_PACOTEID",
                        column: x => x.PACOTEID,
                        principalTable: "PACOTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOS_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOS_USUARIOS_VENDEDORID",
                        column: x => x.VENDEDORID,
                        principalTable: "USUARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORCAMENTOSERVICOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ORCAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SERVICOADICIONALID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORUNITARIO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORCAMENTOSERVICOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOSERVICOS_ORCAMENTOS_ORCAMENTOID",
                        column: x => x.ORCAMENTOID,
                        principalTable: "ORCAMENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ORCAMENTOSERVICOS_SERVICOSADICIONAIS_SERVICOADICIONALID",
                        column: x => x.SERVICOADICIONALID,
                        principalTable: "SERVICOSADICIONAIS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTASPAGAR",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CATEGORIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VENCIMENTO = table.Column<DateTime>(type: "DATE", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DATAPAGAMENTO = table.Column<DateTime>(type: "DATE", nullable: true),
                    VALORPAGO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: true),
                    FORMAPAGAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTASPAGAR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONTASPAGAR_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTASPAGAR_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                        column: x => x.FORMAPAGAMENTOID,
                        principalTable: "FORMASPAGAMENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTASPAGAR_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTRATOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONTEUDO = table.Column<string>(type: "NCLOB", nullable: false),
                    HASHCONTEUDO = table.Column<string>(type: "NCHAR(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EMITIDOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ASSINADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NOMESIGNATARIO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    DOCUMENTOSIGNATARIO = table.Column<string>(type: "NVARCHAR2(14)", maxLength: 14, nullable: true),
                    ASSINATURACLIENTE = table.Column<byte[]>(type: "BLOB", nullable: true),
                    NOMEREPRESENTANTE = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    ASSINATURAEMPRESA = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CANCELADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTRATOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONTRATOS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONVIDADOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    ACOMPANHANTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MESA = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    RSVP = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    CHECKINEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONVIDADOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONVIDADOS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EVENTOEQUIPE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FUNCIONARIOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FUNCAO = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    VALORDIARIA = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    PRESENTE = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOEQUIPE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOEQUIPE_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EVENTOEQUIPE_FUNCIONARIOS_FUNCIONARIOID",
                        column: x => x.FUNCIONARIOID,
                        principalTable: "FUNCIONARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EVENTOMATERIAIS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MATERIALID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADEALUGADA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CUSTOLOCACAOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORREPOSICAOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    QUANTIDADEDEVOLVIDA = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    QUANTIDADEPERDIDA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DEVOLVIDOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOMATERIAIS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOMATERIAIS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EVENTOMATERIAIS_MATERIAIS_MATERIALID",
                        column: x => x.MATERIALID,
                        principalTable: "MATERIAIS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EVENTOSERVICOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SERVICOADICIONALID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORUNITARIO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOSERVICOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOSERVICOS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EVENTOSERVICOS_SERVICOSADICIONAIS_SERVICOADICIONALID",
                        column: x => x.SERVICOADICIONALID,
                        principalTable: "SERVICOSADICIONAIS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MOVIMENTACOESESTOQUE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    CUSTOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    SALDOAPOS = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    DATA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIMENTACOESESTOQUE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MOVIMENTACOESESTOQUE_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MOVIMENTACOESESTOQUE_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PARCELAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NUMERO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VENCIMENTO = table.Column<DateTime>(type: "DATE", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DATAPAGAMENTO = table.Column<DateTime>(type: "DATE", nullable: true),
                    VALORPAGO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: true),
                    FORMAPAGAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PARCELAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PARCELAS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PARCELAS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                        column: x => x.FORMAPAGAMENTOID,
                        principalTable: "FORMASPAGAMENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FECHAMENTOSCOMISSAO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    VENDEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DE = table.Column<DateTime>(type: "DATE", nullable: false),
                    ATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    BASECALCULO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    CONTAPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FECHAMENTOSCOMISSAO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FECHAMENTOSCOMISSAO_CONTASPAGAR_CONTAPAGARID",
                        column: x => x.CONTAPAGARID,
                        principalTable: "CONTASPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FECHAMENTOSCOMISSAO_USUARIOS_VENDEDORID",
                        column: x => x.VENDEDORID,
                        principalTable: "USUARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDITORIA_DATAHORA",
                table: "AUDITORIA",
                column: "DATAHORA");

            migrationBuilder.CreateIndex(
                name: "IX_AUDITORIA_ENTIDADE_REGISTROID",
                table: "AUDITORIA",
                columns: new[] { "ENTIDADE", "REGISTROID" });

            migrationBuilder.CreateIndex(
                name: "IX_CARGOS_NOME",
                table: "CARGOS",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIASCARDAPIO_NOME",
                table: "CATEGORIASCARDAPIO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIASPRODUTO_NOME",
                table: "CATEGORIASPRODUTO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLIENTES_DOCUMENTO",
                table: "CLIENTES",
                column: "DOCUMENTO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLIENTES_NOME",
                table: "CLIENTES",
                column: "NOME");

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_DATAPAGAMENTO",
                table: "CONTASPAGAR",
                column: "DATAPAGAMENTO");

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_EVENTOID",
                table: "CONTASPAGAR",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_FORMAPAGAMENTOID",
                table: "CONTASPAGAR",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_FORNECEDORID",
                table: "CONTASPAGAR",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_STATUS_VENCIMENTO",
                table: "CONTASPAGAR",
                columns: new[] { "STATUS", "VENCIMENTO" });

            migrationBuilder.CreateIndex(
                name: "IX_CONTRATOS_EVENTOID",
                table: "CONTRATOS",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONVIDADOS_EVENTOID_NOME",
                table: "CONVIDADOS",
                columns: new[] { "EVENTOID", "NOME" });

            migrationBuilder.CreateIndex(
                name: "IX_DEGUSTACOES_CLIENTEID",
                table: "DEGUSTACOES",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_DEGUSTACOES_DATAHORA",
                table: "DEGUSTACOES",
                column: "DATAHORA");

            migrationBuilder.CreateIndex(
                name: "IX_DEGUSTACOES_ORCAMENTOID",
                table: "DEGUSTACOES",
                column: "ORCAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_DEGUSTACOES_PACOTEID",
                table: "DEGUSTACOES",
                column: "PACOTEID");

            migrationBuilder.CreateIndex(
                name: "IX_DEGUSTACOES_STATUS_DATAHORA",
                table: "DEGUSTACOES",
                columns: new[] { "STATUS", "DATAHORA" });

            migrationBuilder.CreateIndex(
                name: "IX_ENVIOSPROPOSTA_ORCAMENTOID_ENVIADOEM",
                table: "ENVIOSPROPOSTA",
                columns: new[] { "ORCAMENTOID", "ENVIADOEM" });

            migrationBuilder.CreateIndex(
                name: "IX_ESPACOS_NOME",
                table: "ESPACOS",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOEQUIPE_EVENTOID_FUNCIONARIOID",
                table: "EVENTOEQUIPE",
                columns: new[] { "EVENTOID", "FUNCIONARIOID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOEQUIPE_FUNCIONARIOID",
                table: "EVENTOEQUIPE",
                column: "FUNCIONARIOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOMATERIAIS_EVENTOID_MATERIALID",
                table: "EVENTOMATERIAIS",
                columns: new[] { "EVENTOID", "MATERIALID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOMATERIAIS_MATERIALID",
                table: "EVENTOMATERIAIS",
                column: "MATERIALID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_CLIENTEID",
                table: "EVENTOS",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_ESPACOID_INICIO",
                table: "EVENTOS",
                columns: new[] { "ESPACOID", "INICIO" });

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_INICIO",
                table: "EVENTOS",
                column: "INICIO");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_ORCAMENTOID",
                table: "EVENTOS",
                column: "ORCAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_PACOTEID",
                table: "EVENTOS",
                column: "PACOTEID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_STATUS",
                table: "EVENTOS",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_TIPOEVENTOID",
                table: "EVENTOS",
                column: "TIPOEVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_VENDEDORID",
                table: "EVENTOS",
                column: "VENDEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOSERVICOS_EVENTOID",
                table: "EVENTOSERVICOS",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOSERVICOS_SERVICOADICIONALID",
                table: "EVENTOSERVICOS",
                column: "SERVICOADICIONALID");

            migrationBuilder.CreateIndex(
                name: "IX_FECHAMENTOSCOMISSAO_CONTAPAGARID",
                table: "FECHAMENTOSCOMISSAO",
                column: "CONTAPAGARID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FECHAMENTOSCOMISSAO_VENDEDORID_DE_ATE",
                table: "FECHAMENTOSCOMISSAO",
                columns: new[] { "VENDEDORID", "DE", "ATE" });

            migrationBuilder.CreateIndex(
                name: "IX_FICHASTECNICAS_PRODUTOID",
                table: "FICHASTECNICAS",
                column: "PRODUTOID");

            migrationBuilder.CreateIndex(
                name: "IX_FORMASPAGAMENTO_NOME",
                table: "FORMASPAGAMENTO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FORNECEDORES_DOCUMENTO",
                table: "FORNECEDORES",
                column: "DOCUMENTO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FUNCIONARIOS_CARGOID",
                table: "FUNCIONARIOS",
                column: "CARGOID");

            migrationBuilder.CreateIndex(
                name: "IX_FUNCIONARIOS_CPF",
                table: "FUNCIONARIOS",
                column: "CPF",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ITENSCARDAPIO_CATEGORIACARDAPIOID",
                table: "ITENSCARDAPIO",
                column: "CATEGORIACARDAPIOID");

            migrationBuilder.CreateIndex(
                name: "IX_ITENSCARDAPIO_NOME",
                table: "ITENSCARDAPIO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAIS_FORNECEDORID",
                table: "MATERIAIS",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAIS_NOME",
                table: "MATERIAIS",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_DATA",
                table: "MOVIMENTACOESESTOQUE",
                column: "DATA");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_EVENTOID",
                table: "MOVIMENTACOESESTOQUE",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_PRODUTOID_DATA",
                table: "MOVIMENTACOESESTOQUE",
                columns: new[] { "PRODUTOID", "DATA" });

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_CLIENTEID",
                table: "ORCAMENTOS",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_ESPACOID",
                table: "ORCAMENTOS",
                column: "ESPACOID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_INICIO",
                table: "ORCAMENTOS",
                column: "INICIO");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_PACOTEID",
                table: "ORCAMENTOS",
                column: "PACOTEID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_STATUS",
                table: "ORCAMENTOS",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_TIPOEVENTOID",
                table: "ORCAMENTOS",
                column: "TIPOEVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_VENDEDORID",
                table: "ORCAMENTOS",
                column: "VENDEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOSERVICOS_ORCAMENTOID",
                table: "ORCAMENTOSERVICOS",
                column: "ORCAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOSERVICOS_SERVICOADICIONALID",
                table: "ORCAMENTOSERVICOS",
                column: "SERVICOADICIONALID");

            migrationBuilder.CreateIndex(
                name: "IX_PACOTEITENS_ITEMCARDAPIOID",
                table: "PACOTEITENS",
                column: "ITEMCARDAPIOID");

            migrationBuilder.CreateIndex(
                name: "IX_PACOTES_NOME",
                table: "PACOTES",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PARCELAS_DATAPAGAMENTO",
                table: "PARCELAS",
                column: "DATAPAGAMENTO");

            migrationBuilder.CreateIndex(
                name: "IX_PARCELAS_EVENTOID",
                table: "PARCELAS",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PARCELAS_FORMAPAGAMENTOID",
                table: "PARCELAS",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PARCELAS_STATUS_VENCIMENTO",
                table: "PARCELAS",
                columns: new[] { "STATUS", "VENCIMENTO" });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOS_CATEGORIAPRODUTOID",
                table: "PRODUTOS",
                column: "CATEGORIAPRODUTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOS_CODIGO",
                table: "PRODUTOS",
                column: "CODIGO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOS_FORNECEDORID",
                table: "PRODUTOS",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOS_NOME",
                table: "PRODUTOS",
                column: "NOME");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOS_UNIDADEMEDIDAID",
                table: "PRODUTOS",
                column: "UNIDADEMEDIDAID");

            migrationBuilder.CreateIndex(
                name: "IX_SERVICOSADICIONAIS_NOME",
                table: "SERVICOSADICIONAIS",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TIPOSEVENTO_NOME",
                table: "TIPOSEVENTO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UNIDADESMEDIDA_SIGLA",
                table: "UNIDADESMEDIDA",
                column: "SIGLA",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_LOGIN",
                table: "USUARIOS",
                column: "LOGIN",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUDITORIA");

            migrationBuilder.DropTable(
                name: "CONTRATOS");

            migrationBuilder.DropTable(
                name: "CONVIDADOS");

            migrationBuilder.DropTable(
                name: "DEGUSTACOES");

            migrationBuilder.DropTable(
                name: "ENVIOSPROPOSTA");

            migrationBuilder.DropTable(
                name: "EVENTOEQUIPE");

            migrationBuilder.DropTable(
                name: "EVENTOMATERIAIS");

            migrationBuilder.DropTable(
                name: "EVENTOSERVICOS");

            migrationBuilder.DropTable(
                name: "FECHAMENTOSCOMISSAO");

            migrationBuilder.DropTable(
                name: "FICHASTECNICAS");

            migrationBuilder.DropTable(
                name: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropTable(
                name: "ORCAMENTOSERVICOS");

            migrationBuilder.DropTable(
                name: "PACOTEITENS");

            migrationBuilder.DropTable(
                name: "PARCELAS");

            migrationBuilder.DropTable(
                name: "FUNCIONARIOS");

            migrationBuilder.DropTable(
                name: "MATERIAIS");

            migrationBuilder.DropTable(
                name: "CONTASPAGAR");

            migrationBuilder.DropTable(
                name: "PRODUTOS");

            migrationBuilder.DropTable(
                name: "SERVICOSADICIONAIS");

            migrationBuilder.DropTable(
                name: "ITENSCARDAPIO");

            migrationBuilder.DropTable(
                name: "CARGOS");

            migrationBuilder.DropTable(
                name: "EVENTOS");

            migrationBuilder.DropTable(
                name: "FORMASPAGAMENTO");

            migrationBuilder.DropTable(
                name: "CATEGORIASPRODUTO");

            migrationBuilder.DropTable(
                name: "FORNECEDORES");

            migrationBuilder.DropTable(
                name: "UNIDADESMEDIDA");

            migrationBuilder.DropTable(
                name: "CATEGORIASCARDAPIO");

            migrationBuilder.DropTable(
                name: "ORCAMENTOS");

            migrationBuilder.DropTable(
                name: "CLIENTES");

            migrationBuilder.DropTable(
                name: "ESPACOS");

            migrationBuilder.DropTable(
                name: "PACOTES");

            migrationBuilder.DropTable(
                name: "TIPOSEVENTO");

            migrationBuilder.DropTable(
                name: "USUARIOS");
        }
    }
}
