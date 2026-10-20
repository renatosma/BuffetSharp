using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistComprasEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fechamentoequipeid",
                table: "eventoequipe",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "eventotarefas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prazo = table.Column<DateOnly>(type: "date", nullable: false),
                    responsavel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    concluidaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    concluidapor = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventotarefas", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventotarefas_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fechamentosequipe",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    funcionarioid = table.Column<int>(type: "integer", nullable: false),
                    de = table.Column<DateOnly>(type: "date", nullable: false),
                    ate = table.Column<DateOnly>(type: "date", nullable: false),
                    diarias = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    contapagarid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fechamentosequipe", x => x.id);
                    table.ForeignKey(
                        name: "fk_fechamentosequipe_contaspagar_contapagarid",
                        column: x => x.contapagarid,
                        principalTable: "contaspagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fechamentosequipe_funcionarios_funcionarioid",
                        column: x => x.funcionarioid,
                        principalTable: "funcionarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "modeloschecklist",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modeloschecklist", x => x.id);
                    table.ForeignKey(
                        name: "fk_modeloschecklist_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "modelochecklistitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    modelochecklistid = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    diasantes = table.Column<int>(type: "integer", nullable: false),
                    responsavel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelochecklistitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_modelochecklistitens_modeloschecklist_modelochecklistid",
                        column: x => x.modelochecklistid,
                        principalTable: "modeloschecklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eventoequipe_fechamentoequipeid",
                table: "eventoequipe",
                column: "fechamentoequipeid");

            migrationBuilder.CreateIndex(
                name: "ix_eventotarefas_eventoid_prazo",
                table: "eventotarefas",
                columns: new[] { "eventoid", "prazo" });

            migrationBuilder.CreateIndex(
                name: "ix_fechamentosequipe_contapagarid",
                table: "fechamentosequipe",
                column: "contapagarid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fechamentosequipe_funcionarioid_de_ate",
                table: "fechamentosequipe",
                columns: new[] { "funcionarioid", "de", "ate" });

            migrationBuilder.CreateIndex(
                name: "ix_modelochecklistitens_modelochecklistid",
                table: "modelochecklistitens",
                column: "modelochecklistid");

            migrationBuilder.CreateIndex(
                name: "ix_modeloschecklist_nome",
                table: "modeloschecklist",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modeloschecklist_tipoeventoid",
                table: "modeloschecklist",
                column: "tipoeventoid");

            migrationBuilder.AddForeignKey(
                name: "fk_eventoequipe_fechamentosequipe_fechamentoequipeid",
                table: "eventoequipe",
                column: "fechamentoequipeid",
                principalTable: "fechamentosequipe",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_eventoequipe_fechamentosequipe_fechamentoequipeid",
                table: "eventoequipe");

            migrationBuilder.DropTable(
                name: "eventotarefas");

            migrationBuilder.DropTable(
                name: "fechamentosequipe");

            migrationBuilder.DropTable(
                name: "modelochecklistitens");

            migrationBuilder.DropTable(
                name: "modeloschecklist");

            migrationBuilder.DropIndex(
                name: "ix_eventoequipe_fechamentoequipeid",
                table: "eventoequipe");

            migrationBuilder.DropColumn(
                name: "fechamentoequipeid",
                table: "eventoequipe");
        }
    }
}
