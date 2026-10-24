using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class FormaCondicaoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CONDICAOPAGAMENTOID",
                table: "PEDIDOSCOMPRA",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FORMAPAGAMENTOID",
                table: "PEDIDOSCOMPRA",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FORMAPAGAMENTOID",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FORMAPAGAMENTOID",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_CONDICAOPAGAMENTOID",
                table: "PEDIDOSCOMPRA",
                column: "CONDICAOPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_FORMAPAGAMENTOID",
                table: "PEDIDOSCOMPRA",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_FORMAPAGAMENTOID",
                table: "ORCAMENTOS",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_FORMAPAGAMENTOID",
                table: "EVENTOS",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.AddForeignKey(
                name: "FK_EVENTOS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "EVENTOS",
                column: "FORMAPAGAMENTOID",
                principalTable: "FORMASPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ORCAMENTOS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "ORCAMENTOS",
                column: "FORMAPAGAMENTOID",
                principalTable: "FORMASPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDOSCOMPRA_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                table: "PEDIDOSCOMPRA",
                column: "CONDICAOPAGAMENTOID",
                principalTable: "CONDICOESPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDOSCOMPRA_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "PEDIDOSCOMPRA",
                column: "FORMAPAGAMENTOID",
                principalTable: "FORMASPAGAMENTO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EVENTOS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "EVENTOS");

            migrationBuilder.DropForeignKey(
                name: "FK_ORCAMENTOS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "ORCAMENTOS");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDOSCOMPRA_CONDICOESPAGAMENTO_CONDICAOPAGAMENTOID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDOSCOMPRA_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDOSCOMPRA_CONDICAOPAGAMENTOID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDOSCOMPRA_FORMAPAGAMENTOID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropIndex(
                name: "IX_ORCAMENTOS_FORMAPAGAMENTOID",
                table: "ORCAMENTOS");

            migrationBuilder.DropIndex(
                name: "IX_EVENTOS_FORMAPAGAMENTOID",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "CONDICAOPAGAMENTOID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "FORMAPAGAMENTOID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "FORMAPAGAMENTOID",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "FORMAPAGAMENTOID",
                table: "EVENTOS");
        }
    }
}
