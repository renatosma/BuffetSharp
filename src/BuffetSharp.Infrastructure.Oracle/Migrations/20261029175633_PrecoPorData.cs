using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class PrecoPorData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AJUSTEDESCRICAO",
                table: "ORCAMENTOS",
                type: "NVARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AJUSTELOCACAOPERCENTUAL",
                table: "ORCAMENTOS",
                type: "DECIMAL(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AJUSTEPESSOAPERCENTUAL",
                table: "ORCAMENTOS",
                type: "DECIMAL(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MINIMOCONVIDADOSDATA",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "REGRASPRECO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DIASSEMANA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DIAINICIO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    MESINICIO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DIAFIM = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    MESFIM = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: true),
                    ANUAL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    EXCLUSIVA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PERCENTUAL = table.Column<decimal>(type: "DECIMAL(6,2)", precision: 6, scale: 2, nullable: false),
                    AJUSTAPESSOA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    AJUSTALOCACAO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PACOTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ESPACOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    MINIMOCONVIDADOS = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGRASPRECO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_REGRASPRECO_ESPACOS_ESPACOID",
                        column: x => x.ESPACOID,
                        principalTable: "ESPACOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REGRASPRECO_PACOTES_PACOTEID",
                        column: x => x.PACOTEID,
                        principalTable: "PACOTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REGRASPRECO_ESPACOID",
                table: "REGRASPRECO",
                column: "ESPACOID");

            migrationBuilder.CreateIndex(
                name: "IX_REGRASPRECO_PACOTEID",
                table: "REGRASPRECO",
                column: "PACOTEID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REGRASPRECO");

            migrationBuilder.DropColumn(
                name: "AJUSTEDESCRICAO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "AJUSTELOCACAOPERCENTUAL",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "AJUSTEPESSOAPERCENTUAL",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "MINIMOCONVIDADOSDATA",
                table: "ORCAMENTOS");
        }
    }
}
