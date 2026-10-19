using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ParcelamentoContasPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgrupadaEmId",
                table: "ContasPagar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Documento",
                table: "ContasPagar",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroParcela",
                table: "ContasPagar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelamentoId",
                table: "ContasPagar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParcelamentosPagar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Descricao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    EventoId = table.Column<int>(type: "int", nullable: true),
                    Documento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelamentosPagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParcelamentosPagar_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParcelamentosPagar_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_AgrupadaEmId",
                table: "ContasPagar",
                column: "AgrupadaEmId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_ParcelamentoId",
                table: "ContasPagar",
                column: "ParcelamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelamentosPagar_EventoId",
                table: "ParcelamentosPagar",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelamentosPagar_FornecedorId",
                table: "ParcelamentosPagar",
                column: "FornecedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContasPagar_ParcelamentosPagar_AgrupadaEmId",
                table: "ContasPagar",
                column: "AgrupadaEmId",
                principalTable: "ParcelamentosPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContasPagar_ParcelamentosPagar_ParcelamentoId",
                table: "ContasPagar",
                column: "ParcelamentoId",
                principalTable: "ParcelamentosPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContasPagar_ParcelamentosPagar_AgrupadaEmId",
                table: "ContasPagar");

            migrationBuilder.DropForeignKey(
                name: "FK_ContasPagar_ParcelamentosPagar_ParcelamentoId",
                table: "ContasPagar");

            migrationBuilder.DropTable(
                name: "ParcelamentosPagar");

            migrationBuilder.DropIndex(
                name: "IX_ContasPagar_AgrupadaEmId",
                table: "ContasPagar");

            migrationBuilder.DropIndex(
                name: "IX_ContasPagar_ParcelamentoId",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "AgrupadaEmId",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "Documento",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "NumeroParcela",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "ParcelamentoId",
                table: "ContasPagar");
        }
    }
}
