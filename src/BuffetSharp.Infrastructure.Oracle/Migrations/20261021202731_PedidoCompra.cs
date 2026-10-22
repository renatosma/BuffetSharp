using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class PedidoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DEPEDIDOCOMPRA",
                table: "MOVIMENTACOESESTOQUE",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PEDIDOSCOMPRA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    PREVISAOENTREGA = table.Column<DateTime>(type: "DATE", nullable: true),
                    CATEGORIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    NOTAFISCAL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    DATACOMPRA = table.Column<DateTime>(type: "DATE", nullable: true),
                    RECEBIDOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CONTAPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PARCELAMENTOPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PEDIDOSCOMPRA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PEDIDOSCOMPRA_CONTASPAGAR_CONTAPAGARID",
                        column: x => x.CONTAPAGARID,
                        principalTable: "CONTASPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PEDIDOSCOMPRA_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PEDIDOSCOMPRA_PARCELAMENTOSPAGAR_PARCELAMENTOPAGARID",
                        column: x => x.PARCELAMENTOPAGARID,
                        principalTable: "PARCELAMENTOSPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PEDIDOCOMPRAITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PEDIDOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PRODUTOEMBALAGEMID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    CUSTOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    MOVIMENTACAOESTOQUEID = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PEDIDOCOMPRAITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PEDIDOCOMPRAITENS_MOVIMENTACOESESTOQUE_MOVIMENTACAOESTOQUEID",
                        column: x => x.MOVIMENTACAOESTOQUEID,
                        principalTable: "MOVIMENTACOESESTOQUE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PEDIDOCOMPRAITENS_PEDIDOSCOMPRA_PEDIDOCOMPRAID",
                        column: x => x.PEDIDOCOMPRAID,
                        principalTable: "PEDIDOSCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PEDIDOCOMPRAITENS_PRODUTOEMBALAGENS_PRODUTOEMBALAGEMID",
                        column: x => x.PRODUTOEMBALAGEMID,
                        principalTable: "PRODUTOEMBALAGENS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PEDIDOCOMPRAITENS_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOCOMPRAITENS_MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS",
                column: "MOVIMENTACAOESTOQUEID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOCOMPRAITENS_PEDIDOCOMPRAID",
                table: "PEDIDOCOMPRAITENS",
                column: "PEDIDOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOCOMPRAITENS_PRODUTOEMBALAGEMID",
                table: "PEDIDOCOMPRAITENS",
                column: "PRODUTOEMBALAGEMID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOCOMPRAITENS_PRODUTOID",
                table: "PEDIDOCOMPRAITENS",
                column: "PRODUTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_CONTAPAGARID",
                table: "PEDIDOSCOMPRA",
                column: "CONTAPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_FORNECEDORID",
                table: "PEDIDOSCOMPRA",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_NOTAFISCAL",
                table: "PEDIDOSCOMPRA",
                column: "NOTAFISCAL");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_PARCELAMENTOPAGARID",
                table: "PEDIDOSCOMPRA",
                column: "PARCELAMENTOPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_STATUS_DATA",
                table: "PEDIDOSCOMPRA",
                columns: new[] { "STATUS", "DATA" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PEDIDOCOMPRAITENS");

            migrationBuilder.DropTable(
                name: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "DEPEDIDOCOMPRA",
                table: "MOVIMENTACOESESTOQUE");
        }
    }
}
