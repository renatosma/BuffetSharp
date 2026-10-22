using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PedidoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "depedidocompra",
                table: "movimentacoesestoque",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "pedidoscompra",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fornecedorid = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    previsaoentrega = table.Column<DateOnly>(type: "date", nullable: true),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notafiscal = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    datacompra = table.Column<DateOnly>(type: "date", nullable: true),
                    recebidoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivocancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contapagarid = table.Column<int>(type: "integer", nullable: true),
                    parcelamentopagarid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidoscompra", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedidoscompra_contaspagar_contapagarid",
                        column: x => x.contapagarid,
                        principalTable: "contaspagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidoscompra_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidoscompra_parcelamentospagar_parcelamentopagarid",
                        column: x => x.parcelamentopagarid,
                        principalTable: "parcelamentospagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidocompraitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pedidocompraid = table.Column<int>(type: "integer", nullable: false),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    produtoembalagemid = table.Column<int>(type: "integer", nullable: true),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    custounitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    movimentacaoestoqueid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidocompraitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedidocompraitens_movimentacoesestoque_movimentacaoestoqueid",
                        column: x => x.movimentacaoestoqueid,
                        principalTable: "movimentacoesestoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidocompraitens_pedidoscompra_pedidocompraid",
                        column: x => x.pedidocompraid,
                        principalTable: "pedidoscompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pedidocompraitens_produtoembalagens_produtoembalagemid",
                        column: x => x.produtoembalagemid,
                        principalTable: "produtoembalagens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidocompraitens_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedidocompraitens_movimentacaoestoqueid",
                table: "pedidocompraitens",
                column: "movimentacaoestoqueid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidocompraitens_pedidocompraid",
                table: "pedidocompraitens",
                column: "pedidocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidocompraitens_produtoembalagemid",
                table: "pedidocompraitens",
                column: "produtoembalagemid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidocompraitens_produtoid",
                table: "pedidocompraitens",
                column: "produtoid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_contapagarid",
                table: "pedidoscompra",
                column: "contapagarid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_fornecedorid",
                table: "pedidoscompra",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_notafiscal",
                table: "pedidoscompra",
                column: "notafiscal");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_parcelamentopagarid",
                table: "pedidoscompra",
                column: "parcelamentopagarid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_status_data",
                table: "pedidoscompra",
                columns: new[] { "status", "data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedidocompraitens");

            migrationBuilder.DropTable(
                name: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "depedidocompra",
                table: "movimentacoesestoque");
        }
    }
}
