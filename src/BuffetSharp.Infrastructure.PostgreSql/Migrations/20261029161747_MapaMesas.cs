using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MapaMesas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "mesaid",
                table: "convidados",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "espacomesas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    espacoid = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    lugares = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_espacomesas", x => x.id);
                    table.ForeignKey(
                        name: "fk_espacomesas_espacos_espacoid",
                        column: x => x.espacoid,
                        principalTable: "espacos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventomesas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    lugares = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventomesas", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventomesas_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_convidados_mesaid",
                table: "convidados",
                column: "mesaid");

            migrationBuilder.CreateIndex(
                name: "ix_espacomesas_espacoid_numero",
                table: "espacomesas",
                columns: new[] { "espacoid", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventomesas_eventoid_numero",
                table: "eventomesas",
                columns: new[] { "eventoid", "numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_convidados_eventomesas_mesaid",
                table: "convidados",
                column: "mesaid",
                principalTable: "eventomesas",
                principalColumn: "id");
            // A mesa em texto de cada convidado vira uma mesa do mapa do evento (redonda, numerada na ordem do texto e posta numa
            // grade); o convidado passa a apontar para ela. Nomes que são só o número ("Mesa 3", "3") deixam de ser repetidos.
            migrationBuilder.Sql(
                "INSERT INTO EventoMesas (EventoId, Numero, Nome, Lugares, Formato, X, Y) " +
                "SELECT EventoId, Numero, Mesa, CASE WHEN Pessoas > 30 THEN 30 WHEN Pessoas > 10 THEN Pessoas ELSE 10 END, 'Redonda', " +
                "100 + (Numero - 1) % 8 * 150, 100 + (Numero - 1) / 8 % 5 * 150 " +
                "FROM (SELECT EventoId, Mesa, SUM(1 + Acompanhantes) AS Pessoas, " +
                "ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY LENGTH(Mesa), Mesa) AS Numero " +
                "FROM Convidados WHERE Mesa IS NOT NULL GROUP BY EventoId, Mesa) m");
            migrationBuilder.Sql(
                "UPDATE Convidados SET MesaId = (SELECT m.Id FROM EventoMesas m WHERE m.EventoId = Convidados.EventoId AND m.Nome = Convidados.Mesa) " +
                "WHERE Mesa IS NOT NULL");
            migrationBuilder.Sql("UPDATE EventoMesas SET Nome = NULL WHERE Nome = CONCAT('Mesa ', Numero) OR Nome = CONCAT('', Numero)");

            migrationBuilder.DropColumn(
                name: "mesa",
                table: "convidados");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_convidados_eventomesas_mesaid",
                table: "convidados");

            migrationBuilder.DropTable(
                name: "espacomesas");

            migrationBuilder.DropTable(
                name: "eventomesas");

            migrationBuilder.DropIndex(
                name: "ix_convidados_mesaid",
                table: "convidados");

            migrationBuilder.DropColumn(
                name: "mesaid",
                table: "convidados");

            migrationBuilder.AddColumn<string>(
                name: "mesa",
                table: "convidados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
