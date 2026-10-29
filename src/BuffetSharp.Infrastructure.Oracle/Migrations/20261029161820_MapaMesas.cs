using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class MapaMesas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MESAID",
                table: "CONVIDADOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ESPACOMESAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ESPACOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NUMERO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: true),
                    LUGARES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORMATO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESPACOMESAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ESPACOMESAS_ESPACOS_ESPACOID",
                        column: x => x.ESPACOID,
                        principalTable: "ESPACOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EVENTOMESAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NUMERO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: true),
                    LUGARES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORMATO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOMESAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOMESAS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONVIDADOS_MESAID",
                table: "CONVIDADOS",
                column: "MESAID");

            migrationBuilder.CreateIndex(
                name: "IX_ESPACOMESAS_ESPACOID_NUMERO",
                table: "ESPACOMESAS",
                columns: new[] { "ESPACOID", "NUMERO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOMESAS_EVENTOID_NUMERO",
                table: "EVENTOMESAS",
                columns: new[] { "EVENTOID", "NUMERO" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CONVIDADOS_EVENTOMESAS_MESAID",
                table: "CONVIDADOS",
                column: "MESAID",
                principalTable: "EVENTOMESAS",
                principalColumn: "ID");
            // A mesa em texto de cada convidado vira uma mesa do mapa do evento (redonda, numerada na ordem do texto e posta numa
            // grade); o convidado passa a apontar para ela. Nomes que são só o número ("Mesa 3", "3") deixam de ser repetidos.
            migrationBuilder.Sql(
                "INSERT INTO EventoMesas (EventoId, Numero, Nome, Lugares, Formato, X, Y) " +
                "SELECT EventoId, Numero, Mesa, CASE WHEN Pessoas > 30 THEN 30 WHEN Pessoas > 10 THEN Pessoas ELSE 10 END, 'Redonda', " +
                "100 + MOD(Numero - 1, 8) * 150, 100 + MOD(FLOOR((Numero - 1) / 8), 5) * 150 " +
                "FROM (SELECT EventoId, Mesa, SUM(1 + Acompanhantes) AS Pessoas, " +
                "ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY LENGTH(Mesa), Mesa) AS Numero " +
                "FROM Convidados WHERE Mesa IS NOT NULL GROUP BY EventoId, Mesa) m");
            migrationBuilder.Sql(
                "UPDATE Convidados SET MesaId = (SELECT m.Id FROM EventoMesas m WHERE m.EventoId = Convidados.EventoId AND m.Nome = Convidados.Mesa) " +
                "WHERE Mesa IS NOT NULL");
            migrationBuilder.Sql("UPDATE EventoMesas SET Nome = NULL WHERE Nome = CONCAT('Mesa ', Numero) OR Nome = CONCAT('', Numero)");

            migrationBuilder.DropColumn(
                name: "MESA",
                table: "CONVIDADOS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONVIDADOS_EVENTOMESAS_MESAID",
                table: "CONVIDADOS");

            migrationBuilder.DropTable(
                name: "ESPACOMESAS");

            migrationBuilder.DropTable(
                name: "EVENTOMESAS");

            migrationBuilder.DropIndex(
                name: "IX_CONVIDADOS_MESAID",
                table: "CONVIDADOS");

            migrationBuilder.DropColumn(
                name: "MESAID",
                table: "CONVIDADOS");

            migrationBuilder.AddColumn<string>(
                name: "MESA",
                table: "CONVIDADOS",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
