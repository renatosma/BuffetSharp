using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FornecedoresEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventofornecedores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventoid = table.Column<int>(type: "integer", nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: false),
                    eventoservicoid = table.Column<int>(type: "integer", nullable: true),
                    servico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    situacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    responsavel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    confirmadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    pagamentoid = table.Column<int>(type: "integer", nullable: true),
                    etapachegadaid = table.Column<int>(type: "integer", nullable: true),
                    nota = table.Column<int>(type: "integer", nullable: true),
                    comentario = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    avaliadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventofornecedores", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventofornecedores_eventoetapas_etapachegadaid",
                        column: x => x.etapachegadaid,
                        principalTable: "eventoetapas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_eventofornecedores_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_eventofornecedores_eventoservicos_eventoservicoid",
                        column: x => x.eventoservicoid,
                        principalTable: "eventoservicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventofornecedores_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventofornecedores_parcelamentospagar_pagamentoid",
                        column: x => x.pagamentoid,
                        principalTable: "parcelamentospagar",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_eventofornecedores_etapachegadaid",
                table: "eventofornecedores",
                column: "etapachegadaid");

            migrationBuilder.CreateIndex(
                name: "ix_eventofornecedores_eventoid",
                table: "eventofornecedores",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_eventofornecedores_eventoservicoid",
                table: "eventofornecedores",
                column: "eventoservicoid");

            migrationBuilder.CreateIndex(
                name: "ix_eventofornecedores_fornecedorid",
                table: "eventofornecedores",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_eventofornecedores_pagamentoid",
                table: "eventofornecedores",
                column: "pagamentoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventofornecedores");
        }
    }
}
