using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CronogramaEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventoetapas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    horario = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    duracaominutos = table.Column<int>(type: "integer", nullable: false),
                    atividade = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    local = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    responsavel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    funcionarioid = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventoetapas", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventoetapas_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_eventoetapas_funcionarios_funcionarioid",
                        column: x => x.funcionarioid,
                        principalTable: "funcionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "modeloscronograma",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modeloscronograma", x => x.id);
                    table.ForeignKey(
                        name: "fk_modeloscronograma_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "modelocronogramaitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    modelocronogramaid = table.Column<int>(type: "integer", nullable: false),
                    atividade = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    minutosdoinicio = table.Column<int>(type: "integer", nullable: false),
                    duracaominutos = table.Column<int>(type: "integer", nullable: false),
                    responsavel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    local = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelocronogramaitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_modelocronogramaitens_modeloscronograma_modelocronogramaid",
                        column: x => x.modelocronogramaid,
                        principalTable: "modeloscronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eventoetapas_eventoid_horario",
                table: "eventoetapas",
                columns: new[] { "eventoid", "horario" });

            migrationBuilder.CreateIndex(
                name: "ix_eventoetapas_funcionarioid",
                table: "eventoetapas",
                column: "funcionarioid");

            migrationBuilder.CreateIndex(
                name: "ix_modelocronogramaitens_modelocronogramaid",
                table: "modelocronogramaitens",
                column: "modelocronogramaid");

            migrationBuilder.CreateIndex(
                name: "ix_modeloscronograma_nome",
                table: "modeloscronograma",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modeloscronograma_tipoeventoid",
                table: "modeloscronograma",
                column: "tipoeventoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventoetapas");

            migrationBuilder.DropTable(
                name: "modelocronogramaitens");

            migrationBuilder.DropTable(
                name: "modeloscronograma");
        }
    }
}
