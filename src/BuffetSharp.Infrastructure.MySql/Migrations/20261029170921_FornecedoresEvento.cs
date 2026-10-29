using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class FornecedoresEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventoFornecedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: false),
                    EventoServicoId = table.Column<int>(type: "int", nullable: true),
                    Servico = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Situacao = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Responsavel = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Telefone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Observacao = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ConfirmadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PagamentoId = table.Column<int>(type: "int", nullable: true),
                    EtapaChegadaId = table.Column<int>(type: "int", nullable: true),
                    Nota = table.Column<int>(type: "int", nullable: true),
                    Comentario = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    AvaliadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoFornecedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoFornecedores_EventoEtapas_EtapaChegadaId",
                        column: x => x.EtapaChegadaId,
                        principalTable: "EventoEtapas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventoFornecedores_EventoServicos_EventoServicoId",
                        column: x => x.EventoServicoId,
                        principalTable: "EventoServicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoFornecedores_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventoFornecedores_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoFornecedores_ParcelamentosPagar_PagamentoId",
                        column: x => x.PagamentoId,
                        principalTable: "ParcelamentosPagar",
                        principalColumn: "Id");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EventoFornecedores_EtapaChegadaId",
                table: "EventoFornecedores",
                column: "EtapaChegadaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoFornecedores_EventoId",
                table: "EventoFornecedores",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoFornecedores_EventoServicoId",
                table: "EventoFornecedores",
                column: "EventoServicoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoFornecedores_FornecedorId",
                table: "EventoFornecedores",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoFornecedores_PagamentoId",
                table: "EventoFornecedores",
                column: "PagamentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoFornecedores");
        }
    }
}
