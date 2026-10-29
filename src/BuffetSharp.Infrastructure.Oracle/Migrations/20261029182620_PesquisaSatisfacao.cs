using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class PesquisaSatisfacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PESQUISASSATISFACAO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ENVIADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    RESPONDIDAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    REGISTRADAPOR = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    NPS = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    NOTACOMIDA = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    NOTAATENDIMENTO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    NOTAESPACO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    NOTAORGANIZACAO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    COMENTARIO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    AVALIACAOGOOGLEPEDIDAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TRATATIVADETRATOR = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    DETRATORTRATADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PESQUISASSATISFACAO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PESQUISASSATISFACAO_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PESQUISASSATISFACAO_EVENTOID",
                table: "PESQUISASSATISFACAO",
                column: "EVENTOID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PESQUISASSATISFACAO");
        }
    }
}
