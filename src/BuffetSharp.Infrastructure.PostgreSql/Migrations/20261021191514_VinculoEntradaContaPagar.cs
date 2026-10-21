using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class VinculoEntradaContaPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contapagarid",
                table: "movimentacoesestoque",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "parcelamentopagarid",
                table: "movimentacoesestoque",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_contapagarid",
                table: "movimentacoesestoque",
                column: "contapagarid");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacoesestoque_parcelamentopagarid",
                table: "movimentacoesestoque",
                column: "parcelamentopagarid");

            migrationBuilder.AddForeignKey(
                name: "fk_movimentacoesestoque_contaspagar_contapagarid",
                table: "movimentacoesestoque",
                column: "contapagarid",
                principalTable: "contaspagar",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_movimentacoesestoque_parcelamentospagar_parcelamentopagarid",
                table: "movimentacoesestoque",
                column: "parcelamentopagarid",
                principalTable: "parcelamentospagar",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movimentacoesestoque_contaspagar_contapagarid",
                table: "movimentacoesestoque");

            migrationBuilder.DropForeignKey(
                name: "fk_movimentacoesestoque_parcelamentospagar_parcelamentopagarid",
                table: "movimentacoesestoque");

            migrationBuilder.DropIndex(
                name: "ix_movimentacoesestoque_contapagarid",
                table: "movimentacoesestoque");

            migrationBuilder.DropIndex(
                name: "ix_movimentacoesestoque_parcelamentopagarid",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "contapagarid",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "parcelamentopagarid",
                table: "movimentacoesestoque");
        }
    }
}
