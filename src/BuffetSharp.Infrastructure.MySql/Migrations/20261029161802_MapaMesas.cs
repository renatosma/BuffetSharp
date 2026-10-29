using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class MapaMesas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MesaId",
                table: "Convidados",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EspacoMesas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EspacoId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Lugares = table.Column<int>(type: "int", nullable: false),
                    Formato = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EspacoMesas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EspacoMesas_Espacos_EspacoId",
                        column: x => x.EspacoId,
                        principalTable: "Espacos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EventoMesas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Lugares = table.Column<int>(type: "int", nullable: false),
                    Formato = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoMesas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoMesas_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Convidados_MesaId",
                table: "Convidados",
                column: "MesaId");

            migrationBuilder.CreateIndex(
                name: "IX_EspacoMesas_EspacoId_Numero",
                table: "EspacoMesas",
                columns: new[] { "EspacoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventoMesas_EventoId_Numero",
                table: "EventoMesas",
                columns: new[] { "EventoId", "Numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Convidados_EventoMesas_MesaId",
                table: "Convidados",
                column: "MesaId",
                principalTable: "EventoMesas",
                principalColumn: "Id");
            // A mesa em texto de cada convidado vira uma mesa do mapa do evento (redonda, numerada na ordem do texto e posta numa
            // grade); o convidado passa a apontar para ela. Nomes que são só o número ("Mesa 3", "3") deixam de ser repetidos.
            migrationBuilder.Sql(
                "INSERT INTO EventoMesas (EventoId, Numero, Nome, Lugares, Formato, X, Y) " +
                "SELECT EventoId, Numero, Mesa, CASE WHEN Pessoas > 30 THEN 30 WHEN Pessoas > 10 THEN Pessoas ELSE 10 END, 'Redonda', " +
                "100 + (Numero - 1) % 8 * 150, 100 + (Numero - 1) DIV 8 % 5 * 150 " +
                "FROM (SELECT EventoId, Mesa, SUM(1 + Acompanhantes) AS Pessoas, " +
                "ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY CHAR_LENGTH(Mesa), Mesa) AS Numero " +
                "FROM Convidados WHERE Mesa IS NOT NULL GROUP BY EventoId, Mesa) m");
            migrationBuilder.Sql(
                "UPDATE Convidados SET MesaId = (SELECT m.Id FROM EventoMesas m WHERE m.EventoId = Convidados.EventoId AND m.Nome = Convidados.Mesa) " +
                "WHERE Mesa IS NOT NULL");
            migrationBuilder.Sql("UPDATE EventoMesas SET Nome = NULL WHERE Nome = CONCAT('Mesa ', Numero) OR Nome = CONCAT('', Numero)");

            migrationBuilder.DropColumn(
                name: "Mesa",
                table: "Convidados");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Convidados_EventoMesas_MesaId",
                table: "Convidados");

            migrationBuilder.DropTable(
                name: "EspacoMesas");

            migrationBuilder.DropTable(
                name: "EventoMesas");

            migrationBuilder.DropIndex(
                name: "IX_Convidados_MesaId",
                table: "Convidados");

            migrationBuilder.DropColumn(
                name: "MesaId",
                table: "Convidados");

            migrationBuilder.AddColumn<string>(
                name: "Mesa",
                table: "Convidados",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
