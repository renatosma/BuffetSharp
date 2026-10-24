using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class FormaPorParcela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FormaPagamentoId",
                table: "CondicaoPagamentoParcelas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CondicaoPagamentoParcelas_FormaPagamentoId",
                table: "CondicaoPagamentoParcelas",
                column: "FormaPagamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CondicaoPagamentoParcelas_FormasPagamento_FormaPagamentoId",
                table: "CondicaoPagamentoParcelas",
                column: "FormaPagamentoId",
                principalTable: "FormasPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CondicaoPagamentoParcelas_FormasPagamento_FormaPagamentoId",
                table: "CondicaoPagamentoParcelas");

            migrationBuilder.DropIndex(
                name: "IX_CondicaoPagamentoParcelas_FormaPagamentoId",
                table: "CondicaoPagamentoParcelas");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoId",
                table: "CondicaoPagamentoParcelas");
        }
    }
}
