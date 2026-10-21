using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class VinculoEntradaContaPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContaPagarId",
                table: "MovimentacoesEstoque",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelamentoPagarId",
                table: "MovimentacoesEstoque",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_ContaPagarId",
                table: "MovimentacoesEstoque",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_ParcelamentoPagarId",
                table: "MovimentacoesEstoque",
                column: "ParcelamentoPagarId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesEstoque_ContasPagar_ContaPagarId",
                table: "MovimentacoesEstoque",
                column: "ContaPagarId",
                principalTable: "ContasPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesEstoque_ParcelamentosPagar_ParcelamentoPagarId",
                table: "MovimentacoesEstoque",
                column: "ParcelamentoPagarId",
                principalTable: "ParcelamentosPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesEstoque_ContasPagar_ContaPagarId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesEstoque_ParcelamentosPagar_ParcelamentoPagarId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesEstoque_ContaPagarId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesEstoque_ParcelamentoPagarId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "ContaPagarId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "ParcelamentoPagarId",
                table: "MovimentacoesEstoque");
        }
    }
}
