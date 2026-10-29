using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PesquisaSatisfacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pesquisassatisfacao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    enviadaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    respondidaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    registradapor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nps = table.Column<int>(type: "integer", nullable: true),
                    notacomida = table.Column<int>(type: "integer", nullable: true),
                    notaatendimento = table.Column<int>(type: "integer", nullable: true),
                    notaespaco = table.Column<int>(type: "integer", nullable: true),
                    notaorganizacao = table.Column<int>(type: "integer", nullable: true),
                    comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    avaliacaogooglepedidaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    tratativadetrator = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    detratortratadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pesquisassatisfacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_pesquisassatisfacao_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pesquisassatisfacao_eventoid",
                table: "pesquisassatisfacao",
                column: "eventoid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pesquisassatisfacao");
        }
    }
}
