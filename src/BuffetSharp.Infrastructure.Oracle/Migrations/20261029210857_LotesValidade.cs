using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class LotesValidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CONTROLAVALIDADE",
                table: "PRODUTOS",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LOTESESTOQUE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NUMERO = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: true),
                    VALIDADE = table.Column<DateTime>(type: "DATE", nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    SALDO = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    CUSTOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    ENTRADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOTESESTOQUE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LOTESESTOQUE_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MOVIMENTACAOLOTES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    MOVIMENTACAOESTOQUEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LOTEESTOQUEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIMENTACAOLOTES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MOVIMENTACAOLOTES_LOTESESTOQUE_LOTEESTOQUEID",
                        column: x => x.LOTEESTOQUEID,
                        principalTable: "LOTESESTOQUE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MOVIMENTACAOLOTES_MOVIMENTACOESESTOQUE_MOVIMENTACAOESTOQUEID",
                        column: x => x.MOVIMENTACAOESTOQUEID,
                        principalTable: "MOVIMENTACOESESTOQUE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOTESESTOQUE_PRODUTOID_VALIDADE",
                table: "LOTESESTOQUE",
                columns: new[] { "PRODUTOID", "VALIDADE" });

            migrationBuilder.CreateIndex(
                name: "IX_LOTESESTOQUE_VALIDADE",
                table: "LOTESESTOQUE",
                column: "VALIDADE");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACAOLOTES_LOTEESTOQUEID",
                table: "MOVIMENTACAOLOTES",
                column: "LOTEESTOQUEID");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACAOLOTES_MOVIMENTACAOESTOQUEID",
                table: "MOVIMENTACAOLOTES",
                column: "MOVIMENTACAOESTOQUEID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MOVIMENTACAOLOTES");

            migrationBuilder.DropTable(
                name: "LOTESESTOQUE");

            migrationBuilder.DropColumn(
                name: "CONTROLAVALIDADE",
                table: "PRODUTOS");
        }
    }
}
