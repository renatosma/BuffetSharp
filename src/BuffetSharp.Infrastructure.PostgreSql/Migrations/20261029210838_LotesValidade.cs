using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class LotesValidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "controlavalidade",
                table: "produtos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "lotesestoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    validade = table.Column<DateOnly>(type: "date", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    custounitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    entradaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotesestoque", x => x.id);
                    table.ForeignKey(
                        name: "fk_lotesestoque_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movimentacaolotes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    movimentacaoestoqueid = table.Column<int>(type: "integer", nullable: false),
                    loteestoqueid = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentacaolotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentacaolotes_lotesestoque_loteestoqueid",
                        column: x => x.loteestoqueid,
                        principalTable: "lotesestoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentacaolotes_movimentacoesestoque_movimentacaoestoqueid",
                        column: x => x.movimentacaoestoqueid,
                        principalTable: "movimentacoesestoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lotesestoque_produtoid_validade",
                table: "lotesestoque",
                columns: new[] { "produtoid", "validade" });

            migrationBuilder.CreateIndex(
                name: "ix_lotesestoque_validade",
                table: "lotesestoque",
                column: "validade");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacaolotes_loteestoqueid",
                table: "movimentacaolotes",
                column: "loteestoqueid");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacaolotes_movimentacaoestoqueid",
                table: "movimentacaolotes",
                column: "movimentacaoestoqueid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimentacaolotes");

            migrationBuilder.DropTable(
                name: "lotesestoque");

            migrationBuilder.DropColumn(
                name: "controlavalidade",
                table: "produtos");
        }
    }
}
