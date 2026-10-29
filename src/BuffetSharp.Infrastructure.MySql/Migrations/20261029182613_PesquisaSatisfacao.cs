using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
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
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RespondidaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RegistradaPor = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Nps = table.Column<int>(type: "int", nullable: true),
                    NotaComida = table.Column<int>(type: "int", nullable: true),
                    NotaAtendimento = table.Column<int>(type: "int", nullable: true),
                    NotaEspaco = table.Column<int>(type: "int", nullable: true),
                    NotaOrganizacao = table.Column<int>(type: "int", nullable: true),
                    Comentario = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    AvaliacaoGooglePedidaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TratativaDetrator = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    DetratorTratadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
