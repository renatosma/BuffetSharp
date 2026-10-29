using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PrecoPorData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ajustedescricao",
                table: "orcamentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ajustelocacaopercentual",
                table: "orcamentos",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ajustepessoapercentual",
                table: "orcamentos",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "minimoconvidadosdata",
                table: "orcamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "regraspreco",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    diassemana = table.Column<int>(type: "integer", nullable: false),
                    diainicio = table.Column<int>(type: "integer", nullable: true),
                    mesinicio = table.Column<int>(type: "integer", nullable: true),
                    diafim = table.Column<int>(type: "integer", nullable: true),
                    mesfim = table.Column<int>(type: "integer", nullable: true),
                    data = table.Column<DateOnly>(type: "date", nullable: true),
                    anual = table.Column<bool>(type: "boolean", nullable: false),
                    exclusiva = table.Column<bool>(type: "boolean", nullable: false),
                    percentual = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    ajustapessoa = table.Column<bool>(type: "boolean", nullable: false),
                    ajustalocacao = table.Column<bool>(type: "boolean", nullable: false),
                    pacoteid = table.Column<int>(type: "integer", nullable: true),
                    espacoid = table.Column<int>(type: "integer", nullable: true),
                    minimoconvidados = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regraspreco", x => x.id);
                    table.ForeignKey(
                        name: "fk_regraspreco_espacos_espacoid",
                        column: x => x.espacoid,
                        principalTable: "espacos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_regraspreco_pacotes_pacoteid",
                        column: x => x.pacoteid,
                        principalTable: "pacotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regraspreco_espacoid",
                table: "regraspreco",
                column: "espacoid");

            migrationBuilder.CreateIndex(
                name: "ix_regraspreco_pacoteid",
                table: "regraspreco",
                column: "pacoteid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regraspreco");

            migrationBuilder.DropColumn(
                name: "ajustedescricao",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "ajustelocacaopercentual",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "ajustepessoapercentual",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "minimoconvidadosdata",
                table: "orcamentos");
        }
    }
}
