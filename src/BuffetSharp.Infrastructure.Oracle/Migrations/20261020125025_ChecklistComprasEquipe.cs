using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistComprasEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FECHAMENTOEQUIPEID",
                table: "EVENTOEQUIPE",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EVENTOTAREFAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PRAZO = table.Column<DateTime>(type: "DATE", nullable: false),
                    RESPONSAVEL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    ORDEM = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONCLUIDAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CONCLUIDAPOR = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOTAREFAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOTAREFAS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FECHAMENTOSEQUIPE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FUNCIONARIOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DE = table.Column<DateTime>(type: "DATE", nullable: false),
                    ATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    DIARIAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    CONTAPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FECHAMENTOSEQUIPE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FECHAMENTOSEQUIPE_CONTASPAGAR_CONTAPAGARID",
                        column: x => x.CONTAPAGARID,
                        principalTable: "CONTASPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FECHAMENTOSEQUIPE_FUNCIONARIOS_FUNCIONARIOID",
                        column: x => x.FUNCIONARIOID,
                        principalTable: "FUNCIONARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MODELOSCHECKLIST",
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
                    table.PrimaryKey("PK_MODELOSCHECKLIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MODELOSCHECKLIST_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MODELOCHECKLISTITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    MODELOCHECKLISTID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DIASANTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RESPONSAVEL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    ORDEM = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MODELOCHECKLISTITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MODELOCHECKLISTITENS_MODELOSCHECKLIST_MODELOCHECKLISTID",
                        column: x => x.MODELOCHECKLISTID,
                        principalTable: "MODELOSCHECKLIST",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOEQUIPE_FECHAMENTOEQUIPEID",
                table: "EVENTOEQUIPE",
                column: "FECHAMENTOEQUIPEID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOTAREFAS_EVENTOID_PRAZO",
                table: "EVENTOTAREFAS",
                columns: new[] { "EVENTOID", "PRAZO" });

            migrationBuilder.CreateIndex(
                name: "IX_FECHAMENTOSEQUIPE_CONTAPAGARID",
                table: "FECHAMENTOSEQUIPE",
                column: "CONTAPAGARID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FECHAMENTOSEQUIPE_FUNCIONARIOID_DE_ATE",
                table: "FECHAMENTOSEQUIPE",
                columns: new[] { "FUNCIONARIOID", "DE", "ATE" });

            migrationBuilder.CreateIndex(
                name: "IX_MODELOCHECKLISTITENS_MODELOCHECKLISTID",
                table: "MODELOCHECKLISTITENS",
                column: "MODELOCHECKLISTID");

            migrationBuilder.CreateIndex(
                name: "IX_MODELOSCHECKLIST_NOME",
                table: "MODELOSCHECKLIST",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MODELOSCHECKLIST_TIPOEVENTOID",
                table: "MODELOSCHECKLIST",
                column: "TIPOEVENTOID");

            migrationBuilder.AddForeignKey(
                name: "FK_EVENTOEQUIPE_FECHAMENTOSEQUIPE_FECHAMENTOEQUIPEID",
                table: "EVENTOEQUIPE",
                column: "FECHAMENTOEQUIPEID",
                principalTable: "FECHAMENTOSEQUIPE",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EVENTOEQUIPE_FECHAMENTOSEQUIPE_FECHAMENTOEQUIPEID",
                table: "EVENTOEQUIPE");

            migrationBuilder.DropTable(
                name: "EVENTOTAREFAS");

            migrationBuilder.DropTable(
                name: "FECHAMENTOSEQUIPE");

            migrationBuilder.DropTable(
                name: "MODELOCHECKLISTITENS");

            migrationBuilder.DropTable(
                name: "MODELOSCHECKLIST");

            migrationBuilder.DropIndex(
                name: "IX_EVENTOEQUIPE_FECHAMENTOEQUIPEID",
                table: "EVENTOEQUIPE");

            migrationBuilder.DropColumn(
                name: "FECHAMENTOEQUIPEID",
                table: "EVENTOEQUIPE");
        }
    }
}
