using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FormaCondicaoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "condicaopagamentoid",
                table: "pedidoscompra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "formapagamentoid",
                table: "pedidoscompra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "formapagamentoid",
                table: "orcamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "formapagamentoid",
                table: "eventos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_condicaopagamentoid",
                table: "pedidoscompra",
                column: "condicaopagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_formapagamentoid",
                table: "pedidoscompra",
                column: "formapagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_formapagamentoid",
                table: "orcamentos",
                column: "formapagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_formapagamentoid",
                table: "eventos",
                column: "formapagamentoid");

            migrationBuilder.AddForeignKey(
                name: "fk_eventos_formaspagamento_formapagamentoid",
                table: "eventos",
                column: "formapagamentoid",
                principalTable: "formaspagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orcamentos_formaspagamento_formapagamentoid",
                table: "orcamentos",
                column: "formapagamentoid",
                principalTable: "formaspagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidoscompra_condicoespagamento_condicaopagamentoid",
                table: "pedidoscompra",
                column: "condicaopagamentoid",
                principalTable: "condicoespagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidoscompra_formaspagamento_formapagamentoid",
                table: "pedidoscompra",
                column: "formapagamentoid",
                principalTable: "formaspagamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_eventos_formaspagamento_formapagamentoid",
                table: "eventos");

            migrationBuilder.DropForeignKey(
                name: "fk_orcamentos_formaspagamento_formapagamentoid",
                table: "orcamentos");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidoscompra_condicoespagamento_condicaopagamentoid",
                table: "pedidoscompra");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidoscompra_formaspagamento_formapagamentoid",
                table: "pedidoscompra");

            migrationBuilder.DropIndex(
                name: "ix_pedidoscompra_condicaopagamentoid",
                table: "pedidoscompra");

            migrationBuilder.DropIndex(
                name: "ix_pedidoscompra_formapagamentoid",
                table: "pedidoscompra");

            migrationBuilder.DropIndex(
                name: "ix_orcamentos_formapagamentoid",
                table: "orcamentos");

            migrationBuilder.DropIndex(
                name: "ix_eventos_formapagamentoid",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "condicaopagamentoid",
                table: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "formapagamentoid",
                table: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "formapagamentoid",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "formapagamentoid",
                table: "eventos");
        }
    }
}
