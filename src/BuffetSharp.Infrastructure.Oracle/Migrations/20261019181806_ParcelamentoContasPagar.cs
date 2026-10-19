using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ParcelamentoContasPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AGRUPADAEMID",
                table: "CONTASPAGAR",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DOCUMENTO",
                table: "CONTASPAGAR",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NUMEROPARCELA",
                table: "CONTASPAGAR",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PARCELAMENTOID",
                table: "CONTASPAGAR",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PARCELAMENTOSPAGAR",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CATEGORIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VALORTOTAL = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DOCUMENTO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PARCELAMENTOSPAGAR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PARCELAMENTOSPAGAR_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PARCELAMENTOSPAGAR_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_AGRUPADAEMID",
                table: "CONTASPAGAR",
                column: "AGRUPADAEMID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_PARCELAMENTOID",
                table: "CONTASPAGAR",
                column: "PARCELAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PARCELAMENTOSPAGAR_EVENTOID",
                table: "PARCELAMENTOSPAGAR",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_PARCELAMENTOSPAGAR_FORNECEDORID",
                table: "PARCELAMENTOSPAGAR",
                column: "FORNECEDORID");

            migrationBuilder.AddForeignKey(
                name: "FK_CONTASPAGAR_PARCELAMENTOSPAGAR_AGRUPADAEMID",
                table: "CONTASPAGAR",
                column: "AGRUPADAEMID",
                principalTable: "PARCELAMENTOSPAGAR",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CONTASPAGAR_PARCELAMENTOSPAGAR_PARCELAMENTOID",
                table: "CONTASPAGAR",
                column: "PARCELAMENTOID",
                principalTable: "PARCELAMENTOSPAGAR",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONTASPAGAR_PARCELAMENTOSPAGAR_AGRUPADAEMID",
                table: "CONTASPAGAR");

            migrationBuilder.DropForeignKey(
                name: "FK_CONTASPAGAR_PARCELAMENTOSPAGAR_PARCELAMENTOID",
                table: "CONTASPAGAR");

            migrationBuilder.DropTable(
                name: "PARCELAMENTOSPAGAR");

            migrationBuilder.DropIndex(
                name: "IX_CONTASPAGAR_AGRUPADAEMID",
                table: "CONTASPAGAR");

            migrationBuilder.DropIndex(
                name: "IX_CONTASPAGAR_PARCELAMENTOID",
                table: "CONTASPAGAR");

            migrationBuilder.DropColumn(
                name: "AGRUPADAEMID",
                table: "CONTASPAGAR");

            migrationBuilder.DropColumn(
                name: "DOCUMENTO",
                table: "CONTASPAGAR");

            migrationBuilder.DropColumn(
                name: "NUMEROPARCELA",
                table: "CONTASPAGAR");

            migrationBuilder.DropColumn(
                name: "PARCELAMENTOID",
                table: "CONTASPAGAR");
        }
    }
}
