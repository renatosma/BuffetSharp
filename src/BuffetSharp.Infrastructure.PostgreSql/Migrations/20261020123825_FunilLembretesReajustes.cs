using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FunilLembretesReajustes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detalheorigem = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tipoeventoid = table.Column<int>(type: "integer", nullable: true),
                    dataprevista = table.Column<DateOnly>(type: "date", nullable: true),
                    convidados = table.Column<int>(type: "integer", nullable: true),
                    responsavelid = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    proximocontato = table.Column<DateOnly>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    motivoperda = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    clienteid = table.Column<int>(type: "integer", nullable: true),
                    convertidoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_leads_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_tiposevento_tipoeventoid",
                        column: x => x.tipoeventoid,
                        principalTable: "tiposevento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_usuarios_responsavelid",
                        column: x => x.responsavelid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lembretestratados",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    tratadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lembretestratados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reajustespreco",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    vigenciaem = table.Column<DateOnly>(type: "date", nullable: false),
                    alcance = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    arredondamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    aplicadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    desfeitoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reajustespreco", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leadinteracoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    leadid = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leadinteracoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_leadinteracoes_leads_leadid",
                        column: x => x.leadid,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reajusteitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reajusteprecoid = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    itemid = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    valoranterior = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valornovo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reajusteitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_reajusteitens_reajustespreco_reajusteprecoid",
                        column: x => x.reajusteprecoid,
                        principalTable: "reajustespreco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leadinteracoes_leadid_data",
                table: "leadinteracoes",
                columns: new[] { "leadid", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_clienteid",
                table: "leads",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "ix_leads_criadoem",
                table: "leads",
                column: "criadoem");

            migrationBuilder.CreateIndex(
                name: "ix_leads_responsavelid",
                table: "leads",
                column: "responsavelid");

            migrationBuilder.CreateIndex(
                name: "ix_leads_status_proximocontato",
                table: "leads",
                columns: new[] { "status", "proximocontato" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_tipoeventoid",
                table: "leads",
                column: "tipoeventoid");

            migrationBuilder.CreateIndex(
                name: "ix_lembretestratados_chave",
                table: "lembretestratados",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lembretestratados_tratadoem",
                table: "lembretestratados",
                column: "tratadoem");

            migrationBuilder.CreateIndex(
                name: "ix_reajusteitens_reajusteprecoid_tipo_itemid",
                table: "reajusteitens",
                columns: new[] { "reajusteprecoid", "tipo", "itemid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reajustespreco_status_vigenciaem",
                table: "reajustespreco",
                columns: new[] { "status", "vigenciaem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leadinteracoes");

            migrationBuilder.DropTable(
                name: "lembretestratados");

            migrationBuilder.DropTable(
                name: "reajusteitens");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropTable(
                name: "reajustespreco");
        }
    }
}
