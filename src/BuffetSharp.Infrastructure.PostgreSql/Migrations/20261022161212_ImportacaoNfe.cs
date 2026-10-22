using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ImportacaoNfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chaveacesso",
                table: "notasfiscaisentrada",
                type: "character varying(44)",
                maxLength: 44,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "codigosprodutofornecedor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fornecedorid = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    produtoembalagemid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codigosprodutofornecedor", x => x.id);
                    table.ForeignKey(
                        name: "fk_codigosprodutofornecedor_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_codigosprodutofornecedor_produtoembalagens_produtoembalagem~",
                        column: x => x.produtoembalagemid,
                        principalTable: "produtoembalagens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_codigosprodutofornecedor_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisentrada_chaveacesso",
                table: "notasfiscaisentrada",
                column: "chaveacesso");

            migrationBuilder.CreateIndex(
                name: "ix_codigosprodutofornecedor_fornecedorid_codigo",
                table: "codigosprodutofornecedor",
                columns: new[] { "fornecedorid", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_codigosprodutofornecedor_produtoembalagemid",
                table: "codigosprodutofornecedor",
                column: "produtoembalagemid");

            migrationBuilder.CreateIndex(
                name: "ix_codigosprodutofornecedor_produtoid",
                table: "codigosprodutofornecedor",
                column: "produtoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigosprodutofornecedor");

            migrationBuilder.DropIndex(
                name: "ix_notasfiscaisentrada_chaveacesso",
                table: "notasfiscaisentrada");

            migrationBuilder.DropColumn(
                name: "chaveacesso",
                table: "notasfiscaisentrada");
        }
    }
}
