using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class FunilLembretesReajustes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LEADS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    ORIGEM = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DETALHEORIGEM = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    TIPOEVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DATAPREVISTA = table.Column<DateTime>(type: "DATE", nullable: true),
                    CONVIDADOS = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    RESPONSAVELID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    PROXIMOCONTATO = table.Column<DateTime>(type: "DATE", nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    MOTIVOPERDA = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CLIENTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CONVERTIDOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LEADS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LEADS_CLIENTES_CLIENTEID",
                        column: x => x.CLIENTEID,
                        principalTable: "CLIENTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LEADS_TIPOSEVENTO_TIPOEVENTOID",
                        column: x => x.TIPOEVENTOID,
                        principalTable: "TIPOSEVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LEADS_USUARIOS_RESPONSAVELID",
                        column: x => x.RESPONSAVELID,
                        principalTable: "USUARIOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LEMBRETESTRATADOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CHAVE = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    CANAL = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    TRATADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LEMBRETESTRATADOS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "REAJUSTESPRECO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PERCENTUAL = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    VIGENCIAEM = table.Column<DateTime>(type: "DATE", nullable: false),
                    ALCANCE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ARREDONDAMENTO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    APLICADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DESFEITOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REAJUSTESPRECO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LEADINTERACOES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    LEADID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LEADINTERACOES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LEADINTERACOES_LEADS_LEADID",
                        column: x => x.LEADID,
                        principalTable: "LEADS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "REAJUSTEITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    REAJUSTEPRECOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ITEMID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    VALORANTERIOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORNOVO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REAJUSTEITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_REAJUSTEITENS_REAJUSTESPRECO_REAJUSTEPRECOID",
                        column: x => x.REAJUSTEPRECOID,
                        principalTable: "REAJUSTESPRECO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LEADINTERACOES_LEADID_DATA",
                table: "LEADINTERACOES",
                columns: new[] { "LEADID", "DATA" });

            migrationBuilder.CreateIndex(
                name: "IX_LEADS_CLIENTEID",
                table: "LEADS",
                column: "CLIENTEID");

            migrationBuilder.CreateIndex(
                name: "IX_LEADS_CRIADOEM",
                table: "LEADS",
                column: "CRIADOEM");

            migrationBuilder.CreateIndex(
                name: "IX_LEADS_RESPONSAVELID",
                table: "LEADS",
                column: "RESPONSAVELID");

            migrationBuilder.CreateIndex(
                name: "IX_LEADS_STATUS_PROXIMOCONTATO",
                table: "LEADS",
                columns: new[] { "STATUS", "PROXIMOCONTATO" });

            migrationBuilder.CreateIndex(
                name: "IX_LEADS_TIPOEVENTOID",
                table: "LEADS",
                column: "TIPOEVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_LEMBRETESTRATADOS_CHAVE",
                table: "LEMBRETESTRATADOS",
                column: "CHAVE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LEMBRETESTRATADOS_TRATADOEM",
                table: "LEMBRETESTRATADOS",
                column: "TRATADOEM");

            migrationBuilder.CreateIndex(
                name: "IX_REAJUSTEITENS_REAJUSTEPRECOID_TIPO_ITEMID",
                table: "REAJUSTEITENS",
                columns: new[] { "REAJUSTEPRECOID", "TIPO", "ITEMID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REAJUSTESPRECO_STATUS_VIGENCIAEM",
                table: "REAJUSTESPRECO",
                columns: new[] { "STATUS", "VIGENCIAEM" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LEADINTERACOES");

            migrationBuilder.DropTable(
                name: "LEMBRETESTRATADOS");

            migrationBuilder.DropTable(
                name: "REAJUSTEITENS");

            migrationBuilder.DropTable(
                name: "LEADS");

            migrationBuilder.DropTable(
                name: "REAJUSTESPRECO");
        }
    }
}
