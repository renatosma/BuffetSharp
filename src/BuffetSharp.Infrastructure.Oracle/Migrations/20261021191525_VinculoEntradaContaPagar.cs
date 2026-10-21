using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class VinculoEntradaContaPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CONTAPAGARID",
                table: "MOVIMENTACOESESTOQUE",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PARCELAMENTOPAGARID",
                table: "MOVIMENTACOESESTOQUE",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_CONTAPAGARID",
                table: "MOVIMENTACOESESTOQUE",
                column: "CONTAPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTACOESESTOQUE_PARCELAMENTOPAGARID",
                table: "MOVIMENTACOESESTOQUE",
                column: "PARCELAMENTOPAGARID");

            migrationBuilder.AddForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_CONTASPAGAR_CONTAPAGARID",
                table: "MOVIMENTACOESESTOQUE",
                column: "CONTAPAGARID",
                principalTable: "CONTASPAGAR",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_PARCELAMENTOSPAGAR_PARCELAMENTOPAGARID",
                table: "MOVIMENTACOESESTOQUE",
                column: "PARCELAMENTOPAGARID",
                principalTable: "PARCELAMENTOSPAGAR",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_CONTASPAGAR_CONTAPAGARID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropForeignKey(
                name: "FK_MOVIMENTACOESESTOQUE_PARCELAMENTOSPAGAR_PARCELAMENTOPAGARID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropIndex(
                name: "IX_MOVIMENTACOESESTOQUE_CONTAPAGARID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropIndex(
                name: "IX_MOVIMENTACOESESTOQUE_PARCELAMENTOPAGARID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "CONTAPAGARID",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "PARCELAMENTOPAGARID",
                table: "MOVIMENTACOESESTOQUE");
        }
    }
}
