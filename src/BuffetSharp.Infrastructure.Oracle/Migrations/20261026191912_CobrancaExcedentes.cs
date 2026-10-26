using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class CobrancaExcedentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EXCEDENTES",
                table: "PARCELAS",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EXCEDENTESAPURADOSEM",
                table: "EVENTOS",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EXCEDENTESINTEIRA",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EXCEDENTESISENTOS",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EXCEDENTESMEIA",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PRESENTESAPURADOS",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VALOREXCEDENTES",
                table: "EVENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EXCEDENTES",
                table: "PARCELAS");

            migrationBuilder.DropColumn(
                name: "EXCEDENTESAPURADOSEM",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "EXCEDENTESINTEIRA",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "EXCEDENTESISENTOS",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "EXCEDENTESMEIA",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "PRESENTESAPURADOS",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "VALOREXCEDENTES",
                table: "EVENTOS");
        }
    }
}
