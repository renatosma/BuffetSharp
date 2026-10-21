using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class EstornoEntradaEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstornadaEm",
                table: "MovimentacoesEstoque",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstornoDeId",
                table: "MovimentacoesEstoque",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_EstornoDeId",
                table: "MovimentacoesEstoque",
                column: "EstornoDeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesEstoque_MovimentacoesEstoque_EstornoDeId",
                table: "MovimentacoesEstoque",
                column: "EstornoDeId",
                principalTable: "MovimentacoesEstoque",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesEstoque_MovimentacoesEstoque_EstornoDeId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesEstoque_EstornoDeId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "EstornadaEm",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "EstornoDeId",
                table: "MovimentacoesEstoque");
        }
    }
}
