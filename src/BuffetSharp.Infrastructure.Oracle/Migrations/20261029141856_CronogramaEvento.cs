using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class CronogramaEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EVENTOETAPAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HORARIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DURACAOMINUTOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVIDADE = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    LOCAL = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    RESPONSAVEL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    FUNCIONARIOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    ORDEM = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOETAPAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOETAPAS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EVENTOETAPAS_FUNCIONARIOS_FUNCIONARIOID",
                        column: x => x.FUNCIONARIOID,
                        principalTable: "FUNCIONARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MODELOSCRONOGRAMA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MODELOSCRONOGRAMA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MODELOSCRONOGRAMA_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MODELOCRONOGRAMAITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    MODELOCRONOGRAMAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVIDADE = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    MINUTOSDOINICIO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DURACAOMINUTOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RESPONSAVEL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    LOCAL = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    ORDEM = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MODELOCRONOGRAMAITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MODELOCRONOGRAMAITENS_MODELOSCRONOGRAMA_MODELOCRONOGRAMAID",
                        column: x => x.MODELOCRONOGRAMAID,
                        principalTable: "MODELOSCRONOGRAMA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOETAPAS_EVENTOID_HORARIO",
                table: "EVENTOETAPAS",
                columns: new[] { "EVENTOID", "HORARIO" });

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOETAPAS_FUNCIONARIOID",
                table: "EVENTOETAPAS",
                column: "FUNCIONARIOID");

            migrationBuilder.CreateIndex(
                name: "IX_MODELOCRONOGRAMAITENS_MODELOCRONOGRAMAID",
                table: "MODELOCRONOGRAMAITENS",
                column: "MODELOCRONOGRAMAID");

            migrationBuilder.CreateIndex(
                name: "IX_MODELOSCRONOGRAMA_NOME",
                table: "MODELOSCRONOGRAMA",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MODELOSCRONOGRAMA_TIPOEVENTOID",
                table: "MODELOSCRONOGRAMA",
                column: "TIPOEVENTOID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EVENTOETAPAS");

            migrationBuilder.DropTable(
                name: "MODELOCRONOGRAMAITENS");

            migrationBuilder.DropTable(
                name: "MODELOSCRONOGRAMA");
        }
    }
}
