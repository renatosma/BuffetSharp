using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CondicoesModelosContatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "condicaopagamentoid",
                table: "orcamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "custounitariocompra",
                table: "movimentacoesestoque",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fatorconversao",
                table: "movimentacoesestoque",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "fornecedorid",
                table: "movimentacoesestoque",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantidadecompra",
                table: "movimentacoesestoque",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unidadecompra",
                table: "movimentacoesestoque",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "funcionarios",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "funcionarios",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                table: "funcionarios",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "funcionarios",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "funcionarios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "funcionarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                table: "funcionarios",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "condicaopagamentoid",
                table: "eventos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "espacos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "espacos",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                table: "espacos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "espacos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "espacos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "espacos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                table: "espacos",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "modelocontratoid",
                table: "contratos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "condicoespagamento",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    percentualentrada = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    diasentrada = table.Column<int>(type: "integer", nullable: false),
                    quantidadeparcelas = table.Column<int>(type: "integer", nullable: false),
                    diasprimeiraparcela = table.Column<int>(type: "integer", nullable: false),
                    mensal = table.Column<bool>(type: "boolean", nullable: false),
                    intervalodias = table.Column<int>(type: "integer", nullable: false),
                    quitardiasantesevento = table.Column<int>(type: "integer", nullable: true),
                    formapagamentoid = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoespagamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoespagamento_formaspagamento_formapagamentoid",
                        column: x => x.formapagamentoid,
                        principalTable: "formaspagamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contatosadicionais",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    observacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    clienteid = table.Column<int>(type: "integer", nullable: true),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    funcionarioid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contatosadicionais", x => x.id);
                    table.ForeignKey(
                        name: "fk_contatosadicionais_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_contatosadicionais_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_contatosadicionais_funcionarios_funcionarioid",
                        column: x => x.funcionarioid,
                        principalTable: "funcionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enderecosadicionais",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identificacao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    clienteid = table.Column<int>(type: "integer", nullable: true),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    funcionarioid = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_enderecosadicionais", x => x.id);
                    table.ForeignKey(
                        name: "fk_enderecosadicionais_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_enderecosadicionais_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_enderecosadicionais_funcionarios_funcionarioid",
                        column: x => x.funcionarioid,
                        principalTable: "funcionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modeloscontrato",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: true),
                    padrao = table.Column<bool>(type: "boolean", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modeloscontrato", x => x.id);
                    table.ForeignKey(
                        name: "fk_modeloscontrato_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produtoembalagens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    unidademedidaid = table.Column<int>(type: "integer", nullable: false),
                    fator = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    descricao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    codigofornecedor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtoembalagens", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtoembalagens_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produtoembalagens_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_produtoembalagens_unidadesmedida_unidademedidaid",
                        column: x => x.unidademedidaid,
                        principalTable: "unidadesmedida",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_condicaopagamentoid",
                table: "orcamentos",
                column: "condicaopagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_fornecedorid",
                table: "movimentacoesestoque",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_condicaopagamentoid",
                table: "eventos",
                column: "condicaopagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_contratos_modelocontratoid",
                table: "contratos",
                column: "modelocontratoid");

            migrationBuilder.CreateIndex(
                name: "ix_condicoespagamento_formapagamentoid",
                table: "condicoespagamento",
                column: "formapagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_condicoespagamento_nome",
                table: "condicoespagamento",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contatosadicionais_clienteid",
                table: "contatosadicionais",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_contatosadicionais_fornecedorid",
                table: "contatosadicionais",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_contatosadicionais_funcionarioid",
                table: "contatosadicionais",
                column: "funcionarioid");

            migrationBuilder.CreateIndex(
                name: "ix_enderecosadicionais_clienteid",
                table: "enderecosadicionais",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_enderecosadicionais_fornecedorid",
                table: "enderecosadicionais",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_enderecosadicionais_funcionarioid",
                table: "enderecosadicionais",
                column: "funcionarioid");

            migrationBuilder.CreateIndex(
                name: "ix_modeloscontrato_nome",
                table: "modeloscontrato",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modeloscontrato_tipoeventoid",
                table: "modeloscontrato",
                column: "tipoeventoid");

            migrationBuilder.CreateIndex(
                name: "ix_produtoembalagens_fornecedorid",
                table: "produtoembalagens",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_produtoembalagens_produtoid",
                table: "produtoembalagens",
                column: "produtoid");

            migrationBuilder.CreateIndex(
                name: "ix_produtoembalagens_unidademedidaid",
                table: "produtoembalagens",
                column: "unidademedidaid");

            migrationBuilder.AddForeignKey(
                name: "fk_contratos_modeloscontrato_modelocontratoid",
                table: "contratos",
                column: "modelocontratoid",
                principalTable: "modeloscontrato",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_eventos_condicoespagamento_condicaopagamentoid",
                table: "eventos",
                column: "condicaopagamentoid",
                principalTable: "condicoespagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_movimentacoesestoque_fornecedores_fornecedorid",
                table: "movimentacoesestoque",
                column: "fornecedorid",
                principalTable: "fornecedores",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orcamentos_condicoespagamento_condicaopagamentoid",
                table: "orcamentos",
                column: "condicaopagamentoid",
                principalTable: "condicoespagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_contratos_modeloscontrato_modelocontratoid",
                table: "contratos");

            migrationBuilder.DropForeignKey(
                name: "fk_eventos_condicoespagamento_condicaopagamentoid",
                table: "eventos");

            migrationBuilder.DropForeignKey(
                name: "fk_movimentacoesestoque_fornecedores_fornecedorid",
                table: "movimentacoesestoque");

            migrationBuilder.DropForeignKey(
                name: "fk_orcamentos_condicoespagamento_condicaopagamentoid",
                table: "orcamentos");

            migrationBuilder.DropTable(
                name: "condicoespagamento");

            migrationBuilder.DropTable(
                name: "contatosadicionais");

            migrationBuilder.DropTable(
                name: "enderecosadicionais");

            migrationBuilder.DropTable(
                name: "modeloscontrato");

            migrationBuilder.DropTable(
                name: "produtoembalagens");

            migrationBuilder.DropIndex(
                name: "ix_orcamentos_condicaopagamentoid",
                table: "orcamentos");

            migrationBuilder.DropIndex(
                name: "ix_movimentacoesestoque_fornecedorid",
                table: "movimentacoesestoque");

            migrationBuilder.DropIndex(
                name: "ix_eventos_condicaopagamentoid",
                table: "eventos");

            migrationBuilder.DropIndex(
                name: "ix_contratos_modelocontratoid",
                table: "contratos");

            migrationBuilder.DropColumn(
                name: "condicaopagamentoid",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custounitariocompra",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "fatorconversao",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "fornecedorid",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "quantidadecompra",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "unidadecompra",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "condicaopagamentoid",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "modelocontratoid",
                table: "contratos");
        }
    }
}
