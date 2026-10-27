using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class NotaFiscalServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONFIGURACOESFISCAIS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    AMBIENTE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    CNPJ = table.Column<string>(type: "NVARCHAR2(14)", maxLength: 14, nullable: true),
                    INSCRICAOMUNICIPAL = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    CODIGOMUNICIPIO = table.Column<string>(type: "NVARCHAR2(7)", maxLength: 7, nullable: true),
                    REGIME = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ITEMLISTASERVICO = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    CODIGOTRIBUTARIOMUNICIPIO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    CODIGOCNAE = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    ALIQUOTAISS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    ISSRETIDO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DISCRIMINACAO = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONFIGURACOESFISCAIS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NOTASFISCAISSERVICO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    REFERENCIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    AMBIENTE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VALORSERVICOS = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    ALIQUOTAISS = table.Column<decimal>(type: "DECIMAL(5,2)", precision: 5, scale: 2, nullable: false),
                    ISSRETIDO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DISCRIMINACAO = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    TOMADORDOCUMENTO = table.Column<string>(type: "NVARCHAR2(14)", maxLength: 14, nullable: false),
                    TOMADORNOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    TOMADOREMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    NUMERO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    CODIGOVERIFICACAO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    DATAEMISSAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    URL = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CAMINHOXML = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    MENSAGEM = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    SOLICITADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CANCELADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTASFISCAISSERVICO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NOTASFISCAISSERVICO_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISSERVICO_EVENTOID_STATUS",
                table: "NOTASFISCAISSERVICO",
                columns: new[] { "EVENTOID", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISSERVICO_REFERENCIA",
                table: "NOTASFISCAISSERVICO",
                column: "REFERENCIA",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISSERVICO_SOLICITADAEM",
                table: "NOTASFISCAISSERVICO",
                column: "SOLICITADAEM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONFIGURACOESFISCAIS");

            migrationBuilder.DropTable(
                name: "NOTASFISCAISSERVICO");
        }
    }
}
