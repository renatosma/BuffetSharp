using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ConvitesAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "convitesenviados",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uid = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sequencia = table.Column<int>(type: "integer", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    cancelado = table.Column<bool>(type: "boolean", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contato = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    enviadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    eventoid = table.Column<int>(type: "integer", nullable: true),
                    degustacaoid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convitesenviados", x => x.id);
                    table.ForeignKey(
                        name: "fk_convitesenviados_degustacoes_degustacaoid",
                        column: x => x.degustacaoid,
                        principalTable: "degustacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_convitesenviados_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_convitesenviados_degustacaoid",
                table: "convitesenviados",
                column: "degustacaoid");

            migrationBuilder.CreateIndex(
                name: "ix_convitesenviados_eventoid",
                table: "convitesenviados",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_convitesenviados_uid",
                table: "convitesenviados",
                column: "uid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "convitesenviados");
        }
    }
}
