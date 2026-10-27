using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class NotaFiscalServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoesfiscais",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ambiente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    inscricaomunicipal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigomunicipio = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    regime = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    itemlistaservico = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    codigotributariomunicipio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigocnae = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    aliquotaiss = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    issretido = table.Column<bool>(type: "boolean", nullable: false),
                    discriminacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoesfiscais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notasfiscaisservico",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    referencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    ambiente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valorservicos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aliquotaiss = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    issretido = table.Column<bool>(type: "boolean", nullable: false),
                    discriminacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tomadordocumento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    tomadornome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tomadoremail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigoverificacao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    dataemissao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    caminhoxml = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    solicitadaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    canceladaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivocancelamento = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notasfiscaisservico", x => x.id);
                    table.ForeignKey(
                        name: "fk_notasfiscaisservico_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisservico_eventoid_status",
                table: "notasfiscaisservico",
                columns: new[] { "eventoid", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisservico_referencia",
                table: "notasfiscaisservico",
                column: "referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisservico_solicitadaem",
                table: "notasfiscaisservico",
                column: "solicitadaem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoesfiscais");

            migrationBuilder.DropTable(
                name: "notasfiscaisservico");
        }
    }
}
