using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FormaPorParcela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "formapagamentoid",
                table: "condicaopagamentoparcelas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_condicaopagamentoparcelas_formapagamentoid",
                table: "condicaopagamentoparcelas",
                column: "formapagamentoid");

            migrationBuilder.AddForeignKey(
                name: "fk_condicaopagamentoparcelas_formaspagamento_formapagamentoid",
                table: "condicaopagamentoparcelas",
                column: "formapagamentoid",
                principalTable: "formaspagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_condicaopagamentoparcelas_formaspagamento_formapagamentoid",
                table: "condicaopagamentoparcelas");

            migrationBuilder.DropIndex(
                name: "ix_condicaopagamentoparcelas_formapagamentoid",
                table: "condicaopagamentoparcelas");

            migrationBuilder.DropColumn(
                name: "formapagamentoid",
                table: "condicaopagamentoparcelas");
        }
    }
}
