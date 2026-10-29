using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ConflitoEscalaEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EXCECAOAUTORIZADAPOR",
                table: "EVENTOEQUIPE",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXCECAOCONFLITO",
                table: "EVENTOEQUIPE",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "INDISPONIBILIDADESFUNCIONARIO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FUNCIONARIOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    INICIO = table.Column<DateTime>(type: "DATE", nullable: false),
                    FIM = table.Column<DateTime>(type: "DATE", nullable: false),
                    MOTIVO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INDISPONIBILIDADESFUNCIONARIO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INDISPONIBILIDADESFUNCIONARIO_FUNCIONARIOS_FUNCIONARIOID",
                        column: x => x.FUNCIONARIOID,
                        principalTable: "FUNCIONARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INDISPONIBILIDADESFUNCIONARIO_FUNCIONARIOID_INICIO",
                table: "INDISPONIBILIDADESFUNCIONARIO",
                columns: new[] { "FUNCIONARIOID", "INICIO" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INDISPONIBILIDADESFUNCIONARIO");

            migrationBuilder.DropColumn(
                name: "EXCECAOAUTORIZADAPOR",
                table: "EVENTOEQUIPE");

            migrationBuilder.DropColumn(
                name: "EXCECAOCONFLITO",
                table: "EVENTOEQUIPE");
        }
    }
}
