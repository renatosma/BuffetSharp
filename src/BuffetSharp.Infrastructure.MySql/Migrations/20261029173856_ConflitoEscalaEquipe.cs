using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ConflitoEscalaEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExcecaoAutorizadaPor",
                table: "EventoEquipe",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcecaoConflito",
                table: "EventoEquipe",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IndisponibilidadesFuncionario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FuncionarioId = table.Column<int>(type: "int", nullable: false),
                    Inicio = table.Column<DateTime>(type: "date", nullable: false),
                    Fim = table.Column<DateTime>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Observacao = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndisponibilidadesFuncionario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndisponibilidadesFuncionario_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IndisponibilidadesFuncionario_FuncionarioId_Inicio",
                table: "IndisponibilidadesFuncionario",
                columns: new[] { "FuncionarioId", "Inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndisponibilidadesFuncionario");

            migrationBuilder.DropColumn(
                name: "ExcecaoAutorizadaPor",
                table: "EventoEquipe");

            migrationBuilder.DropColumn(
                name: "ExcecaoConflito",
                table: "EventoEquipe");
        }
    }
}
