using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class CobrancaExcedentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Excedentes",
                table: "Parcelas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExcedentesApuradosEm",
                table: "Eventos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExcedentesInteira",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExcedentesIsentos",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExcedentesMeia",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PresentesApurados",
                table: "Eventos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorExcedentes",
                table: "Eventos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Excedentes",
                table: "Parcelas");

            migrationBuilder.DropColumn(
                name: "ExcedentesApuradosEm",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "ExcedentesInteira",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "ExcedentesIsentos",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "ExcedentesMeia",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "PresentesApurados",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "ValorExcedentes",
                table: "Eventos");
        }
    }
}
