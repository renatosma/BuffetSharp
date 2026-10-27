using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class CobrancaRegistrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COBRANCASREGISTRADAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PARCELAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    AMBIENTE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VENCIMENTO = table.Column<DateTime>(type: "DATE", nullable: false),
                    REFERENCIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    IDEXTERNO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    CLIENTEEXTERNO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    LINHADIGITAVEL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    NOSSONUMERO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    URLBOLETO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    URLFATURA = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    PIXCOPIAECOLA = table.Column<string>(type: "NVARCHAR2(600)", maxLength: 600, nullable: true),
                    DATAPAGAMENTO = table.Column<DateTime>(type: "DATE", nullable: true),
                    VALORPAGO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: true),
                    VALORLIQUIDO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: true),
                    PAGACOM = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    BAIXADA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    MENSAGEM = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    SOLICITADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CANCELADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COBRANCASREGISTRADAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COBRANCASREGISTRADAS_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COBRANCASREGISTRADAS_PARCELAS_PARCELAID",
                        column: x => x.PARCELAID,
                        principalTable: "PARCELAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CONFIGURACOESCOBRANCA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    AMBIENTE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    TIPOPADRAO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    CONTAFINANCEIRAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    MULTAPERCENTUAL = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    JUROSMENSALPERCENTUAL = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONFIGURACOESCOBRANCA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONFIGURACOESCOBRANCA_CONTASFINANCEIRAS_CONTAFINANCEIRAID",
                        column: x => x.CONTAFINANCEIRAID,
                        principalTable: "CONTASFINANCEIRAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COBRANCASREGISTRADAS_EVENTOID",
                table: "COBRANCASREGISTRADAS",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_COBRANCASREGISTRADAS_PARCELAID",
                table: "COBRANCASREGISTRADAS",
                column: "PARCELAID");

            migrationBuilder.CreateIndex(
                name: "IX_COBRANCASREGISTRADAS_REFERENCIA",
                table: "COBRANCASREGISTRADAS",
                column: "REFERENCIA",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COBRANCASREGISTRADAS_SOLICITADAEM",
                table: "COBRANCASREGISTRADAS",
                column: "SOLICITADAEM");

            migrationBuilder.CreateIndex(
                name: "IX_COBRANCASREGISTRADAS_STATUS_PARCELAID",
                table: "COBRANCASREGISTRADAS",
                columns: new[] { "STATUS", "PARCELAID" });

            migrationBuilder.CreateIndex(
                name: "IX_CONFIGURACOESCOBRANCA_CONTAFINANCEIRAID",
                table: "CONFIGURACOESCOBRANCA",
                column: "CONTAFINANCEIRAID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COBRANCASREGISTRADAS");

            migrationBuilder.DropTable(
                name: "CONFIGURACOESCOBRANCA");
        }
    }
}
