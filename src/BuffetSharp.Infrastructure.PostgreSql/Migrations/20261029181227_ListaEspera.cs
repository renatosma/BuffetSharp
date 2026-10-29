using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ListaEspera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "esperasdata",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clienteid = table.Column<int>(type: "integer", nullable: true),
                    leadid = table.Column<int>(type: "integer", nullable: true),
                    inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    espacoid = table.Column<int>(type: "integer", nullable: true),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: true),
                    convidados = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    situacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entrouem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    avisadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    encerradaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    orcamentoid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_esperasdata", x => x.id);
                    table.ForeignKey(
                        name: "fk_esperasdata_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_esperasdata_espacos_espacoid",
                        column: x => x.espacoid,
                        principalTable: "espacos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_esperasdata_leads_leadid",
                        column: x => x.leadid,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_esperasdata_orcamentos_orcamentoid",
                        column: x => x.orcamentoid,
                        principalTable: "orcamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_esperasdata_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_esperasdata_clienteid",
                table: "esperasdata",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_esperasdata_espacoid",
                table: "esperasdata",
                column: "espacoid");

            migrationBuilder.CreateIndex(
                name: "ix_esperasdata_leadid",
                table: "esperasdata",
                column: "leadid");

            migrationBuilder.CreateIndex(
                name: "ix_esperasdata_orcamentoid",
                table: "esperasdata",
                column: "orcamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_esperasdata_situacao_inicio",
                table: "esperasdata",
                columns: new[] { "situacao", "inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_esperasdata_tipoeventoid",
                table: "esperasdata",
                column: "tipoeventoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "esperasdata");
        }
    }
}
