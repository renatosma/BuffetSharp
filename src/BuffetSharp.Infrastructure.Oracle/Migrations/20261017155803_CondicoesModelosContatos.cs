using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class CondicoesModelosContatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CONDICAOPAGAMENTOID",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOUNITARIOCOMPRA",
                table: "MOVIMENTACOESESTOQUE",
                type: "DECIMAL(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FATORCONVERSAO",
                table: "MOVIMENTACOESESTOQUE",
                type: "DECIMAL(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FORNECEDORID",
                table: "MOVIMENTACOESESTOQUE",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QUANTIDADECOMPRA",
                table: "MOVIMENTACOESESTOQUE",
                type: "DECIMAL(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UNIDADECOMPRA",
                table: "MOVIMENTACOESESTOQUE",
                type: "NVARCHAR2(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_BAIRRO",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_CEP",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_CIDADE",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_COMPLEMENTO",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_LOGRADOURO",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_NUMERO",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_UF",
                table: "FUNCIONARIOS",
                type: "NVARCHAR2(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CONDICAOPAGAMENTOID",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_BAIRRO",
                table: "ESPACOS",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_CEP",
                table: "ESPACOS",
                type: "NVARCHAR2(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_CIDADE",
                table: "ESPACOS",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_COMPLEMENTO",
                table: "ESPACOS",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_LOGRADOURO",
                table: "ESPACOS",
                type: "NVARCHAR2(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_NUMERO",
                table: "ESPACOS",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ENDERECO_UF",
                table: "ESPACOS",
                type: "NVARCHAR2(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MODELOCONTRATOID",
                table: "CONTRATOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CONDICOESPAGAMENTO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    PERCENTUALENTRADA = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    DIASENTRADA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADEPARCELAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DIASPRIMEIRAPARCELA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MENSAL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    INTERVALODIAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUITARDIASANTESEVENTO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    FORMAPAGAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONDICOESPAGAMENTO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONDICOESPAGAMENTO_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                        column: x => x.FORMAPAGAMENTOID,
                        principalTable: "FORMASPAGAMENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTATOSADICIONAIS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VALOR = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    FUNCIONARIOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTATOSADICIONAIS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONTATOSADICIONAIS_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CONTATOSADICIONAIS_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CONTATOSADICIONAIS_FUNCIONARIOS_FUNCIONARIOID",
                        column: x => x.FUNCIONARIOID,
                        principalTable: "FUNCIONARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ENDERECOSADICIONAIS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    IDENTIFICACAO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    FUNCIONARIOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
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
                    table.PrimaryKey("PK_ENDERECOSADICIONAIS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ENDERECOSADICIONAIS_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ENDERECOSADICIONAIS_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ENDERECOSADICIONAIS_FUNCIONARIOS_FUNCIONARIOID",
                        column: x => x.FUNCIONARIOID,
                        principalTable: "FUNCIONARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MODELOSCONTRATO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PADRAO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CONTEUDO = table.Column<string>(type: "NCLOB", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MODELOSCONTRATO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MODELOSCONTRATO_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRODUTOEMBALAGENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    UNIDADEMEDIDAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FATOR = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    CODIGOFORNECEDOR = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUTOEMBALAGENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRODUTOEMBALAGENS_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRODUTOEMBALAGENS_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRODUTOEMBALAGENS_UNIDADESMEDIDA_UNIDADEMEDIDAID",
                        column: x => x.UNIDADEMEDIDAID,
                        principalTable: "UNIDADESMEDIDA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_CONDICAOPAGAMENTOID",
                table: "ORCAMENTOS",
                column: "CONDICAOPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_FORNECEDORID",
                table: "MOVIMENTACOESESTOQUE",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_CONDICAOPAGAMENTOID",
                table: "EVENTOS",
                column: "CONDICAOPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRATOS_MODELOCONTRATOID",
                table: "CONTRATOS",
                column: "MODELOCONTRATOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONDICOESPAGAMENTO_FORMAPAGAMENTOID",
                table: "CONDICOESPAGAMENTO",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONDICOESPAGAMENTO_NOME",
                table: "CONDICOESPAGAMENTO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTATOSADICIONAIS_CLIENTEID",
                table: "CONTATOSADICIONAIS",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTATOSADICIONAIS_FORNECEDORID",
                table: "CONTATOSADICIONAIS",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTATOSADICIONAIS_FUNCIONARIOID",
                table: "CONTATOSADICIONAIS",
                column: "FUNCIONARIOID");

            migrationBuilder.CreateIndex(
                name: "IX_ENDERECOSADICIONAIS_CLIENTEID",
                table: "ENDERECOSADICIONAIS",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_ENDERECOSADICIONAIS_FORNECEDORID",
                table: "ENDERECOSADICIONAIS",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_ENDERECOSADICIONAIS_FUNCIONARIOID",
                table: "ENDERECOSADICIONAIS",
                column: "FUNCIONARIOID");

            migrationBuilder.CreateIndex(
                name: "IX_MODELOSCONTRATO_NOME",
                table: "MODELOSCONTRATO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MODELOSCONTRATO_TIPOEVENTOID",
                table: "MODELOSCONTRATO",
                column: "TIPOEVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOEMBALAGENS_FORNECEDORID",
                table: "PRODUTOEMBALAGENS",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOEMBALAGENS_PRODUTOID",
                table: "PRODUTOEMBALAGENS",
                column: "PRODUTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUTOEMBALAGENS_UNIDADEMEDIDAID",
                table: "PRODUTOEMBALAGENS",
                column: "UNIDADEMEDIDAID");

            migrationBuilder.AddForeignKey(
                name: "FK_CONTRATOS_MODELOSCONTRATO_MODELOCONTRATOID",
                table: "CONTRATOS",
                column: "MODELOCONTRATOID",
                principalTable: "MODELOSCONTRATO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EVENTOS_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                table: "EVENTOS",
                column: "CONDICAOPAGAMENTOID",
                principalTable: "CONDICOESPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_FORNECEDORES_FORNECEDORID",
                table: "MOVIMENTACOESESTOQUE",
                column: "FORNECEDORID",
                principalTable: "FORNECEDORES",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ORCAMENTOS_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                table: "ORCAMENTOS",
                column: "CONDICAOPAGAMENTOID",
                principalTable: "CONDICOESPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONTRATOS_MODELOSCONTRATO_MODELOCONTRATOID",
                table: "CONTRATOS");

            migrationBuilder.DropForeignKey(
                name: "FK_EVENTOS_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                table: "EVENTOS");

            migrationBuilder.DropForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_FORNECEDORES_FORNECEDORID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropForeignKey(
                name: "FK_ORCAMENTOS_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                table: "ORCAMENTOS");

            migrationBuilder.DropTable(
                name: "CONDICOESPAGAMENTO");

            migrationBuilder.DropTable(
                name: "CONTATOSADICIONAIS");

            migrationBuilder.DropTable(
                name: "ENDERECOSADICIONAIS");

            migrationBuilder.DropTable(
                name: "MODELOSCONTRATO");

            migrationBuilder.DropTable(
                name: "PRODUTOEMBALAGENS");

            migrationBuilder.DropIndex(
                name: "IX_ORCAMENTOS_CONDICAOPAGAMENTOID",
                table: "ORCAMENTOS");

            migrationBuilder.DropIndex(
                name: "IX_MOVIMENTACOESESTOQUE_FORNECEDORID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropIndex(
                name: "IX_EVENTOS_CONDICAOPAGAMENTOID",
                table: "EVENTOS");

            migrationBuilder.DropIndex(
                name: "IX_CONTRATOS_MODELOCONTRATOID",
                table: "CONTRATOS");

            migrationBuilder.DropColumn(
                name: "CONDICAOPAGAMENTOID",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOUNITARIOCOMPRA",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "FATORCONVERSAO",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "FORNECEDORID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "QUANTIDADECOMPRA",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "UNIDADECOMPRA",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "ENDERECO_BAIRRO",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_CEP",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_CIDADE",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_COMPLEMENTO",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_LOGRADOURO",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_NUMERO",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_UF",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "CONDICAOPAGAMENTOID",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_BAIRRO",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_CEP",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_CIDADE",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_COMPLEMENTO",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_LOGRADOURO",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_NUMERO",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "ENDERECO_UF",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "MODELOCONTRATOID",
                table: "CONTRATOS");
        }
    }
}
