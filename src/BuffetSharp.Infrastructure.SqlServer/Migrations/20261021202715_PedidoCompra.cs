using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PedidoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DePedidoCompra",
                table: "MovimentacoesEstoque",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PedidosCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FornecedorId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    PrevisaoEntrega = table.Column<DateOnly>(type: "date", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NotaFiscal = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DataCompra = table.Column<DateOnly>(type: "date", nullable: true),
                    RecebidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContaPagarId = table.Column<int>(type: "int", nullable: true),
                    ParcelamentoPagarId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosCompra_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosCompra_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosCompra_ParcelamentosPagar_ParcelamentoPagarId",
                        column: x => x.ParcelamentoPagarId,
                        principalTable: "ParcelamentosPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PedidoCompraItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoCompraId = table.Column<int>(type: "int", nullable: false),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    ProdutoEmbalagemId = table.Column<int>(type: "int", nullable: true),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MovimentacaoEstoqueId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoCompraItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoCompraItens_MovimentacoesEstoque_MovimentacaoEstoqueId",
                        column: x => x.MovimentacaoEstoqueId,
                        principalTable: "MovimentacoesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidoCompraItens_PedidosCompra_PedidoCompraId",
                        column: x => x.PedidoCompraId,
                        principalTable: "PedidosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PedidoCompraItens_ProdutoEmbalagens_ProdutoEmbalagemId",
                        column: x => x.ProdutoEmbalagemId,
                        principalTable: "ProdutoEmbalagens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidoCompraItens_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidoCompraItens_MovimentacaoEstoqueId",
                table: "PedidoCompraItens",
                column: "MovimentacaoEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoCompraItens_PedidoCompraId",
                table: "PedidoCompraItens",
                column: "PedidoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoCompraItens_ProdutoEmbalagemId",
                table: "PedidoCompraItens",
                column: "ProdutoEmbalagemId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoCompraItens_ProdutoId",
                table: "PedidoCompraItens",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_ContaPagarId",
                table: "PedidosCompra",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_FornecedorId",
                table: "PedidosCompra",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_NotaFiscal",
                table: "PedidosCompra",
                column: "NotaFiscal");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_ParcelamentoPagarId",
                table: "PedidosCompra",
                column: "ParcelamentoPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_Status_Data",
                table: "PedidosCompra",
                columns: new[] { "Status", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidoCompraItens");

            migrationBuilder.DropTable(
                name: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "DePedidoCompra",
                table: "MovimentacoesEstoque");
        }
    }
}
