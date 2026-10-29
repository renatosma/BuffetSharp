using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ListaEspera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ESPERASDATA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    LEADID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    INICIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ESPACOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CONVIDADOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    SITUACAO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ENTROUEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    AVISADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ENCERRADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ORCAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESPERASDATA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ESPERASDATA_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ESPERASDATA_ESPACOS_ESPACOID",
                        column: x => x.ESPACOID,
                        principalTable: "ESPACOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ESPERASDATA_LEADS_LEADID",
                        column: x => x.LEADID,
                        principalTable: "LEADS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ESPERASDATA_ORCAMENTOS_ORCAMENTOID",
                        column: x => x.ORCAMENTOID,
                        principalTable: "ORCAMENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ESPERASDATA_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ESPERASDATA_CLIENTEID",
                table: "ESPERASDATA",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_ESPERASDATA_ESPACOID",
                table: "ESPERASDATA",
                column: "ESPACOID");

            migrationBuilder.CreateIndex(
                name: "IX_ESPERASDATA_LEADID",
                table: "ESPERASDATA",
                column: "LEADID");

            migrationBuilder.CreateIndex(
                name: "IX_ESPERASDATA_ORCAMENTOID",
                table: "ESPERASDATA",
                column: "ORCAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_ESPERASDATA_SITUACAO_INICIO",
                table: "ESPERASDATA",
                columns: new[] { "SITUACAO", "INICIO" });

            migrationBuilder.CreateIndex(
                name: "IX_ESPERASDATA_TIPOEVENTOID",
                table: "ESPERASDATA",
                column: "TIPOEVENTOID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ESPERASDATA");
        }
    }
}
