using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ImportacaoNfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CHAVEACESSO",
                table: "NOTASFISCAISENTRADA",
                type: "NVARCHAR2(44)",
                maxLength: 44,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CODIGOSPRODUTOFORNECEDOR",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CODIGO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: true),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PRODUTOEMBALAGEMID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CODIGOSPRODUTOFORNECEDOR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CODIGOSPRODUTOFORNECEDOR_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CODIGOSPRODUTOFORNECEDOR_PRODUTOEMBALAGENS_PRODUTOEMBALAGEMID",
                        column: x => x.PRODUTOEMBALAGEMID,
                        principalTable: "PRODUTOEMBALAGENS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CODIGOSPRODUTOFORNECEDOR_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISENTRADA_CHAVEACESSO",
                table: "NOTASFISCAISENTRADA",
                column: "CHAVEACESSO");

            migrationBuilder.CreateIndex(
                name: "IX_CODIGOSPRODUTOFORNECEDOR_FORNECEDORID_CODIGO",
                table: "CODIGOSPRODUTOFORNECEDOR",
                columns: new[] { "FORNECEDORID", "CODIGO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CODIGOSPRODUTOFORNECEDOR_PRODUTOEMBALAGEMID",
                table: "CODIGOSPRODUTOFORNECEDOR",
                column: "PRODUTOEMBALAGEMID");

            migrationBuilder.CreateIndex(
                name: "IX_CODIGOSPRODUTOFORNECEDOR_PRODUTOID",
                table: "CODIGOSPRODUTOFORNECEDOR",
                column: "PRODUTOID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CODIGOSPRODUTOFORNECEDOR");

            migrationBuilder.DropIndex(
                name: "IX_NOTASFISCAISENTRADA_CHAVEACESSO",
                table: "NOTASFISCAISENTRADA");

            migrationBuilder.DropColumn(
                name: "CHAVEACESSO",
                table: "NOTASFISCAISENTRADA");
        }
    }
}
