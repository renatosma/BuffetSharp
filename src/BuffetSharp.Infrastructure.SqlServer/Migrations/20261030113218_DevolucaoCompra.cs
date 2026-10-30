using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DevolucaoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuantidadeDevolvida",
                table: "RecebimentoItens",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditoUsado",
                table: "NotasFiscaisEntrada",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorDevolvido",
                table: "NotasFiscaisEntrada",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DevolucoesCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecebimentoCompraId = table.Column<int>(type: "int", nullable: false),
                    PedidoCompraId = table.Column<int>(type: "int", nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FornecedorRepoe = table.Column<bool>(type: "bit", nullable: false),
                    NotaDevolucao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotaFiscalEntradaId = table.Column<int>(type: "int", nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorAbatido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorCredito = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditoUtilizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevolucoesCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevolucoesCompra_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevolucoesCompra_NotasFiscaisEntrada_NotaFiscalEntradaId",
                        column: x => x.NotaFiscalEntradaId,
                        principalTable: "NotasFiscaisEntrada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevolucoesCompra_PedidosCompra_PedidoCompraId",
                        column: x => x.PedidoCompraId,
                        principalTable: "PedidosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevolucoesCompra_RecebimentosCompra_RecebimentoCompraId",
                        column: x => x.RecebimentoCompraId,
                        principalTable: "RecebimentosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditosUsadosNota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotaFiscalEntradaId = table.Column<int>(type: "int", nullable: false),
                    DevolucaoCompraId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditosUsadosNota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditosUsadosNota_DevolucoesCompra_DevolucaoCompraId",
                        column: x => x.DevolucaoCompraId,
                        principalTable: "DevolucoesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditosUsadosNota_NotasFiscaisEntrada_NotaFiscalEntradaId",
                        column: x => x.NotaFiscalEntradaId,
                        principalTable: "NotasFiscaisEntrada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DevolucaoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DevolucaoCompraId = table.Column<int>(type: "int", nullable: false),
                    RecebimentoItemId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MovimentacaoEstoqueId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevolucaoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevolucaoItens_DevolucoesCompra_DevolucaoCompraId",
                        column: x => x.DevolucaoCompraId,
                        principalTable: "DevolucoesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DevolucaoItens_MovimentacoesEstoque_MovimentacaoEstoqueId",
                        column: x => x.MovimentacaoEstoqueId,
                        principalTable: "MovimentacoesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevolucaoItens_RecebimentoItens_RecebimentoItemId",
                        column: x => x.RecebimentoItemId,
                        principalTable: "RecebimentoItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditosUsadosNota_DevolucaoCompraId",
                table: "CreditosUsadosNota",
                column: "DevolucaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditosUsadosNota_NotaFiscalEntradaId",
                table: "CreditosUsadosNota",
                column: "NotaFiscalEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucaoItens_DevolucaoCompraId",
                table: "DevolucaoItens",
                column: "DevolucaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucaoItens_MovimentacaoEstoqueId",
                table: "DevolucaoItens",
                column: "MovimentacaoEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucaoItens_RecebimentoItemId",
                table: "DevolucaoItens",
                column: "RecebimentoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucoesCompra_FornecedorId_Data",
                table: "DevolucoesCompra",
                columns: new[] { "FornecedorId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_DevolucoesCompra_NotaFiscalEntradaId",
                table: "DevolucoesCompra",
                column: "NotaFiscalEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucoesCompra_PedidoCompraId",
                table: "DevolucoesCompra",
                column: "PedidoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucoesCompra_RecebimentoCompraId",
                table: "DevolucoesCompra",
                column: "RecebimentoCompraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditosUsadosNota");

            migrationBuilder.DropTable(
                name: "DevolucaoItens");

            migrationBuilder.DropTable(
                name: "DevolucoesCompra");

            migrationBuilder.DropColumn(
                name: "QuantidadeDevolvida",
                table: "RecebimentoItens");

            migrationBuilder.DropColumn(
                name: "CreditoUsado",
                table: "NotasFiscaisEntrada");

            migrationBuilder.DropColumn(
                name: "ValorDevolvido",
                table: "NotasFiscaisEntrada");
        }
    }
}
