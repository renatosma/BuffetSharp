using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ListaEspera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EsperasData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    LeadId = table.Column<int>(type: "int", nullable: true),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EspacoId = table.Column<int>(type: "int", nullable: true),
                    TipoEventoId = table.Column<int>(type: "int", nullable: true),
                    Convidados = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Situacao = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntrouEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvisadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrcamentoId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EsperasData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EsperasData_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EsperasData_Espacos_EspacoId",
                        column: x => x.EspacoId,
                        principalTable: "Espacos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EsperasData_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EsperasData_Orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalTable: "Orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EsperasData_TiposEvento_TipoEventoId",
                        column: x => x.TipoEventoId,
                        principalTable: "TiposEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EsperasData_ClienteId",
                table: "EsperasData",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_EsperasData_EspacoId",
                table: "EsperasData",
                column: "EspacoId");

            migrationBuilder.CreateIndex(
                name: "IX_EsperasData_LeadId",
                table: "EsperasData",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_EsperasData_OrcamentoId",
                table: "EsperasData",
                column: "OrcamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_EsperasData_Situacao_Inicio",
                table: "EsperasData",
                columns: new[] { "Situacao", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_EsperasData_TipoEventoId",
                table: "EsperasData",
                column: "TipoEventoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EsperasData");
        }
    }
}
