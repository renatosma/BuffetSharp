using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class RestricoesAlimentares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CONTEM",
                table: "PRODUTOS",
                type: "NUMBER(10)",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CONTEMINFORMADO",
                table: "ITENSCARDAPIO",
                type: "NUMBER(10)",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SUBSTITUTOID",
                table: "ITENSCARDAPIO",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OBSERVACAORESTRICAO",
                table: "CONVIDADOS",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RESTRICOES",
                table: "CONVIDADOS",
                type: "NUMBER(10)",
                maxLength: 30,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EVENTORESTRICOES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RESTRICOES = table.Column<int>(type: "NUMBER(10)", maxLength: 30, nullable: false),
                    PESSOAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTORESTRICOES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTORESTRICOES_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ITENSCARDAPIO_SUBSTITUTOID",
                table: "ITENSCARDAPIO",
                column: "SUBSTITUTOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTORESTRICOES_EVENTOID",
                table: "EVENTORESTRICOES",
                column: "EVENTOID");

            migrationBuilder.AddForeignKey(
                name: "FK_ITENSCARDAPIO_ITENSCARDAPIO_SUBSTITUTOID",
                table: "ITENSCARDAPIO",
                column: "SUBSTITUTOID",
                principalTable: "ITENSCARDAPIO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ITENSCARDAPIO_ITENSCARDAPIO_SUBSTITUTOID",
                table: "ITENSCARDAPIO");

            migrationBuilder.DropTable(
                name: "EVENTORESTRICOES");

            migrationBuilder.DropIndex(
                name: "IX_ITENSCARDAPIO_SUBSTITUTOID",
                table: "ITENSCARDAPIO");

            migrationBuilder.DropColumn(
                name: "CONTEM",
                table: "PRODUTOS");

            migrationBuilder.DropColumn(
                name: "CONTEMINFORMADO",
                table: "ITENSCARDAPIO");

            migrationBuilder.DropColumn(
                name: "SUBSTITUTOID",
                table: "ITENSCARDAPIO");

            migrationBuilder.DropColumn(
                name: "OBSERVACAORESTRICAO",
                table: "CONVIDADOS");

            migrationBuilder.DropColumn(
                name: "RESTRICOES",
                table: "CONVIDADOS");
        }
    }
}
