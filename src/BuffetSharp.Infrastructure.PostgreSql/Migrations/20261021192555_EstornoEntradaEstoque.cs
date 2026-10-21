using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class EstornoEntradaEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "estornadaem",
                table: "movimentacoesestoque",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estornodeid",
                table: "movimentacoesestoque",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_estornodeid",
                table: "movimentacoesestoque",
                column: "estornodeid");

            migrationBuilder.AddForeignKey(
                name: "fk_movimentacoesestoque_movimentacoesestoque_estornodeid",
                table: "movimentacoesestoque",
                column: "estornodeid",
                principalTable: "movimentacoesestoque",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movimentacoesestoque_movimentacoesestoque_estornodeid",
                table: "movimentacoesestoque");

            migrationBuilder.DropIndex(
                name: "ix_movimentacoesestoque_estornodeid",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "estornadaem",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "estornodeid",
                table: "movimentacoesestoque");
        }
    }
}
