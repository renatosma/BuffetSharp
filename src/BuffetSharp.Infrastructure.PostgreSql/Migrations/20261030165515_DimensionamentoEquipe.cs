using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DimensionamentoEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "porhora",
                table: "fichastecnicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "soadultos",
                table: "fichastecnicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "regrasequipe",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: true),
                    cargoid = table.Column<int>(type: "integer", nullable: false),
                    convidadosporprofissional = table.Column<int>(type: "integer", nullable: false),
                    minimo = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regrasequipe", x => x.id);
                    table.ForeignKey(
                        name: "fk_regrasequipe_cargos_cargoid",
                        column: x => x.cargoid,
                        principalTable: "cargos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_regrasequipe_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regrasequipe_cargoid",
                table: "regrasequipe",
                column: "cargoid");

            migrationBuilder.CreateIndex(
                name: "ix_regrasequipe_tipoeventoid_cargoid",
                table: "regrasequipe",
                columns: new[] { "tipoeventoid", "cargoid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regrasequipe");

            migrationBuilder.DropColumn(
                name: "porhora",
                table: "fichastecnicas");

            migrationBuilder.DropColumn(
                name: "soadultos",
                table: "fichastecnicas");
        }
    }
}
