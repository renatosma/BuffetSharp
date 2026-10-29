using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class CronogramaEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventoEtapas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Horario = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Atividade = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Local = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true),
                    Responsavel = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    FuncionarioId = table.Column<int>(type: "int", nullable: true),
                    Observacao = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoEtapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoEtapas_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventoEtapas_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModelosCronograma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TipoEventoId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosCronograma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelosCronograma_TiposEvento_TipoEventoId",
                        column: x => x.TipoEventoId,
                        principalTable: "TiposEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModeloCronogramaItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ModeloCronogramaId = table.Column<int>(type: "int", nullable: false),
                    Atividade = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    MinutosDoInicio = table.Column<int>(type: "int", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Responsavel = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Local = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeloCronogramaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModeloCronogramaItens_ModelosCronograma_ModeloCronogramaId",
                        column: x => x.ModeloCronogramaId,
                        principalTable: "ModelosCronograma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EventoEtapas_EventoId_Horario",
                table: "EventoEtapas",
                columns: new[] { "EventoId", "Horario" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoEtapas_FuncionarioId",
                table: "EventoEtapas",
                column: "FuncionarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeloCronogramaItens_ModeloCronogramaId",
                table: "ModeloCronogramaItens",
                column: "ModeloCronogramaId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelosCronograma_Nome",
                table: "ModelosCronograma",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelosCronograma_TipoEventoId",
                table: "ModelosCronograma",
                column: "TipoEventoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoEtapas");

            migrationBuilder.DropTable(
                name: "ModeloCronogramaItens");

            migrationBuilder.DropTable(
                name: "ModelosCronograma");
        }
    }
}
