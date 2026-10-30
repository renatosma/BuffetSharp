using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class DimensionamentoEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PORHORA",
                table: "FICHASTECNICAS",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SOADULTOS",
                table: "FICHASTECNICAS",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "REGRASEQUIPE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CARGOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONVIDADOSPORPROFISSIONAL = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MINIMO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGRASEQUIPE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_REGRASEQUIPE_CARGOS_CARGOID",
                        column: x => x.CARGOID,
                        principalTable: "CARGOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REGRASEQUIPE_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REGRASEQUIPE_CARGOID",
                table: "REGRASEQUIPE",
                column: "CARGOID");

            migrationBuilder.CreateIndex(
                name: "IX_REGRASEQUIPE_TIPOEVENTOID_CARGOID",
                table: "REGRASEQUIPE",
                columns: new[] { "TIPOEVENTOID", "CARGOID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REGRASEQUIPE");

            migrationBuilder.DropColumn(
                name: "PORHORA",
                table: "FICHASTECNICAS");

            migrationBuilder.DropColumn(
                name: "SOADULTOS",
                table: "FICHASTECNICAS");
        }
    }
}
