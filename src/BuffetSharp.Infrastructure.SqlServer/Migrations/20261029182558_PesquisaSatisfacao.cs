using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PesquisaSatisfacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PesquisasSatisfacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegistradaPor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Nps = table.Column<int>(type: "int", nullable: true),
                    NotaComida = table.Column<int>(type: "int", nullable: true),
                    NotaAtendimento = table.Column<int>(type: "int", nullable: true),
                    NotaEspaco = table.Column<int>(type: "int", nullable: true),
                    NotaOrganizacao = table.Column<int>(type: "int", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AvaliacaoGooglePedidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TratativaDetrator = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DetratorTratadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesquisasSatisfacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PesquisasSatisfacao_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PesquisasSatisfacao_EventoId",
                table: "PesquisasSatisfacao",
                column: "EventoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PesquisasSatisfacao");
        }
    }
}
