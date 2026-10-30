using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class DevolucaoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QUANTIDADEDEVOLVIDA",
                table: "RECEBIMENTOITENS",
                type: "DECIMAL(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CREDITOUSADO",
                table: "NOTASFISCAISENTRADA",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VALORDEVOLVIDO",
                table: "NOTASFISCAISENTRADA",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DEVOLUCOESCOMPRA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RECEBIMENTOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PEDIDOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    MOTIVO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    FORNECEDORREPOE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    NOTADEVOLUCAO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    REGISTRADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    NOTAFISCALENTRADAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORABATIDO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORCREDITO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    CREDITOUTILIZADO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEVOLUCOESCOMPRA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DEVOLUCOESCOMPRA_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DEVOLUCOESCOMPRA_NOTASFISCAISENTRADA_NOTAFISCALENTRADAID",
                        column: x => x.NOTAFISCALENTRADAID,
                        principalTable: "NOTASFISCAISENTRADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DEVOLUCOESCOMPRA_PEDIDOSCOMPRA_PEDIDOCOMPRAID",
                        column: x => x.PEDIDOCOMPRAID,
                        principalTable: "PEDIDOSCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DEVOLUCOESCOMPRA_RECEBIMENTOSCOMPRA_RECEBIMENTOCOMPRAID",
                        column: x => x.RECEBIMENTOCOMPRAID,
                        principalTable: "RECEBIMENTOSCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CREDITOSUSADOSNOTA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOTAFISCALENTRADAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DEVOLUCAOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDITOSUSADOSNOTA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CREDITOSUSADOSNOTA_DEVOLUCOESCOMPRA_DEVOLUCAOCOMPRAID",
                        column: x => x.DEVOLUCAOCOMPRAID,
                        principalTable: "DEVOLUCOESCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CREDITOSUSADOSNOTA_NOTASFISCAISENTRADA_NOTAFISCALENTRADAID",
                        column: x => x.NOTAFISCALENTRADAID,
                        principalTable: "NOTASFISCAISENTRADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DEVOLUCAOITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DEVOLUCAOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RECEBIMENTOITEMID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    CUSTOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    MOVIMENTACAOESTOQUEID = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEVOLUCAOITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DEVOLUCAOITENS_DEVOLUCOESCOMPRA_DEVOLUCAOCOMPRAID",
                        column: x => x.DEVOLUCAOCOMPRAID,
                        principalTable: "DEVOLUCOESCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DEVOLUCAOITENS_MOVIMENTACOESESTOQUE_MOVIMENTACAOESTOQUEID",
                        column: x => x.MOVIMENTACAOESTOQUEID,
                        principalTable: "MOVIMENTACOESESTOQUE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DEVOLUCAOITENS_RECEBIMENTOITENS_RECEBIMENTOITEMID",
                        column: x => x.RECEBIMENTOITEMID,
                        principalTable: "RECEBIMENTOITENS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CREDITOSUSADOSNOTA_DEVOLUCAOCOMPRAID",
                table: "CREDITOSUSADOSNOTA",
                column: "DEVOLUCAOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_CREDITOSUSADOSNOTA_NOTAFISCALENTRADAID",
                table: "CREDITOSUSADOSNOTA",
                column: "NOTAFISCALENTRADAID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCAOITENS_DEVOLUCAOCOMPRAID",
                table: "DEVOLUCAOITENS",
                column: "DEVOLUCAOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCAOITENS_MOVIMENTACAOESTOQUEID",
                table: "DEVOLUCAOITENS",
                column: "MOVIMENTACAOESTOQUEID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCAOITENS_RECEBIMENTOITEMID",
                table: "DEVOLUCAOITENS",
                column: "RECEBIMENTOITEMID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCOESCOMPRA_FORNECEDORID_DATA",
                table: "DEVOLUCOESCOMPRA",
                columns: new[] { "FORNECEDORID", "DATA" });

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCOESCOMPRA_NOTAFISCALENTRADAID",
                table: "DEVOLUCOESCOMPRA",
                column: "NOTAFISCALENTRADAID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCOESCOMPRA_PEDIDOCOMPRAID",
                table: "DEVOLUCOESCOMPRA",
                column: "PEDIDOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVOLUCOESCOMPRA_RECEBIMENTOCOMPRAID",
                table: "DEVOLUCOESCOMPRA",
                column: "RECEBIMENTOCOMPRAID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CREDITOSUSADOSNOTA");

            migrationBuilder.DropTable(
                name: "DEVOLUCAOITENS");

            migrationBuilder.DropTable(
                name: "DEVOLUCOESCOMPRA");

            migrationBuilder.DropColumn(
                name: "QUANTIDADEDEVOLVIDA",
                table: "RECEBIMENTOITENS");

            migrationBuilder.DropColumn(
                name: "CREDITOUSADO",
                table: "NOTASFISCAISENTRADA");

            migrationBuilder.DropColumn(
                name: "VALORDEVOLVIDO",
                table: "NOTASFISCAISENTRADA");
        }
    }
}
