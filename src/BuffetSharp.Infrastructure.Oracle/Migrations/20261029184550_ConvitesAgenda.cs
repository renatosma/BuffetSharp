using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ConvitesAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONVITESENVIADOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    UID = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    SEQUENCIA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    INICIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CANCELADO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DESTINATARIO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CONTATO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    CANAL = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    ENVIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DEGUSTACAOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONVITESENVIADOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONVITESENVIADOS_DEGUSTACOES_DEGUSTACAOID",
                        column: x => x.DEGUSTACAOID,
                        principalTable: "DEGUSTACOES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CONVITESENVIADOS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONVITESENVIADOS_DEGUSTACAOID",
                table: "CONVITESENVIADOS",
                column: "DEGUSTACAOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONVITESENVIADOS_EVENTOID",
                table: "CONVITESENVIADOS",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_CONVITESENVIADOS_UID",
                table: "CONVITESENVIADOS",
                column: "UID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONVITESENVIADOS");
        }
    }
}
