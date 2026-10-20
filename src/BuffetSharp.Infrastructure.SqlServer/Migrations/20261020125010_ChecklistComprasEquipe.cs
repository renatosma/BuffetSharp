using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistComprasEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FechamentoEquipeId",
                table: "EventoEquipe",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventoTarefas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Prazo = table.Column<DateOnly>(type: "date", nullable: false),
                    Responsavel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    ConcluidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcluidaPor = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoTarefas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoTarefas_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FechamentosEquipe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FuncionarioId = table.Column<int>(type: "int", nullable: false),
                    De = table.Column<DateOnly>(type: "date", nullable: false),
                    Ate = table.Column<DateOnly>(type: "date", nullable: false),
                    Diarias = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContaPagarId = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosEquipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosEquipe_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosEquipe_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelosChecklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoEventoId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosChecklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelosChecklist_TiposEvento_TipoEventoId",
                        column: x => x.TipoEventoId,
                        principalTable: "TiposEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModeloChecklistItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModeloChecklistId = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DiasAntes = table.Column<int>(type: "int", nullable: false),
                    Responsavel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeloChecklistItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModeloChecklistItens_ModelosChecklist_ModeloChecklistId",
                        column: x => x.ModeloChecklistId,
                        principalTable: "ModelosChecklist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventoEquipe_FechamentoEquipeId",
                table: "EventoEquipe",
                column: "FechamentoEquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoTarefas_EventoId_Prazo",
                table: "EventoTarefas",
                columns: new[] { "EventoId", "Prazo" });

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosEquipe_ContaPagarId",
                table: "FechamentosEquipe",
                column: "ContaPagarId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosEquipe_FuncionarioId_De_Ate",
                table: "FechamentosEquipe",
                columns: new[] { "FuncionarioId", "De", "Ate" });

            migrationBuilder.CreateIndex(
                name: "IX_ModeloChecklistItens_ModeloChecklistId",
                table: "ModeloChecklistItens",
                column: "ModeloChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelosChecklist_Nome",
                table: "ModelosChecklist",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelosChecklist_TipoEventoId",
                table: "ModelosChecklist",
                column: "TipoEventoId");

            migrationBuilder.AddForeignKey(
                name: "FK_EventoEquipe_FechamentosEquipe_FechamentoEquipeId",
                table: "EventoEquipe",
                column: "FechamentoEquipeId",
                principalTable: "FechamentosEquipe",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventoEquipe_FechamentosEquipe_FechamentoEquipeId",
                table: "EventoEquipe");

            migrationBuilder.DropTable(
                name: "EventoTarefas");

            migrationBuilder.DropTable(
                name: "FechamentosEquipe");

            migrationBuilder.DropTable(
                name: "ModeloChecklistItens");

            migrationBuilder.DropTable(
                name: "ModelosChecklist");

            migrationBuilder.DropIndex(
                name: "IX_EventoEquipe_FechamentoEquipeId",
                table: "EventoEquipe");

            migrationBuilder.DropColumn(
                name: "FechamentoEquipeId",
                table: "EventoEquipe");
        }
    }
}
