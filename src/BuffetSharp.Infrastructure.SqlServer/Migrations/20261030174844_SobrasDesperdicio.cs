using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SobrasDesperdicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SobrasRegistradasEm",
                table: "Eventos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SobrasRegistradasPor",
                table: "Eventos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SobrasPrato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    ItemCardapioId = table.Column<int>(type: "int", nullable: true),
                    Prato = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Porcoes = table.Column<int>(type: "int", nullable: false),
                    Percentual = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Instituicao = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Custo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SobrasPrato", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SobrasPrato_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SobrasPrato_ItensCardapio_ItemCardapioId",
                        column: x => x.ItemCardapioId,
                        principalTable: "ItensCardapio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SobrasPrato_EventoId",
                table: "SobrasPrato",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_SobrasPrato_ItemCardapioId",
                table: "SobrasPrato",
                column: "ItemCardapioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SobrasPrato");

            migrationBuilder.DropColumn(
                name: "SobrasRegistradasEm",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "SobrasRegistradasPor",
                table: "Eventos");
        }
    }
}
