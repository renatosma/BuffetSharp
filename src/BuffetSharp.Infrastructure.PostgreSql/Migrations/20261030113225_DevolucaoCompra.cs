using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DevolucaoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "quantidadedevolvida",
                table: "recebimentoitens",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "creditousado",
                table: "notasfiscaisentrada",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valordevolvido",
                table: "notasfiscaisentrada",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "devolucoescompra",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recebimentocompraid = table.Column<int>(type: "integer", nullable: false),
                    pedidocompraid = table.Column<int>(type: "integer", nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fornecedorrepoe = table.Column<bool>(type: "boolean", nullable: false),
                    notadevolucao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    registradoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    notafiscalentradaid = table.Column<int>(type: "integer", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valorabatido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valorcredito = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    creditoutilizado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devolucoescompra", x => x.id);
                    table.ForeignKey(
                        name: "fk_devolucoescompra_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_devolucoescompra_notasfiscaisentrada_notafiscalentradaid",
                        column: x => x.notafiscalentradaid,
                        principalTable: "notasfiscaisentrada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_devolucoescompra_pedidoscompra_pedidocompraid",
                        column: x => x.pedidocompraid,
                        principalTable: "pedidoscompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_devolucoescompra_recebimentoscompra_recebimentocompraid",
                        column: x => x.recebimentocompraid,
                        principalTable: "recebimentoscompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "creditosusadosnota",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notafiscalentradaid = table.Column<int>(type: "integer", nullable: false),
                    devolucaocompraid = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creditosusadosnota", x => x.id);
                    table.ForeignKey(
                        name: "fk_creditosusadosnota_devolucoescompra_devolucaocompraid",
                        column: x => x.devolucaocompraid,
                        principalTable: "devolucoescompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_creditosusadosnota_notasfiscaisentrada_notafiscalentradaid",
                        column: x => x.notafiscalentradaid,
                        principalTable: "notasfiscaisentrada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devolucaoitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    devolucaocompraid = table.Column<int>(type: "integer", nullable: false),
                    recebimentoitemid = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    custounitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    movimentacaoestoqueid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devolucaoitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_devolucaoitens_devolucoescompra_devolucaocompraid",
                        column: x => x.devolucaocompraid,
                        principalTable: "devolucoescompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_devolucaoitens_movimentacoesestoque_movimentacaoestoqueid",
                        column: x => x.movimentacaoestoqueid,
                        principalTable: "movimentacoesestoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_devolucaoitens_recebimentoitens_recebimentoitemid",
                        column: x => x.recebimentoitemid,
                        principalTable: "recebimentoitens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_creditosusadosnota_devolucaocompraid",
                table: "creditosusadosnota",
                column: "devolucaocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_creditosusadosnota_notafiscalentradaid",
                table: "creditosusadosnota",
                column: "notafiscalentradaid");

            migrationBuilder.CreateIndex(
                name: "ix_devolucaoitens_devolucaocompraid",
                table: "devolucaoitens",
                column: "devolucaocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_devolucaoitens_movimentacaoestoqueid",
                table: "devolucaoitens",
                column: "movimentacaoestoqueid");

            migrationBuilder.CreateIndex(
                name: "ix_devolucaoitens_recebimentoitemid",
                table: "devolucaoitens",
                column: "recebimentoitemid");

            migrationBuilder.CreateIndex(
                name: "ix_devolucoescompra_fornecedorid_data",
                table: "devolucoescompra",
                columns: new[] { "fornecedorid", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_devolucoescompra_notafiscalentradaid",
                table: "devolucoescompra",
                column: "notafiscalentradaid");

            migrationBuilder.CreateIndex(
                name: "ix_devolucoescompra_pedidocompraid",
                table: "devolucoescompra",
                column: "pedidocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_devolucoescompra_recebimentocompraid",
                table: "devolucoescompra",
                column: "recebimentocompraid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creditosusadosnota");

            migrationBuilder.DropTable(
                name: "devolucaoitens");

            migrationBuilder.DropTable(
                name: "devolucoescompra");

            migrationBuilder.DropColumn(
                name: "quantidadedevolvida",
                table: "recebimentoitens");

            migrationBuilder.DropColumn(
                name: "creditousado",
                table: "notasfiscaisentrada");

            migrationBuilder.DropColumn(
                name: "valordevolvido",
                table: "notasfiscaisentrada");
        }
    }
}
