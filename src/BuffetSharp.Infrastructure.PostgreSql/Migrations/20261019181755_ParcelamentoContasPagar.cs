using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ParcelamentoContasPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "agrupadaemid",
                table: "contaspagar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "documento",
                table: "contaspagar",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "numeroparcela",
                table: "contaspagar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "parcelamentoid",
                table: "contaspagar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "parcelamentospagar",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valortotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    eventoid = table.Column<int>(type: "integer", nullable: true),
                    documento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parcelamentospagar", x => x.id);
                    table.ForeignKey(
                        name: "fk_parcelamentospagar_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parcelamentospagar_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_agrupadaemid",
                table: "contaspagar",
                column: "agrupadaemid");

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_parcelamentoid",
                table: "contaspagar",
                column: "parcelamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_parcelamentospagar_eventoid",
                table: "parcelamentospagar",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_parcelamentospagar_fornecedorid",
                table: "parcelamentospagar",
                column: "fornecedorid");

            migrationBuilder.AddForeignKey(
                name: "fk_contaspagar_parcelamentospagar_agrupadaemid",
                table: "contaspagar",
                column: "agrupadaemid",
                principalTable: "parcelamentospagar",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_contaspagar_parcelamentospagar_parcelamentoid",
                table: "contaspagar",
                column: "parcelamentoid",
                principalTable: "parcelamentospagar",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_contaspagar_parcelamentospagar_agrupadaemid",
                table: "contaspagar");

            migrationBuilder.DropForeignKey(
                name: "fk_contaspagar_parcelamentospagar_parcelamentoid",
                table: "contaspagar");

            migrationBuilder.DropTable(
                name: "parcelamentospagar");

            migrationBuilder.DropIndex(
                name: "ix_contaspagar_agrupadaemid",
                table: "contaspagar");

            migrationBuilder.DropIndex(
                name: "ix_contaspagar_parcelamentoid",
                table: "contaspagar");

            migrationBuilder.DropColumn(
                name: "agrupadaemid",
                table: "contaspagar");

            migrationBuilder.DropColumn(
                name: "documento",
                table: "contaspagar");

            migrationBuilder.DropColumn(
                name: "numeroparcela",
                table: "contaspagar");

            migrationBuilder.DropColumn(
                name: "parcelamentoid",
                table: "contaspagar");
        }
    }
}
