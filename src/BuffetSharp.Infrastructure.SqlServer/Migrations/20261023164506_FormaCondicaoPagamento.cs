using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class FormaCondicaoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CondicaoPagamentoId",
                table: "PedidosCompra",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormaPagamentoId",
                table: "PedidosCompra",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormaPagamentoId",
                table: "Orcamentos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormaPagamentoId",
                table: "Eventos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_CondicaoPagamentoId",
                table: "PedidosCompra",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_FormaPagamentoId",
                table: "PedidosCompra",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_FormaPagamentoId",
                table: "Orcamentos",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_FormaPagamentoId",
                table: "Eventos",
                column: "FormaPagamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Eventos_FormasPagamento_FormaPagamentoId",
                table: "Eventos",
                column: "FormaPagamentoId",
                principalTable: "FormasPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orcamentos_FormasPagamento_FormaPagamentoId",
                table: "Orcamentos",
                column: "FormaPagamentoId",
                principalTable: "FormasPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompra_CondicoesPagamento_CondicaoPagamentoId",
                table: "PedidosCompra",
                column: "CondicaoPagamentoId",
                principalTable: "CondicoesPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompra_FormasPagamento_FormaPagamentoId",
                table: "PedidosCompra",
                column: "FormaPagamentoId",
                principalTable: "FormasPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Eventos_FormasPagamento_FormaPagamentoId",
                table: "Eventos");

            migrationBuilder.DropForeignKey(
                name: "FK_Orcamentos_FormasPagamento_FormaPagamentoId",
                table: "Orcamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompra_CondicoesPagamento_CondicaoPagamentoId",
                table: "PedidosCompra");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompra_FormasPagamento_FormaPagamentoId",
                table: "PedidosCompra");

            migrationBuilder.DropIndex(
                name: "IX_PedidosCompra_CondicaoPagamentoId",
                table: "PedidosCompra");

            migrationBuilder.DropIndex(
                name: "IX_PedidosCompra_FormaPagamentoId",
                table: "PedidosCompra");

            migrationBuilder.DropIndex(
                name: "IX_Orcamentos_FormaPagamentoId",
                table: "Orcamentos");

            migrationBuilder.DropIndex(
                name: "IX_Eventos_FormaPagamentoId",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "CondicaoPagamentoId",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoId",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoId",
                table: "Eventos");
        }
    }
}
