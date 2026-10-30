using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SobrasDesperdicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "sobrasregistradasem",
                table: "eventos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sobrasregistradaspor",
                table: "eventos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sobrasprato",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    itemcardapioid = table.Column<int>(type: "integer", nullable: true),
                    prato = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    porcoes = table.Column<int>(type: "integer", nullable: false),
                    percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    destino = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    instituicao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    custo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sobrasprato", x => x.id);
                    table.ForeignKey(
                        name: "fk_sobrasprato_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sobrasprato_itenscardapio_itemcardapioid",
                        column: x => x.itemcardapioid,
                        principalTable: "itenscardapio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sobrasprato_eventoid",
                table: "sobrasprato",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_sobrasprato_itemcardapioid",
                table: "sobrasprato",
                column: "itemcardapioid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sobrasprato");

            migrationBuilder.DropColumn(
                name: "sobrasregistradasem",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "sobrasregistradaspor",
                table: "eventos");
        }
    }
}
