using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class FaixasEtarias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IDADEMAXIMAISENTO",
                table: "PACOTES",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "IDADEMAXIMAMEIA",
                table: "PACOTES",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENTUALMEIA",
                table: "PACOTES",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "CONVIDADOSISENTOS",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CONVIDADOSMEIA",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IDADEMAXIMAISENTO",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "IDADEMAXIMAMEIA",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENTUALMEIA",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "CONVIDADOSISENTOS",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CONVIDADOSMEIA",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IDADEMAXIMAISENTO",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "IDADEMAXIMAMEIA",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENTUALMEIA",
                table: "EVENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IDADEMAXIMAISENTO",
                table: "PACOTES");

            migrationBuilder.DropColumn(
                name: "IDADEMAXIMAMEIA",
                table: "PACOTES");

            migrationBuilder.DropColumn(
                name: "PERCENTUALMEIA",
                table: "PACOTES");

            migrationBuilder.DropColumn(
                name: "CONVIDADOSISENTOS",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CONVIDADOSMEIA",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "IDADEMAXIMAISENTO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "IDADEMAXIMAMEIA",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "PERCENTUALMEIA",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CONVIDADOSISENTOS",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "CONVIDADOSMEIA",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "IDADEMAXIMAISENTO",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "IDADEMAXIMAMEIA",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "PERCENTUALMEIA",
                table: "EVENTOS");
        }
    }
}
