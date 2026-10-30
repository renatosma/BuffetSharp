using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class LotesValidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ControlaValidade",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LotesEstoque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Validade = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Saldo = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EntradaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesEstoque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesEstoque_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacaoLotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MovimentacaoEstoqueId = table.Column<int>(type: "int", nullable: false),
                    LoteEstoqueId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacaoLotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacaoLotes_LotesEstoque_LoteEstoqueId",
                        column: x => x.LoteEstoqueId,
                        principalTable: "LotesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentacaoLotes_MovimentacoesEstoque_MovimentacaoEstoqueId",
                        column: x => x.MovimentacaoEstoqueId,
                        principalTable: "MovimentacoesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotesEstoque_ProdutoId_Validade",
                table: "LotesEstoque",
                columns: new[] { "ProdutoId", "Validade" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesEstoque_Validade",
                table: "LotesEstoque",
                column: "Validade");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacaoLotes_LoteEstoqueId",
                table: "MovimentacaoLotes",
                column: "LoteEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacaoLotes_MovimentacaoEstoqueId",
                table: "MovimentacaoLotes",
                column: "MovimentacaoEstoqueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentacaoLotes");

            migrationBuilder.DropTable(
                name: "LotesEstoque");

            migrationBuilder.DropColumn(
                name: "ControlaValidade",
                table: "Produtos");
        }
    }
}
