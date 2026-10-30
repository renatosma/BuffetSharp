using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class SobrasDesperdicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SOBRASREGISTRADASEM",
                table: "EVENTOS",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SOBRASREGISTRADASPOR",
                table: "EVENTOS",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SOBRASPRATO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ITEMCARDAPIOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PRATO = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    PORCOES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PERCENTUAL = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    DESTINO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    INSTITUICAO = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: true),
                    CUSTO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SOBRASPRATO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SOBRASPRATO_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SOBRASPRATO_ITENSCARDAPIO_ITEMCARDAPIOID",
                        column: x => x.ITEMCARDAPIOID,
                        principalTable: "ITENSCARDAPIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SOBRASPRATO_EVENTOID",
                table: "SOBRASPRATO",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_SOBRASPRATO_ITEMCARDAPIOID",
                table: "SOBRASPRATO",
                column: "ITEMCARDAPIOID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SOBRASPRATO");

            migrationBuilder.DropColumn(
                name: "SOBRASREGISTRADASEM",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "SOBRASREGISTRADASPOR",
                table: "EVENTOS");
        }
    }
}
