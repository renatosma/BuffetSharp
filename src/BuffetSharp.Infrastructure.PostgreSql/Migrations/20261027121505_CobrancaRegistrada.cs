using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CobrancaRegistrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cobrancasregistradas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parcelaid = table.Column<int>(type: "integer", nullable: true),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ambiente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    referencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    idexterno = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    clienteexterno = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    linhadigitavel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    nossonumero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    urlboleto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    urlfatura = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pixcopiaecola = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    datapagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    valorpago = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    valorliquido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    pagacom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    baixada = table.Column<bool>(type: "boolean", nullable: false),
                    mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    solicitadaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    canceladaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cobrancasregistradas", x => x.id);
                    table.ForeignKey(
                        name: "fk_cobrancasregistradas_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cobrancasregistradas_parcelas_parcelaid",
                        column: x => x.parcelaid,
                        principalTable: "parcelas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "configuracoescobranca",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ambiente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipopadrao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    contafinanceiraid = table.Column<int>(type: "integer", nullable: true),
                    multapercentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    jurosmensalpercentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoescobranca", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoescobranca_contasfinanceiras_contafinanceiraid",
                        column: x => x.contafinanceiraid,
                        principalTable: "contasfinanceiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cobrancasregistradas_eventoid",
                table: "cobrancasregistradas",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancasregistradas_parcelaid",
                table: "cobrancasregistradas",
                column: "parcelaid");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancasregistradas_referencia",
                table: "cobrancasregistradas",
                column: "referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cobrancasregistradas_solicitadaem",
                table: "cobrancasregistradas",
                column: "solicitadaem");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancasregistradas_status_parcelaid",
                table: "cobrancasregistradas",
                columns: new[] { "status", "parcelaid" });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoescobranca_contafinanceiraid",
                table: "configuracoescobranca",
                column: "contafinanceiraid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cobrancasregistradas");

            migrationBuilder.DropTable(
                name: "configuracoescobranca");
        }
    }
}
