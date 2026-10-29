using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ConvitesAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConvitesEnviados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Uid = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Sequencia = table.Column<int>(type: "int", nullable: false),
                    Inicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Fim = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Cancelado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Destinatario = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Contato = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Canal = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Usuario = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    EnviadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EventoId = table.Column<int>(type: "int", nullable: true),
                    DegustacaoId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvitesEnviados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvitesEnviados_Degustacoes_DegustacaoId",
                        column: x => x.DegustacaoId,
                        principalTable: "Degustacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConvitesEnviados_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesEnviados_DegustacaoId",
                table: "ConvitesEnviados",
                column: "DegustacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesEnviados_EventoId",
                table: "ConvitesEnviados",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesEnviados_Uid",
                table: "ConvitesEnviados",
                column: "Uid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConvitesEnviados");
        }
    }
}
