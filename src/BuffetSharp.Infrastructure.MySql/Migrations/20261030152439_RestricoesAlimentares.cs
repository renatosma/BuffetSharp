using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RestricoesAlimentares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Contem",
                table: "Produtos",
                type: "int",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContemInformado",
                table: "ItensCardapio",
                type: "int",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubstitutoId",
                table: "ItensCardapio",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacaoRestricao",
                table: "Convidados",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Restricoes",
                table: "Convidados",
                type: "int",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EventoRestricoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Restricoes = table.Column<int>(type: "int", maxLength: 30, nullable: false),
                    Pessoas = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoRestricoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoRestricoes_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ItensCardapio_SubstitutoId",
                table: "ItensCardapio",
                column: "SubstitutoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoRestricoes_EventoId",
                table: "EventoRestricoes",
                column: "EventoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensCardapio_ItensCardapio_SubstitutoId",
                table: "ItensCardapio",
                column: "SubstitutoId",
                principalTable: "ItensCardapio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensCardapio_ItensCardapio_SubstitutoId",
                table: "ItensCardapio");

            migrationBuilder.DropTable(
                name: "EventoRestricoes");

            migrationBuilder.DropIndex(
                name: "IX_ItensCardapio_SubstitutoId",
                table: "ItensCardapio");

            migrationBuilder.DropColumn(
                name: "Contem",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "ContemInformado",
                table: "ItensCardapio");

            migrationBuilder.DropColumn(
                name: "SubstitutoId",
                table: "ItensCardapio");

            migrationBuilder.DropColumn(
                name: "ObservacaoRestricao",
                table: "Convidados");

            migrationBuilder.DropColumn(
                name: "Restricoes",
                table: "Convidados");
        }
    }
}
