using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ParcelasCondicaoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONDICAOPAGAMENTOPARCELAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CONDICAOPAGAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DIAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PERCENTUAL = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONDICAOPAGAMENTOPARCELAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONDICAOPAGAMENTOPARCELAS_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                        column: x => x.CONDICAOPAGAMENTOID,
                        principalTable: "CONDICOESPAGAMENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONDICAOPAGAMENTOPARCELAS_CONDICAOPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS",
                column: "CONDICAOPAGAMENTOID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONDICAOPAGAMENTOPARCELAS");
        }
    }
}
