using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ImportacaoNfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveAcesso",
                table: "NotasFiscaisEntrada",
                type: "nvarchar(44)",
                maxLength: 44,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodigosProdutoFornecedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FornecedorId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    ProdutoEmbalagemId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosProdutoFornecedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodigosProdutoFornecedor_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodigosProdutoFornecedor_ProdutoEmbalagens_ProdutoEmbalagemId",
                        column: x => x.ProdutoEmbalagemId,
                        principalTable: "ProdutoEmbalagens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CodigosProdutoFornecedor_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisEntrada_ChaveAcesso",
                table: "NotasFiscaisEntrada",
                column: "ChaveAcesso");

            migrationBuilder.CreateIndex(
                name: "IX_CodigosProdutoFornecedor_FornecedorId_Codigo",
                table: "CodigosProdutoFornecedor",
                columns: new[] { "FornecedorId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodigosProdutoFornecedor_ProdutoEmbalagemId",
                table: "CodigosProdutoFornecedor",
                column: "ProdutoEmbalagemId");

            migrationBuilder.CreateIndex(
                name: "IX_CodigosProdutoFornecedor_ProdutoId",
                table: "CodigosProdutoFornecedor",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosProdutoFornecedor");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscaisEntrada_ChaveAcesso",
                table: "NotasFiscaisEntrada");

            migrationBuilder.DropColumn(
                name: "ChaveAcesso",
                table: "NotasFiscaisEntrada");
        }
    }
}
