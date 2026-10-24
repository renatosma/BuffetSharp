using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class FormaPorParcela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FORMAPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONDICAOPAGAMENTOPARCELAS_FORMAPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.AddForeignKey(
                name: "FK_CONDICAOPAGAMENTOPARCELAS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS",
                column: "FORMAPAGAMENTOID",
                principalTable: "FORMASPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONDICAOPAGAMENTOPARCELAS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS");

            migrationBuilder.DropIndex(
                name: "IX_CONDICAOPAGAMENTOPARCELAS_FORMAPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS");

            migrationBuilder.DropColumn(
                name: "FORMAPAGAMENTOID",
                table: "CONDICAOPAGAMENTOPARCELAS");
        }
    }
}
