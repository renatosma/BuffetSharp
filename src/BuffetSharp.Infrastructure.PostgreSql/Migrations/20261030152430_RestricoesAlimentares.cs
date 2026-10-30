using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RestricoesAlimentares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contem",
                table: "produtos",
                type: "integer",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "conteminformado",
                table: "itenscardapio",
                type: "integer",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "substitutoid",
                table: "itenscardapio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacaorestricao",
                table: "convidados",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "restricoes",
                table: "convidados",
                type: "integer",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "eventorestricoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    restricoes = table.Column<int>(type: "integer", maxLength: 30, nullable: false),
                    pessoas = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventorestricoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventorestricoes_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_itenscardapio_substitutoid",
                table: "itenscardapio",
                column: "substitutoid");

            migrationBuilder.CreateIndex(
                name: "ix_eventorestricoes_eventoid",
                table: "eventorestricoes",
                column: "eventoid");

            migrationBuilder.AddForeignKey(
                name: "fk_itenscardapio_itenscardapio_substitutoid",
                table: "itenscardapio",
                column: "substitutoid",
                principalTable: "itenscardapio",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_itenscardapio_itenscardapio_substitutoid",
                table: "itenscardapio");

            migrationBuilder.DropTable(
                name: "eventorestricoes");

            migrationBuilder.DropIndex(
                name: "ix_itenscardapio_substitutoid",
                table: "itenscardapio");

            migrationBuilder.DropColumn(
                name: "contem",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "conteminformado",
                table: "itenscardapio");

            migrationBuilder.DropColumn(
                name: "substitutoid",
                table: "itenscardapio");

            migrationBuilder.DropColumn(
                name: "observacaorestricao",
                table: "convidados");

            migrationBuilder.DropColumn(
                name: "restricoes",
                table: "convidados");
        }
    }
}
