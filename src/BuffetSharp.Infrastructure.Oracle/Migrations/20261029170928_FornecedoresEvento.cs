using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class FornecedoresEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EVENTOFORNECEDORES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EVENTOSERVICOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SERVICO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    SITUACAO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    RESPONSAVEL = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    CONFIRMADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PAGAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ETAPACHEGADAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    NOTA = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    COMENTARIO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    AVALIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOFORNECEDORES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTOFORNECEDORES_EVENTOETAPAS_ETAPACHEGADAID",
                        column: x => x.ETAPACHEGADAID,
                        principalTable: "EVENTOETAPAS",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_EVENTOFORNECEDORES_EVENTOSERVICOS_EVENTOSERVICOID",
                        column: x => x.EVENTOSERVICOID,
                        principalTable: "EVENTOSERVICOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOFORNECEDORES_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EVENTOFORNECEDORES_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTOFORNECEDORES_PARCELAMENTOSPAGAR_PAGAMENTOID",
                        column: x => x.PAGAMENTOID,
                        principalTable: "PARCELAMENTOSPAGAR",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOFORNECEDORES_ETAPACHEGADAID",
                table: "EVENTOFORNECEDORES",
                column: "ETAPACHEGADAID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOFORNECEDORES_EVENTOID",
                table: "EVENTOFORNECEDORES",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOFORNECEDORES_EVENTOSERVICOID",
                table: "EVENTOFORNECEDORES",
                column: "EVENTOSERVICOID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOFORNECEDORES_FORNECEDORID",
                table: "EVENTOFORNECEDORES",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOFORNECEDORES_PAGAMENTOID",
                table: "EVENTOFORNECEDORES",
                column: "PAGAMENTOID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EVENTOFORNECEDORES");
        }
    }
}
