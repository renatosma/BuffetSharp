using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CobrancaExcedentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "excedentes",
                table: "parcelas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "excedentesapuradosem",
                table: "eventos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "excedentesinteira",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "excedentesisentos",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "excedentesmeia",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "presentesapurados",
                table: "eventos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valorexcedentes",
                table: "eventos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "excedentes",
                table: "parcelas");

            migrationBuilder.DropColumn(
                name: "excedentesapuradosem",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "excedentesinteira",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "excedentesisentos",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "excedentesmeia",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "presentesapurados",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "valorexcedentes",
                table: "eventos");
        }
    }
}
