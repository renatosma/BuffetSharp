using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class EstornoEntradaEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ESTORNADAEM",
                table: "MOVIMENTACOESESTOQUE",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ESTORNODEID",
                table: "MOVIMENTACOESESTOQUE",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_ESTORNODEID",
                table: "MOVIMENTACOESESTOQUE",
                column: "ESTORNODEID");

            migrationBuilder.AddForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_MOVIMENTACOESESTOQUE_ESTORNODEID",
                table: "MOVIMENTACOESESTOQUE",
                column: "ESTORNODEID",
                principalTable: "MOVIMENTACOESESTOQUE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_MOVIMENTACOESESTOQUE_ESTORNODEID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropIndex(
                name: "IX_MOVIMENTACOESESTOQUE_ESTORNODEID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "ESTORNADAEM",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "ESTORNODEID",
                table: "MOVIMENTACOESESTOQUE");
        }
    }
}
